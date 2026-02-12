# Domain Pitfalls: Backtesting Engine for DCA Bot

**Domain:** Adding backtesting/parameter-sweep engine to existing BTC Smart DCA bot
**Researched:** 2026-02-12
**Confidence:** HIGH for backtesting patterns, MEDIUM for CoinGecko API specifics
**Context:** Existing v1.0 bot has stateless MultiplierCalculator, DailyPrice entities in PostgreSQL, DcaOptions config

---

## Critical Pitfalls

Mistakes that produce misleading backtest results, leading to false confidence in strategy parameters and real-money losses.

---

### Pitfall 1: Look-Ahead Bias in 30-Day High and 200-Day MA Calculations

**What goes wrong:** The backtest simulation "knows" future prices when calculating the 30-day high or 200-day SMA, producing results impossible to achieve in live trading.

**Why it happens:** The existing `PriceDataService.Get30DayHighAsync()` queries the database with `DateTime.UtcNow` as the reference point. In a backtest, if you load all historical prices into the database first, then replay dates, the queries still see future data unless the simulation explicitly restricts the query window to the simulated date.

Concretely, the production code does:
```csharp
// PriceDataService.cs line 166
var today = DateOnly.FromDateTime(DateTime.UtcNow);  // <-- THIS IS THE BUG IN BACKTEST
var startDate = today.AddDays(-lookbackDays);
```

If you reuse this service in backtesting without overriding `today`, the 30-day high for a simulated 2023-06-15 will actually be computed from 2026-02-12 data.

**Why it matters for THIS project specifically:**
- The dip-tier multiplier depends entirely on the drop-from-30-day-high calculation. If the 30-day high includes future data, the backtest will trigger different tiers than live trading would.
- The 200-day MA bear boost relies on `currentPrice < ma200Day`. If the MA includes future prices, bear/bull regime detection is wrong.
- Because multipliers are multiplicative (dip tier * bear boost, capped at 4.5x), even small look-ahead bias in either component compounds into large dollar-amount errors.

**Consequences:**
- Backtest shows the strategy buying 3x during a dip when live trading would have bought 1.5x (or vice versa)
- Parameter sweep selects tier thresholds that only work with hindsight
- False confidence leads to deploying suboptimal or dangerous parameters

**Prevention:**
1. **Create a date-aware abstraction for "current date":**
   ```csharp
   public interface IDateTimeProvider
   {
       DateOnly Today { get; }
       DateTimeOffset UtcNow { get; }
   }

   // Production implementation
   public class SystemDateTimeProvider : IDateTimeProvider
   {
       public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
       public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
   }

   // Backtest implementation
   public class SimulatedDateTimeProvider(DateOnly simulatedDate) : IDateTimeProvider
   {
       public DateOnly Today => simulatedDate;
       public DateTimeOffset UtcNow => new(simulatedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
   }
   ```

2. **Inject IDateTimeProvider into PriceDataService instead of using DateTime.UtcNow directly.** This is a small refactor of the existing code that makes it backtest-safe without breaking production.

3. **Filter historical data with a strict upper bound:**
   ```csharp
   // In backtest mode, NEVER query prices after the simulated date
   var prices = allPrices.Where(p => p.Date <= simulatedDate && p.Date >= simulatedDate.AddDays(-lookback));
   ```

4. **Validation test:** Run backtest for a known date, manually verify the 30-day high and 200-day SMA match what a human would compute from the data available on that date only.

**Warning signs:**
- Backtest returns are suspiciously better than fixed DCA (especially during volatile periods)
- Multiplier distributions in backtest don't match intuition about market conditions
- Changing lookback window doesn't affect results as much as expected

**Detection:**
- Compare the multiplier assigned on a specific historical date against a manual calculation using only data available on that date
- Check that the first 200 days of simulation produce MA=0 or fallback behavior (not enough data yet)

**Phase mapping:** Must be addressed in the very first backtest implementation. This is not something to "fix later" -- every backtest result before this fix is unreliable.

---

### Pitfall 2: Overfitting Parameters to Historical Data via Exhaustive Sweep

**What goes wrong:** Parameter sweep finds the "optimal" tier thresholds and multiplier values that performed best historically, but these parameters fail in live trading because they're tuned to noise, not signal.

**Why it happens:** With the current DcaOptions structure, the sweep space includes:
- `MultiplierTiers[].DropPercentage`: 3 tier thresholds (continuous values, e.g., 3-25%)
- `MultiplierTiers[].Multiplier`: 3 tier multipliers (continuous values, e.g., 1.0-5.0)
- `BearBoostFactor`: 1 value (e.g., 1.0-3.0)
- `MaxMultiplierCap`: 1 value
- `HighLookbackDays`: 1 value (e.g., 14-60)

Even with coarse grids, this is 5+ dimensions. A grid of 10 values per dimension = 100,000 parameter combinations. With 2-4 years of daily data (~730-1460 data points), the optimizer has far more degrees of freedom than the data can support. The "best" configuration will almost certainly be overfit.

**Specific risk for DCA:** Unlike momentum strategies where overfitting produces catastrophic losses, DCA overfitting is insidious -- the strategy still "works" (you still accumulate BTC), but the user deploys parameters that spend aggressively during periods that don't actually represent good buying opportunities, resulting in higher cost basis than simpler parameters would achieve.

**Consequences:**
- User deploys parameters that looked great on 2022-2024 data but underperform in 2025+
- Overfit parameters often have extreme values (e.g., 5x multiplier at 3% drop) that create real-money risk
- False confidence in precise parameter values when the data supports only rough ranges

**Prevention:**
1. **Walk-forward validation (critical):**
   - Split data: train on first 70%, validate on last 30%
   - Parameters selected on training period must also perform well on validation period
   - If a parameter set is top-10 on training but bottom-50% on validation, it's overfit

2. **Constrain sweep space aggressively:**
   ```csharp
   // Instead of sweeping ALL parameters, fix the structure and sweep only 2-3 key values
   // Good: sweep DropPercentage thresholds with fixed multiplier ratios
   // Bad: sweep all 8+ parameters independently

   public class SweepConfiguration
   {
       // Fix multiplier ratios relative to each other (e.g., 1x, 1.5x, 2x, 3x)
       // Only sweep the DropPercentage boundaries
       public decimal[] Tier1DropRange { get; set; } = [3, 5, 7, 10];
       public decimal[] Tier2DropRange { get; set; } = [8, 10, 12, 15];
       public decimal[] Tier3DropRange { get; set; } = [15, 20, 25, 30];
       // This is 4*4*4 = 64 combinations, not 100,000
   }
   ```

3. **Report parameter sensitivity, not just "best":**
   - Show how results change as each parameter varies (sensitivity analysis)
   - Robust parameters should have a broad "plateau" of good performance
   - Parameters where moving 1% changes results dramatically are overfit

4. **Include fixed-DCA baseline in all comparisons:**
   - If the "optimal" smart DCA only beats fixed DCA by 2%, the strategy advantage is within noise
   - The baseline proves the multiplier logic adds value beyond random variation

5. **Penalize complexity:**
   - A configuration with 3 tiers should demonstrate meaningful improvement over 2 tiers
   - Report "improvement per parameter" to discourage over-parameterization

**Warning signs:**
- "Best" parameters have very specific values (e.g., DropPercentage = 7.3%, not 5% or 10%)
- Top parameters on different time slices are very different
- Small changes in tier thresholds cause large result changes
- "Optimal" multipliers are at extreme ends of the sweep range

**Detection:**
- Run walk-forward validation; if top params on train period rank poorly on test period, overfitting is confirmed
- Check if multiple parameter sets produce similar results (robust) or if only one narrow set wins (overfit)

**Phase mapping:** Address during parameter sweep implementation. The sweep engine must include walk-forward validation from the start, not as an afterthought.

---

### Pitfall 3: Simulating Perfect Execution (Ignoring Slippage and Execution Reality)

**What goes wrong:** Backtest assumes every buy executes at the exact daily close price for the exact calculated amount, but live trading uses IOC orders with 5% slippage tolerance and gets filled at varying prices.

**Why it happens:** The production `DcaExecutionService` places IOC (immediate-or-cancel) orders with a 5% slippage limit (line 165: `currentPrice * 1.05m`). The actual fill price depends on order book depth. In the backtest, using `DailyPrice.Close` as the execution price ignores:
- Bid-ask spread (typically 0.01-0.1% for BTC spot)
- Price movement between signal and execution
- Order book depth (small issue for BTC, but exists)

**Why this matters for DCA specifically:**
- For a $10 base amount, slippage is negligible (~$0.01)
- For a 4.5x multiplied buy ($45), still small
- But across 1,000+ simulated days, systematic bias accumulates
- More importantly: if the sweep optimizer finds parameters that trigger high multipliers frequently, the cumulative slippage error grows, and the optimizer selects parameters that systematically over-estimate returns

**Consequences:**
- Backtest shows 3-5% better cost basis than achievable in practice
- Parameters that trigger frequent high-multiplier buys look better in backtest than they actually are
- Comparison between smart DCA and fixed DCA is biased (both have slippage, but smart DCA has more variance in buy sizes)

**Prevention:**
1. **Add configurable slippage model:**
   ```csharp
   public class BacktestExecutionModel
   {
       // Simulated execution price = Close * (1 + SlippageBps/10000)
       public decimal SlippageBps { get; set; } = 5; // 0.05% default for BTC spot

       public decimal GetExecutionPrice(DailyPrice candle)
       {
           // For buys, execution price is slightly above close
           return candle.Close * (1 + SlippageBps / 10000m);
       }
   }
   ```

2. **Use the daily OHLC range to add realistic variance:**
   ```csharp
   // Instead of always using Close, simulate execution within the day's range
   // This is more realistic than a fixed slippage percentage
   public decimal GetRealisticExecutionPrice(DailyPrice candle)
   {
       // Weighted average: 50% close, 25% high, 25% average(open,close)
       // Represents "typical" execution during the day, slightly worse than VWAP
       return candle.Close * 0.5m + candle.High * 0.25m + (candle.Open + candle.Close) / 2m * 0.25m;
   }
   ```

3. **Apply slippage to BOTH smart DCA and fixed DCA baselines equally** so comparisons remain fair.

4. **Report results with and without slippage** to show sensitivity.

**Warning signs:**
- Backtest average cost basis is unrealistically close to the theoretical optimum
- No difference in results when varying slippage assumptions
- High-multiplier parameter sets show disproportionate improvement vs fixed DCA

**Phase mapping:** Include slippage model in the simulation engine from the start. It's a small addition that prevents systematic bias in all results.

---

### Pitfall 4: Historical Data Quality Issues (CoinGecko Specific)

**What goes wrong:** CoinGecko historical data has gaps, timezone inconsistencies, or uses aggregated/adjusted prices that don't match what Hyperliquid would have shown, producing unreliable backtests.

**Why it happens:** CoinGecko aggregates prices across multiple exchanges. The project needs historical BTC data for backtesting, but will use Hyperliquid for live trading. These are different price feeds.

**Known CoinGecko data issues (MEDIUM confidence, based on training data):**

a) **Rate limits on free tier:** The CoinGecko free API tier allows approximately 10-30 calls/minute (this changed multiple times; verify current limits). Fetching 4 years of daily data requires careful request management.

b) **Data granularity depends on range:** For the `/coins/{id}/market_chart/range` endpoint:
   - Ranges under 90 days: may return hourly data points
   - Ranges over 90 days: returns daily data points
   - The "daily" data point is typically a snapshot, not a proper OHLC candle

c) **OHLC endpoint limitations:** The `/coins/{id}/ohlc/range` endpoint (if available on your tier) returns OHLC candles, but:
   - Free tier may limit to 1-2 years of history
   - Candle intervals available may be limited (1d, 7d, 14d, 30d -- not all tiers)
   - Missing days are not flagged; they're simply absent from the response

d) **Price is aggregated, not exchange-specific:** CoinGecko's BTC price is a volume-weighted average across many exchanges. Hyperliquid spot BTC price may differ by 0.1-1%, especially during volatile periods. This introduces a systematic but small bias.

e) **Timestamp format:** CoinGecko returns Unix timestamps in milliseconds. The existing `DailyPrice` entity uses `DateOnly Date` and `DateTimeOffset Timestamp`. Timezone mapping must be explicit.

f) **Weekend/holiday gaps:** BTC trades 24/7, so there should be no gaps. But CoinGecko's aggregation may occasionally miss data points, especially for exchange-specific prices.

**Consequences:**
- Missing data points cause incorrect 30-day high (uses data from 35 days ago as "30-day high")
- Missing data causes incorrect 200-day SMA (averaged over 195 actual data points but divided by 200, or worse, only 195)
- Price discrepancy between CoinGecko aggregate and Hyperliquid means backtest multiplier decisions don't match what live trading would decide
- Rate limit exhaustion during data fetch causes incomplete historical dataset

**Prevention:**
1. **Validate data completeness after fetch:**
   ```csharp
   public void ValidateHistoricalData(List<DailyPrice> prices, DateOnly startDate, DateOnly endDate)
   {
       var expectedDays = endDate.DayNumber - startDate.DayNumber + 1;
       var actualDays = prices.Count;
       var completeness = (decimal)actualDays / expectedDays * 100;

       if (completeness < 95)
       {
           logger.LogWarning(
               "Historical data only {Completeness:F1}% complete: {Actual}/{Expected} days from {Start} to {End}",
               completeness, actualDays, expectedDays, startDate, endDate);
       }

       // Find gaps
       var sorted = prices.OrderBy(p => p.Date).ToList();
       for (int i = 1; i < sorted.Count; i++)
       {
           var gap = sorted[i].Date.DayNumber - sorted[i - 1].Date.DayNumber;
           if (gap > 1)
           {
               logger.LogWarning("Data gap: {Days} missing days between {From} and {To}",
                   gap - 1, sorted[i - 1].Date, sorted[i].Date);
           }
       }
   }
   ```

2. **Interpolate missing days (with caution):**
   ```csharp
   // For small gaps (1-2 days), linear interpolation is acceptable
   // For larger gaps, flag and skip those periods in the backtest
   public List<DailyPrice> FillSmallGaps(List<DailyPrice> prices, int maxGapDays = 2)
   {
       // ... interpolation logic ...
       // IMPORTANT: Mark interpolated prices so they can be excluded from analysis
   }
   ```

3. **Cache aggressively, fetch once:**
   ```csharp
   // Fetch all historical data once and store in PostgreSQL
   // Don't re-fetch on every backtest run
   // Only fetch incremental updates for new days
   ```

4. **Respect rate limits with exponential backoff:**
   ```csharp
   // CoinGecko free tier: ~10-30 req/min (verify current limits)
   // Fetch in chunks with delays between requests
   public async Task FetchHistoricalDataAsync(DateOnly start, DateOnly end, CancellationToken ct)
   {
       var chunkSize = TimeSpan.FromDays(365); // 1 year per request (returns daily granularity)
       var current = start;

       while (current < end)
       {
           var chunkEnd = DateOnly.FromDateTime(
               current.ToDateTime(TimeOnly.MinValue).Add(chunkSize));
           if (chunkEnd > end) chunkEnd = end;

           await FetchChunkAsync(current, chunkEnd, ct);
           current = chunkEnd.AddDays(1);

           // Rate limit: wait between requests
           await Task.Delay(TimeSpan.FromSeconds(6), ct); // ~10 req/min safe margin
       }
   }
   ```

5. **Document the price source mismatch:**
   - State clearly in API response: "Backtest uses CoinGecko aggregated prices, live trading uses Hyperliquid spot prices"
   - This is an inherent limitation, not a bug to fix

**Warning signs:**
- Backtest period has fewer data points than calendar days
- SMA calculation uses different denominators on different runs
- HTTP 429 errors during data fetch
- Prices in database don't match CoinGecko web UI for same date

**Phase mapping:** Address during historical data ingestion phase (first phase of backtesting). Data quality validation must run before any simulation.

---

### Pitfall 5: Reusing Production MultiplierCalculator Without Decoupling Side Effects

**What goes wrong:** The production `DcaExecutionService.CalculateMultiplierAsync()` is tightly coupled to infrastructure services (IPriceDataService, IOptionsMonitor, ILogger) and mixes pure calculation with data fetching. Attempting to reuse it directly in a backtest loop causes database queries per simulated day, making the simulation extremely slow and potentially incorrect.

**Why it happens:** Looking at the existing code in `DcaExecutionService.cs` (lines 270-354), `CalculateMultiplierAsync`:
- Calls `priceDataService.Get30DayHighAsync()` which queries PostgreSQL
- Calls `priceDataService.Get200DaySmaAsync()` which queries PostgreSQL
- Uses `dcaOptions.CurrentValue` which reads from IOptionsMonitor
- Has graceful degradation logic (catch-all returns 1.0x) that masks errors in backtest

For a 1,460-day backtest, this means 2,920 database queries just for the multiplier calculations, plus the overhead of EF Core change tracking, connection pooling, etc.

**Consequences:**
- Backtest takes minutes instead of milliseconds
- Database load from backtesting interferes with production price data refresh
- The graceful degradation (catch returning 1.0x) hides bugs in the backtest -- a simulation should FAIL loudly, not silently fall back
- IOptionsMonitor returns the CURRENT config, not the config being tested in the sweep

**Prevention:**
1. **Extract pure multiplier calculation logic into a static/pure method:**
   ```csharp
   // This already exists conceptually in the code but is entangled with async data fetching
   // Extract the PURE calculation:
   public static class MultiplierCalculator
   {
       public static MultiplierResult Calculate(
           decimal currentPrice,
           decimal high30Day,
           decimal ma200Day,
           IReadOnlyList<MultiplierTier> tiers,
           decimal bearBoostFactor,
           decimal maxCap)
       {
           // Drop percentage
           decimal dropPercent = high30Day > 0
               ? (high30Day - currentPrice) / high30Day * 100m
               : 0m;

           // Tier matching
           var matchedTier = tiers
               .OrderByDescending(t => t.DropPercentage)
               .FirstOrDefault(t => dropPercent >= t.DropPercentage);

           decimal dipMultiplier = matchedTier?.Multiplier ?? 1.0m;

           // Bear boost
           decimal bearMultiplier = (ma200Day > 0 && currentPrice < ma200Day)
               ? bearBoostFactor
               : 1.0m;

           // Stack and cap
           decimal total = Math.Min(dipMultiplier * bearMultiplier, maxCap);

           return new MultiplierResult(total, dipMultiplier, bearMultiplier, ...);
       }
   }
   ```

2. **Pre-compute rolling windows in-memory for the backtest:**
   ```csharp
   // Load ALL prices once, then compute 30-day high and 200-day SMA for each date
   // using in-memory sliding windows -- no database queries during simulation
   public class BacktestPriceProvider
   {
       private readonly IReadOnlyList<DailyPrice> _prices; // sorted by date

       public (decimal High30Day, decimal Ma200Day) GetIndicators(DateOnly date, int lookbackDays, int maPeriod)
       {
           var idx = FindIndex(date);
           var high30 = _prices.Skip(Math.Max(0, idx - lookbackDays)).Take(lookbackDays).Max(p => p.Close);
           var ma200 = _prices.Skip(Math.Max(0, idx - maPeriod)).Take(maPeriod).Average(p => p.Close);
           return (high30, ma200);
       }
   }
   ```

3. **The backtest should throw on missing data, not fall back to 1.0x:**
   ```csharp
   // Production: graceful degradation is correct (don't skip a buy because data is stale)
   // Backtest: failure means the test is invalid, surface the error
   if (high30Day == 0)
       throw new BacktestDataException($"No 30-day high available for {simulatedDate}");
   ```

4. **Pass DcaOptions directly, not via IOptionsMonitor:**
   - The sweep needs to test different DcaOptions per run
   - IOptionsMonitor reads from appsettings.json -- it won't have the sweep parameters
   - The simulation engine should accept DcaOptions as a parameter

**Warning signs:**
- Backtest runs take more than a few seconds for 4 years of daily data
- Database connection pool exhaustion during parameter sweeps
- All simulated days show multiplier=1.0x (graceful degradation masking errors)
- Sweep results don't change when parameters change (same IOptionsMonitor value used)

**Phase mapping:** Address as the first step of building the simulation engine. Extracting the pure multiplier calculation is a prerequisite for everything else.

---

## Moderate Pitfalls

Mistakes that cause delays, incorrect metrics, or suboptimal architecture decisions.

---

### Pitfall 6: Incorrect "Warm-Up Period" Handling

**What goes wrong:** The backtest starts computing multipliers from day 1 of the data, but the 200-day SMA requires 200 days of prior data. Results for the first 200 days are computed with incomplete indicators, skewing aggregate metrics.

**Why it happens:** The production code handles insufficient data by returning 0 for the MA and skipping the bear boost (lines 203-209 in PriceDataService). In the backtest, this means the first ~200 days always use multiplier=1.0x (no bear boost), which:
- Inflates the "fixed DCA equivalent" comparison for those days
- Makes the smart DCA look worse relative to fixed DCA in the aggregate
- If the first 200 days include a major dip, the backtest misses the bear boost advantage entirely

**Consequences:**
- Aggregate cost-basis metrics are biased by the warm-up period
- Parameter sweep results depend on how much of the data falls in the warm-up period
- Comparison between smart DCA and fixed DCA is unfair for the first 200 days

**Prevention:**
1. **Explicitly define and document the warm-up period:**
   ```csharp
   public class BacktestConfig
   {
       public int WarmUpDays { get; set; } = 200; // MA period
       // Simulation starts AFTER warm-up period
       // Warm-up days are used to compute initial indicators but not included in results
   }
   ```

2. **Separate "simulation period" from "data period":**
   - Data needed: 2020-01-01 to 2024-12-31 (5 years)
   - Warm-up: 2020-01-01 to 2020-07-19 (200 days, indicators computed but buys not counted)
   - Results period: 2020-07-20 to 2024-12-31 (4.45 years of actual simulated buys)

3. **Report the warm-up handling in API response:**
   ```json
   {
       "dataRange": { "start": "2020-01-01", "end": "2024-12-31" },
       "simulationRange": { "start": "2020-07-20", "end": "2024-12-31" },
       "warmUpDays": 200,
       "note": "First 200 days used for indicator calculation only"
   }
   ```

**Warning signs:**
- First ~200 entries in simulation results all show bear_boost=1.0x regardless of market conditions
- Aggregate metrics change significantly when you add or remove 1 year of data at the start

**Phase mapping:** Address when designing the simulation engine's date iteration logic.

---

### Pitfall 7: Metrics Calculation Errors

**What goes wrong:** Annualized returns, drawdown, or cost-basis comparisons are calculated incorrectly, producing misleading metrics.

**Why it happens:** DCA metrics are subtly different from portfolio return metrics. Common errors:

a) **Cost basis calculation:** The average cost basis is NOT `sum(prices) / count`. It's `sum(costs) / sum(btc_quantity)` -- a volume-weighted average. With multipliers, higher-cost periods may also have higher buy amounts, and the weighting matters.

b) **Drawdown for accumulation strategies:** Traditional max drawdown is calculated on portfolio value. For DCA, the relevant metric is "unrealized loss vs total invested" not "peak-to-trough of portfolio value." A DCA portfolio that invested $1000 total and currently holds BTC worth $900 has a 10% drawdown from investment, even if BTC's price drawdown was 50%.

c) **Annualization errors:** If the simulation covers 3.5 years, annualizing by multiplying by (12/42 months) is wrong. The correct annualization depends on the metric:
   - Return on investment: `(final_value / total_invested)^(365/days) - 1`
   - Cost basis improvement: not naturally annualizable (it's a total metric)

d) **Smart DCA vs Fixed DCA comparison bias:** The two strategies invest DIFFERENT total amounts (smart DCA spends more during dips). Comparing cost basis directly is misleading unless you also compare total BTC acquired, total spent, and return on investment.

**Consequences:**
- User thinks strategy produces 15% annualized improvement when it's actually 5%
- Drawdown metric looks unrealistically good because it's calculated wrong
- Smart vs fixed comparison misleads because total invested amounts differ

**Prevention:**
1. **Use precise cost-basis calculation:**
   ```csharp
   public class BacktestMetrics
   {
       public decimal TotalInvested => Purchases.Sum(p => p.Cost);
       public decimal TotalBtcAcquired => Purchases.Sum(p => p.Quantity);
       public decimal AverageCostBasis => TotalInvested / TotalBtcAcquired;

       // Current value at end of simulation
       public decimal FinalPortfolioValue => TotalBtcAcquired * FinalPrice;

       // Return on investment
       public decimal TotalROI => (FinalPortfolioValue - TotalInvested) / TotalInvested;

       // Annualized ROI (CAGR-style but for DCA is approximate)
       public decimal AnnualizedROI => Math.Pow((double)(1 + TotalROI), 365.0 / SimulationDays) - 1;
   }
   ```

2. **Calculate drawdown correctly for accumulation:**
   ```csharp
   public decimal CalculateMaxDrawdownFromInvestment(List<DailySimResult> results)
   {
       decimal maxDrawdown = 0;
       decimal runningInvested = 0;

       foreach (var day in results)
       {
           runningInvested += day.CostToday;
           var currentValue = day.TotalBtcHeld * day.Price;
           var drawdown = (runningInvested - currentValue) / runningInvested;
           maxDrawdown = Math.Max(maxDrawdown, drawdown);
       }

       return maxDrawdown; // Percentage of total invested that was "underwater"
   }
   ```

3. **For smart vs fixed comparison, normalize by total invested:**
   ```csharp
   // Don't just compare cost basis -- compare BTC per dollar
   public class ComparisonResult
   {
       public decimal SmartDcaTotalInvested { get; set; }
       public decimal FixedDcaTotalInvested { get; set; }
       public decimal SmartDcaTotalBtc { get; set; }
       public decimal FixedDcaTotalBtc { get; set; }

       // Key metric: BTC acquired per dollar invested
       public decimal SmartBtcPerDollar => SmartDcaTotalBtc / SmartDcaTotalInvested;
       public decimal FixedBtcPerDollar => FixedDcaTotalBtc / FixedDcaTotalInvested;
       public decimal Improvement => (SmartBtcPerDollar - FixedBtcPerDollar) / FixedBtcPerDollar * 100;
   }
   ```

4. **Handle edge case: simulation ends on a dip:**
   - If the last day is a 30% drawdown, ROI looks terrible even if the strategy was working
   - Report both "final day" metrics and "peak" metrics
   - Consider reporting metrics at multiple endpoints (end of each year)

**Warning signs:**
- Annualized returns exceed 100% (likely a calculation error)
- Max drawdown is 0 or negative (calculation wrong)
- Smart DCA improvement is exactly proportional to average multiplier (ignoring timing)

**Phase mapping:** Address when implementing the metrics calculation layer. Have unit tests for each metric against hand-calculated examples.

---

### Pitfall 8: Parameter Sweep Combinatorial Explosion

**What goes wrong:** Sweep across all parameters creates hundreds of thousands of combinations, takes hours to run, and produces an overwhelming results matrix that's impossible to interpret.

**Why it happens:** The natural approach is to test every combination:
- 3 tier thresholds * 10 values each = 10^3 = 1,000 threshold combos
- 3 tier multipliers * 8 values each = 8^3 = 512 multiplier combos
- BearBoostFactor: 5 values
- MaxMultiplierCap: 5 values
- HighLookbackDays: 5 values
- Total: 1,000 * 512 * 5 * 5 * 5 = **64 million** combinations

Even with the pure in-memory simulation running at 1ms per simulation (1,460 days), 64 million simulations = 64,000 seconds = 17.8 hours.

**Consequences:**
- API endpoint times out (even background processing takes hours)
- Results matrix is too large to store or display meaningfully
- User gets overwhelmed with data, can't make a decision
- Server resource exhaustion during sweep

**Prevention:**
1. **Hierarchical sweep (recommended for this project):**
   ```
   Phase 1: Fix multiplier values, sweep only tier thresholds (3 params, ~100 combos)
   Phase 2: Fix thresholds from Phase 1, sweep multiplier values (~100 combos)
   Phase 3: Fix tiers, sweep bear boost and lookback (~25 combos)
   Total: ~225 simulations instead of millions
   ```

2. **Smart defaults with limited sweeps:**
   ```csharp
   public class SweepPresets
   {
       // "Conservative" preset: sweep only tier thresholds
       public static SweepConfig Conservative => new()
       {
           TierThresholds = [[3,8,15], [5,10,20], [5,10,25], [5,15,30], [7,12,20]],
           FixedMultipliers = [1.5m, 2.0m, 3.0m], // Don't sweep these
           FixedBearBoost = 1.5m,
           FixedLookback = 30
       };

       // "Full" preset: larger sweep but still bounded
       public static SweepConfig Full => new()
       {
           TierThresholds = GenerateThresholdCombos(min: 3, max: 30, step: 2),
           MultiplierSets = GenerateMultiplierSets(min: 1.2m, max: 4.0m, step: 0.5m),
           BearBoostRange = [1.0m, 1.25m, 1.5m, 2.0m],
           LookbackRange = [14, 21, 30, 45, 60]
       };
   }
   ```

3. **Enforce ordering constraints to eliminate invalid combos:**
   ```csharp
   // Tier thresholds must be strictly ascending: tier1 < tier2 < tier3
   // This eliminates ~5/6 of threshold combinations
   // Multipliers should be non-decreasing: mult1 <= mult2 <= mult3

   public static bool IsValidTierConfig(decimal[] thresholds, decimal[] multipliers)
   {
       for (int i = 1; i < thresholds.Length; i++)
       {
           if (thresholds[i] <= thresholds[i - 1]) return false;
           if (multipliers[i] < multipliers[i - 1]) return false;
       }
       return true;
   }
   ```

4. **Report estimated runtime before starting sweep:**
   ```csharp
   public SweepEstimate EstimateSweepRuntime(SweepConfig config, int historicalDays)
   {
       var totalCombinations = config.CalculateCombinations();
       var estimatedMsPerSimulation = historicalDays * 0.01; // ~0.01ms per day
       var totalMs = totalCombinations * estimatedMsPerSimulation;

       return new SweepEstimate
       {
           TotalCombinations = totalCombinations,
           EstimatedDuration = TimeSpan.FromMilliseconds(totalMs),
           RecommendedMaxCombinations = 10_000
       };
   }
   ```

5. **Pagination and top-N results:**
   - Don't return all 10,000 results
   - Return top 20 by primary metric, plus summary statistics
   - Allow drilling into specific parameter ranges

**Warning signs:**
- API request for sweep takes more than 30 seconds
- Memory usage spikes during sweep (storing too many results)
- User can't interpret results because there are too many

**Phase mapping:** Address during sweep API design. The sweep endpoint must accept a configuration that limits combinatorial explosion.

---

### Pitfall 9: Not Accounting for BTC Rounding in Simulation

**What goes wrong:** The simulation calculates exact fractional BTC quantities, but Hyperliquid rounds to 5 decimal places (0.00001 BTC). Over 1,000+ purchases, the rounding difference accumulates to a meaningful amount.

**Why it happens:** The production code rounds BTC quantity to 5 decimals (line 129: `Math.Round(btcQuantity, BtcDecimals, MidpointRounding.ToZero)`). The backtest might skip this rounding for simplicity, or round differently.

**Consequences:**
- For a $10 buy at $100,000 BTC: exact = 0.0001 BTC, rounded = 0.00010 BTC (same)
- For a $10 buy at $97,123 BTC: exact = 0.000102960..., rounded = 0.00010 BTC (lost $0.29 of intended buy)
- Over 1,460 days: cumulative rounding error of $50-200 depending on prices
- Comparison between smart and fixed DCA may be affected differently by rounding

**Prevention:**
1. **Apply the same rounding in simulation as production:**
   ```csharp
   public decimal SimulatePurchase(decimal usdAmount, decimal btcPrice)
   {
       var exactQuantity = usdAmount / btcPrice;
       var roundedQuantity = Math.Round(exactQuantity, 5, MidpointRounding.ToZero); // Match production
       var actualCost = roundedQuantity * btcPrice;
       return roundedQuantity; // This is what you actually get
   }
   ```

2. **Track "dust" (unspent USD from rounding):**
   ```csharp
   var intendedCost = usdAmount;
   var actualCost = roundedQuantity * btcPrice;
   var dust = intendedCost - actualCost;
   // dust accumulates across days; in production it stays in the USDC balance
   ```

**Warning signs:**
- Total BTC in simulation doesn't match sum of individual day quantities (floating point)
- Total cost exceeds total invested (rounding went the wrong way)

**Phase mapping:** Include rounding in the simulation engine from the start. Minor effort, prevents systematic bias.

---

### Pitfall 10: Mixing Backtest and Production Code Paths

**What goes wrong:** Adding backtesting creates conditional branches in production code (if backtesting then ... else ...), introducing bugs in the live trading path.

**Why it happens:** The temptation is to add a `bool isBacktest` parameter to existing services and branch accordingly. This is dangerous because:
- Production code paths get longer and harder to reason about
- A bug in a backtest-only branch could accidentally execute in production
- Test coverage becomes harder (now testing 2x the paths)
- The existing `DryRun` mode already adds one branch; adding backtest is a second

**Consequences:**
- Regression in live trading due to backtest code
- Live trading accidentally uses backtest data source
- Backtest accidentally triggers real orders (if sharing DI container)
- Increased complexity in an already safety-critical path

**Prevention:**
1. **Strict separation: backtest code should NOT modify any existing service:**
   ```
   /Application/Services/DcaExecutionService.cs        -- UNCHANGED
   /Application/Services/PriceDataService.cs           -- UNCHANGED
   /Application/Backtesting/BacktestSimulationEngine.cs -- NEW
   /Application/Backtesting/BacktestPriceProvider.cs    -- NEW
   /Application/Backtesting/BacktestMetrics.cs          -- NEW
   /Application/Backtesting/ParameterSweepEngine.cs     -- NEW
   ```

2. **Extract shared logic into pure functions, not shared services:**
   ```csharp
   // GOOD: Pure function shared between production and backtest
   public static class MultiplierCalculator
   {
       public static MultiplierResult Calculate(decimal price, decimal high30, decimal ma200,
           IReadOnlyList<MultiplierTier> tiers, decimal bearBoost, decimal maxCap) { ... }
   }

   // BAD: Shared service with backtest flag
   public class DcaExecutionService
   {
       public async Task Execute(bool isBacktest = false) // DON'T DO THIS
   ```

3. **The only production code change should be extracting the pure multiplier calculation.** Everything else is new code in a new namespace.

4. **Use separate DI registration for backtest endpoints:**
   ```csharp
   // Backtest services are registered separately and never share instances with production
   // Backtest endpoints are in their own route group
   app.MapGroup("/api/backtest").MapBacktestEndpoints();
   ```

**Warning signs:**
- PRs that modify DcaExecutionService.cs or PriceDataService.cs "for backtesting"
- `if (isBacktest)` branches appearing in production services
- Backtest services taking dependencies on HyperliquidClient (should never need it)

**Phase mapping:** Establish this boundary in the very first backtest PR. Create the `/Application/Backtesting/` namespace and document the "no modification" rule.

---

### Pitfall 11: Insufficient Balance Simulation

**What goes wrong:** Backtest assumes unlimited USDC balance, so every multiplied buy executes fully. In reality, the user has a finite balance, and high-multiplier buys during extended dips can exhaust funds.

**Why it happens:** The production code caps the buy at available balance (line 102: `Math.Min(usdcBalance, multipliedAmount)`). If the backtest ignores balance constraints, parameter sets that trigger frequent 4.5x buys look great in simulation but would fail in practice because the user runs out of USDC.

**Consequences:**
- Sweep selects aggressive parameters that work in theory but are impractical
- User deploys 3x multiplier parameters, runs out of USDC in week 2 of a dip, misses the deepest part
- Backtest shows accumulating during a crash, but live trading would stop buying halfway through

**Prevention:**
1. **Simulate with a realistic starting balance:**
   ```csharp
   public class BacktestConfig
   {
       public decimal InitialUsdcBalance { get; set; } = 10_000m;
       // OR
       public decimal MonthlyUsdcDeposit { get; set; } = 500m; // Periodic top-ups
   }
   ```

2. **Track balance throughout simulation:**
   ```csharp
   decimal usdcBalance = config.InitialUsdcBalance;

   foreach (var day in simulationDays)
   {
       // Monthly deposit simulation
       if (day.Day == 1)
           usdcBalance += config.MonthlyUsdcDeposit;

       var multipliedAmount = baseAmount * multiplier;
       var actualBuy = Math.Min(usdcBalance, multipliedAmount);

       if (actualBuy < MinimumOrderValue)
       {
           // Skip: insufficient balance
           results.Add(new SimDay { Skipped = true, Reason = "Insufficient balance" });
           continue;
       }

       usdcBalance -= actualBuy;
       // ... record purchase ...
   }
   ```

3. **Report "days skipped due to insufficient balance" as a key metric** -- this tells the user how practical the parameter set is.

4. **Offer both modes:** "unlimited balance" (theoretical) and "realistic balance" (practical). Show both in results so the user understands the gap.

**Warning signs:**
- Aggressive parameter sets show zero skipped days (not simulating balance)
- Total invested exceeds realistic amount a person would have
- No "insufficient balance" entries in simulation results

**Phase mapping:** Address in the simulation engine. Include balance simulation as an optional but recommended parameter.

---

## Minor Pitfalls

Mistakes that cause annoyance or minor inaccuracies but are easily fixable.

---

### Pitfall 12: Not Caching Backtest Results

**What goes wrong:** User triggers the same backtest parameters multiple times (e.g., refreshing the page), and each request re-runs the full simulation, wasting CPU and creating poor UX with long load times.

**Prevention:**
- Cache results by parameter hash: `SHA256(JsonSerialize(backtestRequest))`
- Store in PostgreSQL or Redis with a TTL (e.g., 24 hours -- results don't change for fixed historical data)
- Return cached result immediately if available
- Invalidate cache when new historical data is ingested

**Phase mapping:** Nice-to-have optimization after core simulation works.

---

### Pitfall 13: Confusing Close Price, High Price, and VWAP in Analysis

**What goes wrong:** The 30-day high in production uses `Max(Close)` (line 171 of PriceDataService). The backtest might accidentally use `Max(High)` for the 30-day high, producing different multiplier decisions than production.

**Prevention:**
- Document explicitly: "30-day high = max Close price in lookback window"
- Use the same field (`DailyPrice.Close`) in both production and backtest
- The extracted pure `MultiplierCalculator` should take `high30Day` as a parameter, and the caller is responsible for computing it consistently

**Phase mapping:** Address when extracting the multiplier calculator.

---

### Pitfall 14: Decimal Precision Issues in Aggregate Calculations

**What goes wrong:** C# `decimal` has 28-29 significant digits, but aggregating 1,460 daily prices for SMA or summing 1,460 purchase costs can accumulate floating-point-like issues if intermediate calculations use division.

**Prevention:**
- Use `decimal` throughout (already done in the codebase)
- Be careful with `Average()` on large collections (it's fine for decimal in .NET)
- For percentages, compute `(a - b) / b * 100` rather than `a / b * 100 - 100` (the former has better precision when a and b are close)

**Phase mapping:** Low risk for C# decimal. Just be consistent.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Severity | Mitigation |
|-------------|----------------|----------|------------|
| **Historical Data Ingestion** | CoinGecko rate limits exhaust during large fetch | Moderate | Fetch once, cache in DB, rate-limit requests with delays |
| **Historical Data Ingestion** | Missing data points not detected | Critical | Validate completeness after fetch, log gaps |
| **Historical Data Ingestion** | CoinGecko vs Hyperliquid price mismatch | Minor | Document as known limitation, not fixable |
| **Simulation Engine** | Look-ahead bias in 30-day high / 200-day MA | Critical | Use IDateTimeProvider or pass simulated date explicitly |
| **Simulation Engine** | Reusing production services directly (slow, incorrect) | Critical | Extract pure calculation, use in-memory price provider |
| **Simulation Engine** | No warm-up period handling | Moderate | Skip first 200 days from metrics, still simulate them |
| **Simulation Engine** | Ignoring BTC rounding | Moderate | Apply same Math.Round as production |
| **Simulation Engine** | Unlimited balance assumption | Moderate | Add optional balance simulation |
| **Simulation Engine** | Perfect execution assumption | Moderate | Add configurable slippage model |
| **Parameter Sweep** | Combinatorial explosion | Moderate | Hierarchical sweep, enforce ordering constraints |
| **Parameter Sweep** | Overfitting to historical data | Critical | Walk-forward validation, report sensitivity |
| **Metrics Calculation** | Incorrect cost basis or drawdown | Moderate | Unit test each metric against hand calculations |
| **Metrics Calculation** | Unfair smart vs fixed comparison | Moderate | Normalize by total invested, report BTC per dollar |
| **API Integration** | Mixing backtest code into production paths | Critical | Strict namespace separation, no production code changes |
| **API Integration** | Not caching backtest results | Minor | Hash-based cache with TTL |

---

## Summary: Top 5 "Must Not Skip" Items

These are the pitfalls where skipping prevention leads to fundamentally invalid backtest results:

1. **Look-ahead bias (Pitfall 1):** If the simulation can see future prices, ALL results are meaningless. This must be architecturally prevented, not just "handled carefully."

2. **Overfitting via exhaustive sweep (Pitfall 2):** Walk-forward validation must be built into the sweep engine from day one. Without it, every "optimal" parameter set is suspect.

3. **Reusing production services directly (Pitfall 5):** Extract pure multiplier logic and use in-memory price data. This is both a correctness issue (IOptionsMonitor gives wrong config) and a performance issue (1000x slower with DB queries).

4. **Mixing production and backtest code (Pitfall 10):** Create a clean `/Application/Backtesting/` namespace. The only production change should be extracting `MultiplierCalculator` into a pure static class.

5. **Metrics calculation errors (Pitfall 7):** Cost basis must be volume-weighted. Drawdown must account for DCA's accumulation pattern. Smart vs fixed must normalize by total invested.

---

## Confidence Assessment

| Pitfall Category | Confidence | Rationale |
|------------------|------------|-----------|
| Look-ahead bias | HIGH | Universal backtesting pitfall, verified against codebase (PriceDataService uses DateTime.UtcNow) |
| Overfitting | HIGH | Well-established quantitative finance principle, directly applicable to parameter sweep |
| Execution simulation | HIGH | Production code shows IOC with 5% slippage; backtest needs to model this |
| CoinGecko specifics | MEDIUM | Based on training knowledge of CoinGecko API; rate limits and data format may have changed |
| Code separation | HIGH | Examined existing codebase structure; DcaExecutionService has clear coupling points |
| Metrics calculation | HIGH | Standard financial metrics; DCA-specific variants are well-documented in quant literature |
| Performance | HIGH | Math is straightforward: N combinations * M days = total compute; bounded by design |

**Note:** WebSearch and WebFetch were unavailable for this research. CoinGecko API specifics (Pitfall 4) should be verified against current documentation before implementation. All other pitfalls are verified against the existing codebase or are established software engineering / quantitative finance principles.

---

**Research completed:** 2026-02-12
**Ready for roadmap:** Yes

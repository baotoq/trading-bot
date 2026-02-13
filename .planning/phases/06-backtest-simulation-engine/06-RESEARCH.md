# Phase 6: Backtest Simulation Engine - Research

**Researched:** 2026-02-13
**Domain:** Financial backtesting, time series simulation, portfolio metrics calculation
**Confidence:** HIGH

## Summary

This phase builds a day-by-day DCA simulation engine that replays a smart DCA strategy against historical price data. The simulator accepts a strategy configuration and price array as inputs, processes each day sequentially while computing sliding windows (30-day high, 200-day MA), calculates multipliers via the extracted MultiplierCalculator, executes simulated purchases for both smart DCA and fixed DCA baselines, and returns comprehensive metrics with full purchase logs.

The domain is well-established: financial backtesting follows deterministic patterns with clear metrics (total invested, portfolio value, cost basis, max drawdown). C# decimal type provides the precision needed for financial calculations. MoreLINQ's Windowed operator handles sliding window calculations elegantly. The existing MultiplierCalculator (Phase 5) already provides pure, testable calculation logic ready for reuse.

**Primary recommendation:** Build BacktestSimulator as a pure static class in Application/Services namespace, use record types for results and DTOs, leverage MoreLINQ for sliding windows, calculate both fixed-DCA baselines (same-base and match-total) in parallel with smart DCA, structure results as nested sections for clarity, always include full purchase log with running totals and window values.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Simulation behavior:**
- Include BOTH comparison methods: same-base (fixed amount daily, multiplier=1) AND match-total (spread smart DCA's total spend equally across all days)
- Metrics list is complete as specified in roadmap: total invested, total BTC, avg cost basis, portfolio value, return %, cost basis delta, extra BTC %, efficiency ratio
- Max drawdown = peak-to-trough of (portfolio value - total invested) / total invested — worst unrealized loss relative to money put in
- Tier breakdown per tier: trigger count + extra USD spent + extra BTC acquired
- Results structured as nested sections: smartDca, fixedDcaSameBase, fixedDcaMatchTotal, comparison, tierBreakdown

**Purchase log detail:**
- Always included (not opt-in) — every backtest returns the full day-by-day log
- Include running totals per entry: cumulative invested, cumulative BTC, running avg cost basis
- Include window values per entry: 30-day high and MA200 used for that day's calculation
- Include BOTH smart DCA and fixed DCA entries in the log — side-by-side comparison for any given day

**Specific ideas:**
- Both comparison methods requested explicitly — user wants to see both the "spending difference" (same-base) and "efficiency difference" (match-total) angles
- Full transparency in purchase log — include the window values (high30Day, MA200) so users can verify why a particular tier triggered on any given day
- Tier breakdown should show the extra BTC per tier, not just extra spend — user wants to see the concrete BTC impact of each multiplier tier

### Claude's Discretion

- Sliding window computation approach (from data vs pre-computed)
- Warmup strategy for insufficient MA200 data
- Gap handling in price data
- Config DTO design (reuse DcaOptions vs backtest-specific DTO)
- Portfolio valuation timing (last day's price is the obvious choice)
- Whether fixed-DCA baseline buys on same days as smart DCA or independently

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 10.0 | Runtime and language features | Current LTS, project baseline |
| System.Linq | Built-in | LINQ operations for collections | Native .NET, zero dependencies |
| MoreLINQ | 4.4.0+ | Extended LINQ operations (Windowed) | Industry standard for sliding windows, already used in .NET ecosystem |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | 2.9.3 | Test framework | Already in use, for BacktestSimulator unit tests |
| FluentAssertions | 7.0.0 | Assertion library | Already in use, for readable test assertions |
| Snapper | 2.4.1 | Snapshot testing | Already in use (Phase 5), for regression tests on backtest results |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| MoreLINQ Windowed | Manual sliding window with for loops | MoreLINQ is cleaner and battle-tested; manual implementation risks off-by-one errors |
| Static class | BacktestSimulator service with DI | Static class is simpler for pure computation, matches MultiplierCalculator pattern, easier to test |
| Record types for results | Class types | Records provide value equality and immutability by default, perfect for result objects |
| In-memory computation | Streaming/yield return | Full materialization is fine for 2-4 years of daily data (~1460 rows max), simpler code |

**Installation:**

```bash
# Add MoreLINQ if not already present
dotnet add TradingBot.ApiService package MoreLINQ

# Test dependencies already installed (xUnit, FluentAssertions, Snapper)
```

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── Application/
│   └── Services/
│       ├── MultiplierCalculator.cs       # EXISTING: Pure static calculation (Phase 5)
│       ├── BacktestSimulator.cs          # NEW: Pure static simulation engine
│       └── Backtest/
│           ├── BacktestConfig.cs         # NEW: Configuration DTO
│           ├── BacktestResult.cs         # NEW: Result record with nested sections
│           ├── PurchaseLogEntry.cs       # NEW: Single day's purchase detail
│           └── TierBreakdown.cs          # NEW: Per-tier trigger stats
tests/TradingBot.ApiService.Tests/
├── Application/
│   └── Services/
│       ├── MultiplierCalculatorTests.cs  # EXISTING: Phase 5 tests
│       └── BacktestSimulatorTests.cs     # NEW: Simulation engine tests
```

### Pattern 1: Pure Static Simulator

**What:** Static class with pure functions - no state, no async, no DI, deterministic outputs. Same pattern as MultiplierCalculator (Phase 5).

**When to use:** For calculations that don't need I/O, database access, or mutable state. Perfect for reusable logic in API endpoints (Phase 8) and parameter sweeps.

**Example:**

```csharp
namespace TradingBot.ApiService.Application.Services;

public static class BacktestSimulator
{
    public static BacktestResult Run(
        BacktestConfig config,
        IReadOnlyList<DailyPriceData> priceData)
    {
        // Validate inputs
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(priceData);

        if (priceData.Count == 0)
            throw new ArgumentException("Price data cannot be empty", nameof(priceData));

        // Simulate day-by-day
        var purchaseLog = new List<PurchaseLogEntry>();

        for (int i = 0; i < priceData.Count; i++)
        {
            var currentDay = priceData[i];

            // Compute sliding windows
            var high30Day = ComputeHigh30Day(priceData, i, config.HighLookbackDays);
            var ma200Day = ComputeMA200(priceData, i, config.BearMarketMaPeriod);

            // Calculate multiplier for smart DCA
            var multiplierResult = MultiplierCalculator.Calculate(
                currentDay.Close,
                config.BaseDailyAmount,
                high30Day,
                ma200Day,
                config.Tiers,
                config.BearBoostFactor,
                config.MaxMultiplierCap);

            // Execute simulated purchases (smart + fixed baselines)
            // ... purchase logic
        }

        // Aggregate metrics and return result
        return BuildResult(purchaseLog, config);
    }

    private static decimal ComputeHigh30Day(
        IReadOnlyList<DailyPriceData> priceData,
        int currentIndex,
        int lookbackDays) { /* ... */ }

    private static decimal ComputeMA200(
        IReadOnlyList<DailyPriceData> priceData,
        int currentIndex,
        int maPeriod) { /* ... */ }
}
```

**Key points:**
- Synchronous (no async) — all data pre-loaded in memory
- No DI dependencies — only MultiplierCalculator static call
- Immutable inputs (IReadOnlyList)
- Throws ArgumentException for invalid inputs (fail fast)

### Pattern 2: Sliding Window Calculation with MoreLINQ

**What:** Use MoreLINQ's Windowed operator for clean, efficient sliding window calculations.

**When to use:** For 30-day high lookback and 200-day MA computation over time series.

**Example:**

```csharp
// Compute 30-day high using MoreLINQ Windowed
private static decimal ComputeHigh30Day(
    IReadOnlyList<DailyPriceData> priceData,
    int currentIndex,
    int lookbackDays)
{
    // Edge case: insufficient data for full window
    if (currentIndex < lookbackDays - 1)
    {
        // Warmup strategy: use all available data up to current day
        var availableWindow = priceData.Take(currentIndex + 1);
        return availableWindow.Max(p => p.High);
    }

    // Full window available
    var window = priceData.Skip(currentIndex - lookbackDays + 1).Take(lookbackDays);
    return window.Max(p => p.High);
}

// Compute 200-day MA using MoreLINQ Windowed
private static decimal ComputeMA200(
    IReadOnlyList<DailyPriceData> priceData,
    int currentIndex,
    int maPeriod)
{
    // Edge case: insufficient data for MA
    if (currentIndex < maPeriod - 1)
    {
        // Warmup strategy: return 0 to signal "unavailable"
        // MultiplierCalculator treats ma200Day=0 as non-bear market
        return 0m;
    }

    // Full window available
    var window = priceData.Skip(currentIndex - maPeriod + 1).Take(maPeriod);
    return window.Average(p => p.Close);
}
```

**Alternative using MoreLINQ Windowed for bulk processing:**

```csharp
// Pre-compute all 30-day highs in one pass
var high30DayValues = priceData
    .Windowed(30)
    .Select(window => window.Max(p => p.High))
    .ToList();

// Pre-compute all MA200 values in one pass
var ma200Values = priceData
    .Windowed(200)
    .Select(window => window.Average(p => p.Close))
    .ToList();
```

**Recommendation:** Use index-based approach (first example) for clarity in day-by-day loop. Pre-computation (second example) is optimization that can wait until performance profiling shows need.

### Pattern 3: Nested Result Structure

**What:** Structure backtest results as nested records for clear organization and type safety.

**When to use:** When results have multiple logical sections (smart DCA, fixed DCA, comparison, tier breakdown).

**Example:**

```csharp
public record BacktestResult(
    SmartDcaMetrics SmartDca,
    FixedDcaMetrics FixedDcaSameBase,
    FixedDcaMetrics FixedDcaMatchTotal,
    ComparisonMetrics Comparison,
    IReadOnlyList<TierBreakdown> TierBreakdown,
    IReadOnlyList<PurchaseLogEntry> PurchaseLog);

public record SmartDcaMetrics(
    decimal TotalInvested,
    decimal TotalBtc,
    decimal AvgCostBasis,
    decimal PortfolioValue,
    decimal ReturnPercent,
    decimal MaxDrawdown);

public record FixedDcaMetrics(
    decimal TotalInvested,
    decimal TotalBtc,
    decimal AvgCostBasis,
    decimal PortfolioValue,
    decimal ReturnPercent);

public record ComparisonMetrics(
    decimal CostBasisDelta,      // Smart avg cost - Fixed avg cost
    decimal ExtraBtcPercent,     // (Smart BTC - Fixed BTC) / Fixed BTC * 100
    decimal EfficiencyRatio);    // Smart return % / Fixed return %

public record TierBreakdown(
    string TierName,              // ">= 5%", ">= 10%", ">= 20%"
    int TriggerCount,
    decimal ExtraUsdSpent,        // USD spent above base amount
    decimal ExtraBtcAcquired);    // Extra BTC from this tier's multiplier

public record PurchaseLogEntry(
    DateOnly Date,
    decimal Price,
    // Smart DCA fields
    decimal SmartMultiplier,
    string SmartTier,
    decimal SmartAmountUsd,
    decimal SmartBtcBought,
    decimal SmartCumulativeUsd,
    decimal SmartCumulativeBtc,
    decimal SmartRunningCostBasis,
    // Fixed DCA (same-base) fields
    decimal FixedSameBaseAmountUsd,
    decimal FixedSameBaseBtcBought,
    decimal FixedSameBaseCumulativeUsd,
    decimal FixedSameBaseCumulativeBtc,
    decimal FixedSameBaseRunningCostBasis,
    // Fixed DCA (match-total) fields
    decimal FixedMatchTotalAmountUsd,
    decimal FixedMatchTotalBtcBought,
    decimal FixedMatchTotalCumulativeUsd,
    decimal FixedMatchTotalCumulativeBtc,
    decimal FixedMatchTotalRunningCostBasis,
    // Window values for transparency
    decimal High30Day,
    decimal Ma200Day);
```

**Benefits:**
- Type safety (can't mix up smart vs fixed metrics)
- Clear API (intellisense shows nested structure)
- Serializes cleanly to JSON for API endpoints (Phase 8)
- Immutable (records)

### Pattern 4: Cost Basis and Portfolio Metrics Calculation

**What:** Financial metrics calculations with decimal precision for accuracy.

**When to use:** Aggregating purchase data into portfolio-level metrics.

**Example:**

```csharp
// Average cost basis (weighted average price)
private static decimal CalculateAvgCostBasis(
    decimal totalInvested,
    decimal totalBtc)
{
    if (totalBtc == 0) return 0m;
    return totalInvested / totalBtc;
}

// Portfolio value at current price
private static decimal CalculatePortfolioValue(
    decimal totalBtc,
    decimal currentPrice)
{
    return totalBtc * currentPrice;
}

// Return percentage
private static decimal CalculateReturnPercent(
    decimal portfolioValue,
    decimal totalInvested)
{
    if (totalInvested == 0) return 0m;
    return ((portfolioValue - totalInvested) / totalInvested) * 100m;
}

// Max drawdown (peak-to-trough unrealized loss)
private static decimal CalculateMaxDrawdown(
    IReadOnlyList<PurchaseLogEntry> purchaseLog,
    IReadOnlyList<DailyPriceData> priceData)
{
    decimal maxDrawdown = 0m;
    decimal peakValue = 0m;

    for (int i = 0; i < purchaseLog.Count; i++)
    {
        var entry = purchaseLog[i];
        var currentPrice = priceData[i].Close;

        // Portfolio value at this day
        var portfolioValue = entry.SmartCumulativeBtc * currentPrice;
        var unrealizedPnL = portfolioValue - entry.SmartCumulativeUsd;

        // Track peak
        if (unrealizedPnL > peakValue)
            peakValue = unrealizedPnL;

        // Calculate drawdown from peak
        if (peakValue > 0)
        {
            var drawdown = (unrealizedPnL - peakValue) / entry.SmartCumulativeUsd * 100m;
            if (drawdown < maxDrawdown)
                maxDrawdown = drawdown;
        }
    }

    return Math.Abs(maxDrawdown); // Return as positive percentage
}

// Efficiency ratio (smart return % / fixed return %)
private static decimal CalculateEfficiencyRatio(
    decimal smartReturnPercent,
    decimal fixedReturnPercent)
{
    if (fixedReturnPercent == 0) return 0m;
    return smartReturnPercent / fixedReturnPercent;
}
```

**Key insights:**
- Use decimal for all financial calculations (no float/double)
- Guard against division by zero
- Max drawdown tracks worst unrealized loss from peak
- Efficiency ratio > 1.0 means smart DCA outperformed

### Pattern 5: Warmup Strategy for Insufficient Data

**What:** Handle edge cases where sliding windows don't have enough data (early days in simulation).

**When to use:** First 30 days (no full 30-day high window), first 200 days (no MA200).

**Example:**

```csharp
// Warmup strategy for 30-day high: use all available data
private static decimal ComputeHigh30Day(
    IReadOnlyList<DailyPriceData> priceData,
    int currentIndex,
    int lookbackDays)
{
    if (currentIndex < lookbackDays - 1)
    {
        // Insufficient data: use all available days (0 to currentIndex)
        var availableWindow = priceData.Take(currentIndex + 1);
        return availableWindow.Max(p => p.High);
    }

    // Full window available
    var window = priceData.Skip(currentIndex - lookbackDays + 1).Take(lookbackDays);
    return window.Max(p => p.High);
}

// Warmup strategy for MA200: return 0 to signal unavailable
private static decimal ComputeMA200(
    IReadOnlyList<DailyPriceData> priceData,
    int currentIndex,
    int maPeriod)
{
    if (currentIndex < maPeriod - 1)
    {
        // Insufficient data: return 0
        // MultiplierCalculator treats ma200Day=0 as non-bear market (conservative)
        return 0m;
    }

    // Full window available
    var window = priceData.Skip(currentIndex - maPeriod + 1).Take(maPeriod);
    return window.Average(p => p.Close);
}
```

**Rationale:**
- 30-day high: Using partial window (e.g., 10 days if only 10 days available) is better than no calculation — still provides relative high for drop percentage
- MA200: Returning 0 signals "unavailable" which MultiplierCalculator interprets as non-bear market — conservative default that avoids false bear signals

**Alternative warmup strategies considered:**
- Skip first 200 days entirely (wasteful, loses early simulation data)
- Use exponentially weighted MA during warmup (complex, inconsistent with live DCA)
- Require price data to start 200 days before backtest start (inflexible, Phase 7 data ingestion complication)

**Recommendation:** Implement the "partial window for high, zero for MA" strategy above. It's simple, conservative, and matches how the system would behave if launched with incomplete historical data.

### Anti-Patterns to Avoid

- **Async static methods:** BacktestSimulator should be 100% synchronous. All data is in-memory, no I/O needed.
- **Mutating input data:** IReadOnlyList ensures price data can't be modified. Don't convert to mutable List internally.
- **Floating point types:** Never use float or double for financial calculations. Always decimal.
- **Null return values:** If simulation fails, throw exception with clear message. Don't return null BacktestResult.
- **Off-by-one errors in windows:** Be explicit about inclusive/exclusive boundaries. Test with edge cases (day 0, day 29, day 199).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Sliding windows | Manual for loops with index tracking | MoreLINQ Windowed or simple Skip/Take | MoreLINQ is battle-tested; manual loops risk off-by-one errors |
| Decimal financial math | Custom rounding, currency types | Built-in decimal type | Decimal is precise for base-10 money calculations, already in .NET |
| Result aggregation | Manual loops to sum/average | LINQ Sum/Average/Max/Min | LINQ is readable and optimized |
| Cost basis tracking | Manual running total with mutable variables | Immutable records with Select + Aggregate | Functional approach is clearer and less error-prone |

**Key insight:** C# decimal + LINQ + MoreLINQ cover 95% of backtest simulation needs. Don't reinvent financial libraries or custom aggregation logic. Keep it simple and leverage framework.

## Common Pitfalls

### Pitfall 1: Off-by-One Errors in Sliding Windows

**What goes wrong:** Window calculations include/exclude wrong days, leading to incorrect 30-day high or MA200.

**Why it happens:** Confusion between 0-based indexing, inclusive/exclusive ranges, and "30 days including today" vs "30 days before today".

**How to avoid:**
- Be explicit: "30-day high" means current day + previous 29 days (30 total)
- Test boundary cases: day 0, day 29 (first full window), day 199 (first full MA200)
- Verify manually: if currentIndex=29, window should include indices [0..29] (30 items)

**Warning signs:**
- Window size is 29 or 31 instead of 30
- MA200 calculation starts at day 201 instead of day 199
- First purchase log entry has incorrect high30Day

**Code pattern:**

```csharp
// CORRECT: 30-day window including current day
var window = priceData.Skip(currentIndex - 29).Take(30);

// WRONG: only 29 days
var window = priceData.Skip(currentIndex - 29).Take(29);

// WRONG: 31 days
var window = priceData.Skip(currentIndex - 30).Take(30);
```

### Pitfall 2: Division by Zero in Metrics

**What goes wrong:** Calculating return % when total invested is 0, or efficiency ratio when fixed return is 0.

**Why it happens:** Edge cases in test data (empty simulation, zero purchases) or early days before any buys.

**How to avoid:** Guard all division operations with zero checks, return sensible defaults (0 or 0m).

**Warning signs:**
- DivideByZeroException in metric calculation
- Tests fail with zero-length price data

**Code pattern:**

```csharp
// CORRECT: guard against division by zero
private static decimal CalculateReturnPercent(decimal portfolioValue, decimal totalInvested)
{
    if (totalInvested == 0) return 0m;
    return ((portfolioValue - totalInvested) / totalInvested) * 100m;
}

// WRONG: can throw DivideByZeroException
private static decimal CalculateReturnPercent(decimal portfolioValue, decimal totalInvested)
{
    return ((portfolioValue - totalInvested) / totalInvested) * 100m;
}
```

### Pitfall 3: Incorrect Cost Basis Calculation

**What goes wrong:** Running average cost basis computed incorrectly, leading to wrong metrics and comparisons.

**Why it happens:** Not understanding weighted average: cost basis = total USD spent / total BTC acquired, not average of individual purchase prices.

**How to avoid:** Use weighted average formula consistently. Test with known inputs.

**Warning signs:**
- Cost basis doesn't match manual calculation
- Cost basis decreases when price goes up (impossible)

**Code pattern:**

```csharp
// CORRECT: weighted average
decimal runningCostBasis = cumulativeUsdSpent / cumulativeBtc;

// WRONG: simple average of prices
decimal runningCostBasis = purchasePrices.Average();
```

### Pitfall 4: Fixed DCA Match-Total Incorrect Distribution

**What goes wrong:** "Match-total" fixed DCA doesn't correctly spread smart DCA's total spend equally across all days.

**Why it happens:** Computing match-total daily amount before knowing final smart DCA total, or not rounding correctly.

**How to avoid:**
1. Run smart DCA simulation first, get total USD spent
2. Compute match-total daily amount = smart total / number of days
3. Simulate match-total DCA with this fixed daily amount

**Warning signs:**
- Match-total total invested doesn't equal smart DCA total invested
- Rounding errors accumulate (use decimal, not double)

**Code pattern:**

```csharp
// CORRECT: two-pass approach
// Pass 1: Run smart DCA, get total
var smartTotalInvested = RunSmartDca(priceData, config);

// Pass 2: Run match-total with fixed daily amount
var matchTotalDailyAmount = smartTotalInvested / priceData.Count;
var matchTotalResult = RunFixedDca(priceData, matchTotalDailyAmount);

// Verify: should be equal (within rounding tolerance)
Assert.Equal(smartTotalInvested, matchTotalResult.TotalInvested, precision: 2);
```

### Pitfall 5: Max Drawdown Calculation Errors

**What goes wrong:** Max drawdown computed incorrectly — using portfolio value peak instead of unrealized PnL peak, or wrong denominator.

**Why it happens:** Drawdown definition is nuanced: peak-to-trough of (portfolio value - total invested) / total invested, not portfolio value alone.

**How to avoid:**
- Track unrealized PnL (portfolio value - money invested), not portfolio value
- Peak is max unrealized PnL (max profit), trough is lowest point after peak
- Denominator is total invested at trough time, not peak time
- Return as positive percentage (absolute value)

**Warning signs:**
- Max drawdown is positive (should be negative or zero)
- Drawdown doesn't match manual calculation from purchase log

**Code pattern:**

```csharp
// CORRECT: track unrealized PnL peak
decimal maxDrawdown = 0m;
decimal peakUnrealizedPnL = 0m;

for (int i = 0; i < purchaseLog.Count; i++)
{
    var portfolioValue = purchaseLog[i].SmartCumulativeBtc * priceData[i].Close;
    var unrealizedPnL = portfolioValue - purchaseLog[i].SmartCumulativeUsd;

    if (unrealizedPnL > peakUnrealizedPnL)
        peakUnrealizedPnL = unrealizedPnL;

    if (peakUnrealizedPnL > 0)
    {
        var drawdown = (unrealizedPnL - peakUnrealizedPnL) / purchaseLog[i].SmartCumulativeUsd * 100m;
        if (drawdown < maxDrawdown)
            maxDrawdown = drawdown;
    }
}

return Math.Abs(maxDrawdown); // Return as positive %
```

## Code Examples

Verified patterns from financial backtesting and C# decimal best practices:

### Full Backtest Simulation Loop

```csharp
// Day-by-day simulation with running totals
public static BacktestResult Run(
    BacktestConfig config,
    IReadOnlyList<DailyPriceData> priceData)
{
    var purchaseLog = new List<PurchaseLogEntry>();

    // Running totals for smart DCA
    decimal smartCumulativeUsd = 0m;
    decimal smartCumulativeBtc = 0m;

    // Running totals for fixed DCA (same-base)
    decimal fixedSameBaseCumulativeUsd = 0m;
    decimal fixedSameBaseCumulativeBtc = 0m;

    for (int i = 0; i < priceData.Count; i++)
    {
        var currentDay = priceData[i];

        // Compute sliding windows
        var high30Day = ComputeHigh30Day(priceData, i, config.HighLookbackDays);
        var ma200Day = ComputeMA200(priceData, i, config.BearMarketMaPeriod);

        // Smart DCA: calculate multiplier
        var multiplierResult = MultiplierCalculator.Calculate(
            currentDay.Close,
            config.BaseDailyAmount,
            high30Day,
            ma200Day,
            config.Tiers,
            config.BearBoostFactor,
            config.MaxMultiplierCap);

        // Smart DCA: execute purchase
        decimal smartAmountUsd = multiplierResult.FinalAmount;
        decimal smartBtcBought = smartAmountUsd / currentDay.Close;
        smartCumulativeUsd += smartAmountUsd;
        smartCumulativeBtc += smartBtcBought;
        decimal smartCostBasis = smartCumulativeUsd / smartCumulativeBtc;

        // Fixed DCA (same-base): execute purchase
        decimal fixedSameBaseAmountUsd = config.BaseDailyAmount; // Always 1.0x
        decimal fixedSameBaseBtcBought = fixedSameBaseAmountUsd / currentDay.Close;
        fixedSameBaseCumulativeUsd += fixedSameBaseAmountUsd;
        fixedSameBaseCumulativeBtc += fixedSameBaseBtcBought;
        decimal fixedSameBaseCostBasis = fixedSameBaseCumulativeUsd / fixedSameBaseCumulativeBtc;

        // Record purchase log entry
        purchaseLog.Add(new PurchaseLogEntry(
            Date: currentDay.Date,
            Price: currentDay.Close,
            SmartMultiplier: multiplierResult.Multiplier,
            SmartTier: multiplierResult.Tier,
            SmartAmountUsd: smartAmountUsd,
            SmartBtcBought: smartBtcBought,
            SmartCumulativeUsd: smartCumulativeUsd,
            SmartCumulativeBtc: smartCumulativeBtc,
            SmartRunningCostBasis: smartCostBasis,
            FixedSameBaseAmountUsd: fixedSameBaseAmountUsd,
            FixedSameBaseBtcBought: fixedSameBaseBtcBought,
            FixedSameBaseCumulativeUsd: fixedSameBaseCumulativeUsd,
            FixedSameBaseCumulativeBtc: fixedSameBaseCumulativeBtc,
            FixedSameBaseRunningCostBasis: fixedSameBaseCostBasis,
            // ... match-total fields (computed in second pass)
            High30Day: high30Day,
            Ma200Day: ma200Day));
    }

    // Second pass: compute match-total fixed DCA
    decimal matchTotalDailyAmount = smartCumulativeUsd / priceData.Count;
    // ... run match-total simulation

    // Aggregate metrics
    var finalPrice = priceData[^1].Close;
    var smartMetrics = ComputeMetrics(smartCumulativeUsd, smartCumulativeBtc, finalPrice, purchaseLog, priceData);
    // ... compute fixed metrics, comparison, tier breakdown

    return new BacktestResult(/* ... */);
}
```

### Tier Breakdown Aggregation

```csharp
// Aggregate tier trigger counts and extra spend/BTC
private static IReadOnlyList<TierBreakdown> ComputeTierBreakdown(
    IReadOnlyList<PurchaseLogEntry> purchaseLog,
    IReadOnlyList<MultiplierTier> tiers,
    decimal baseDailyAmount)
{
    var breakdown = new List<TierBreakdown>();

    foreach (var tier in tiers)
    {
        var tierName = $">= {tier.DropPercentage}%";

        // Filter log entries for this tier
        var tierEntries = purchaseLog.Where(e => e.SmartTier == tierName).ToList();

        int triggerCount = tierEntries.Count;
        decimal extraUsdSpent = tierEntries.Sum(e => e.SmartAmountUsd - baseDailyAmount);
        decimal extraBtcAcquired = tierEntries.Sum(e => e.SmartBtcBought - (baseDailyAmount / e.Price));

        breakdown.Add(new TierBreakdown(
            TierName: tierName,
            TriggerCount: triggerCount,
            ExtraUsdSpent: extraUsdSpent,
            ExtraBtcAcquired: extraBtcAcquired));
    }

    return breakdown.AsReadOnly();
}
```

### Test: Verify Deterministic Results

```csharp
[Fact]
public void Run_SameInputs_ProducesDeterministicResults()
{
    // Arrange
    var config = new BacktestConfig(/* ... */);
    var priceData = GenerateSamplePriceData(365); // 1 year

    // Act
    var result1 = BacktestSimulator.Run(config, priceData);
    var result2 = BacktestSimulator.Run(config, priceData);

    // Assert: exact same results every time
    result1.Should().BeEquivalentTo(result2);
    result1.SmartDca.TotalBtc.Should().Be(result2.SmartDca.TotalBtc);
    result1.PurchaseLog.Count.Should().Be(result2.PurchaseLog.Count);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Float/double for money | Decimal type | .NET 1.0 (2002) | Eliminates rounding errors in financial calculations |
| Manual sliding windows | MoreLINQ Windowed | MoreLINQ 1.0 (2010) | Cleaner, less error-prone window calculations |
| Mutable state in simulation | Immutable records for results | C# 9 (2020) | Thread-safe, easier to test, value equality built-in |
| Async everywhere | Pure sync for in-memory compute | .NET 5+ (2020) | Simpler code for CPU-bound operations |
| Custom cost basis tracking | LINQ aggregation | LINQ (2008) | Declarative, readable, optimized |

**Deprecated/outdated:**
- Using float or double for financial data (causes 0.1 + 0.2 != 0.3 issues)
- Mutable classes for result objects (records are superior)
- Custom loop logic for aggregations (LINQ is clearer)

## Open Questions

1. **Gap Handling in Price Data**
   - What we know: User will fetch 2-4 years of data from CoinGecko (Phase 7), weekends/holidays may have gaps
   - What's unclear: Should backtest skip missing days, interpolate, or fail?
   - Recommendation: Skip missing days — treat price array as is, don't invent data. User can validate completeness via data status endpoint (Phase 7) before running backtest. Document assumption: "Backtest assumes one purchase per day in the provided price data; missing days are not simulated."

2. **Config DTO Design: Reuse DcaOptions vs Backtest-Specific**
   - What we know: DcaOptions already has BaseDailyAmount, Tiers, BearBoostFactor, MaxMultiplierCap, HighLookbackDays, BearMarketMaPeriod
   - What's unclear: Backtest needs date range start/end, but DcaOptions has DailyBuyHour/Minute (irrelevant for backtest)
   - Recommendation: Create backtest-specific BacktestConfig record that mirrors DcaOptions fields but omits schedule-related fields. This keeps concerns separate and allows backtest config to diverge in Phase 8 (parameter sweeps need ranges, not single values).

3. **Portfolio Valuation Timing**
   - What we know: Need final portfolio value to compute return %
   - What's unclear: Use last day's closing price, or last day's high/low/average?
   - Recommendation: Use last day's closing price — matches real-world scenario (you'd check portfolio value at end of day), consistent with how daily purchases use Close price.

4. **Fixed DCA Baseline: Same Days vs Independent**
   - What we know: User wants both same-base and match-total comparisons
   - What's unclear: Does fixed DCA buy on every day in the price array (same as smart DCA), or only on days when smart DCA would have bought?
   - Recommendation: Fixed DCA buys on same days as smart DCA (every day in price array). This isolates the "multiplier strategy" variable — both strategies buy daily, only difference is the amount (fixed vs smart). If fixed bought on different days, we'd be testing "daily vs sporadic" not "fixed vs smart multipliers."

## Sources

### Primary (HIGH confidence)

- [Handling Precision in Financial Calculations in .NET - Medium](https://medium.com/@stanislavbabenko/handling-precision-in-financial-calculations-in-net-a-deep-dive-into-decimal-and-common-pitfalls-1211cc5edd3b) - Decimal type best practices
- [Precision Matters in C#: Money, Time Zones, Date Ranges](https://developersvoice.com/blog/csharp/precision-handling-money-time-ranges/) - Financial precision patterns
- [MoreLINQ Windowed Method Documentation](https://morelinq.github.io/2.6/ref/api/html/M_MoreLinq_MoreEnumerable_Windowed__1.htm) - Official API reference
- [Exploring MoreLINQ Part 15 - Windowing](https://markheath.net/post/exploring-morelinq-15-windowing) - Practical windowing examples
- [Maximum Drawdown (MDD) Formula + Calculator](https://www.wallstreetprep.com/knowledge/maximum-drawdown-mdd/) - Financial metric definition
- [Backtest MasterClass — Part 3: Maximum Drawdown - Medium](https://medium.com/mudrex/backtest-masterclass-part-3-8827ba335d25) - Drawdown calculation in backtesting
- Codebase analysis: `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Application/Services/MultiplierCalculator.cs` - Phase 5 pure static class pattern
- Codebase analysis: `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Models/DailyPrice.cs` - Existing price data model

### Secondary (MEDIUM confidence)

- [QuantConnect Warm Up Periods Documentation](https://www.quantconnect.com/docs/v2/writing-algorithms/historical-data/warm-up-periods) - Warmup strategy patterns
- [Cost Basis Average Calculation - Portfolio Slicer](http://www.portfolioslicer.com/docs/excel-howto-track-cost-base.html) - Weighted average cost basis
- [DCA Backtest Calculator - DRIPCalc](https://www.dripcalc.com/backtest/) - DCA performance metrics examples
- [Sliding Window in C#: Reusable and Efficient Approach](https://spin.atomicobject.com/sliding-window-c-sharp/) - Sliding window implementation patterns
- [LEAN Algorithmic Trading Engine](https://www.lean.io/) - Open-source C# backtesting platform architecture
- [Sharpe Ratio vs Sortino Ratio - Picture Perfect Portfolios](https://pictureperfectportfolios.com/sharpe-ratio-vs-sortino-ratio/) - Portfolio efficiency metrics comparison

### Tertiary (LOW confidence)

- [GitHub - mccaffers/backtesting-engine](https://github.com/mccaffers/backtesting-engine) - C# backtesting engine example
- [SimpleBacktestLib C# Library](https://github.com/NotCoffee418/SimpleBacktestLib) - Basic backtesting patterns
- [BackAlpha Dollar Cost Averaging Strategy](https://backalpha.com/strategies/dollar-cost-averaging) - DCA backtest methodology

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - .NET 10 decimal type is proven, MoreLINQ is stable and widely used, existing test stack verified
- Architecture: HIGH - Pure static class pattern proven in Phase 5, nested records are idiomatic C# 9+, sliding window calculations have clear implementations
- Pitfalls: HIGH - Identified from financial calculation best practices (decimal precision), sliding window common errors (off-by-one), and drawdown calculation nuances (peak-to-trough definition)

**Research date:** 2026-02-13
**Valid until:** 2026-03-13 (30 days - stable .NET 10 LTS, established MoreLINQ library, financial metrics are timeless)

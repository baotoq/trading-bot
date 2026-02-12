# Architecture: Backtesting Engine Integration

**Domain:** DCA Strategy Backtesting for existing BTC Smart DCA Bot
**Researched:** 2026-02-12
**Confidence:** HIGH (based on thorough analysis of existing codebase, well-understood patterns)

## Executive Summary

The backtesting engine integrates into the existing .NET 10.0 DCA bot by **extracting the multiplier calculation logic into a reusable pure-function core** and building a new simulation layer that feeds historical price data through it. The key architectural insight is that `DcaExecutionService.CalculateMultiplierAsync` currently contains the multiplier logic *inline* -- it needs to be extracted into a standalone `MultiplierCalculator` static class that both live execution and backtesting can call without any infrastructure dependencies (no DB, no HTTP, no DI).

**Key Architectural Decisions:**
1. **Extract MultiplierCalculator** as a static pure-function class -- zero dependencies, infinitely reusable
2. **IPriceDataProvider abstraction** to swap between live API and in-memory historical data
3. **Synchronous request-response** for single backtests (sub-second for 4 years of daily data)
4. **Parallel.ForEachAsync** for parameter sweeps (CPU-bound, embarrassingly parallel)
5. **No persistence** of backtest results -- compute on-the-fly (cheap to recompute, expensive to store every sweep)
6. **Minimal API endpoints** returning structured JSON -- no UI, no WebSocket, no async polling
7. **CoinGecko as historical data source** -- 4+ years of BTC daily OHLC, ingested into existing DailyPrice table

## Integration Points with Existing Code

### What Gets Reused (Unchanged)

| Component | Location | How Backtesting Uses It |
|-----------|----------|------------------------|
| `DailyPrice` entity | `Models/DailyPrice.cs` | Same entity stores CoinGecko historical data |
| `DcaOptions` model | `Configuration/DcaOptions.cs` | Backtesting creates DcaOptions instances for each parameter combination |
| `MultiplierTier` model | `Configuration/DcaOptions.cs` | Parameter sweep generates different tier configurations |
| `TradingBotDbContext` | `Infrastructure/Data/TradingBotDbContext.cs` | Query historical DailyPrice data for simulation |
| `BaseEntity` / `AuditedEntity` | `BuildingBlocks/` | If backtest results are persisted (optional) |

### What Gets Extracted (Refactored)

| Current Location | What's Extracted | New Location |
|------------------|-----------------|--------------|
| `DcaExecutionService.CalculateMultiplierAsync` (lines 270-354) | Multiplier calculation logic | `Application/Services/MultiplierCalculator.cs` (new static class) |
| Inline 30-day high / drop-% / bear-boost logic | Pure math: (high, current, tiers) -> multiplier | Same static class, zero dependencies |

### What's New (Added)

| Component | Purpose | Location |
|-----------|---------|----------|
| `MultiplierCalculator` | Static pure-function multiplier logic | `Application/Services/MultiplierCalculator.cs` |
| `BacktestSimulator` | Runs DCA simulation over price series | `Application/Backtesting/BacktestSimulator.cs` |
| `ParameterSweepService` | Parallelizes simulation across parameter combos | `Application/Backtesting/ParameterSweepService.cs` |
| `CoinGeckoClient` | Fetches 4-year BTC OHLC history | `Infrastructure/CoinGecko/CoinGeckoClient.cs` |
| `CoinGeckoDataIngestionService` | One-time/on-demand import to DailyPrice | `Infrastructure/CoinGecko/CoinGeckoDataIngestionService.cs` |
| `BacktestRequest` | Request DTO for single backtest | `Application/Backtesting/Models/BacktestRequest.cs` |
| `BacktestResult` | Response DTO with metrics | `Application/Backtesting/Models/BacktestResult.cs` |
| `SweepRequest` / `SweepResult` | DTOs for parameter sweep | `Application/Backtesting/Models/SweepModels.cs` |
| `CoinGeckoOptions` | API key, base URL config | `Configuration/CoinGeckoOptions.cs` |
| Backtest API endpoints | Minimal API route group | Registered in `Program.cs` |

## Recommended Architecture

```
EXISTING (unchanged)                    NEW (backtesting)
========================               ========================

Program.cs ─────────────────────────── + MapBacktestEndpoints()
  |                                        |
  |                                    Backtest API Endpoints
  |                                    POST /api/backtest
  |                                    POST /api/backtest/sweep
  |                                    POST /api/backtest/data/ingest
  |                                    GET  /api/backtest/data/status
  |                                        |
DcaExecutionService ──── EXTRACTS ──── MultiplierCalculator (static)
  |                      LOGIC TO          |
  | (now calls                         BacktestSimulator
  |  MultiplierCalculator                  |
  |  instead of inline)                ParameterSweepService
  |                                        |
PriceDataService                       CoinGeckoClient
  |                                        |
TradingBotDbContext ─── SHARED ──────── (same DailyPrice table)
```

## Component Design

### 1. MultiplierCalculator (Extracted Core Logic)

**The most critical refactoring.** Currently the multiplier logic lives inside `DcaExecutionService.CalculateMultiplierAsync` as a private async method that depends on `IPriceDataService` (DB queries). This must be split into:

- **Pure calculation**: given (currentPrice, high30Day, ma200Day, tiers, bearBoostFactor, maxCap) -> MultiplierResult
- **Data fetching**: stays in DcaExecutionService, calls PriceDataService, then passes values to calculator

```csharp
// Application/Services/MultiplierCalculator.cs
public static class MultiplierCalculator
{
    /// <summary>
    /// Pure function: computes multiplier from price data and configuration.
    /// No I/O, no DI, no async -- suitable for tight simulation loops.
    /// </summary>
    public static MultiplierResult Calculate(
        decimal currentPrice,
        decimal high30Day,
        decimal ma200Day,
        IReadOnlyList<MultiplierTier> tiers,
        decimal bearBoostFactor,
        decimal maxMultiplierCap)
    {
        // Drop percentage from 30-day high
        decimal dropPercent = high30Day > 0
            ? (high30Day - currentPrice) / high30Day * 100m
            : 0m;

        // Tier matching (descending, first match wins)
        decimal dipMultiplier = 1.0m;
        string tier = "None";
        var matchedTier = tiers
            .OrderByDescending(t => t.DropPercentage)
            .FirstOrDefault(t => dropPercent >= t.DropPercentage);

        if (matchedTier != null)
        {
            dipMultiplier = matchedTier.Multiplier;
            tier = $">= {matchedTier.DropPercentage}%";
        }

        // Bear market boost
        decimal bearMultiplier = (ma200Day > 0 && currentPrice < ma200Day)
            ? bearBoostFactor
            : 1.0m;

        // Stack multiplicatively with cap
        decimal total = Math.Min(dipMultiplier * bearMultiplier, maxMultiplierCap);

        return new MultiplierResult(total, dipMultiplier, bearMultiplier,
            tier, dropPercent, high30Day, ma200Day);
    }
}
```

**After extraction, `DcaExecutionService.CalculateMultiplierAsync` becomes a thin wrapper:**

```csharp
private async Task<MultiplierResult> CalculateMultiplierAsync(
    decimal currentPrice, DcaOptions options, CancellationToken ct)
{
    try
    {
        var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
        var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

        return MultiplierCalculator.Calculate(
            currentPrice, high30Day, ma200Day,
            options.MultiplierTiers, options.BearBoostFactor,
            options.MaxMultiplierCap);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Multiplier calculation failed, falling back to 1.0x");
        return MultiplierResult.Default;
    }
}
```

**Why static, not DI?** The backtesting simulation loop calls this millions of times during parameter sweeps. Zero allocation overhead, no service resolution, maximum throughput.

### 2. BacktestSimulator

The core simulation engine. Given a price series and DCA configuration, simulates daily purchases and produces metrics.

```csharp
// Application/Backtesting/BacktestSimulator.cs
public class BacktestSimulator
{
    /// <summary>
    /// Simulates a DCA strategy over historical price data.
    /// Pure computation -- no I/O during simulation.
    /// Loads price data once, then iterates in-memory.
    /// </summary>
    public BacktestResult Simulate(
        IReadOnlyList<DailyPrice> priceData,  // Pre-loaded, sorted by date
        BacktestParameters parameters)
    {
        // Pre-compute 200-day SMA and 30-day high for each day
        // using sliding window (O(n) -- no repeated DB queries)

        var purchases = new List<SimulatedPurchase>();
        decimal totalCostUsd = 0m;
        decimal totalBtc = 0m;
        decimal peakValue = 0m;
        decimal maxDrawdown = 0m;

        for (int i = parameters.WarmupDays; i < priceData.Count; i++)
        {
            var today = priceData[i];
            var high30Day = ComputeRollingHigh(priceData, i, parameters.HighLookbackDays);
            var ma200Day = ComputeRollingSma(priceData, i, parameters.MaPeriod);

            var multiplierResult = MultiplierCalculator.Calculate(
                today.Close, high30Day, ma200Day,
                parameters.Tiers, parameters.BearBoostFactor,
                parameters.MaxMultiplierCap);

            var amount = parameters.BaseDailyAmount * multiplierResult.TotalMultiplier;
            var btcBought = amount / today.Close;

            totalCostUsd += amount;
            totalBtc += btcBought;

            // Track portfolio value for drawdown calculation
            var portfolioValue = totalBtc * today.Close;
            peakValue = Math.Max(peakValue, portfolioValue);
            var drawdown = peakValue > 0 ? (peakValue - portfolioValue) / peakValue : 0;
            maxDrawdown = Math.Max(maxDrawdown, drawdown);

            purchases.Add(new SimulatedPurchase(
                today.Date, today.Close, amount, btcBought,
                multiplierResult.TotalMultiplier, multiplierResult.Tier));
        }

        // Compute fixed DCA baseline for comparison
        var fixedResult = SimulateFixedDca(priceData, parameters);

        return new BacktestResult
        {
            // Smart DCA metrics
            TotalInvested = totalCostUsd,
            TotalBtc = totalBtc,
            AverageCostBasis = totalCostUsd / totalBtc,
            FinalPortfolioValue = totalBtc * priceData[^1].Close,
            MaxDrawdownPercent = maxDrawdown * 100m,
            TotalPurchases = purchases.Count,

            // Fixed DCA comparison
            FixedDcaTotalBtc = fixedResult.TotalBtc,
            FixedDcaAverageCost = fixedResult.AverageCostBasis,
            BtcAdvantagePercent = (totalBtc - fixedResult.TotalBtc) / fixedResult.TotalBtc * 100m,
            CostBasisAdvantagePercent = (fixedResult.AverageCostBasis - (totalCostUsd / totalBtc))
                / fixedResult.AverageCostBasis * 100m,

            // Detailed purchase log (optional, can be trimmed for large sweeps)
            Purchases = purchases,

            // Metadata
            StartDate = priceData[parameters.WarmupDays].Date,
            EndDate = priceData[^1].Date,
            Parameters = parameters
        };
    }

    // Efficient O(1) sliding window computations
    private decimal ComputeRollingHigh(IReadOnlyList<DailyPrice> data, int index, int lookback) { ... }
    private decimal ComputeRollingSma(IReadOnlyList<DailyPrice> data, int index, int period) { ... }
}
```

**Key design choices:**
- **Price data loaded once** into memory before simulation starts (eliminates DB I/O during loop)
- **Sliding window** for 30-day high and 200-day SMA (not repeated DB queries like live code)
- **Fixed DCA baseline** always computed for comparison
- **No async** in the simulation loop -- pure CPU work
- **Returns structured result** with all metrics for JSON serialization

### 3. ParameterSweepService

Generates parameter combinations and runs simulations in parallel.

```csharp
// Application/Backtesting/ParameterSweepService.cs
public class ParameterSweepService
{
    private readonly BacktestSimulator _simulator;

    /// <summary>
    /// Runs backtests across all combinations of parameter ranges.
    /// Uses Parallel.ForEachAsync for CPU-bound parallelism.
    /// </summary>
    public async Task<SweepResult> RunSweepAsync(
        IReadOnlyList<DailyPrice> priceData,
        SweepRequest request,
        CancellationToken ct)
    {
        var combinations = GenerateCombinations(request);
        var results = new ConcurrentBag<BacktestResult>();

        await Parallel.ForEachAsync(
            combinations,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            },
            (parameters, token) =>
            {
                var result = _simulator.Simulate(priceData, parameters);
                results.Add(result);
                return ValueTask.CompletedTask;
            });

        // Rank results by BTC advantage over fixed DCA
        var ranked = results
            .OrderByDescending(r => r.BtcAdvantagePercent)
            .ToList();

        return new SweepResult
        {
            TotalCombinations = combinations.Count,
            TopResults = ranked.Take(request.TopN ?? 10).ToList(),
            WorstResults = ranked.TakeLast(3).Reverse().ToList(),
            BestParameters = ranked.First().Parameters,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private List<BacktestParameters> GenerateCombinations(SweepRequest request)
    {
        // Cartesian product of:
        // - Base daily amounts: [5, 10, 15, 20, ...]
        // - Tier configurations: [{5%->1.5x, 10%->2x, 20%->3x}, {3%->1.5x, 7%->2x, ...}, ...]
        // - Bear boost factors: [1.0, 1.25, 1.5, 2.0]
        // - Max caps: [3.0, 4.5, 6.0]
        // - High lookback days: [14, 30, 60]
        // - MA periods: [100, 200, 365]
    }
}
```

**Why Parallel.ForEachAsync:**
- Each simulation is independent (embarrassingly parallel)
- CPU-bound, not I/O-bound -- thread pool parallelism is ideal
- `ConcurrentBag<T>` for lock-free result collection
- Respects cancellation token for graceful abort
- Automatically throttles to CPU count (avoids oversubscription)

**Performance estimate:** A single 4-year backtest (1,460 daily prices) runs in microseconds. A sweep of 1,000 combinations completes in milliseconds on modern hardware. No need for background jobs or async polling.

### 4. CoinGecko Historical Data Client

```csharp
// Infrastructure/CoinGecko/CoinGeckoClient.cs
public class CoinGeckoClient(
    HttpClient http,
    IOptions<CoinGeckoOptions> options,
    ILogger<CoinGeckoClient> logger)
{
    /// <summary>
    /// Fetches BTC daily OHLC data from CoinGecko.
    /// Endpoint: /coins/{id}/ohlc/range (pro) or /coins/{id}/market_chart/range (free)
    /// </summary>
    public async Task<List<CoinGeckoDailyPrice>> GetHistoricalOhlcAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        // CoinGecko free tier: /coins/bitcoin/market_chart/range
        // Parameters: vs_currency=usd, from=<unix>, to=<unix>
        // Returns: prices[], market_caps[], total_volumes[]
        // Granularity: daily when range > 90 days

        // Rate limiting: free tier is 10-30 req/min, with 1 req sufficient here
        // For OHLC specifically: /coins/bitcoin/ohlc?days=max (limited to 180 days on free)
        // Best approach for free tier: use market_chart/range and derive OHLC from daily data points
    }
}
```

**CoinGecko API specifics (MEDIUM confidence, from training data):**
- Free tier provides `/coins/bitcoin/market_chart/range` with daily granularity for ranges > 90 days
- OHLC endpoint (`/coins/bitcoin/ohlc`) may be limited to 180 days on free tier
- For 4-year history, `market_chart/range` is the right endpoint (returns daily price points)
- Rate limit: approximately 10-30 requests/minute on free tier (ample for one-time ingestion)
- Response format: `{ "prices": [[timestamp_ms, price], ...] }` -- need to convert to DailyPrice format
- Alternative: Pro/Demo API key removes rate limits and provides OHLC endpoint with longer history

**Data ingestion strategy:** Fetch once, store in existing DailyPrice table, refresh incrementally.

### 5. CoinGecko Data Ingestion Service

```csharp
// Infrastructure/CoinGecko/CoinGeckoDataIngestionService.cs
public class CoinGeckoDataIngestionService(
    CoinGeckoClient coinGeckoClient,
    TradingBotDbContext dbContext,
    ILogger<CoinGeckoDataIngestionService> logger)
{
    /// <summary>
    /// Ingests historical BTC price data from CoinGecko into the DailyPrice table.
    /// Idempotent: skips dates that already exist.
    /// Called on-demand via API endpoint.
    /// </summary>
    public async Task<DataIngestionResult> IngestHistoricalDataAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        // 1. Check what dates already exist in DailyPrice
        // 2. Identify gaps
        // 3. Fetch missing ranges from CoinGecko (respecting rate limits)
        // 4. Upsert into DailyPrice table
        // 5. Return summary (dates ingested, gaps filled, total records)
    }
}
```

**Important:** This writes to the same `DailyPrice` table that the live bot uses. The data source column is not currently tracked. For backtesting, this is fine -- BTC price is BTC price regardless of source. The existing `Symbol = "BTC"` and `Date` composite key handles deduplication naturally.

### 6. API Endpoints

```csharp
// Registered in Program.cs
app.MapGroup("/api/backtest")
    .MapBacktestEndpoints();

// Application/Backtesting/BacktestEndpoints.cs
public static class BacktestEndpoints
{
    public static RouteGroupBuilder MapBacktestEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/backtest -- Run single backtest
        group.MapPost("/", async (
            BacktestRequest request,
            TradingBotDbContext db,
            CancellationToken ct) =>
        {
            var priceData = await db.DailyPrices
                .Where(p => p.Symbol == "BTC"
                    && p.Date >= request.StartDate
                    && p.Date <= request.EndDate)
                .OrderBy(p => p.Date)
                .ToListAsync(ct);

            var simulator = new BacktestSimulator();
            var result = simulator.Simulate(priceData, request.ToParameters());
            return Results.Ok(result);
        });

        // POST /api/backtest/sweep -- Run parameter sweep
        group.MapPost("/sweep", async (
            SweepRequest request,
            TradingBotDbContext db,
            CancellationToken ct) =>
        {
            var priceData = await db.DailyPrices
                .Where(p => p.Symbol == "BTC"
                    && p.Date >= request.StartDate
                    && p.Date <= request.EndDate)
                .OrderBy(p => p.Date)
                .ToListAsync(ct);

            var sweepService = new ParameterSweepService(new BacktestSimulator());
            var result = await sweepService.RunSweepAsync(priceData, request, ct);
            return Results.Ok(result);
        });

        // POST /api/backtest/data/ingest -- Trigger CoinGecko data ingestion
        group.MapPost("/data/ingest", async (
            DataIngestionRequest request,
            CoinGeckoDataIngestionService ingestion,
            CancellationToken ct) =>
        {
            var result = await ingestion.IngestHistoricalDataAsync(
                request.From, request.To, ct);
            return Results.Ok(result);
        });

        // GET /api/backtest/data/status -- Check available price data range
        group.MapGet("/data/status", async (
            TradingBotDbContext db,
            CancellationToken ct) =>
        {
            var earliest = await db.DailyPrices
                .Where(p => p.Symbol == "BTC")
                .MinAsync(p => (DateOnly?)p.Date, ct);
            var latest = await db.DailyPrices
                .Where(p => p.Symbol == "BTC")
                .MaxAsync(p => (DateOnly?)p.Date, ct);
            var count = await db.DailyPrices
                .Where(p => p.Symbol == "BTC")
                .CountAsync(ct);

            return Results.Ok(new { earliest, latest, totalDays = count });
        });

        return group;
    }
}
```

## Data Flow

### Single Backtest Flow

```
POST /api/backtest
{
  "startDate": "2022-01-01",
  "endDate": "2025-12-31",
  "baseDailyAmount": 10.0,
  "multiplierTiers": [
    { "dropPercentage": 5, "multiplier": 1.5 },
    { "dropPercentage": 10, "multiplier": 2.0 },
    { "dropPercentage": 20, "multiplier": 3.0 }
  ],
  "bearBoostFactor": 1.5,
  "maxMultiplierCap": 4.5,
  "highLookbackDays": 30,
  "maPeriod": 200
}
       |
       v
[1] Load DailyPrice from PostgreSQL (WHERE Symbol='BTC' AND Date BETWEEN ...)
    ~1,460 rows for 4 years, single query, ~5ms
       |
       v
[2] BacktestSimulator.Simulate(priceData, parameters)
    - Pre-compute sliding windows for 30-day high and 200-day SMA
    - Iterate each day: MultiplierCalculator.Calculate() -> simulated purchase
    - Track cumulative BTC, cost, drawdown
    - Also run fixed DCA baseline
    Pure CPU, ~1ms for 4 years
       |
       v
[3] Return BacktestResult as JSON
    - Smart DCA metrics (total BTC, avg cost, drawdown)
    - Fixed DCA comparison
    - Advantage percentages
    - Full purchase log (optional)

Total: ~10-50ms request-response
```

### Parameter Sweep Flow

```
POST /api/backtest/sweep
{
  "startDate": "2022-01-01",
  "endDate": "2025-12-31",
  "baseDailyAmounts": [5, 10, 20],
  "tierConfigurations": [
    { "name": "conservative", "tiers": [...] },
    { "name": "aggressive", "tiers": [...] }
  ],
  "bearBoostFactors": [1.0, 1.25, 1.5, 2.0],
  "maxMultiplierCaps": [3.0, 4.5, 6.0],
  "highLookbackDays": [14, 30, 60],
  "maPeriods": [100, 200],
  "topN": 10
}
       |
       v
[1] Load DailyPrice from PostgreSQL (same as single backtest)
    Loaded ONCE, shared across all simulations (read-only)
       |
       v
[2] ParameterSweepService.GenerateCombinations()
    3 * 2 * 4 * 3 * 3 * 2 = 432 combinations (example)
       |
       v
[3] Parallel.ForEachAsync over all combinations
    Each thread gets: (same priceData reference, unique parameters)
    -> BacktestSimulator.Simulate() per combination
    -> ConcurrentBag<BacktestResult>.Add()
    ~432 simulations, ~1ms each, 8 cores = ~60ms wall time
       |
       v
[4] Rank by BtcAdvantagePercent, return top N
    - Best parameters with metrics
    - Worst parameters for contrast
    - Summary statistics

Total: ~100-500ms for hundreds of combinations
```

### Data Ingestion Flow

```
POST /api/backtest/data/ingest
{
  "from": "2020-01-01",
  "to": "2025-12-31"
}
       |
       v
[1] Query DailyPrice for existing dates in range
       |
       v
[2] Identify gaps (dates without price data)
       |
       v
[3] CoinGeckoClient.GetHistoricalOhlcAsync(from, to)
    - Single HTTP request to CoinGecko API
    - /coins/bitcoin/market_chart/range?vs_currency=usd&from=...&to=...
    - Returns ~2,190 daily data points for 6 years
       |
       v
[4] Transform CoinGecko response to DailyPrice entities
    - Map [timestamp, price] to DailyPrice { Date, Close, Open, High, Low }
    - Note: market_chart endpoint returns price only; OHLC endpoint gives full candles
       |
       v
[5] Upsert into DailyPrice table (skip existing dates)
       |
       v
[6] Return { datesIngested, totalRecords, dateRange }
```

## Synchronous vs Asynchronous: Why Synchronous

**Decision: Synchronous request-response for both single backtests and parameter sweeps.**

**Rationale:**

| Factor | Analysis |
|--------|----------|
| Single backtest duration | ~5-50ms (DB query + simulation). No reason to go async. |
| Sweep duration (100 combos) | ~100-300ms. Well within HTTP request timeout. |
| Sweep duration (10,000 combos) | ~2-5 seconds. Acceptable for request-response. |
| Memory per simulation | ~200KB (1,460 DailyPrice records). Price data shared (one copy). |
| Complexity of async pattern | Requires: job queue, polling endpoint, job status table, cleanup. All unnecessary. |
| User experience | Immediate response is better than poll-and-wait for sub-second results. |

**When to reconsider:** If sweeps exceed 10,000 combinations (>30 seconds), consider chunked responses or background processing. This is unlikely given the parameter space (DCA parameters have a natural small range).

**Request timeout safety:** Set `[RequestTimeout(60_000)]` on sweep endpoint for safety. Include `CancellationToken` propagation so client can abort.

## Storage: Why Compute-on-the-fly

**Decision: Do not persist backtest results. Recompute each time.**

**Rationale:**

| Factor | Compute-on-fly | Persist |
|--------|---------------|---------|
| Speed | ~50ms per backtest | ~5ms read (but same DB query time for price data) |
| Storage cost | 0 | Hundreds of MB for sweep results |
| Staleness | Always fresh | Must invalidate when parameters change |
| Complexity | Simple | Need BacktestRun entity, migration, cleanup job |
| Use case | "Try different params, see results" | "Compare runs over time" |

The user's workflow is exploratory: try configurations, see metrics, adjust. They don't need to store every result. If they find a good configuration, they apply it to `DcaOptions` in appsettings.json.

**Exception:** The data ingestion status (date range available) is inherently persisted because it writes to DailyPrice table.

## Project Structure

```
TradingBot.ApiService/
  Application/
    Backtesting/                          # NEW - all backtesting logic
      BacktestSimulator.cs                # Core simulation engine
      ParameterSweepService.cs            # Parallel sweep orchestration
      BacktestEndpoints.cs                # Minimal API endpoint definitions
      Models/
        BacktestRequest.cs                # Input DTO for single backtest
        BacktestResult.cs                 # Output DTO with all metrics
        BacktestParameters.cs             # Internal parameter record
        SimulatedPurchase.cs              # Per-day simulation output
        SweepRequest.cs                   # Input DTO for parameter sweep
        SweepResult.cs                    # Output DTO for sweep results
        DataIngestionRequest.cs           # Input DTO for data import
        DataIngestionResult.cs            # Output DTO for import status
    Services/
      MultiplierCalculator.cs             # NEW - extracted from DcaExecutionService
      DcaExecutionService.cs              # MODIFIED - uses MultiplierCalculator
      PriceDataService.cs                 # UNCHANGED
      IPriceDataService.cs                # UNCHANGED
  Configuration/
    CoinGeckoOptions.cs                   # NEW - API key, base URL
    DcaOptions.cs                         # UNCHANGED
  Infrastructure/
    CoinGecko/                            # NEW - external data source
      CoinGeckoClient.cs                  # HTTP client for CoinGecko API
      CoinGeckoDataIngestionService.cs    # Import historical data to DailyPrice
      ServiceCollectionExtensions.cs      # DI registration
      Models/
        CoinGeckoModels.cs                # API response DTOs
  Models/
    DailyPrice.cs                         # UNCHANGED
    Purchase.cs                           # UNCHANGED
  Program.cs                              # MODIFIED - registers backtesting services + endpoints
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Making Simulation Async

**What:** Using `async Task` and `await` inside the simulation loop
**Why bad:** Simulation is pure CPU. Async adds state machine overhead per iteration. With 1,460 iterations * 1,000 sweep combos = 1.46M async state machines -- measurable overhead for zero benefit.
**Instead:** Keep `BacktestSimulator.Simulate()` as synchronous. Only the outer sweep uses `Parallel.ForEachAsync`.

### Anti-Pattern 2: Querying DB Per Simulated Day

**What:** Calling `dbContext.DailyPrices.Where(...)` inside the simulation loop for each day's 30-day high or 200-day SMA
**Why bad:** 1,460 DB round-trips per simulation. Turns microsecond operation into seconds.
**Instead:** Load all price data once, compute rolling windows in-memory with O(1) sliding window.

### Anti-Pattern 3: Creating New DcaOptions for Each Simulation

**What:** Using `IOptionsMonitor<DcaOptions>` or binding from configuration inside backtesting
**Why bad:** Backtesting needs to test MANY different configurations. The options system is designed for THE ONE active configuration.
**Instead:** Use a `BacktestParameters` record that maps to the same fields but is created programmatically, not from config.

### Anti-Pattern 4: Sharing Mutable State in Parallel Sweep

**What:** Using a shared `List<BacktestResult>` across parallel tasks
**Why bad:** Race condition, corrupted data.
**Instead:** Use `ConcurrentBag<T>` or collect per-partition and merge after.

### Anti-Pattern 5: Coupling Backtesting to Live DCA Execution Path

**What:** Calling `DcaExecutionService.ExecuteDailyPurchaseAsync` with mocked dependencies for backtesting
**Why bad:** That method has distributed locks, idempotency checks, Hyperliquid API calls, DB persistence, event publishing -- none of which make sense for simulation.
**Instead:** Extract the pure logic (MultiplierCalculator) and build a separate, lightweight simulation path.

### Anti-Pattern 6: Background Job for Parameter Sweeps

**What:** Implementing a BacktestBackgroundService that processes sweep requests from a queue
**Why bad:** Overengineered. A 1,000-combination sweep takes <1 second. Adding queue infrastructure, job status tracking, polling endpoints adds weeks of work for zero user benefit.
**Instead:** Synchronous endpoint with Parallel.ForEachAsync. If it ever gets slow, add it later.

## Data Model Details

### BacktestRequest (Input)

```csharp
public record BacktestRequest
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal BaseDailyAmount { get; init; } = 10.0m;
    public List<MultiplierTier> MultiplierTiers { get; init; } = [];
    public decimal BearBoostFactor { get; init; } = 1.5m;
    public decimal MaxMultiplierCap { get; init; } = 4.5m;
    public int HighLookbackDays { get; init; } = 30;
    public int MaPeriod { get; init; } = 200;
    public bool IncludePurchaseLog { get; init; } = true;
}
```

### BacktestResult (Output)

```csharp
public record BacktestResult
{
    // Strategy metrics
    public decimal TotalInvested { get; init; }
    public decimal TotalBtc { get; init; }
    public decimal AverageCostBasis { get; init; }
    public decimal FinalPortfolioValue { get; init; }
    public decimal ReturnPercent { get; init; }
    public decimal MaxDrawdownPercent { get; init; }
    public int TotalPurchases { get; init; }

    // Fixed DCA comparison
    public decimal FixedDcaTotalBtc { get; init; }
    public decimal FixedDcaAverageCost { get; init; }
    public decimal FixedDcaFinalValue { get; init; }

    // Advantage metrics (smart DCA over fixed DCA)
    public decimal BtcAdvantagePercent { get; init; }
    public decimal CostBasisAdvantagePercent { get; init; }

    // Time range
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    // Parameters used
    public BacktestParameters Parameters { get; init; }

    // Optional detailed log
    public List<SimulatedPurchase>? Purchases { get; init; }
}
```

### SweepRequest (Input)

```csharp
public record SweepRequest
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    // Parameter ranges to sweep
    public List<decimal> BaseDailyAmounts { get; init; } = [10.0m];
    public List<List<MultiplierTier>> TierConfigurations { get; init; } = [];
    public List<decimal> BearBoostFactors { get; init; } = [1.0m, 1.5m];
    public List<decimal> MaxMultiplierCaps { get; init; } = [4.5m];
    public List<int> HighLookbackDays { get; init; } = [30];
    public List<int> MaPeriods { get; init; } = [200];

    // Output control
    public int? TopN { get; init; } = 10;
}
```

## Suggested Build Order

Respects dependencies and enables incremental validation.

### Phase 1: MultiplierCalculator Extraction

**What:** Extract multiplier logic from DcaExecutionService into static MultiplierCalculator class.
**Modify:** `DcaExecutionService.cs` (thin wrapper calling static method)
**Create:** `Application/Services/MultiplierCalculator.cs`
**Test:** Unit tests for MultiplierCalculator with various price/tier scenarios
**Validates:** Core logic works identically to current inline implementation (regression test)
**Risk:** LOW -- mechanical refactoring, behavior-preserving

### Phase 2: Backtest Models and Simulator

**What:** Build the core simulation engine with request/response DTOs.
**Create:** All files in `Application/Backtesting/` and `Application/Backtesting/Models/`
**Depends on:** Phase 1 (MultiplierCalculator)
**Test:** Unit tests with hardcoded price data arrays
**Validates:** Simulation produces correct metrics, fixed DCA comparison is accurate

### Phase 3: CoinGecko Integration and Data Ingestion

**What:** Historical data source and import pipeline.
**Create:** All files in `Infrastructure/CoinGecko/`
**Create:** `Configuration/CoinGeckoOptions.cs`
**Depends on:** Existing DailyPrice entity and TradingBotDbContext
**Test:** Integration test fetching real CoinGecko data, verifying DailyPrice records
**Validates:** Can populate 4 years of BTC price history

### Phase 4: API Endpoints and Parameter Sweep

**What:** HTTP endpoints for triggering backtests, sweeps, and data ingestion.
**Create:** `Application/Backtesting/BacktestEndpoints.cs`
**Modify:** `Program.cs` (register services and map endpoints)
**Depends on:** Phases 1-3
**Test:** Integration tests via HTTP client, verify JSON response structure
**Validates:** Full end-to-end: ingest data -> run backtest -> get results via API

### Dependency Chain

```
Phase 1: MultiplierCalculator extraction
    |
    v
Phase 2: BacktestSimulator + models (uses MultiplierCalculator)
    |
    v
Phase 3: CoinGecko integration (populates DailyPrice data)
    |
    v
Phase 4: API endpoints + sweep (wires everything together)
```

Phases 2 and 3 are independent of each other and could be built in parallel. Phase 4 requires both.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| MultiplierCalculator extraction | HIGH | Mechanical refactoring of existing code |
| BacktestSimulator design | HIGH | Standard simulation pattern, pure computation |
| Parallel sweep approach | HIGH | Well-understood .NET pattern (Parallel.ForEachAsync) |
| Synchronous over async | HIGH | Performance math is clear: sub-second for realistic sweeps |
| CoinGecko API specifics | MEDIUM | Based on training data; verify free tier endpoint availability and rate limits before implementation |
| CoinGecko OHLC vs market_chart | MEDIUM | Free tier may not provide full OHLC; market_chart/range provides prices but may need OHLC derivation |
| No-persist decision | HIGH | Correct for exploratory use case; easy to add persistence later if needed |

## Open Questions

1. **CoinGecko free vs demo tier:** Does the free tier provide daily OHLC (open/high/low/close) or only closing prices? The `/coins/bitcoin/ohlc` endpoint may require a paid plan for ranges > 180 days. Verify during Phase 3 implementation.

2. **Sliding window efficiency:** For the 30-day rolling maximum, a naive approach is O(30) per day. A monotonic deque gives O(1) amortized. For 1,460 data points this is negligible, but worth noting for correctness.

3. **Price data source consistency:** CoinGecko BTC prices may differ slightly from Hyperliquid spot prices (different exchanges, different timestamps). This means backtest results are an approximation, not exact. Document this limitation in API response metadata.

4. **Warm-up period handling:** The first 200 days of price data are needed to compute 200-day SMA. The simulation should skip these days (or use available data with a warning). The `WarmupDays` parameter in `BacktestParameters` handles this, but the API should validate that the date range is long enough.

---

*Architecture research: 2026-02-12*
*Focus: Backtesting engine integration with existing BTC Smart DCA Bot*

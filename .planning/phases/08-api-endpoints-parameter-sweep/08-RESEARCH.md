# Phase 8: API Endpoints & Parameter Sweep - Research

**Researched:** 2026-02-13
**Domain:** ASP.NET Core Minimal APIs, Parameter Optimization, Parallel Backtesting
**Confidence:** HIGH

## Summary

Phase 8 exposes the existing BacktestSimulator (Phase 6) and historical price data (Phase 7) through RESTful endpoints. The primary technical challenges are: (1) generating and executing parameter combinations efficiently, (2) ranking and filtering large result sets, (3) implementing walk-forward validation for overfitting detection, and (4) designing APIs that balance completeness with performance.

.NET 10's Minimal API framework has reached production maturity with built-in validation, OpenAPI integration, and excellent async patterns. The codebase already demonstrates strong endpoint design patterns in `DataEndpoints.cs` using route groups, structured responses, and async/await. Parameter sweep execution can leverage Task.WhenAll for parallel backtest execution, with Channel<T> for job queuing if needed. Walk-forward validation requires splitting time series data into train/test periods and comparing performance metrics—a straightforward statistical pattern without special library dependencies.

**Primary recommendation:** Use Minimal APIs with built-in validation, generate parameter combinations using LINQ cartesian product, execute backtests in parallel with Task.WhenAll (with configurable concurrency limit), and implement walk-forward validation as a pure function that compares train/test metric ratios.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Sweep parameter design:**
- Parameter ranges specified as **explicit lists** (e.g., `baseAmount: [10, 25, 50]`) — no min/max/step
- **All configurable DCA parameters** are sweepable: base amount, tier thresholds, tier multipliers, bear boost, max cap, MA window
- Ship with **built-in presets only** ("conservative", "full") — no user-defined custom presets
- Combinations are generated as cartesian product of all provided parameter lists

**Result ranking & output:**
- Default optimization target: **efficiency ratio** (extra BTC gained per extra dollar spent)
- User can choose ranking metric via `rankBy` field — options: efficiency, costBasis, extraBtc, returnPct
- Sweep response: **summary metrics for all combinations**, full purchase logs for **top 5** results only
- All results include smart DCA vs fixed DCA comparison metrics

**Walk-forward validation:**
- **Optional** — user opts in with a `validate: true` flag (off by default)
- Train/test split uses a **fixed ratio** (e.g., 70/30)
- Flags parameter sets that degrade out-of-sample

**Single backtest request:**
- Default date range: **last 2 years** of available data (if user doesn't specify start/end)
- Strategy config defaults to **current production DcaOptions** — user overrides specific fields
- **Purchase log always included** in response (full day-by-day detail)
- **Fixed DCA comparison always included** (smart DCA vs fixed DCA side-by-side)

### Claude's Discretion

- Safety cap for maximum parameter combinations (reasonable default with option to override)
- Walk-forward degradation heuristic (how to define and measure performance drop between train/test)
- How overfitting warnings are surfaced in sweep results (per-result field vs separate section)
- Train/test ratio default value
- Exact preset parameter values for "conservative" and "full"

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core Minimal APIs | .NET 10 | HTTP endpoint definition | Built-in validation, OpenAPI, route groups—production-ready for 2026 |
| System.Text.Json | .NET 10 | JSON serialization | Built-in, UTF-8 optimized, source generation support for performance |
| LINQ | .NET 10 | Cartesian product generation | Native C# for multi-list combinations via SelectMany |
| Task.WhenAll | .NET 10 | Parallel backtest execution | Standard async pattern for concurrent I/O operations |
| Entity Framework Core | .NET 10 | Data access | Already used in codebase for DailyPrices and historical data |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Threading.Channels | .NET 10 | Async producer-consumer queue | If sweep jobs need queuing/throttling beyond simple Task.WhenAll |
| FluentValidation | 11.x | Complex validation | If built-in validation insufficient for nested parameter lists |
| IAsyncEnumerable | .NET 10 | Streaming responses | If returning large sweep results progressively |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Minimal APIs | MVC Controllers | Controllers add ceremony without benefit for this API-focused service |
| Task.WhenAll | Parallel.ForEachAsync | Parallel.ForEachAsync good for CPU-bound work, but backtesting is I/O-light computation |
| LINQ cartesian product | MoreLINQ library | MoreLINQ adds dependency for functionality easily implemented with SelectMany |
| Built-in validation | FluentValidation | FluentValidation overkill unless complex cross-property validation needed |

**Installation:**

No additional packages required—all core functionality uses .NET 10 standard library.

Optional (if needed):
```bash
# Only if complex validation required
dotnet add package FluentValidation.AspNetCore --version 11.x
```

---

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── Endpoints/
│   ├── BacktestEndpoints.cs       # Single backtest endpoint
│   ├── SweepEndpoints.cs          # Parameter sweep endpoints
│   └── DataEndpoints.cs           # Existing data pipeline endpoints
├── Application/Services/Backtest/
│   ├── BacktestSimulator.cs       # Existing (Phase 6)
│   ├── BacktestConfig.cs          # Existing request DTO
│   ├── BacktestResult.cs          # Existing response DTO
│   ├── ParameterSweepService.cs   # NEW: Generates combinations, executes sweep
│   ├── WalkForwardValidator.cs    # NEW: Implements train/test validation
│   └── Presets/
│       └── SweepPresets.cs        # NEW: Conservative/Full preset definitions
├── Models/
│   ├── SweepRequest.cs            # NEW: Parameter ranges and options
│   ├── SweepResult.cs             # NEW: Ranked results with metrics
│   └── WalkForwardResult.cs       # NEW: Train/test comparison
```

### Pattern 1: Minimal API with Route Groups

**What:** Group related endpoints under a common prefix using MapGroup

**When to use:** All new API endpoints (following DataEndpoints.cs pattern)

**Example:**
```csharp
// Source: TradingBot.ApiService/Endpoints/DataEndpoints.cs (existing pattern)
public static class BacktestEndpoints
{
    public static WebApplication MapBacktestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backtest");
        group.MapPost("/run", RunBacktestAsync);
        group.MapPost("/sweep", RunSweepAsync);
        return app;
    }

    private static async Task<IResult> RunBacktestAsync(
        [FromBody] BacktestRequest request,
        TradingBotDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Validation happens automatically with .NET 10 built-in validation
        // Return TypedResults for OpenAPI schema generation
        return TypedResults.Ok(result);
    }
}
```

**Source:** [ASP.NET Core Minimal APIs - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)

### Pattern 2: Built-in Validation with DataAnnotations

**What:** .NET 10's native Minimal API validation with automatic 400 Bad Request

**When to use:** All request DTOs (no manual validation code needed)

**Example:**
```csharp
// Source: Multiple sources on .NET 10 validation
public record BacktestRequest
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    [Required]
    [Range(1, 1000)]
    public decimal? BaseDailyAmount { get; init; }

    [Range(1, 365)]
    public int? HighLookbackDays { get; init; }

    // Other fields default to production DcaOptions if null
}

public record SweepRequest
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    [Required]
    [MinLength(1)]
    public List<decimal> BaseAmounts { get; init; } = [];

    [Range(1, 10000)]
    public int? MaxCombinations { get; init; } = 1000; // Safety cap

    public bool Validate { get; init; } = false; // Walk-forward

    public string RankBy { get; init; } = "efficiency"; // efficiency|costBasis|extraBtc|returnPct
}

// In Program.cs startup:
builder.Services.AddValidation(); // Enables built-in validation
```

**Sources:**
- [Minimal API Validation in .NET 10 - Medium](https://medium.com/@adrianbailador/minimal-api-validation-in-net-10-8997a48b8a66)
- [ASP.NET 10: Validating incoming models in Minimal APIs](https://timdeschryver.dev/blog/aspnet-10-validating-incoming-models-in-minimal-apis)

### Pattern 3: Cartesian Product with LINQ

**What:** Generate all parameter combinations using nested SelectMany

**When to use:** Parameter sweep combination generation

**Example:**
```csharp
// Source: Adapted from Eric Lippert's blog
public static class ParameterCombinations
{
    public static IEnumerable<BacktestConfig> GenerateCartesian(SweepRequest request)
    {
        // Start with base amounts
        var combinations = request.BaseAmounts.Select(baseAmount =>
            new { baseAmount });

        // Add lookback days if provided
        if (request.HighLookbackDays?.Any() == true)
        {
            combinations = combinations.SelectMany(c =>
                request.HighLookbackDays.Select(lookback =>
                    new { c.baseAmount, lookback }));
        }

        // Add bear boost if provided
        if (request.BearBoosts?.Any() == true)
        {
            combinations = combinations.SelectMany(c =>
                request.BearBoosts.Select(boost =>
                    new { c.baseAmount, c.lookback, boost }));
        }

        // Continue for all parameters...

        // Project to BacktestConfig
        return combinations.Select(c => new BacktestConfig(
            BaseDailyAmount: c.baseAmount,
            HighLookbackDays: c.lookback ?? defaults.HighLookbackDays,
            // ... map all fields
        ));
    }
}
```

**Source:** [Computing a Cartesian product with LINQ - Eric Lippert](https://ericlippert.com/2010/06/28/computing-a-cartesian-product-with-linq/)

### Pattern 4: Parallel Execution with Task.WhenAll

**What:** Execute independent backtests concurrently with controlled parallelism

**When to use:** Running parameter sweep backtests

**Example:**
```csharp
// Source: Multiple ASP.NET Core async best practices
public class ParameterSweepService
{
    private readonly TradingBotDbContext _db;
    private readonly ILogger<ParameterSweepService> _logger;
    private const int MaxParallelism = 8; // Configurable

    public async Task<SweepResult> ExecuteSweepAsync(
        SweepRequest request,
        IReadOnlyList<DailyPriceData> priceData,
        CancellationToken ct)
    {
        var configs = GenerateCombinations(request);

        // Apply safety cap
        var safeConfigs = configs.Take(request.MaxCombinations ?? 1000).ToList();

        _logger.LogInformation(
            "Executing sweep with {Count} parameter combinations",
            safeConfigs.Count);

        // Partition for controlled parallelism
        var results = new List<SweepEntry>();
        var batches = safeConfigs.Chunk(MaxParallelism);

        foreach (var batch in batches)
        {
            var tasks = batch.Select(config => Task.Run(() =>
                BacktestSimulator.Run(config, priceData), ct));

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults.Select((result, i) =>
                new SweepEntry(batch[i], result)));

            ct.ThrowIfCancellationRequested();
        }

        // Rank and filter
        var ranked = RankResults(results, request.RankBy);

        return new SweepResult(
            TotalCombinations: results.Count,
            Results: ranked.Take(100).ToList(), // Top 100 summaries
            TopResults: ranked.Take(5).ToList() // Top 5 with full logs
        );
    }
}
```

**Sources:**
- [Task.WhenAll vs Parallel.ForEach - 2026](https://copyprogramming.com/howto/parallel-foreach-vs-task-run-and-task-whenall)
- [Efficiently Managing Multiple Tasks with Task.WhenAll - Medium](https://medium.com/c-sharp-programming/efficiently-managing-multiple-tasks-with-task-whenall-6480b75d73e8)

### Pattern 5: Walk-Forward Validation

**What:** Split time series into train/test periods, compare out-of-sample performance

**When to use:** When user sets `validate: true` in sweep request

**Example:**
```csharp
public class WalkForwardValidator
{
    public WalkForwardResult Validate(
        BacktestConfig config,
        IReadOnlyList<DailyPriceData> fullData,
        decimal trainRatio = 0.7m)
    {
        // Split data
        int trainSize = (int)(fullData.Count * trainRatio);
        var trainData = fullData.Take(trainSize).ToList();
        var testData = fullData.Skip(trainSize).ToList();

        // Run on both periods
        var trainResult = BacktestSimulator.Run(config, trainData);
        var testResult = BacktestSimulator.Run(config, testData);

        // Calculate degradation
        var returnDelta = testResult.SmartDca.ReturnPercent -
                          trainResult.SmartDca.ReturnPercent;
        var efficiencyDelta = testResult.Comparison.EfficiencyRatio -
                              trainResult.Comparison.EfficiencyRatio;

        // Flag if significant degradation (>20% drop)
        bool isOverfit = returnDelta < -20m || efficiencyDelta < -0.2m;

        return new WalkForwardResult(
            TrainMetrics: trainResult.SmartDca,
            TestMetrics: testResult.SmartDca,
            ReturnDegradation: returnDelta,
            EfficiencyDegradation: efficiencyDelta,
            OverfitWarning: isOverfit
        );
    }
}
```

**Sources:**
- [Understanding Walk Forward Validation - Medium](https://medium.com/@ahmedfahad04/understanding-walk-forward-validation-in-time-series-analysis-a-practical-guide-ea3814015abf)
- [Walk forward optimization - Wikipedia](https://en.wikipedia.org/wiki/Walk_forward_optimization)

### Anti-Patterns to Avoid

- **Buffering large results:** Don't load all purchase logs into memory. Use projections for summary-only results and include full logs for top N only.
- **Sequential execution:** Don't run backtests one-by-one. Use Task.WhenAll for parallelism (respecting safety limits).
- **String-based ranking:** Don't use string comparison for `rankBy` field. Use enum or pattern matching for type safety.
- **Ignoring cancellation:** Always pass CancellationToken through async chains to allow request cancellation.
- **Manual validation:** Don't write validation code. Use .NET 10 built-in validation with DataAnnotations.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Request validation | Manual validation logic | .NET 10 built-in validation | Automatic 400 responses with ProblemDetails, DataAnnotations, zero boilerplate |
| Cartesian product | Nested loops | LINQ SelectMany | Functional, composable, avoids index bugs |
| Parallel execution | Manual threading | Task.WhenAll + batching | Handles exceptions, cancellation, and async correctly |
| JSON serialization | Custom serializers | System.Text.Json | UTF-8 optimized, source generation support, OpenAPI integration |
| Walk-forward logic | Complex ML libraries | Pure functions (split, run, compare) | No dependencies, transparent, easy to test |

**Key insight:** Modern .NET 10 provides all the primitives needed for parameter sweep and validation. Resist the urge to add libraries for problems the framework already solves elegantly.

---

## Common Pitfalls

### Pitfall 1: Combinatorial Explosion

**What goes wrong:** User specifies 10 values for 5 parameters = 100,000 combinations = hours of runtime

**Why it happens:** Cartesian product grows exponentially (n1 × n2 × n3 × ...)

**How to avoid:**
- Enforce safety cap (default 1000 combinations, user can override up to 10,000)
- Calculate combination count before execution: `combinations = param1.Count × param2.Count × ...`
- Return 400 Bad Request if exceeds cap: "Exceeds max combinations (10,000). Reduce parameter ranges."
- Log warning if > 100 combinations to set expectations

**Warning signs:** Request takes >30 seconds to respond, memory usage spikes, logs show thousands of iterations

### Pitfall 2: Full Logs for All Results

**What goes wrong:** Sweep with 1000 combinations × 730 days × 200 bytes per log entry = 146 MB JSON response

**Why it happens:** Including full PurchaseLog for all combinations without filtering

**How to avoid:**
- Summary metrics only for all results (TotalInvested, TotalBtc, ReturnPercent, etc.)
- Full purchase logs only for top 5 results (as specified in locked decisions)
- Use projections in LINQ to avoid materializing unnecessary data:
  ```csharp
  var summaries = results.Select(r => new SweepSummary
  {
      Config = r.Config,
      Metrics = r.Result.SmartDca,
      Comparison = r.Result.Comparison
      // No PurchaseLog here
  });
  ```

**Warning signs:** Response times >10 seconds, large JSON payloads, client timeouts

### Pitfall 3: Ignoring Data Availability

**What goes wrong:** User requests backtest for 2020-2024 but only 2022-2024 data exists

**Why it happens:** Not validating date range against available DailyPrices data

**How to avoid:**
- Query min/max dates from DailyPrices table before running backtest
- Return 400 Bad Request if requested range exceeds available data
- Default to "last 2 years of available data" if no date range specified:
  ```csharp
  var maxDate = await db.DailyPrices.MaxAsync(p => p.Date, ct);
  var defaultStart = maxDate.AddYears(-2);
  var startDate = request.StartDate ?? defaultStart;
  ```

**Warning signs:** Empty price data array passed to BacktestSimulator, ArgumentException from simulator

### Pitfall 4: Walk-Forward Data Leakage

**What goes wrong:** Training on full dataset then "testing" on subset (contaminated test set)

**Why it happens:** Not strictly splitting data before running backtests

**How to avoid:**
- Split price data FIRST: `trainData = fullData.Take(trainSize)`, `testData = fullData.Skip(trainSize)`
- NEVER use test data in train period (no looking ahead)
- Document split methodology clearly in response:
  ```csharp
  TrainPeriod: "2022-01-01 to 2023-06-30 (70%)",
  TestPeriod: "2023-07-01 to 2024-12-31 (30%)"
  ```

**Warning signs:** Test performance always equals train performance, unrealistic out-of-sample results

### Pitfall 5: Task.WhenAll Exception Handling

**What goes wrong:** One backtest throws exception, entire sweep fails silently

**Why it happens:** Task.WhenAll creates AggregateException but await only throws first exception

**How to avoid:**
- Wrap individual backtests in try-catch if partial results acceptable:
  ```csharp
  var tasks = configs.Select(async config =>
  {
      try { return BacktestSimulator.Run(config, priceData); }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Backtest failed for config: {Config}", config);
          return null; // or sentinel value
      }
  });
  var results = (await Task.WhenAll(tasks)).Where(r => r != null);
  ```
- OR let exception bubble if any failure should fail entire sweep
- Always check Task.Exception property for full exception details if needed

**Warning signs:** Sweep returns incomplete results without error, logs show exceptions but response is 200 OK

---

## Code Examples

Verified patterns from official sources:

### Default Date Range from Available Data

```csharp
// Pattern: Query available data and default to last 2 years
public static async Task<DateRange> GetDefaultDateRangeAsync(
    TradingBotDbContext db,
    DateOnly? requestStart,
    DateOnly? requestEnd,
    CancellationToken ct)
{
    // Get available data range
    var stats = await db.DailyPrices
        .Where(p => p.Symbol == "BTC")
        .GroupBy(p => p.Symbol)
        .Select(g => new { Min = g.Min(p => p.Date), Max = g.Max(p => p.Date) })
        .FirstOrDefaultAsync(ct);

    if (stats == null)
    {
        throw new InvalidOperationException(
            "No historical data available. Run POST /api/backtest/data/ingest first.");
    }

    // Default: last 2 years of available data
    var defaultStart = stats.Max.AddYears(-2);
    if (defaultStart < stats.Min) defaultStart = stats.Min;

    var start = requestStart ?? defaultStart;
    var end = requestEnd ?? stats.Max;

    // Validate range
    if (start < stats.Min || end > stats.Max)
    {
        throw new ArgumentException(
            $"Date range [{start}, {end}] exceeds available data [{stats.Min}, {stats.Max}]");
    }

    return new DateRange(start, end);
}
```

### Preset Definitions

```csharp
// Pattern: Static preset configurations
public static class SweepPresets
{
    public static SweepRequest Conservative => new()
    {
        BaseAmounts = [10m, 15m, 20m],
        HighLookbackDays = [21, 30],
        BearMarketMaPeriods = [200],
        BearBoosts = [1.0m, 1.5m],
        MaxMultiplierCaps = [3.0m, 4.0m],
        TierSets = [
            // Single tier set: standard 3-tier structure
            new TierSet([
                new(10m, 1.5m),
                new(20m, 2.0m),
                new(30m, 2.5m)
            ])
        ]
    };

    public static SweepRequest Full => new()
    {
        BaseAmounts = [10m, 15m, 20m, 25m, 30m],
        HighLookbackDays = [14, 21, 30, 60],
        BearMarketMaPeriods = [100, 150, 200],
        BearBoosts = [1.0m, 1.25m, 1.5m, 2.0m],
        MaxMultiplierCaps = [3.0m, 4.0m, 5.0m],
        TierSets = [
            new([new(10m, 1.5m), new(20m, 2.0m), new(30m, 2.5m)]),
            new([new(15m, 2.0m), new(25m, 3.0m)]),
            new([new(20m, 2.5m), new(40m, 4.0m)])
        ]
    };

    // Estimated combinations:
    // Conservative: 3 × 2 × 1 × 2 × 2 × 1 = 24 combinations
    // Full: 5 × 4 × 3 × 4 × 3 × 3 = 2,160 combinations
}
```

### Ranking Results

```csharp
public static class ResultRanker
{
    public static List<SweepEntry> Rank(
        IEnumerable<SweepEntry> results,
        string rankBy)
    {
        return rankBy.ToLowerInvariant() switch
        {
            "efficiency" => results.OrderByDescending(r =>
                r.Result.Comparison.EfficiencyRatio).ToList(),

            "costbasis" => results.OrderBy(r =>
                r.Result.SmartDca.AvgCostBasis).ToList(),

            "extrabtc" => results.OrderByDescending(r =>
                r.Result.Comparison.ExtraBtcPercentMatchTotal).ToList(),

            "returnpct" => results.OrderByDescending(r =>
                r.Result.SmartDca.ReturnPercent).ToList(),

            _ => throw new ArgumentException($"Invalid rankBy value: {rankBy}")
        };
    }
}
```

### OpenAPI Response Metadata

```csharp
// Pattern: Use Produces<T> for OpenAPI schema generation
public static class BacktestEndpoints
{
    public static RouteGroupBuilder MapBacktestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backtest")
            .WithTags("Backtest")
            .WithOpenApi();

        group.MapPost("/run", RunBacktestAsync)
            .Produces<BacktestResponse>(200)
            .Produces<ProblemDetails>(400)
            .WithName("RunBacktest")
            .WithSummary("Run single backtest with strategy config");

        group.MapPost("/sweep", RunSweepAsync)
            .Produces<SweepResponse>(200)
            .Produces<ProblemDetails>(400)
            .WithName("RunParameterSweep")
            .WithSummary("Run parameter sweep across multiple configurations");

        return group;
    }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual validation in endpoints | Built-in DataAnnotations validation | .NET 10 (2025) | Zero boilerplate, automatic 400 responses |
| MVC Controllers for APIs | Minimal APIs | .NET 6+ (mature in .NET 10) | Less ceremony, better performance, cleaner code |
| Newtonsoft.Json | System.Text.Json | .NET 5+ (default in .NET 10) | UTF-8 optimization, source generation, built-in async |
| BlockingCollection for queues | Channel<T> | .NET Core 3.0+ | Async-native, ValueTask efficiency, backpressure support |
| Reflection-based serialization | Source generation (JsonSerializerContext) | .NET 6+ | 40% startup improvement, reduced allocations |

**Deprecated/outdated:**
- `AddControllers()` + `[ApiController]` attribute: Use Minimal APIs for new API-focused services
- `Newtonsoft.Json`: System.Text.Json is now standard, mature, and faster
- `IActionResult`: Use `IResult` (Minimal APIs) or `TypedResults` for OpenAPI schema

---

## Open Questions

1. **Maximum concurrency for parallel backtests**
   - What we know: Task.WhenAll supports unlimited tasks, but CPU/memory bound
   - What's unclear: Optimal batch size for this workload (CPU-light math, no I/O during simulation)
   - Recommendation: Start with MaxParallelism=8, make configurable, monitor memory usage

2. **Walk-forward degradation threshold**
   - What we know: Need to flag parameter sets that degrade out-of-sample
   - What's unclear: What % drop constitutes "overfitting" (20%? 30%? metric-dependent?)
   - Recommendation: Start with 20% drop in return or efficiency, surface as warning (not error), document methodology

3. **Streaming vs batch responses**
   - What we know: IAsyncEnumerable can stream large result sets
   - What's unclear: Whether sweep results justify streaming (100-1000 results is manageable in single response)
   - Recommendation: Start with single JSON response (top 100 summaries), add streaming later if needed

4. **Safety cap override strategy**
   - What we know: Default cap of 1000 combinations prevents accidents
   - What's unclear: Should high caps require explicit confirmation or just log warnings?
   - Recommendation: Allow override up to 10,000 via `maxCombinations` field, return warning header for >1000

---

## Sources

### Primary (HIGH confidence)

**ASP.NET Core Minimal APIs:**
- [Minimal APIs quick reference - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [APIs overview - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0)
- [Minimal API Endpoints Complete Guide - codewithmukesh](https://codewithmukesh.com/blog/minimal-apis-aspnet-core/)

**Validation:**
- [Minimal API Validation in .NET 10 - Adrian Bailador](https://medium.com/@adrianbailador/minimal-api-validation-in-net-10-8997a48b8a66)
- [ASP.NET 10: Validating incoming models - Tim Deschryver](https://timdeschryver.dev/blog/aspnet-10-validating-incoming-models-in-minimal-apis)

**Parallel Execution:**
- [Task-based asynchronous programming - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-based-asynchronous-programming)
- [Efficiently Managing Multiple Tasks with Task.WhenAll - Medium](https://medium.com/c-sharp-programming/efficiently-managing-multiple-tasks-with-task-whenall-6480b75d73e8)

**Channels:**
- [Channels - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Channels in C# - Adrian Bailador](https://medium.com/@adrianbailador/channels-in-c-80853bc53130)

**Serialization:**
- [Source-generation modes - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes)
- [Tuning JSON Serialization in .NET - Medium](https://medium.com/turbo-net/tuning-your-serialization-in-net-with-json-attributes-faster-leaner-and-cleaner-22e603e9fbec)

### Secondary (MEDIUM confidence)

**Walk-Forward Validation:**
- [Understanding Walk Forward Validation - Medium](https://medium.com/@ahmedfahad04/understanding-walk-forward-validation-in-time-series-analysis-a-practical-guide-ea3814015abf)
- [Walk forward optimization - Wikipedia](https://en.wikipedia.org/wiki/Walk_forward_optimization)

**RESTful API Design:**
- [RESTful API Design Best Practices 2026](https://dasroot.net/posts/2026/01/restful-api-design-best-practices-2026/)
- [API Design Best Practices - Hakia](https://hakia.com/engineering/api-design-best-practices/)

**Error Handling:**
- [Functional Error Handling with Result Pattern - Milan Jovanovic](https://www.milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern)
- [Modern C# Error Handling Patterns 2026 - Medium](https://medium.com/@tejaswini.nareshit/modern-c-error-handling-patterns-you-should-be-using-in-2026-57eacd495123)

### Tertiary (LOW confidence)

**Cartesian Product:**
- [Computing a Cartesian Product with LINQ - Eric Lippert](https://ericlippert.com/2010/06/28/computing-a-cartesian-product-with-linq/) — Blog post from 2010, but LINQ fundamentals unchanged

---

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** - All frameworks and patterns are mature .NET 10 features with official documentation
- Architecture: **HIGH** - Patterns demonstrated in existing codebase (DataEndpoints.cs) and official guidance
- Pitfalls: **MEDIUM** - Based on web search and general async/API experience, not specific to this exact use case
- Walk-forward: **MEDIUM** - Conceptual understanding solid, but implementation details subject to testing

**Research date:** 2026-02-13
**Valid until:** 2026-03-15 (30 days - .NET ecosystem is stable, frameworks mature)

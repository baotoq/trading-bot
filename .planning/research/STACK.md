# Technology Stack: Backtesting Engine Additions

**Project:** BTC Smart DCA Bot - v1.1 Backtesting Engine
**Researched:** 2026-02-12
**Scope:** New dependencies and patterns needed for backtesting. Does NOT re-research existing stack.

## Existing Stack (DO NOT CHANGE)

For reference, these are already in place and validated:

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET 10.0 | 10.0.100 | Runtime |
| ASP.NET Core Minimal APIs | 10.0 | HTTP endpoints |
| EF Core + PostgreSQL | 10.0.0 | Persistence (DailyPrice, Purchase entities) |
| Redis | via Aspire 13.0.2 | Distributed caching |
| MediatR | 13.1.0 | In-process domain events |
| Serilog | 10.0.0 | Structured logging |
| Microsoft.Extensions.Http.Resilience | 10.3.0 | HTTP resilience (Polly-based) |
| HyperliquidClient (custom) | N/A | Exchange API with EIP-712 signing |
| xUnit + FluentAssertions + NSubstitute | Various | Testing |

## Recommended Stack Additions

### 1. Historical Price Data: Direct HTTP to CoinGecko API (NO new package)

**Recommendation:** Use direct `HttpClient` calls to CoinGecko's free API. Do NOT add `CoinGecko.Net` or `CoinGeckoAsyncApi`.

**Why direct HTTP instead of CoinGecko.Net (v5.5.0, Jkorf):**

| Factor | CoinGecko.Net | Direct HttpClient |
|--------|---------------|-------------------|
| Dependency weight | Pulls in `CryptoExchange.Net` 10.5.4 (conflicts with existing 9.13.0) | Zero new dependencies |
| API surface needed | We need exactly ONE endpoint: `/coins/{id}/market_chart/range` | Wrapping one GET request in a typed client is trivial |
| Maintenance burden | Another Jkorf package to keep in sync | Our own code, our own pace |
| Version conflict risk | CoinGecko.Net 5.x requires CryptoExchange.Net >= 10.x, project has 9.13.0 | None |
| Auth complexity | CoinGecko free tier = no auth, just query params | Same |

**CoinGecko API endpoint we need:**

```
GET https://api.coingecko.com/api/v3/coins/bitcoin/market_chart/range
  ?vs_currency=usd
  &from={unix_timestamp}
  &to={unix_timestamp}
```

Returns daily prices (auto-granularity: >90 days returns daily data). Response format:
```json
{
  "prices": [[timestamp_ms, price], ...],
  "market_caps": [[timestamp_ms, cap], ...],
  "total_volumes": [[timestamp_ms, vol], ...]
}
```

**CoinGecko free tier constraints (MEDIUM confidence -- based on training data, verify at implementation):**
- Rate limit: ~10-30 requests/minute (varies, not guaranteed)
- No API key required for free tier
- Historical data available back to 2013 for BTC
- Daily granularity auto-selected for ranges > 90 days
- No OHLC in this endpoint (only close prices) -- acceptable for backtesting since DCA uses daily close

**Why CoinGecko and not alternatives:**

| Source | Pros | Cons | Verdict |
|--------|------|------|---------|
| CoinGecko free API | 4+ years BTC history, no auth, reliable | Rate limited, close-only (not OHLC) | **Use this** |
| Hyperliquid candleSnapshot | Already integrated, has OHLC | Spot market too new for 2-4 year history | Supplement for recent data |
| CoinMarketCap API | Good data quality | Requires API key even for free tier, more complex | Unnecessary |
| Binance API | Deep history, OHLC | Would need Binance.Net (already unused dep) or custom client | Overkill |
| Yahoo Finance | Free, long history | Unreliable API, rate limits, no official .NET support | Fragile |

**Implementation approach:** Create `CoinGeckoClient` following existing `HyperliquidClient` pattern -- inject via `IHttpClientFactory`, use `Microsoft.Extensions.Http.Resilience` for retry/backoff.

### 2. Financial Metrics Calculation: No external library

**Recommendation:** Implement metrics directly in C#. Do NOT add `MathNet.Numerics`.

**Rationale:** The backtesting engine needs these specific metrics:

| Metric | Formula | Complexity | Library needed? |
|--------|---------|------------|-----------------|
| Total cost basis | `sum(purchase.Cost)` | Trivial | No |
| Total BTC accumulated | `sum(purchase.Quantity)` | Trivial | No |
| Average cost per BTC | `totalCost / totalBtc` | Trivial | No |
| Current portfolio value | `totalBtc * currentPrice` | Trivial | No |
| ROI % | `(value - cost) / cost * 100` | Trivial | No |
| Max drawdown | `max peak-to-trough decline in portfolio value` | Simple loop | No |
| Sharpe-like ratio | `mean(dailyReturns) / stddev(dailyReturns) * sqrt(365)` | Simple stats | No |
| Smart vs Fixed DCA comparison | Run both strategies, compare metrics | Architecture | No |
| Cost basis over time | `runningCost / runningBtc at each purchase point` | Running total | No |

**Why NOT MathNet.Numerics (v5.0.0, 57M downloads):**
- MathNet.Numerics is a comprehensive numerical library (linear algebra, interpolation, distributions)
- We need: mean, standard deviation, min/max tracking -- all 5-10 line implementations
- Adding 5MB+ of numerical computing for `list.Average()` and `Math.Sqrt(variance)` is wasteful
- .NET's built-in `System.Linq` provides `Average()`, `Sum()`, `Min()`, `Max()`
- Standard deviation is ~5 lines of C# code

**Reference implementation for the most complex metric (Sharpe-like ratio):**

```csharp
// This is all we need -- no library required
public static decimal CalculateSharpeRatio(IReadOnlyList<decimal> dailyReturns)
{
    if (dailyReturns.Count < 2) return 0;

    var mean = dailyReturns.Average();
    var variance = dailyReturns.Average(r => (r - mean) * (r - mean));
    var stdDev = (decimal)Math.Sqrt((double)variance);

    if (stdDev == 0) return 0;
    return mean / stdDev * (decimal)Math.Sqrt(365);
}
```

### 3. Parameter Sweep / Optimization: No external framework

**Recommendation:** Build a simple nested-loop parameter sweep using `Parallel.ForEachAsync` (built into .NET 6+). Do NOT add an optimization framework.

**Why no optimization framework:**

The parameter space for DCA backtesting is small and discrete:

| Parameter | Typical range | Steps | Values |
|-----------|---------------|-------|--------|
| BaseDailyAmount | $5 - $100 | 5-10 | 5, 10, 20, 50, 100 |
| Tier1 drop % | 3% - 10% | 4 | 3, 5, 7, 10 |
| Tier1 multiplier | 1.2x - 2.0x | 4 | 1.2, 1.5, 1.7, 2.0 |
| Tier2 drop % | 8% - 20% | 4 | 8, 10, 15, 20 |
| Tier2 multiplier | 1.5x - 3.0x | 4 | 1.5, 2.0, 2.5, 3.0 |
| Tier3 drop % | 15% - 30% | 4 | 15, 20, 25, 30 |
| Tier3 multiplier | 2.0x - 5.0x | 4 | 2.0, 3.0, 4.0, 5.0 |
| BearBoostFactor | 1.0x - 2.0x | 4 | 1.0, 1.25, 1.5, 2.0 |
| MaxMultiplierCap | 3.0x - 6.0x | 4 | 3.0, 4.0, 5.0, 6.0 |

Total combinations for a focused sweep: ~5 * 4 * 4 * 4 * 4 = 1,280 to ~20,000 depending on which axes you sweep.

Each simulation is CPU-bound, stateless, and takes <1ms (iterate ~1,460 daily entries for 4 years, calculate multiplier, accumulate BTC). This means:

- **1,000 simulations** in ~1 second (single-threaded)
- **20,000 simulations** in ~5 seconds with `Parallel.ForEachAsync`
- No need for gradient descent, evolutionary algorithms, or Bayesian optimization

**Built-in .NET parallelism is sufficient:**

```csharp
// .NET 6+ built-in, no package needed
await Parallel.ForEachAsync(
    parameterCombinations,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    async (params, ct) =>
    {
        var result = SimulateStrategy(priceData, params);
        results.Enqueue(result); // ConcurrentQueue<T>
    });
```

**Why not BenchmarkDotNet, Optuna.NET, or similar:**
- BenchmarkDotNet (v0.15.8) is for micro-benchmarking code performance, not strategy optimization
- No mature .NET equivalent to Python's Optuna exists
- The problem is embarrassingly parallel brute-force, not optimization search

### 4. Caching Historical Data: PostgreSQL (already have it) + IMemoryCache

**Recommendation:** Store fetched CoinGecko data in the existing `DailyPrice` PostgreSQL table. Use `IMemoryCache` (built into ASP.NET Core) for in-process caching during simulation runs. Do NOT add a new caching layer.

**Data flow:**

```
CoinGecko API --> CoinGeckoClient --> PostgreSQL DailyPrice table (persistent)
                                            |
                                            v
                              EF Core query --> IMemoryCache (in-process, per simulation batch)
                                                    |
                                                    v
                                        BacktestEngine reads from memory
```

**Why this approach:**

| Concern | Solution | Rationale |
|---------|----------|-----------|
| Avoid repeated API calls | Store in DailyPrice table | Already exists, already has the right schema (OHLCV) |
| Fast simulation access | Load once into memory, cache for batch | 4 years of daily data = ~1,460 rows = ~50KB in memory |
| Cache invalidation | Absolute expiry at UTC midnight | Same pattern as existing PriceDataService |
| Redis for caching? | No -- overkill for single-process backtesting | Data is tiny, IMemoryCache avoids serialization overhead |

**IMemoryCache is already available** -- ASP.NET Core registers it by default. No package to add.

**DailyPrice table reuse:** The existing `DailyPrice` entity has all fields needed (Date, Symbol, Open, High, Low, Close, Volume). CoinGecko data will be stored with a `Source` discriminator or simply merged:

- For dates where Hyperliquid data exists: keep Hyperliquid data (it's the actual trading venue)
- For dates before Hyperliquid spot launch: use CoinGecko data
- The backtesting engine reads from DailyPrice regardless of source

### 5. Performance Considerations

**No new packages needed.** The built-in .NET 10.0 capabilities are sufficient.

| Concern | Approach | Built-in tool |
|---------|----------|---------------|
| Parallel simulation | `Parallel.ForEachAsync` | `System.Threading.Tasks` |
| Thread-safe result collection | `ConcurrentQueue<T>` or `Channel<T>` | `System.Collections.Concurrent` / `System.Threading.Channels` |
| Memory-efficient price array | Load once, share read-only across threads | `ReadOnlyMemory<decimal>` or `ImmutableArray<T>` |
| Avoiding GC pressure | Use value types (struct) for simulation state | C# structs |
| Cancellation | `CancellationToken` propagation | `System.Threading` |

**Key design principle:** The simulation engine should be a pure function:
```
SimulationResult Simulate(ReadOnlySpan<DailyPrice> prices, DcaOptions options)
```

No database access, no HTTP calls, no DI -- just math on arrays. This makes it trivially parallelizable and testable.

## What NOT to Add (and Why)

| Package | Why Considered | Why Rejected |
|---------|---------------|--------------|
| **CoinGecko.Net 5.5.0** | CoinGecko .NET client by Jkorf | Version conflict with CryptoExchange.Net 9.13.0 (needs >= 10.x). We need 1 endpoint. Direct HTTP is simpler. |
| **CoinGeckoAsyncApi 1.8.0** | Most downloaded CoinGecko package (217K) | Higher download count but older, less maintained. Still overkill for 1 endpoint. |
| **MathNet.Numerics 5.0.0** | Numerical computing library | We need mean, stddev, min/max -- all trivial in C#. 5MB dependency for 10 lines of math. |
| **BenchmarkDotNet 0.15.8** | Performance benchmarking | Wrong tool -- we need strategy backtesting, not code benchmarking. |
| **System.Threading.Tasks.Dataflow 10.0.3** | TPL Dataflow for pipeline processing | Over-engineered for our use case. `Parallel.ForEachAsync` is simpler for batch parallel processing. |
| **System.Linq.Async 7.0.0** | Async LINQ operators | Not needed -- we load price data into memory once, then process synchronously. |
| **Hangfire/Quartz** | Job scheduling for sweep runs | Already have BackgroundService pattern. Sweeps are triggered via API endpoint, not scheduled. |

## Packages to Consider Removing

| Package | Current Version | Status | Recommendation |
|---------|----------------|--------|----------------|
| **Binance.Net** | 11.11.0 | Unused (no `using` statements anywhere in codebase) | **Remove now** -- reduces build time and avoids version conflict surface |
| **CryptoExchange.Net** | 9.13.0 | Transitive dependency of Binance.Net, also unused directly | **Remove now** -- pulled in by Binance.Net |

Removing these eliminates the CryptoExchange.Net version conflict that would arise if we ever wanted CoinGecko.Net.

## Recommended Stack Summary

### New Code to Write (zero new NuGet packages)

| Component | What | Integration Point |
|-----------|------|-------------------|
| `CoinGeckoClient` | Typed HTTP client for historical BTC prices | `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` (existing) |
| `BacktestEngine` | Pure simulation engine, stateless | Reads `DailyPrice[]`, applies `DcaOptions`, returns `BacktestResult` |
| `ParameterSweepService` | Generates parameter combinations, runs parallel simulations | `Parallel.ForEachAsync` (built-in) |
| `MetricsCalculator` | Static methods for financial metrics | Pure C# math, no dependencies |
| `BacktestEndpoints` | Minimal API endpoints for triggering and retrieving results | ASP.NET Core minimal APIs (existing) |
| `HistoricalDataService` | Orchestrates CoinGecko fetch + DailyPrice storage | `CoinGeckoClient` + `TradingBotDbContext` (existing) |

### Existing Capabilities Reused (already installed)

| Capability | Existing package/feature | How backtesting uses it |
|------------|--------------------------|------------------------|
| HTTP client with resilience | `Microsoft.Extensions.Http.Resilience` 10.3.0 | CoinGecko API calls with retry/backoff |
| Price data storage | EF Core + PostgreSQL via `DailyPrice` entity | Store/retrieve historical prices |
| In-memory caching | `IMemoryCache` (ASP.NET Core built-in) | Cache price arrays during simulation batches |
| Parallel execution | `Parallel.ForEachAsync` (.NET 6+ built-in) | Parameter sweep parallelism |
| JSON serialization | `System.Text.Json` (.NET built-in) | CoinGecko response parsing |
| API endpoints | ASP.NET Core minimal APIs | Backtest trigger and results endpoints |
| Options pattern | `IOptionsMonitor<DcaOptions>` | Default parameters for backtest, reuse existing config |
| Structured logging | Serilog 10.0.0 | Log simulation progress, results |
| Unit testing | xUnit + FluentAssertions + NSubstitute | Test simulation engine, metrics calculator |

## Installation

```bash
# No new packages to install!
# The backtesting engine is built entirely on existing dependencies.

# Optional cleanup: remove unused Binance packages
cd /Users/baotoq/Work/trading-bot/TradingBot.ApiService
dotnet remove package Binance.Net
dotnet remove package CryptoExchange.Net
```

## Configuration Additions

Add to `appsettings.json`:

```json
{
  "CoinGecko": {
    "BaseUrl": "https://api.coingecko.com/api/v3",
    "RequestDelayMs": 2500,
    "MaxRetries": 3
  },
  "Backtest": {
    "DefaultStartDate": "2022-01-01",
    "DefaultEndDate": "2026-01-01",
    "MaxParallelSimulations": 0,
    "CacheMinutes": 60
  }
}
```

- `RequestDelayMs`: Self-imposed delay between CoinGecko requests to stay under rate limit (2.5s = ~24 req/min, well under limit)
- `MaxParallelSimulations`: 0 = use `Environment.ProcessorCount` (auto-detect)
- `CacheMinutes`: How long to keep price data in IMemoryCache for repeated simulations

## Confidence Assessment

| Decision | Confidence | Rationale |
|----------|------------|-----------|
| Direct HTTP for CoinGecko | HIGH | Proven pattern in this codebase (HyperliquidClient). One endpoint is not worth a package. |
| No MathNet.Numerics | HIGH | Metrics are basic arithmetic. Verified the formulas are trivial. |
| No optimization framework | HIGH | Parameter space is small, brute-force is fast, .NET parallelism is built-in. |
| PostgreSQL for price storage | HIGH | DailyPrice entity already exists with correct schema. |
| IMemoryCache for simulation | HIGH | ~50KB of data, single-process, no serialization needed. |
| Remove Binance.Net | HIGH | Confirmed zero usages via grep. |
| CoinGecko rate limits | MEDIUM | Training data says 10-30 req/min free tier, but this should be verified at implementation time. The 2.5s self-imposed delay provides safety margin. |
| CoinGecko data availability | MEDIUM | Training data says BTC history available back to 2013, needs verification. |

## Sources

- **NuGet package search** (verified 2026-02-12): CoinGecko.Net 5.5.0 (Jkorf, 45K downloads), CoinGeckoAsyncApi 1.8.0 (217K downloads), MathNet.Numerics 5.0.0 (57M downloads)
- **Codebase analysis** (verified 2026-02-12): Binance.Net and CryptoExchange.Net have zero `using` statements in application code
- **Existing architecture**: HyperliquidClient pattern for typed HTTP clients, DailyPrice entity for price storage, Microsoft.Extensions.Http.Resilience for HTTP resilience
- **CoinGecko API docs** (MEDIUM confidence, based on training data): `/coins/{id}/market_chart/range` endpoint for historical prices, free tier rate limits

---
*Stack research for v1.1 Backtesting Engine: 2026-02-12*
*Conclusion: Zero new NuGet packages needed. Build on existing infrastructure.*

# Project Research Summary

**Project:** BTC Smart DCA Bot - v1.1 Backtesting Engine
**Domain:** DCA strategy backtesting and parameter optimization
**Researched:** 2026-02-12
**Confidence:** HIGH

## Executive Summary

The v1.1 backtesting engine adds historical strategy simulation and parameter optimization to the existing BTC Smart DCA bot. The core approach is to **extract the multiplier calculation logic from `DcaExecutionService` into a pure static `MultiplierCalculator` class**, then build a lightweight simulation layer that replays historical prices through it. This is a well-understood pattern in quantitative finance: decouple the strategy logic from execution infrastructure, feed it historical data, and measure outcomes. The critical architectural insight is that the existing multiplier logic is already stateless in nature -- it just needs to be untangled from its async data-fetching wrapper.

The recommended stack approach is remarkable in its simplicity: **zero new NuGet packages**. CoinGecko historical price data is fetched via direct `HttpClient` (one API endpoint, no wrapper library needed). Financial metrics are basic arithmetic (mean, stddev, running totals). Parameter sweeps use built-in `Parallel.ForEachAsync`. Everything builds on the existing .NET 10.0, EF Core, PostgreSQL, and ASP.NET Core minimal API infrastructure. The unused `Binance.Net` and `CryptoExchange.Net` packages should be removed to reduce dependency surface.

The primary risks are **look-ahead bias** (simulation accidentally using future prices for 30-day high or 200-day SMA calculations) and **overfitting** (parameter sweep finding historically optimal but forward-fragile configurations). Look-ahead bias is prevented architecturally by loading price data into memory and using strict date-bounded sliding windows -- never querying the database during simulation. Overfitting is mitigated by walk-forward validation (train on 70%, validate on 30%) and constraining the sweep space to avoid combinatorial explosion. Both mitigations must be built in from day one, not retrofitted.

## Key Findings

### Recommended Stack

No new NuGet packages are needed. The backtesting engine is built entirely on existing dependencies. This is the strongest possible position -- zero dependency risk, zero version conflicts, zero new attack surface.

**Core approach (zero new packages):**
- **CoinGecko API via `HttpClient`**: Historical BTC prices -- direct HTTP to one endpoint, following existing `HyperliquidClient` pattern with `IHttpClientFactory` and resilience policies
- **`Parallel.ForEachAsync`**: Parameter sweep parallelism -- built into .NET 6+, embarrassingly parallel workload of ~1,000-20,000 independent simulations
- **`IMemoryCache`**: In-process price data caching during simulation batches -- 4 years of daily data is ~50KB, no need for Redis
- **PostgreSQL `DailyPrice` table**: Persistent storage for CoinGecko historical data -- entity already exists with correct OHLCV schema

**Cleanup recommended:** Remove unused `Binance.Net` (11.11.0) and `CryptoExchange.Net` (9.13.0) -- zero usages found in codebase.

**Configuration additions:** `CoinGeckoOptions` (base URL, request delay of 2.5s for rate limiting) and `BacktestOptions` (default date range, max parallel simulations, cache TTL).

See [STACK.md](STACK.md) for detailed rationale on rejected packages (CoinGecko.Net, MathNet.Numerics, BenchmarkDotNet, TPL Dataflow).

### Expected Features

**Must have (9 table stakes):**
- Historical price data ingestion from CoinGecko (2-4 years BTC daily OHLCV)
- Single-run backtest execution (simulate strategy over date range)
- Strategy configuration as input (maps to existing `DcaOptions` shape)
- Fixed DCA baseline comparison (the core question: does smart DCA beat fixed?)
- Core performance metrics (total invested, total BTC, avg cost basis, ROI, comparison deltas)
- Date range selection
- Day-by-day simulation loop with deterministic replay
- API endpoints returning JSON (`POST /api/backtest`, `POST /api/backtest/sweep`)
- Parameter sweep / grid search (find optimal multiplier tiers and thresholds)

**Should have (12 differentiators for v1.1):**
- Multiplier breakdown per run (how often each tier fired)
- Efficiency ratio (BTC per dollar, smart vs fixed)
- Simulated purchase log (full day-by-day detail)
- Sweep result ranking by chosen optimization target
- Historical data caching in DB and staleness detection
- Data completeness validation
- Multiple strategy comparison (run N configs side-by-side)

**Defer (v2+):**
- Period-specific analysis (bull/bear regime breakdown)
- Drawdown analysis, XIRR/time-weighted returns
- Web UI / charts / visualization (project is API-first)
- Monte Carlo simulation, genetic algorithm/ML optimization
- Multi-asset backtesting, PDF/CSV export
- Backtest result persistence in DB

**Estimated effort:** 8-12 days total.

See [FEATURES.md](FEATURES.md) for complete feature landscape, input/output schemas, and dependency graph.

### Architecture Approach

The architecture follows a **strict separation principle**: backtesting lives in its own `/Application/Backtesting/` namespace and never modifies production code paths. The only production change is extracting `MultiplierCalculator` as a pure static class. The simulation engine is a synchronous, CPU-bound computation that loads price data once into memory, iterates with O(1) sliding windows for 30-day high and 200-day SMA, and returns structured results. No async in the simulation loop. No database access during simulation. No DI in the hot path.

**Major components (6 new, 1 refactored):**
1. **MultiplierCalculator** (extracted) -- Pure static class: `(currentPrice, high30Day, ma200Day, tiers, bearBoost, maxCap) -> MultiplierResult`. Called by both live `DcaExecutionService` and `BacktestSimulator`.
2. **BacktestSimulator** -- Core simulation engine. Takes pre-loaded `DailyPrice[]` and `BacktestParameters`, iterates day-by-day, always includes fixed DCA baseline. Returns `BacktestResult` with all metrics.
3. **ParameterSweepService** -- Generates parameter combinations (cartesian product with ordering constraints), runs `BacktestSimulator` in parallel via `Parallel.ForEachAsync`, ranks results.
4. **CoinGeckoClient** -- Typed HTTP client for `/coins/bitcoin/market_chart/range`. Single-purpose, following `HyperliquidClient` pattern.
5. **CoinGeckoDataIngestionService** -- Fetches historical data, validates completeness, upserts into `DailyPrice` table. Idempotent (skips existing dates).
6. **BacktestEndpoints** -- Minimal API route group: `POST /api/backtest`, `POST /api/backtest/sweep`, `POST /api/backtest/data/ingest`, `GET /api/backtest/data/status`.

**Key architectural decisions:**
- Synchronous request-response (single backtest ~10-50ms, 1,000-combo sweep ~100-500ms)
- No persistence of backtest results (compute-on-the-fly, cheap to recompute)
- Price data loaded once and shared read-only across parallel sweep threads

See [ARCHITECTURE.md](ARCHITECTURE.md) for component design with code examples, data flow diagrams, and anti-patterns to avoid.

### Critical Pitfalls

Research identified 14 pitfalls (5 critical, 6 moderate, 3 minor). The top 5 "must not skip" items:

1. **Look-ahead bias (Critical)** -- Simulation must not see future prices when computing 30-day high or 200-day SMA. Prevent by using in-memory sliding windows with strict date bounds. If `PriceDataService.Get30DayHighAsync()` is reused directly, it queries from `DateTime.UtcNow`, which is wrong for backtesting. The pure `MultiplierCalculator` + pre-loaded price arrays solve this architecturally.

2. **Overfitting via exhaustive sweep (Critical)** -- 5+ parameter dimensions with 2-4 years of daily data means the optimizer has more degrees of freedom than the data supports. Prevent with walk-forward validation (train on 70%, validate on 30%), constrained sweep space (hierarchical sweeps of ~100-225 combos instead of millions), and reporting parameter sensitivity alongside "best" results.

3. **Reusing production services directly (Critical)** -- `DcaExecutionService.CalculateMultiplierAsync` queries the DB, reads from `IOptionsMonitor`, and catches errors silently. In backtesting, this causes 2,920 DB queries per simulation, uses the wrong config for sweep parameters, and hides data errors. Prevent by extracting the pure calculation and using in-memory price providers.

4. **Mixing backtest and production code (Critical)** -- Adding `if (isBacktest)` branches to `DcaExecutionService` or `PriceDataService` risks regressions in live trading. Prevent with strict namespace separation: only `MultiplierCalculator` extraction touches production code. Everything else is new code.

5. **Metrics calculation errors (Moderate but impactful)** -- Cost basis must be volume-weighted (`totalCost / totalBtc`), not `average(prices)`. Drawdown for DCA is "unrealized loss vs total invested", not traditional peak-to-trough. Smart vs fixed comparison must normalize by total invested (different strategies spend different amounts). Prevent with unit tests against hand-calculated examples.

See [PITFALLS.md](PITFALLS.md) for all 14 pitfalls with prevention code examples and phase-specific warnings.

## Implications for Roadmap

Based on combined research, the backtesting engine should be built in 4 phases following a strict dependency chain. Phases 2 and 3 are independent of each other and could be parallelized.

### Phase 1: MultiplierCalculator Extraction

**Rationale:** Every subsequent phase depends on a pure, testable multiplier calculation. This is the architectural foundation. It is also the only phase that modifies production code, so it should be isolated and validated first.
**Delivers:** `MultiplierCalculator` static class, refactored `DcaExecutionService` (thin wrapper), regression tests proving identical behavior to current implementation.
**Features addressed:** Deterministic replay, strategy configuration reuse.
**Pitfalls avoided:** Pitfall 5 (reusing production services), Pitfall 10 (mixing backtest/production code), Pitfall 13 (Close vs High price confusion).
**Risk:** LOW -- mechanical refactoring, behavior-preserving.
**Effort estimate:** 0.5 days.

### Phase 2: Backtest Simulation Engine

**Rationale:** The core value delivery. With `MultiplierCalculator` extracted, the simulation engine can be built and fully tested using hardcoded price arrays -- no external data source needed yet.
**Delivers:** `BacktestSimulator` with day-by-day simulation loop, sliding window indicators, fixed DCA baseline comparison, all core metrics, `BacktestResult` DTOs. Full unit test suite with hand-calculated expected values.
**Features addressed:** Single-run backtest, fixed DCA baseline, core metrics, multiplier breakdown, efficiency ratio, simulated purchase log.
**Pitfalls avoided:** Pitfall 1 (look-ahead bias -- in-memory sliding windows), Pitfall 6 (warm-up period handling), Pitfall 7 (metrics calculation), Pitfall 9 (BTC rounding).
**Risk:** MEDIUM -- edge cases around warm-up periods and metric calculations need careful testing.
**Effort estimate:** 3-4 days.

### Phase 3: CoinGecko Historical Data Pipeline

**Rationale:** Independent of Phase 2. Provides the real data the simulator needs, but the simulator can be tested without it. Building the data pipeline separately allows focused validation of CoinGecko API behavior, rate limiting, and data quality.
**Delivers:** `CoinGeckoClient`, `CoinGeckoDataIngestionService`, `CoinGeckoOptions`, data completeness validation, gap detection, idempotent ingestion into `DailyPrice` table.
**Features addressed:** Historical price data ingestion, data caching in DB, staleness detection.
**Pitfalls avoided:** Pitfall 4 (data quality issues), Pitfall 3 (CoinGecko rate limits).
**Uses:** `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` (existing).
**Risk:** MEDIUM -- CoinGecko free tier rate limits and data format need runtime verification.
**Effort estimate:** 1.5-2 days.

### Phase 4: API Endpoints and Parameter Sweep

**Rationale:** Wires everything together. Requires Phases 1-3 complete. The parameter sweep is the highest-complexity feature but also the highest-value differentiator.
**Delivers:** `POST /api/backtest`, `POST /api/backtest/sweep`, `POST /api/backtest/data/ingest`, `GET /api/backtest/data/status`. `ParameterSweepService` with parallel execution, combination generation with ordering constraints, result ranking. Integration tests for full end-to-end flow.
**Features addressed:** API endpoints, parameter sweep, sweep result ranking, date range selection, multiple strategy comparison.
**Pitfalls avoided:** Pitfall 2 (overfitting -- walk-forward validation), Pitfall 8 (combinatorial explosion -- hierarchical sweeps, safety caps).
**Risk:** MEDIUM-HIGH -- sweep design requires careful constraint management to avoid explosion.
**Effort estimate:** 3-4 days.

### Phase Ordering Rationale

- **Phase 1 first** because it is the only production code change and every other phase depends on it. Isolating it allows focused regression testing.
- **Phases 2 and 3 are independent** because the simulator works with any `DailyPrice[]` (can be hardcoded in tests) and the data pipeline writes to a table the simulator reads from. Building them in parallel is efficient.
- **Phase 4 last** because it integrates all components. The parameter sweep specifically requires both a working simulator (Phase 2) and real historical data (Phase 3) to deliver meaningful results.
- **This ordering avoids the "big bang integration" anti-pattern** -- each phase produces independently testable, shippable code.

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 3 (CoinGecko Data Pipeline):** CoinGecko free tier rate limits, endpoint availability, and response format are based on training data (MEDIUM confidence). Verify against current API docs before implementation. Key questions: Does `/coins/bitcoin/market_chart/range` still return daily granularity for >90 day ranges? What are current free tier rate limits? Is OHLC data available or only close prices?
- **Phase 4 (Parameter Sweep):** Walk-forward validation implementation details and optimal sweep constraint design would benefit from a focused research spike. How should the train/test split work for DCA backtesting specifically? What sensitivity analysis approaches are most informative?

**Phases with standard patterns (skip research):**
- **Phase 1 (MultiplierCalculator Extraction):** Pure mechanical refactoring of existing code. The extraction pattern is visible in the current `DcaExecutionService.cs` source.
- **Phase 2 (Backtest Simulation Engine):** Well-documented DCA simulation pattern. The simulation loop, sliding window computations, and metrics are standard quantitative finance.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Zero new packages. All capabilities exist in current stack. Verified via codebase analysis and NuGet search. |
| Features | MEDIUM-HIGH | Table stakes derived from established DCA backtesting tools (dcabtc.com, portfoliovisualizer). Feature schemas designed against actual `DcaOptions` model. Web search was unavailable for latest competitive analysis. |
| Architecture | HIGH | Based on thorough analysis of existing codebase. Component boundaries, data flow, and code paths are well-defined. The pure-function extraction pattern is proven. |
| Pitfalls | HIGH | 5 critical pitfalls identified from established quantitative finance principles and verified against specific codebase patterns (e.g., `PriceDataService` using `DateTime.UtcNow`). |

**Overall confidence:** HIGH

### Gaps to Address

- **CoinGecko API verification:** Free tier rate limits, endpoint availability, and response format are MEDIUM confidence. Must verify against live API during Phase 3 implementation. The 2.5s self-imposed request delay provides safety margin but may need adjustment.
- **CoinGecko OHLC vs close-only data:** The free tier `market_chart/range` endpoint may return only close prices, not full OHLC candles. For backtesting DCA (which uses daily close for purchases), this is acceptable. But the 30-day high calculation needs close prices specifically -- verify the existing `DailyPrice.Close` field maps correctly from CoinGecko data.
- **Walk-forward validation specifics:** The overfitting prevention strategy (train 70% / test 30%) is directionally correct but the implementation details (how to split, what metrics to compare, how to present results) need refinement during Phase 4 planning.
- **Balance simulation scope:** Pitfall 11 identifies that unlimited-balance backtesting is unrealistic. The current feature list defers this. Consider adding as optional parameter in Phase 2 if effort is low, otherwise defer to post-v1.1.
- **Slippage model:** Pitfall 3 recommends a configurable slippage model. For BTC spot DCA amounts ($10-45/day), the impact is minimal (<0.1%). Recommend deferring to post-v1.1 unless implementation is trivial.

## Sources

### Primary (HIGH confidence)
- **Codebase analysis (2026-02-12):** `DcaExecutionService.cs` multiplier logic, `PriceDataService.cs` indicator calculations, `DailyPrice` entity schema, `DcaOptions` configuration model, `HyperliquidClient` HTTP client pattern
- **NuGet package search (2026-02-12):** CoinGecko.Net 5.5.0, CoinGeckoAsyncApi 1.8.0, MathNet.Numerics 5.0.0, Binance.Net 11.11.0 (zero usages confirmed)

### Secondary (MEDIUM confidence)
- **CoinGecko API patterns:** `/coins/{id}/market_chart/range` endpoint for historical prices, free tier rate limits (~10-30 req/min), daily granularity for >90 day ranges. Based on training data -- verify before implementation.
- **DCA backtesting domain knowledge:** Standard metrics from dcabtc.com, portfoliovisualizer.com. Walk-forward validation and parameter sensitivity analysis from quantitative finance literature.

### Tertiary (LOW confidence)
- **CoinGecko OHLC endpoint:** `/coins/{id}/ohlc/range` may be limited to 180 days on free tier. Needs runtime verification. Fallback is `market_chart/range` with close-only prices.

---
*Research completed: 2026-02-12*
*Ready for roadmap: yes*

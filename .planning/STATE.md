# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v1.1 Backtesting Engine
**Updated:** 2026-02-13T09:16:04Z

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-12)

**Core value:** Validate that smart DCA beats fixed DCA and find optimal multiplier parameters through historical backtesting
**Current focus:** Phase 8 - API Endpoints & Parameter Sweep

## Current Position

Phase: 8 of 8 (API Endpoints & Parameter Sweep) -- COMPLETE
Plan: 2 of 2 complete
Status: Phase 8 complete - all backtest API endpoints implemented
Last activity: 2026-02-13 -- Plan 08-02 complete, Phase 8 complete

Progress: ████████████████████████████████ 100.0% (8/8 phases complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 7 (v1.1)
- Average duration: 3.3 minutes
- Total execution time: 25 minutes

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 05-multiplier-calculator-extraction | 1 | 8m | 8m |
| 06-backtest-simulation-engine | 2 | 6m | 3m |
| 07-historical-data-pipeline | 2 | 6m | 3m |
| 08-api-endpoints-parameter-sweep | 2 | 5m | 2.5m |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [08-02]: Sweep presets: conservative (~24 combos) and full (~2160 combos) for quick vs comprehensive exploration
- [08-02]: Default maxCombinations safety cap: 1000 (user can override up to 10000)
- [08-02]: Parallel execution with batching: 4-16 cores based on Environment.ProcessorCount
- [08-02]: Walk-forward validation optional (validate flag), uses 70/30 train/test split
- [08-02]: Overfitting threshold: -20 percentage points in return OR -0.3 in efficiency ratio
- [08-02]: Top 5 results include full purchase logs and tier breakdown; all others summary only
- [08-01]: All backtest request fields nullable - default to production DcaOptions when null
- [08-01]: Default date range: last 2 years clamped to available data
- [08-01]: Return 400 Bad Request for invalid date ranges or missing data
- [07-01]: Use direct HttpClient instead of CoinGecko.Net library for API calls (better control, avoids version conflicts)
- [07-01]: Use bounded Channel<T> with capacity=1 and DropWrite mode for job queue (single-job enforcement)
- [07-01]: Auto-fill gaps after bulk insert by fetching individual missing dates (maximize completeness without failing job)
- [07-01]: Set Open=High=Low=Close to match free tier limitation (CoinGecko doesn't provide true OHLC on free tier)
- [06-02]: Max drawdown calculated as peak-to-trough unrealized PnL relative to total invested (positive percentage)
- [06-02]: Drawdown only calculated after achieving positive unrealized PnL (avoids spurious drawdowns during initial accumulation)
- [06-02]: Golden snapshot uses 60 days of realistic price movement to exercise all tiers and market conditions
- [06-01]: Sliding windows computed in-loop (not pre-computed) for clarity and memory efficiency
- [06-01]: Warmup strategy: 30-day high uses partial window, MA200 returns 0 during warmup (conservative: no bear detection)
- [06-01]: MultiplierTierConfig separate from Configuration.MultiplierTier to avoid coupling backtest DTOs to mutable configuration classes
- [05-01]: Use ADDITIVE bear boost instead of MULTIPLICATIVE (tierMultiplier + bearBoost, not tierMultiplier * bearBoost) -- makes bear impact more predictable
- [05-01]: Extract to pure static class with zero dependencies -- enables backtest reuse without infrastructure coupling
- [v1.1 Roadmap]: MultiplierCalculator extraction is the only production code change -- all other backtest code lives in separate namespace
- [v1.1 Roadmap]: Phases 6 (Simulation) and 7 (Data Pipeline) are independent and can be built in either order after Phase 5
- [v1.1 Roadmap]: Data pipeline endpoints (API-03, API-04) grouped with Phase 7, backtest endpoints (API-01, API-02) grouped with Phase 8

### Known Risks

1. ~~**CoinGecko API rate limits**~~ -- RESOLVED: Free tier limits verified during Phase 7 implementation
2. ~~**Walk-forward validation design**~~ -- RESOLVED: Implemented in Phase 8 with 70/30 split and overfitting detection
3. ~~**No automated tests from v1.0**~~ -- RESOLVED: 53 unit tests now (24 in Phase 5, 28 in Phase 6, 1 existing)

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-02-13T09:16:04Z
Stopped at: Completed 08-02-PLAN.md (Phase 8 complete - v1.1 Milestone complete)
Resume file: .planning/phases/08-api-endpoints-parameter-sweep/08-02-SUMMARY.md

---
*State updated: 2026-02-13T09:16:04Z after completing Phase 08 Plan 02*

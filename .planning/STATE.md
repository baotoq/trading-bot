# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v1.1 Backtesting Engine
**Updated:** 2026-02-13T09:10:29Z

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-12)

**Core value:** Validate that smart DCA beats fixed DCA and find optimal multiplier parameters through historical backtesting
**Current focus:** Phase 8 - API Endpoints & Parameter Sweep

## Current Position

Phase: 8 of 8 (API Endpoints & Parameter Sweep) -- IN PROGRESS
Plan: 1 of 2 complete
Status: Executing Phase 8 - single backtest endpoint complete
Last activity: 2026-02-13 -- Plan 08-01 complete, ready for 08-02

Progress: ████████████████████████ 43.75% (3.5/8 phases complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 6 (v1.1)
- Average duration: 3.5 minutes
- Total execution time: 21 minutes

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 05-multiplier-calculator-extraction | 1 | 8m | 8m |
| 06-backtest-simulation-engine | 2 | 6m | 3m |
| 07-historical-data-pipeline | 2 | 6m | 3m |
| 08-api-endpoints-parameter-sweep | 1 | 1m | 1m |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

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

1. **CoinGecko API rate limits** -- Free tier limits need runtime verification during Phase 7 (MEDIUM confidence from research)
2. **Walk-forward validation design** -- Implementation details for train/test split need refinement during Phase 8 planning
3. ~~**No automated tests from v1.0**~~ -- RESOLVED: 53 unit tests now (24 in Phase 5, 28 in Phase 6, 1 existing)

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-02-13T09:10:29Z
Stopped at: Completed 08-01-PLAN.md (Phase 8 Plan 1 complete)
Resume file: .planning/phases/08-api-endpoints-parameter-sweep/08-01-SUMMARY.md

---
*State updated: 2026-02-13T09:10:29Z after completing Phase 08 Plan 01*

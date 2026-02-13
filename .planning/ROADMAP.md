# Roadmap: BTC Smart DCA Bot

## Milestones

- **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- **v1.1 Backtesting Engine** -- Phases 5-8 (in progress)

---

### v1.1 Backtesting Engine

**Milestone Goal:** Validate that smart DCA beats fixed DCA and find optimal multiplier parameters through historical backtesting against 2-4 years of BTC price data.

**Phase Numbering:**
- Integer phases (5, 6, 7, 8): Planned milestone work
- Decimal phases (e.g., 6.1): Urgent insertions (marked with INSERTED)

- [ ] **Phase 5: MultiplierCalculator Extraction** - Extract pure calculation logic from production DCA service for backtest reuse
- [ ] **Phase 6: Backtest Simulation Engine** - Day-by-day DCA simulation with metrics and fixed-DCA comparison
- [ ] **Phase 7: Historical Data Pipeline** - CoinGecko ingestion, gap detection, and data status API
- [ ] **Phase 8: API Endpoints & Parameter Sweep** - Backtest and sweep endpoints with walk-forward validation

## Phase Details

### Phase 5: MultiplierCalculator Extraction

**Goal:** Multiplier calculation logic exists as a pure, testable static class reusable by both live DCA and backtesting -- the only production code change in v1.1.
**Depends on:** Phase 4 (v1.0 complete)
**Requirements:** SIM-01
**Success Criteria** (what must be TRUE):
  1. MultiplierCalculator is a pure static class that computes multiplier from (currentPrice, high30Day, ma200Day, tiers, bearBoost, maxCap) without any async calls, DI, or database access
  2. DcaExecutionService delegates to MultiplierCalculator and produces identical behavior to pre-extraction code
  3. Unit tests verify MultiplierCalculator against known input/output pairs for each tier and bear boost combination
**Plans:** 1 plan

Plans:
- [ ] 05-01-PLAN.md -- MultiplierCalculator TDD extraction with additive bear boost and regression tests

### Phase 6: Backtest Simulation Engine

**Goal:** User can simulate a smart DCA strategy against any date range of price data and see comprehensive metrics comparing smart DCA vs fixed DCA.
**Depends on:** Phase 5
**Requirements:** SIM-02, SIM-03, SIM-04, SIM-05, SIM-06, SIM-07, SIM-08, SIM-09
**Success Criteria** (what must be TRUE):
  1. BacktestSimulator accepts a strategy config and price array, returns deterministic results -- same inputs always produce identical outputs
  2. Every backtest result includes both smart DCA and fixed DCA metrics side-by-side (total invested, total BTC, avg cost basis, portfolio value, return %, cost basis delta, extra BTC %, efficiency ratio)
  3. Multiplier tier breakdown shows how often each tier triggered and the extra spend attributed to each tier
  4. Max drawdown (unrealized loss vs total invested) is calculated and included in results
  5. Optional purchase log returns the full day-by-day simulation detail (date, price, multiplier, tier, amount spent, BTC bought)
**Plans:** TBD

Plans:
- [ ] 06-01: BacktestSimulator core loop with sliding windows and fixed DCA baseline
- [ ] 06-02: Metrics, tier breakdown, drawdown, and purchase log

### Phase 7: Historical Data Pipeline

**Goal:** User can ingest 2-4 years of BTC daily prices from CoinGecko into the database and verify data completeness via API.
**Depends on:** Phase 5 (independent of Phase 6 -- can be built in parallel)
**Requirements:** DATA-01, DATA-02, DATA-03, DATA-04, DATA-05, API-03, API-04
**Success Criteria** (what must be TRUE):
  1. POST /api/backtest/data/ingest fetches BTC daily prices from CoinGecko and persists them in the existing DailyPrice PostgreSQL table
  2. Ingestion is incremental -- re-running only fetches dates not already in the database, respecting CoinGecko rate limits
  3. System detects and reports gaps (missing dates) in the stored price data
  4. GET /api/backtest/data/status returns the available date range, total days stored, and any detected gaps
**Plans:** TBD

Plans:
- [ ] 07-01: CoinGeckoClient and data ingestion service
- [ ] 07-02: Gap detection, data status endpoint, and ingest endpoint

### Phase 8: API Endpoints & Parameter Sweep

**Goal:** User can run single backtests and parameter sweeps via API, with sweep results ranked by chosen optimization target and validated against overfitting.
**Depends on:** Phase 6, Phase 7
**Requirements:** SWEEP-01, SWEEP-02, SWEEP-03, SWEEP-04, SWEEP-05, SWEEP-06, API-01, API-02
**Success Criteria** (what must be TRUE):
  1. POST /api/backtest accepts a strategy config and date range, returns structured JSON with all simulation metrics
  2. POST /api/backtest/sweep accepts parameter ranges, generates combinations (with safety cap), runs backtests in parallel, and returns results ranked by user-chosen target (cost basis, total BTC, return %, efficiency)
  3. Sweep presets (conservative, full) provide ready-made parameter ranges for common use cases
  4. Walk-forward validation splits data into train/test periods and flags parameter sets that degrade out-of-sample
  5. Safety cap prevents runaway computation by limiting maximum parameter combinations
**Plans:** TBD

Plans:
- [ ] 08-01: Single backtest endpoint and request/response DTOs
- [ ] 08-02: Parameter sweep service with parallel execution and ranking
- [ ] 08-03: Walk-forward validation, presets, and safety caps

## Phase Dependencies

```
Phase 5 (MultiplierCalculator) --> Phase 6 (Simulation Engine)
Phase 5 (MultiplierCalculator) --> Phase 7 (Data Pipeline)
Phase 6 + Phase 7 -----------> Phase 8 (API & Sweep)
```

Phases 6 and 7 are independent of each other and can be built in parallel after Phase 5.

## Progress

**Execution Order:** 5 -> 6 -> 7 -> 8 (6 and 7 are independent; order is flexible)

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 5. MultiplierCalculator Extraction | v1.1 | 0/1 | Not started | - |
| 6. Backtest Simulation Engine | v1.1 | 0/2 | Not started | - |
| 7. Historical Data Pipeline | v1.1 | 0/2 | Not started | - |
| 8. API Endpoints & Parameter Sweep | v1.1 | 0/3 | Not started | - |

---
*Roadmap updated: 2026-02-13 after v1.1 roadmap creation*

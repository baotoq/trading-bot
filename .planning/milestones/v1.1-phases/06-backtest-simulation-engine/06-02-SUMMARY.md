---
phase: 06-backtest-simulation-engine
plan: 02
subsystem: backtest
tags: [tdd, max-drawdown, snapshot-testing, metrics, edge-cases]

# Dependency graph
requires:
  - phase: 06-01
    provides: Core BacktestSimulator with purchase log and metrics (MaxDrawdown placeholder)
provides:
  - Complete max drawdown calculation for all three strategies
  - Comprehensive test coverage including metrics verification and edge cases
  - Golden snapshot baseline for regression detection
  - 28 total BacktestSimulator tests (53 total project tests)
affects: [07-data-pipeline, 08-api-endpoints]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - TDD with RED-GREEN cycle for max drawdown implementation
    - Snapper golden snapshot testing for full result regression detection
    - Peak-to-trough unrealized PnL drawdown calculation pattern

key-files:
  created:
    - tests/TradingBot.ApiService.Tests/Application/Services/_snapshots/BacktestSimulatorTests_Run_GoldenScenario_MatchSnapshot.json
  modified:
    - TradingBot.ApiService/Application/Services/BacktestSimulator.cs
    - tests/TradingBot.ApiService.Tests/Application/Services/BacktestSimulatorTests.cs

key-decisions:
  - "Max drawdown calculated as peak-to-trough unrealized PnL relative to total invested (positive percentage)"
  - "Drawdown only calculated after achieving positive unrealized PnL (avoids spurious drawdowns during initial accumulation)"
  - "Golden snapshot uses 60 days of realistic price movement to exercise all tiers and market conditions"

patterns-established:
  - "Max drawdown pattern: Track peak unrealized PnL, calculate percentage decline from peak relative to capital invested"
  - "Snapshot testing pattern: Use Snapper with comprehensive realistic scenarios for full result verification"
  - "Edge case test coverage: single day, flat prices, null inputs, monotonic price movements"

# Metrics
duration: 3min
completed: 2026-02-13
---

# Phase 06 Plan 02: Backtest Metrics & Edge Cases Summary

**Complete max drawdown calculation with peak-to-trough unrealized PnL tracking, comprehensive edge case coverage, and golden snapshot baseline for regression detection**

## Performance

- **Duration:** 3 minutes
- **Started:** 2026-02-13T05:05:40Z
- **Completed:** 2026-02-13T05:09:15Z
- **Tasks:** 2 (TDD RED + GREEN)
- **Files modified:** 2 (+ 1 snapshot created)

## Accomplishments
- Max drawdown calculation implemented for all three strategies (smart DCA, same-base, match-total)
- Tracks peak unrealized PnL and worst drawdown from peak relative to total invested
- Returns 0 for monotonically rising prices (no drawdown occurred)
- 15 new tests added: max drawdown (3), tier breakdown (4), comparison metrics (4), edge cases (4)
- Golden snapshot test with 60 days of realistic price movement (decline, bear market, recovery, bull run)
- All 28 BacktestSimulatorTests pass, 53 total project tests pass
- Zero regressions in Phase 5 tests (MultiplierCalculator)
- Phase 6 success criteria fully met

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - Write failing tests for max drawdown, tier breakdown, comparison metrics, and edge cases** - `ee5aace` (test)
   - Added 15 new tests across 4 categories
   - MaxDrawdown tests fail as expected (placeholder returns 0m)
   - Tier breakdown and comparison tests verify Plan 01 implementation

2. **Task 2: GREEN - Implement max drawdown calculation and add golden snapshot** - `103ecef` (feat)
   - Implemented CalculateMaxDrawdown method with peak-to-trough logic
   - Updated CalculateMetrics to accept and use max drawdown parameter
   - Added CreateRealisticPriceData helper for golden snapshot
   - Added golden snapshot test using Snapper
   - All 28 tests pass including golden snapshot

**Plan metadata:** Will be committed after SUMMARY.md and STATE.md updates

_Note: TDD plan with RED-GREEN cycle (no refactor needed)_

## Files Created/Modified

**Created:**
- `tests/TradingBot.ApiService.Tests/Application/Services/_snapshots/BacktestSimulatorTests_Run_GoldenScenario_MatchSnapshot.json` - Golden snapshot baseline (57KB) capturing full BacktestResult structure

**Modified:**
- `TradingBot.ApiService/Application/Services/BacktestSimulator.cs` - Added CalculateMaxDrawdown method, updated Run to calculate max drawdown for all strategies
- `tests/TradingBot.ApiService.Tests/Application/Services/BacktestSimulatorTests.cs` - Added 16 new tests (15 + 1 golden snapshot) and CreateRealisticPriceData helper

## Decisions Made

1. **Max drawdown calculation:** Peak-to-trough unrealized PnL relative to total invested, returned as positive percentage
2. **Drawdown threshold:** Only calculate after achieving positive unrealized PnL (avoids spurious drawdowns during initial accumulation phase)
3. **Golden snapshot data:** 60 days with 4 phases (decline, bear market, recovery, bull run) to exercise all multiplier tiers and market conditions
4. **Edge case coverage:** Single day, flat prices, null inputs, monotonically rising prices all handled correctly

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tests passed on first implementation run.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Phase 6 (Backtest Simulation Engine) complete.** Ready for Phase 7 (Data Pipeline):
- BacktestSimulator fully complete with all metrics (including max drawdown)
- Comprehensive test coverage (28 tests) ensures reliability
- Golden snapshot prevents future regressions
- DailyPriceData input structure defined and tested
- Deterministic simulation enables reliable integration testing

**Ready for Phase 8 (API Endpoints):**
- Complete BacktestResult output structure defined
- All metrics verified and tested
- Snapshot baseline enables API response validation

## Self-Check: PASSED

All claimed files verified:
- FOUND: BacktestSimulator.cs (modified with CalculateMaxDrawdown)
- FOUND: BacktestSimulatorTests.cs (16 new tests added)
- FOUND: BacktestSimulatorTests_Run_GoldenScenario_MatchSnapshot.json (golden snapshot)

All commits verified:
- FOUND: ee5aace (test: RED phase)
- FOUND: 103ecef (feat: GREEN phase)

All tests verified:
- BacktestSimulatorTests: 28 tests pass (12 from Plan 01 + 16 from Plan 02)
- Total project tests: 53 pass
- Zero regressions

---
*Phase: 06-backtest-simulation-engine*
*Completed: 2026-02-13*

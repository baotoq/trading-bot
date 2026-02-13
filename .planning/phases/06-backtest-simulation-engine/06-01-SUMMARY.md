---
phase: 06-backtest-simulation-engine
plan: 01
subsystem: backtest
tags: [tdd, simulation, multiplier-calculator, backtest, dca]

# Dependency graph
requires:
  - phase: 05-multiplier-calculator-extraction
    provides: Pure static MultiplierCalculator for multiplier logic
provides:
  - Pure static BacktestSimulator with day-by-day DCA simulation
  - Complete DTO suite for backtest configuration and results
  - Smart DCA simulation using MultiplierCalculator
  - Same-base and match-total fixed DCA baselines
  - Sliding window calculations (30-day high, 200-day MA)
  - Comprehensive metrics and comparison calculations
  - Full purchase log with side-by-side strategy comparison
affects: [06-02, 07-data-pipeline, 08-api-endpoints]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - TDD with RED-GREEN cycle for simulation engine
    - Pure static simulation (no DI, no async, no database)
    - Immutable DTOs using C# records
    - Sliding window computation with warmup handling
    - Multi-strategy parallel simulation in single pass

key-files:
  created:
    - TradingBot.ApiService/Application/Services/Backtest/DailyPriceData.cs
    - TradingBot.ApiService/Application/Services/Backtest/BacktestConfig.cs
    - TradingBot.ApiService/Application/Services/Backtest/PurchaseLogEntry.cs
    - TradingBot.ApiService/Application/Services/Backtest/BacktestResult.cs
    - TradingBot.ApiService/Application/Services/BacktestSimulator.cs
    - tests/TradingBot.ApiService.Tests/Application/Services/BacktestSimulatorTests.cs
  modified: []

key-decisions:
  - "Sliding windows computed in-loop (not pre-computed) for clarity and memory efficiency"
  - "Warmup strategy: 30-day high uses partial window, MA200 returns 0 during warmup"
  - "Portfolio valuation uses last day's closing price"
  - "Fixed DCA buys on same days as smart DCA (every day in price array)"
  - "MultiplierTierConfig separate from Configuration.MultiplierTier to avoid coupling backtest DTOs to mutable configuration classes"
  - "Three-strategy simulation: smart DCA in first pass, match-total in second pass, all combined in purchase log"

patterns-established:
  - "TDD pattern: failing tests first (RED), implementation second (GREEN), per-task commits"
  - "Backtest namespace pattern: All backtest-specific DTOs in Application.Services.Backtest"
  - "Pure static simulation pattern: No infrastructure dependencies, deterministic results"
  - "Comprehensive purchase log: All strategies side-by-side with window values for transparency"

# Metrics
duration: 3min
completed: 2026-02-13
---

# Phase 06 Plan 01: Backtest Simulation Engine Summary

**Pure static BacktestSimulator with complete smart DCA simulation, same-base and match-total fixed DCA baselines, sliding window calculations, and comprehensive metrics using TDD**

## Performance

- **Duration:** 3 minutes
- **Started:** 2026-02-13T04:58:41Z
- **Completed:** 2026-02-13T05:02:18Z
- **Tasks:** 2 (TDD RED + GREEN)
- **Files modified:** 6

## Accomplishments
- Complete backtest simulation engine for day-by-day DCA replay against historical prices
- Smart DCA uses MultiplierCalculator.Calculate() for tier-based multiplier logic
- Same-base fixed DCA baseline (base amount every day, multiplier=1)
- Match-total fixed DCA baseline (spread smart DCA total equally across all days)
- Sliding window calculations: 30-day high with partial window warmup, 200-day MA returns 0 during warmup
- Full metrics suite: total invested, total BTC, avg cost basis, portfolio value, return %
- Comparison metrics: cost basis deltas, extra BTC %, efficiency ratio
- Tier breakdown showing per-tier trigger counts, extra USD spent, extra BTC acquired
- Comprehensive purchase log with all three strategies side-by-side and window values
- 12 TDD tests with 100% pass rate, zero regressions in existing tests

## Task Commits

Each task was committed atomically:

1. **Task 1: RED - Create DTOs and write failing tests for core simulation** - `c4f2bb6` (test)
   - Created all Backtest namespace DTOs
   - Created BacktestSimulator stub (throws NotImplementedException)
   - Added 12 TDD tests covering core simulation behavior
   - All tests fail as expected (RED phase)

2. **Task 2: GREEN - Implement BacktestSimulator core loop with smart DCA and same-base fixed DCA** - `0654bfe` (feat)
   - Implemented full day-by-day simulation loop
   - Compute sliding windows with warmup handling
   - Smart DCA uses MultiplierCalculator for multiplier logic
   - Same-base and match-total fixed DCA baselines
   - Calculate all metrics and comparison metrics
   - Generate tier breakdown and purchase log
   - All 12 TDD tests pass, zero regressions (GREEN phase)

**Plan metadata:** Will be committed after SUMMARY.md and STATE.md updates

_Note: TDD plan with RED-GREEN cycle (no refactor needed)_

## Files Created/Modified

**Created:**
- `TradingBot.ApiService/Application/Services/Backtest/DailyPriceData.cs` - Input price data record for simulation
- `TradingBot.ApiService/Application/Services/Backtest/BacktestConfig.cs` - Backtest-specific configuration (mirrors DcaOptions multiplier fields)
- `TradingBot.ApiService/Application/Services/Backtest/PurchaseLogEntry.cs` - Single day's purchase log with all three strategies side-by-side
- `TradingBot.ApiService/Application/Services/Backtest/BacktestResult.cs` - Full nested result structure with metrics, comparison, tier breakdown, purchase log
- `TradingBot.ApiService/Application/Services/BacktestSimulator.cs` - Pure static simulation engine (248 lines)
- `tests/TradingBot.ApiService.Tests/Application/Services/BacktestSimulatorTests.cs` - 12 TDD tests for core simulation

**Modified:** None

## Decisions Made

1. **Sliding window computation:** In-loop calculation (not pre-computed) for clarity and memory efficiency
2. **Warmup strategy:** 30-day high uses partial window (max of available days), MA200 returns 0 when insufficient data (conservative: no bear detection during warmup)
3. **Portfolio valuation:** Last day's closing price (final portfolio value = total BTC * last close)
4. **Fixed DCA timing:** Buys on same days as smart DCA (every day in price array)
5. **DTO coupling:** MultiplierTierConfig separate from Configuration.MultiplierTier to avoid coupling backtest DTOs to mutable configuration classes (converted at runtime)
6. **Simulation passes:** Smart DCA + same-base in first pass, match-total in second pass (requires smart total), combined in purchase log

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tests passed on first implementation run.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for Phase 06 Plan 02:** Max drawdown calculation and additional comparison metrics
- BacktestSimulator returns DcaMetrics with MaxDrawdown field (currently 0m placeholder)
- All core simulation logic complete and tested
- Purchase log provides full transparency for debugging/verification

**Ready for Phase 07:** Data pipeline can use DailyPriceData DTO
- Input data structure defined and tested
- Simulation accepts any price array (no gap handling needed - treats as continuous)

**Ready for Phase 08:** API endpoints can accept BacktestConfig and return BacktestResult
- Full request/response DTOs defined
- Deterministic simulation enables reliable API testing

## Self-Check: PASSED

All claimed files verified:
- FOUND: DailyPriceData.cs
- FOUND: BacktestConfig.cs
- FOUND: PurchaseLogEntry.cs
- FOUND: BacktestResult.cs
- FOUND: BacktestSimulator.cs
- FOUND: BacktestSimulatorTests.cs

All commits verified:
- FOUND: c4f2bb6 (test: RED phase)
- FOUND: 0654bfe (feat: GREEN phase)

---
*Phase: 06-backtest-simulation-engine*
*Completed: 2026-02-13*

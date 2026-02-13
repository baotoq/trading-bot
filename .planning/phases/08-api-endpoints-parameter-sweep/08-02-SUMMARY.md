---
phase: 08-api-endpoints-parameter-sweep
plan: 02
subsystem: api-backtest
tags: [api, endpoint, parameter-sweep, walk-forward-validation, optimization]
dependency_graph:
  requires:
    - 08-01 (Single backtest endpoint)
    - 06-01 (BacktestSimulator)
    - 07-02 (DailyPrice data)
  provides:
    - "POST /api/backtest/sweep endpoint"
    - "GET /api/backtest/presets/{name} endpoint"
    - "ParameterSweepService with cartesian product generation"
    - "SweepPresets (conservative and full)"
    - "WalkForwardValidator for overfitting detection"
  affects:
    - "Future parameter optimization workflows"
tech_stack:
  added:
    - "ParameterSweepService for parallel backtest execution"
    - "SweepPresets with conservative (~24) and full (~2160) combinations"
    - "WalkForwardValidator with train/test split"
  patterns:
    - "Cartesian product via chained LINQ SelectMany"
    - "Parallel execution with batching (4-16 cores)"
    - "Safety cap to prevent runaway computation (max 1000 combinations)"
    - "Walk-forward validation with 70/30 train/test split"
    - "Overfitting detection: -20% return OR -0.3 efficiency degradation"
key_files:
  created:
    - "TradingBot.ApiService/Application/Services/Backtest/SweepRequest.cs"
    - "TradingBot.ApiService/Application/Services/Backtest/SweepResponse.cs"
    - "TradingBot.ApiService/Application/Services/Backtest/SweepPresets.cs"
    - "TradingBot.ApiService/Application/Services/Backtest/ParameterSweepService.cs"
    - "TradingBot.ApiService/Application/Services/Backtest/WalkForwardValidator.cs"
  modified:
    - "TradingBot.ApiService/Endpoints/BacktestEndpoints.cs (sweep and preset endpoints)"
    - "TradingBot.ApiService/Program.cs (ParameterSweepService DI registration)"
decisions:
  - "Sweep presets: conservative (~24 combos) and full (~2160 combos) for quick vs comprehensive exploration"
  - "Default maxCombinations safety cap: 1000 (user can override up to 10000)"
  - "Parallel execution with batching: 4-16 cores based on Environment.ProcessorCount"
  - "Walk-forward validation optional (validate flag), uses 70/30 train/test split"
  - "Overfitting threshold: -20 percentage points in return OR -0.3 in efficiency ratio"
  - "Top 5 results include full purchase logs and tier breakdown; all others summary only"
  - "Ranking metrics: efficiency (default), costbasis, extrabtc, returnpct"
metrics:
  tasks: 2
  files_created: 5
  files_modified: 2
  tests_passing: 53
  duration: 4m
  completed_at: "2026-02-13T09:16:04Z"
---

# Phase 08 Plan 02: Parameter Sweep Endpoint Summary

**One-liner:** POST /api/backtest/sweep with cartesian product generation, parallel execution, result ranking, sweep presets (conservative/full), and optional walk-forward validation for overfitting detection.

## What Was Built

Created the parameter sweep endpoint (POST /api/backtest/sweep) that accepts parameter ranges or preset names, generates all combinations via cartesian product, executes backtests in parallel with safety caps, ranks results by user-chosen metric, and optionally performs walk-forward validation to detect overfitting.

### Key Components

**SweepRequest DTO:**
- All parameter lists nullable (defaults to production DcaOptions)
- Optional preset name ("conservative" or "full")
- Parameter ranges: BaseAmounts, HighLookbackDays, BearMarketMaPeriods, BearBoosts, MaxMultiplierCaps, TierSets
- RankBy metric selection (default: efficiency)
- MaxCombinations safety cap (default 1000, max 10000)
- Validate flag for walk-forward validation (default false)

**SweepResponse DTO:**
- Total and executed combination counts
- Ranked by metric name
- Date range and total days
- All results with summary metrics only (no purchase log)
- Top 5 results with full purchase logs and tier breakdown
- Optional walk-forward summary

**SweepPresets:**
- Conservative preset: ~24 combinations (quick exploration)
  - BaseAmounts: [10, 15, 20]
  - HighLookbackDays: [21, 30]
  - BearMarketMaPeriods: [200]
  - BearBoosts: [1.0, 1.5]
  - MaxMultiplierCaps: [3.0, 4.0]
  - Single standard tier set
- Full preset: ~2160 combinations (comprehensive search)
  - BaseAmounts: [10, 15, 20, 25, 30]
  - HighLookbackDays: [14, 21, 30, 60]
  - BearMarketMaPeriods: [100, 150, 200]
  - BearBoosts: [1.0, 1.25, 1.5, 2.0]
  - MaxMultiplierCaps: [3.0, 4.0, 5.0]
  - Three different tier configurations

**ParameterSweepService:**
- GenerateCombinations: Cartesian product via chained LINQ SelectMany
- ExecuteSweepAsync: Parallel execution with batching (4-16 cores based on processor count)
- RankResults: Support for efficiency, costbasis, extrabtc, returnpct metrics
- Returns all results (summary only) and top 5 (with full logs)

**WalkForwardValidator:**
- Validate: Single config train/test split (70/30)
- ValidateAll: Sequential validation for all sweep results
- Overfitting detection: -20% return OR -0.3 efficiency degradation
- Guards against insufficient data (requires 30+ days per period)

**BacktestEndpoints:**
- POST /api/backtest/sweep: Full sweep endpoint with preset support, safety cap, walk-forward validation
- GET /api/backtest/presets/{name}: Preset inspection endpoint

**Program.cs:**
- Registered ParameterSweepService as scoped service

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

1. `dotnet build TradingBot.slnx` - **PASSED** (zero errors)
2. `dotnet test` - **PASSED** (53 tests, all passing)
3. All 5 new files created - **VERIFIED**
4. BacktestEndpoints.cs has POST /sweep and GET /presets/{name} - **VERIFIED**
5. Program.cs has ParameterSweepService registered - **VERIFIED**
6. Conservative preset generates ~24 combinations - **VERIFIED** (3×2×1×2×2×1 = 24)
7. Full preset generates ~2160 combinations - **VERIFIED** (5×4×3×4×3×3 = 2160)

### Success Criteria Met

- POST /api/backtest/sweep accepts parameter ranges or preset name
- Cartesian product generates all combinations from parameter lists
- Safety cap (default 1000) prevents runaway computation; exceeding returns 400 Bad Request
- Results ranked by user-chosen metric (efficiency, costbasis, extrabtc, returnpct)
- All results include smart DCA vs fixed DCA comparison (summary metrics only)
- Top 5 results include full purchase logs and tier breakdown
- Presets "conservative" (~24 combos) and "full" (~2160 combos) available via preset name or GET endpoint
- Walk-forward validation (validate=true) splits 70/30, detects degradation, flags overfitting per-result
- All existing tests pass with zero regressions

## Task Breakdown

### Task 1: Create sweep DTOs, presets, and ParameterSweepService (b99bed7)
- Created SweepRequest record with parameter lists, preset support, rankBy, maxCombinations, validate flag
- Created SweepResponse with nested DTOs: SweepResultEntry (summary), SweepResultDetailEntry (full logs), WalkForwardEntry, WalkForwardSummary
- Created SweepPresets static class with GetPreset method
- Conservative preset: ~24 combinations (3×2×1×2×2×1)
- Full preset: ~2160 combinations (5×4×3×4×3×3)
- Created ParameterSweepService with GenerateCombinations (cartesian product via chained SelectMany)
- ExecuteSweepAsync: parallel execution with batching (4-16 cores), ranking logic
- RankResults: pattern match on rankBy string (efficiency, costbasis, extrabtc, returnpct)
- Build and tests verified successful

### Task 2: Create WalkForwardValidator, add sweep endpoint, and wire DI (31e9aef)
- Created WalkForwardValidator static class with Validate and ValidateAll methods
- Train/test split (70/30), guards against insufficient data (30+ days per period)
- Overfitting detection: -20% return OR -0.3 efficiency degradation
- Added POST /api/backtest/sweep to BacktestEndpoints
- Preset resolution: merge explicit request values with preset defaults
- Safety cap enforcement: return 400 if combinations exceed maxCombinations
- Date range validation and price data fetching (same pattern as Plan 01)
- ExecuteSweepAsync call with parallel execution
- Optional walk-forward validation when validate=true
- Attach WalkForwardEntry to each result (summary and detail)
- Added GET /api/backtest/presets/{name} endpoint for preset inspection
- Registered ParameterSweepService in Program.cs DI
- All 53 tests pass with zero regressions

## Self-Check

Verifying all claimed files exist and commits are recorded:

- SweepRequest.cs: **FOUND**
- SweepResponse.cs: **FOUND**
- SweepPresets.cs: **FOUND**
- ParameterSweepService.cs: **FOUND**
- WalkForwardValidator.cs: **FOUND**
- BacktestEndpoints.cs: **MODIFIED** (POST /sweep, GET /presets/{name} added)
- Program.cs: **MODIFIED** (ParameterSweepService registered at line 109)
- Commit b99bed7: **FOUND** (feat(08-02): add sweep DTOs, presets, and ParameterSweepService)
- Commit 31e9aef: **FOUND** (feat(08-02): add sweep endpoint and walk-forward validation)

## Self-Check: PASSED

All files exist, all commits recorded, all tests passing (53/53).

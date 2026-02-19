---
phase: 08-api-endpoints-parameter-sweep
plan: 01
subsystem: api-backtest
tags: [api, endpoint, backtest, dtos]
dependency_graph:
  requires:
    - 07-02 (DailyPrice data ingestion)
    - 06-01 (BacktestSimulator)
    - 06-02 (BacktestResult structure)
  provides:
    - "POST /api/backtest endpoint"
    - "BacktestRequest/Response DTOs"
    - "Config defaults from production DcaOptions"
  affects:
    - "08-02 (parameter sweep depends on single backtest)"
tech_stack:
  added:
    - "Minimal API endpoints for backtest"
    - "IOptionsMonitor<DcaOptions> for config defaults"
  patterns:
    - "Request/Response DTOs with nullable override fields"
    - "Date range validation against available data"
    - "Default date range: last 2 years of available data"
key_files:
  created:
    - "TradingBot.ApiService/Application/Services/Backtest/BacktestRequest.cs"
    - "TradingBot.ApiService/Application/Services/Backtest/BacktestResponse.cs"
    - "TradingBot.ApiService/Endpoints/BacktestEndpoints.cs"
  modified:
    - "TradingBot.ApiService/Program.cs (endpoint registration)"
decisions:
  - "All request fields nullable - default to production DcaOptions when null"
  - "Default date range: last 2 years clamped to available data"
  - "Return 400 Bad Request for invalid date ranges or missing data"
  - "Clamp start date to minDate if user-provided date is earlier than available data"
metrics:
  tasks: 2
  files_created: 3
  files_modified: 1
  tests_passing: 53
  duration: 1m
  completed_at: "2026-02-13T09:10:29Z"
---

# Phase 08 Plan 01: Single Backtest API Endpoint Summary

**One-liner:** POST /api/backtest endpoint with request/response DTOs, config defaults from production DcaOptions, date range validation, and full BacktestResult response.

## What Was Built

Created the single backtest API endpoint (POST /api/backtest) that accepts optional strategy config and date range parameters, resolves defaults from production DcaOptions, validates against available historical data, fetches DailyPrices, runs BacktestSimulator, and returns a full BacktestResponse with all simulation metrics.

### Key Components

**BacktestRequest DTO:**
- All fields nullable (defaults applied from production DcaOptions)
- Optional date range (StartDate, EndDate)
- Optional strategy config overrides (BaseDailyAmount, HighLookbackDays, BearMarketMaPeriod, BearBoostFactor, MaxMultiplierCap)
- Optional multiplier tier overrides (List<MultiplierTierInput>)

**BacktestResponse DTO:**
- Wraps BacktestResult with resolved config and metadata
- Includes actual dates used and total days simulated
- Config field shows resolved configuration (after defaults applied)

**BacktestEndpoints:**
- POST /api/backtest handler with full validation and default handling
- Queries available data range from DailyPrices table
- Returns 400 Bad Request if no data available or date range invalid
- Default date range: last 2 years of available data (clamped to available range)
- Resolves all nullable request fields to production DcaOptions values
- Fetches price data and runs BacktestSimulator
- Returns complete BacktestResponse with metrics

**Program.cs:**
- Registered MapBacktestEndpoints() after MapDataEndpoints()
- No new DI registrations needed (BacktestSimulator is static, DcaOptions already registered)

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

1. `dotnet build TradingBot.slnx` - **PASSED** (zero errors)
2. `dotnet test` - **PASSED** (53 tests, all passing)
3. BacktestRequest.cs, BacktestResponse.cs, BacktestEndpoints.cs - **EXIST**
4. Program.cs includes `app.MapBacktestEndpoints()` - **VERIFIED**
5. BacktestEndpoints handles all validation cases - **VERIFIED**

### Success Criteria Met

- POST /api/backtest accepts optional strategy config and optional date range
- Missing config fields default to production DcaOptions via IOptionsMonitor
- Missing date range defaults to last 2 years of available data
- Response includes resolved config, date metadata, and full BacktestResult
- Invalid date ranges return 400 Bad Request with error message
- No historical data returns 400 Bad Request with helpful message
- All existing tests pass with zero regressions

## Task Breakdown

### Task 1: Create request/response DTOs (61f1f37)
- Created BacktestRequest record with all nullable override fields
- Created BacktestResponse record wrapping BacktestResult with resolved config
- Added MultiplierTierInput record for tier overrides
- Build verified successful

### Task 2: Create BacktestEndpoints and wire into Program.cs (0dd9d7c)
- Created BacktestEndpoints static class with MapBacktestEndpoints extension
- Implemented RunBacktestAsync handler with full validation logic
- Resolved config defaults from production DcaOptions via IOptionsMonitor
- Query available data range and validate user-provided dates
- Default to last 2 years, clamped to available data
- Return 400 for invalid ranges or missing data
- Fetch DailyPrices, convert to DailyPriceData, run BacktestSimulator
- Return full BacktestResponse with metrics
- Registered endpoint in Program.cs
- All 53 tests pass with zero regressions

## Self-Check

Verifying all claimed files exist and commits are recorded:

- BacktestRequest.cs: **FOUND** (709 bytes, created 2026-02-13)
- BacktestResponse.cs: **FOUND** (351 bytes, created 2026-02-13)
- BacktestEndpoints.cs: **FOUND** (5054 bytes, created 2026-02-13)
- Program.cs: **MODIFIED** (MapBacktestEndpoints() registered at line 138)
- Commit 61f1f37: **FOUND** (feat(08-01): add backtest request/response DTOs)
- Commit 0dd9d7c: **FOUND** (feat(08-01): add POST /api/backtest endpoint)

## Self-Check: PASSED

All files exist, all commits recorded, all tests passing (53/53).

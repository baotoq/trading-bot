---
phase: 07-historical-data-pipeline
plan: 02
subsystem: HistoricalData.API
tags: [api, endpoints, minimal-api, dto]
dependency_graph:
  requires: [IngestionJob, IngestionJobQueue, GapDetectionService, DataIngestionService, DataIngestionBackgroundService, CoinGeckoClient]
  provides: [DataEndpoints, DataStatusResponse, IngestResponse, JobStatusResponse]
  affects: [Program.cs, appsettings.json]
tech_stack:
  added: []
  patterns:
    - Minimal API with MapGroup for route organization
    - Results.Accepted with Location header for async job creation
    - Results.Conflict for concurrent job prevention
    - Estimated progress calculation for long-running jobs
key_files:
  created:
    - TradingBot.ApiService/Application/Services/HistoricalData/Models/DataStatusResponse.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestResponse.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/Models/JobStatusResponse.cs
    - TradingBot.ApiService/Endpoints/DataEndpoints.cs
  modified:
    - TradingBot.ApiService/Program.cs
    - TradingBot.ApiService/appsettings.json
decisions: []
metrics:
  duration_minutes: 1
  completed_at: "2026-02-13T08:27:37Z"
  tasks_completed: 1
  files_created: 4
  files_modified: 2
  commits: 1
---

# Phase 07 Plan 02: Data Pipeline API Endpoints Summary

**One-liner:** Three minimal API endpoints exposing data ingestion, status checking, and job polling with rich DTOs and async job pattern.

## What Was Built

### Core Components

**1. Response DTOs**
- **DataStatusResponse**: Rich data coverage info including date range, gaps (first 20), coverage percentage, freshness indicator (Fresh/Recent/Stale), last ingestion summary
- **IngestResponse**: Job ID + estimated completion time for async job tracking
- **JobStatusResponse**: Complete job details with estimated progress percentage for polling
- **IngestionJobSummary**: Nested DTO for last ingestion info in status response

**2. DataEndpoints (Minimal API)**
- **POST /api/backtest/data/ingest**: Creates ingestion job for last 4 years, returns 202 Accepted with job ID and ~2 min ETA
  - Query parameter: `force` (default: false) for re-fetching existing dates
  - Checks for running/pending job, returns 409 Conflict if found
  - Creates IngestionJob in DB, enqueues job ID via IngestionJobQueue
  - Defensive: Sets job to Failed if queue full (shouldn't happen)
- **GET /api/backtest/data/status**: Returns data coverage statistics
  - Queries min/max date from DailyPrices
  - Calculates freshness: ≤2 days="Fresh", ≤7="Recent", >7="Stale"
  - Gets coverage stats via GapDetectionService (total days, gaps, coverage %)
  - Limits gap dates to first 20
  - Includes last ingestion job summary
  - Empty state: helpful message prompting POST /ingest
- **GET /api/backtest/data/ingest/{jobId}**: Polls job status with progress
  - Returns complete job details (dates, status, timestamps, records fetched)
  - Calculates progress percent:
    - Pending: 0%, Completed/CompletedWithGaps: 100%, Failed: 0%
    - Running: Estimates based on elapsed time vs. estimated total (chunked API calls)
    - Caps at 99% until actually done

**3. DI Registration in Program.cs**
- Added using statements: `TradingBot.ApiService.Application.Services.HistoricalData`, `TradingBot.ApiService.Infrastructure.CoinGecko`, `TradingBot.ApiService.Endpoints`
- Registered services:
  - `builder.Services.AddCoinGecko(builder.Configuration)` - CoinGecko HTTP client
  - `builder.Services.AddSingleton<IngestionJobQueue>()` - Bounded queue
  - `builder.Services.AddScoped<GapDetectionService>()` - Gap detection
  - `builder.Services.AddScoped<DataIngestionService>()` - Ingestion orchestration
  - `builder.Services.AddHostedService<DataIngestionBackgroundService>()` - Background worker
- Mapped endpoints: `app.MapDataEndpoints()` after `app.MapDefaultEndpoints()`

**4. Configuration**
- Added `CoinGecko` section to appsettings.json with empty `ApiKey` field (free tier default)

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

**Build:**
```
✓ dotnet build TradingBot.ApiService - Build succeeded (5 warnings, 0 errors)
```

**Files Created:**
```
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/DataStatusResponse.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestResponse.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/JobStatusResponse.cs
✓ TradingBot.ApiService/Endpoints/DataEndpoints.cs
```

**Files Modified:**
```
✓ TradingBot.ApiService/Program.cs - Added DI registrations and endpoint mapping
✓ TradingBot.ApiService/appsettings.json - Added CoinGecko configuration section
```

**Tests:**
```
✓ dotnet test - All 53 tests passed (no regressions)
```

## Self-Check: PASSED

**Created files exist:**
```
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/DataStatusResponse.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestResponse.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/JobStatusResponse.cs
✓ TradingBot.ApiService/Endpoints/DataEndpoints.cs
```

**Commits exist:**
```
✓ 669bb88 - feat(07-02): add data pipeline API endpoints
```

## Task Breakdown

| Task | Description | Commit | Key Files |
|------|-------------|--------|-----------|
| 1 | Create response DTOs, DataEndpoints with all three routes, and DI wiring | 669bb88 | DataStatusResponse.cs, IngestResponse.cs, JobStatusResponse.cs, DataEndpoints.cs, Program.cs, appsettings.json |

## What's Next

**Phase 07 is complete!** The historical data pipeline is fully operational:
- POST /api/backtest/data/ingest triggers 4-year data fetch
- GET /api/backtest/data/status shows data completeness
- GET /api/backtest/data/ingest/{jobId} polls job progress

**Phase 08 (Backtest API & Orchestration)** will add:
- POST /api/backtest/run - Execute backtests with parameter sweeps
- GET /api/backtest/results/{runId} - Retrieve backtest results
- Backtest orchestration service to coordinate simulation + historical data

## Architecture Notes

**Why Accepted (202) instead of Created (201)?**
- Job creation is asynchronous - the actual ingestion happens in background service
- 202 Accepted signals "request accepted for processing, check back later"
- Location header points to GET /ingest/{jobId} for polling

**Why estimate progress for Running jobs?**
- Users want to see progress during long-running jobs (~2 minutes for 4 years)
- Estimation based on: total days → chunks (90-day windows) → API calls (2.5s each)
- Capped at 99% to avoid showing 100% before job actually completes

**Why limit gap dates to 20?**
- Full gap list could be hundreds of dates for incomplete datasets
- First 20 gaps give useful sample for debugging without overwhelming response
- Full gap detection still available via GapDetectionService for internal use

**Why separate IngestionJobSummary DTO?**
- DataStatusResponse returns last job summary (simplified)
- JobStatusResponse returns full job details
- IngestionJobSummary provides just enough info for status endpoint without duplication

## Success Criteria Met

- [x] POST /api/backtest/data/ingest creates a job, enqueues it, and returns job ID with estimated completion time
- [x] POST /api/backtest/data/ingest returns 409 Conflict if a job is already running or pending
- [x] POST /api/backtest/data/ingest supports force=true to re-fetch existing data
- [x] GET /api/backtest/data/status returns rich data coverage info (date range, gaps, coverage %, freshness, last ingestion)
- [x] GET /api/backtest/data/status returns helpful empty state message when no data exists
- [x] GET /api/backtest/data/ingest/{jobId} returns job progress with estimated completion percentage
- [x] All services are registered in DI and BackgroundService starts automatically
- [x] All existing 53 tests pass without regression

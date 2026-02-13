---
phase: 07-historical-data-pipeline
plan: 01
subsystem: HistoricalData.Ingestion
tags: [infrastructure, data-pipeline, coingecko, gap-detection, background-service]
dependency_graph:
  requires: [TradingBotDbContext, DailyPrice]
  provides: [CoinGeckoClient, DataIngestionService, GapDetectionService, IngestionJobQueue, DataIngestionBackgroundService, IngestionJob]
  affects: [DailyPrices]
tech_stack:
  added:
    - EFCore.BulkExtensions (9.x) - high-performance bulk inserts
    - CryptoExchange.Net (10.5.4) - dependency upgrade for compatibility
    - Channel<T> - bounded queue for job processing
  patterns:
    - HttpClient with resilience handler for CoinGecko API
    - Chunked date range fetching (90-day windows)
    - PostgreSQL generate_series for gap detection
    - Bounded Channel queue with DropWrite for single-job enforcement
    - IServiceScopeFactory pattern for background service
key_files:
  created:
    - TradingBot.ApiService/Models/IngestionJob.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestionJobStatus.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/Models/DataCoverageStats.cs
    - TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoClient.cs
    - TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoOptions.cs
    - TradingBot.ApiService/Infrastructure/CoinGecko/ServiceCollectionExtensions.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/GapDetectionService.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionService.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/IngestionJobQueue.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionBackgroundService.cs
    - TradingBot.ApiService/Infrastructure/Data/Migrations/20260213081957_AddIngestionJob.cs
  modified:
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
    - TradingBot.ApiService/TradingBot.ApiService.csproj
decisions:
  - id: COIN-01
    summary: Use direct HttpClient instead of CoinGecko.Net library for API calls
    rationale: CoinGecko.Net 5.5.0 API structure was incompatible with research assumptions. Direct HttpClient with JSON deserialization provides better control and avoids library coupling.
  - id: COIN-02
    summary: Use bounded Channel<T> with capacity=1 and DropWrite mode for job queue
    rationale: Ensures only one ingestion job runs at a time. DropWrite prevents blocking when enqueueing while job is running - caller can check return value.
  - id: COIN-03
    summary: Auto-fill gaps after bulk insert by fetching individual missing dates
    rationale: CoinGecko may not return data for all dates in range. Individual fetches maximize data completeness without failing entire job.
  - id: COIN-04
    summary: Set Open=High=Low=Close to match free tier limitation
    rationale: CoinGecko free tier doesn't provide true OHLC data, only price points. Using Close for all OHLC fields documents this limitation clearly in code.
metrics:
  duration_minutes: 5
  completed_at: "2026-02-13T08:21:50Z"
  tasks_completed: 2
  files_created: 11
  files_modified: 2
  commits: 2
---

# Phase 07 Plan 01: CoinGecko Data Fetching Infrastructure Summary

**One-liner:** HTTP-based CoinGecko client with chunked 90-day fetching, PostgreSQL gap detection, bulk upsert pipeline, and bounded background job processing.

## What Was Built

### Core Components

**1. IngestionJob Entity & Migration**
- Tracks job lifecycle with status (Pending, Running, Completed, CompletedWithGaps, Failed)
- Records timestamps (StartedAt, CompletedAt), progress (RecordsFetched, GapsDetected), and errors
- Uses UUIDv7 for job IDs, inherits AuditedEntity for automatic timestamps
- Indexed on Status and CreatedAt for efficient querying

**2. CoinGeckoClient**
- Direct HttpClient implementation calling `api.coingecko.com/api/v3/coins/bitcoin/market_chart/range`
- Chunks date ranges into 90-day windows (CoinGecko daily granularity requirement)
- Rate limiting: 25 calls/min enforced via SemaphoreSlim + 2.5s delay between calls
- Groups hourly price points by date, uses last point of day as Close
- Sets O=H=L=C (free tier limitation - no true OHLC data)
- Maps volume from TotalVolumes collection
- Configured with Standard Resilience Handler (3 retries, exponential backoff, circuit breaker)

**3. GapDetectionService**
- Uses PostgreSQL `generate_series` LEFT JOIN to find missing dates
- `DetectGapsAsync`: Returns list of missing DateOnly values for a date range
- `GetCoverageStatsAsync`: Calculates total/stored days, gap count, coverage percentage
- Parameterized SQL queries via FormattableStringFactory (EF Core safe interpolation)

**4. DataIngestionService**
- Orchestrates full pipeline: fetch → bulk upsert → gap detect → auto-fill → status update
- **Incremental mode** (default): Only inserts new rows, skips updates (BulkConfig.PropertiesToIncludeOnUpdate = [])
- **Force mode**: Updates existing rows with fresh OHLC data
- Uses EFCore.BulkExtensions `BulkInsertOrUpdateAsync` with composite key (Date, Symbol)
- Auto-fill gaps: After bulk insert, fetches individual missing dates and inserts via SaveChanges
- Comprehensive error handling: Failed auto-fills logged as warnings, don't fail entire job
- Sets job status: Completed (no gaps), CompletedWithGaps (some missing), Failed (exception)

**5. IngestionJobQueue**
- Bounded `Channel<Guid>` with capacity=1
- `FullMode.DropWrite`: TryEnqueue returns false if queue full (job already running)
- `ReadAllAsync`: Async enumerable for background service consumption
- Singleton registration (lives entire app lifetime)

**6. DataIngestionBackgroundService**
- Inherits `BackgroundService`, runs ExecuteAsync as long-running task
- Consumes jobs via `await foreach (jobId in queue.ReadAllAsync(stoppingToken))`
- Creates scoped service per job (DbContext, CoinGeckoClient, etc.)
- Catches exceptions per job - service continues on failure
- Logs start, per-job processing, and stop events

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Infrastructure] Added System.Runtime.CompilerServices using for FormattableStringFactory**
- **Found during:** Task 1 build
- **Issue:** GapDetectionService used FormattableStringFactory without import, causing CS0103 error
- **Fix:** Added `using System.Runtime.CompilerServices;`
- **Files modified:** GapDetectionService.cs
- **Commit:** 5b5e4ce

**2. [Rule 4 - Architectural Change] Replaced CoinGecko.Net library with direct HttpClient**
- **Found during:** Task 1 build
- **Issue:** CoinGecko.Net 5.5.0 API namespace structure didn't match research assumptions (no CoinGecko.Clients or CoinGecko.Interfaces). Library caused package downgrade conflict with CryptoExchange.Net.
- **Proposed change:** Use direct HttpClient with JSON deserialization, calling CoinGecko REST API directly
- **Rationale:**
  - Better control over HTTP requests and resilience configuration
  - Avoids library coupling and version conflicts
  - Matches existing codebase pattern (HyperliquidClient also uses HttpClient)
  - CoinGecko API is simple JSON REST - no complex SDK needed
- **Impact:** Removed CoinGecko.Net dependency, upgraded CryptoExchange.Net to 10.5.4, created MarketChartResponse DTO
- **Implementation:** Created internal MarketChartResponse class, used JsonSerializer.Deserialize, kept same chunking + rate limiting logic
- **Files affected:** CoinGeckoClient.cs, ServiceCollectionExtensions.cs, TradingBot.ApiService.csproj
- **Commit:** 5b5e4ce

## Verification Results

**Build:**
```
✓ dotnet build TradingBot.ApiService - Build succeeded
```

**Files Created:**
```
✓ TradingBot.ApiService/Models/IngestionJob.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestionJobStatus.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/DataCoverageStats.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoClient.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoOptions.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/ServiceCollectionExtensions.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/GapDetectionService.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionService.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/IngestionJobQueue.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionBackgroundService.cs
```

**Migration:**
```
✓ TradingBot.ApiService/Infrastructure/Data/Migrations/20260213081957_AddIngestionJob.cs
✓ TradingBot.ApiService/Infrastructure/Data/Migrations/20260213081957_AddIngestionJob.Designer.cs
```

**Packages:**
```
✓ EFCore.BulkExtensions (9.1.1) - added
✓ CryptoExchange.Net (10.5.4) - upgraded from 9.13.0
✓ CoinGecko.Net - removed (not needed)
```

## Self-Check: PASSED

**Created files exist:**
```
✓ TradingBot.ApiService/Models/IngestionJob.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/IngestionJobStatus.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/Models/DataCoverageStats.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoClient.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/CoinGeckoOptions.cs
✓ TradingBot.ApiService/Infrastructure/CoinGecko/ServiceCollectionExtensions.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/GapDetectionService.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionService.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/IngestionJobQueue.cs
✓ TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionBackgroundService.cs
✓ TradingBot.ApiService/Infrastructure/Data/Migrations/20260213081957_AddIngestionJob.cs
```

**Commits exist:**
```
✓ 5b5e4ce - feat(07-01): add IngestionJob entity, CoinGeckoClient, and GapDetectionService
✓ 1b8868c - feat(07-01): add DataIngestionService, IngestionJobQueue, and DataIngestionBackgroundService
```

## Task Breakdown

| Task | Description | Commit | Key Files |
|------|-------------|--------|-----------|
| 1 | Create IngestionJob entity, CoinGeckoClient with rate-limited chunked fetching, and GapDetectionService | 5b5e4ce | IngestionJob.cs, CoinGeckoClient.cs, GapDetectionService.cs, AddIngestionJob migration |
| 2 | Create DataIngestionService orchestration, IngestionJobQueue, and DataIngestionBackgroundService | 1b8868c | DataIngestionService.cs, IngestionJobQueue.cs, DataIngestionBackgroundService.cs |

## What's Next

**Phase 07 Plan 02** will expose API endpoints for:
- Triggering ingestion jobs (POST /api/data/ingest)
- Checking job status (GET /api/data/jobs/{id})
- Querying coverage stats (GET /api/data/coverage)
- Retrieving daily prices (GET /api/data/prices)

The background services created in this plan are **not yet registered** in DI or started. Plan 02 will add service registration in Program.cs and wire up the HTTP endpoints.

## Architecture Notes

**Why HttpClient instead of CoinGecko.Net?**
- Research assumed CoinGecko.Net library would provide typed client similar to Binance.Net pattern
- Actual CoinGecko.Net 5.5.0 structure incompatible (namespace differences)
- Direct HttpClient matches existing codebase pattern (HyperliquidClient)
- Provides better control over resilience policies and request formatting
- CoinGecko REST API is simple - no complex SDK needed

**Why DropWrite for Channel queue?**
- Alternative: DropOldest removes queued job if new one arrives
- Problem: In bounded(1) queue, DropOldest = DropWrite (only one slot)
- DropWrite is more explicit: "Job already running, reject new job"
- Caller can check TryEnqueue return value and respond appropriately

**Why auto-fill gaps individually?**
- CoinGecko may not return data for weekends, holidays, or delisted periods
- Bulk fetch returns what's available, may skip dates
- Individual fetches maximize completeness without failing entire job
- Failed gap fills logged as warnings - job still marked CompletedWithGaps

**Why BulkExtensions instead of EF Core batching?**
- EF Core 9 improved batching, but still slower than BulkExtensions for large datasets
- Composite key (Date, Symbol) requires ON CONFLICT handling - BulkExtensions handles this cleanly
- Research validated BulkExtensions works well with PostgreSQL
- Incremental mode: PropertiesToIncludeOnUpdate=[] prevents updates (insert-only)

## Success Criteria Met

- [x] CoinGeckoClient can fetch daily BTC data using chunked 90-day windows with rate limiting
- [x] DataIngestionService orchestrates full pipeline: fetch → bulk upsert → gap detect → auto-fill → status update
- [x] GapDetectionService uses PostgreSQL generate_series for accurate gap detection
- [x] IngestionJobQueue provides single-job enforcement via bounded Channel<T>
- [x] DataIngestionBackgroundService consumes and processes jobs as a long-running service
- [x] IngestionJob entity tracks job lifecycle with status, timestamps, progress, and error info
- [x] All code compiles and follows existing codebase patterns (primary constructors, structured logging, scoped services)

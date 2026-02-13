# Phase 7: Historical Data Pipeline - Research

**Researched:** 2026-02-13
**Domain:** External API integration, async job processing, rate limiting, data gap detection
**Confidence:** HIGH

## Summary

This phase builds a historical price data ingestion pipeline that fetches 2-4 years of BTC daily OHLC data from CoinGecko's free API and persists it to the existing DailyPrice PostgreSQL table. The system handles CoinGecko rate limits (30 calls/min free tier) via throttling, runs ingestion as an async background job with job ID tracking and status polling, detects and reports gaps in calendar day coverage, and exposes data status via API endpoints.

The domain is well-established: CoinGecko.Net provides a strongly-typed C# client for the CoinGecko API with built-in rate limiting support. .NET's System.Threading.Channels pattern is the modern standard for async job queues with backpressure. PostgreSQL's generate_series function excels at gap detection for time series data. Entity Framework Core's bulk insert extensions provide high-performance data ingestion (18x faster than SaveChanges).

**Primary recommendation:** Use CoinGecko.Net client library for API access, implement async job queue with Channel&lt;T&gt; and BackgroundService pattern, throttle requests to 25 calls/min (safe margin under 30 limit), use PostgreSQL generate_series LEFT JOIN for gap detection, expose job status via dedicated polling endpoint, block backtests on gaps for data integrity, persist job metadata in PostgreSQL for observability.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Ingestion behavior:**
- Default date range: 4 years back (covers a full BTC halving cycle)
- Ingestion runs asynchronously — POST /ingest returns job ID + estimated completion time, caller polls for status
- Built-in throttle for CoinGecko rate limits — automatically pace requests to stay under free tier limits, silent to caller
- BTC only — hardcode BTC/USD, no multi-coin abstraction
- Single job at a time — reject new ingest requests if one is already running

**Data quality & gaps:**
- Every calendar day is expected — BTC trades 24/7, any missing date is a gap
- Gap detection runs automatically after every ingestion completes
- Auto-fill attempt on gaps — automatically retry fetching missing dates, report any that still can't be filled
- Backtesting blocked if gaps exist in the requested date range — refuse to run with error, forces clean data

**API response design:**
- GET /data/status returns rich info: date range, total days stored, gap count, gap dates, last ingestion time, data source info, freshness indicator, coverage percentage
- POST /data/ingest returns job ID + estimated completion time
- Job status polling endpoint — Claude's discretion on whether dedicated endpoint or folded into /data/status
- Single concurrent job only — reject if already running

**Error & edge cases:**
- CoinGecko down: retry with exponential backoff up to max retries, then fail job with clear error
- Partial ingestion: keep successfully fetched data — next run picks up where it left off (incremental)
- Force re-ingestion supported via --force/overwrite flag to re-fetch and update existing dates
- Store OHLC (open, high, low, close) per day — richer than close-only for potential future analysis

### Claude's Discretion

- Job polling endpoint structure (dedicated vs folded into status)
- Exact throttle timing and batch sizes for CoinGecko
- Exponential backoff parameters (retry count, base delay)
- Estimated time calculation approach
- Internal data storage schema details

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CoinGecko.Net | 4.8.1+ | CoinGecko API client | Strongly-typed client, supports .NET 10, actively maintained by JKorf, 1K+ stars on GitHub |
| System.Threading.Channels | Built-in | Async job queue with backpressure | Native .NET pattern for producer-consumer, optimized for async/await, recommended by Microsoft |
| EFCore.BulkExtensions | 9.0.0+ | High-performance bulk inserts | 18x faster than SaveChanges, supports PostgreSQL, battle-tested (7K+ stars) |
| Microsoft.Extensions.Hosting | Built-in | Background job infrastructure | Already in use (TimeBackgroundService pattern), standard .NET hosting |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Polly | 8.0+ | Retry with exponential backoff | CoinGecko API failures, transient errors |
| Npgsql | 13.0+ | PostgreSQL gap detection queries | Already in use, generate_series support |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CoinGecko.Net | Direct HttpClient | CoinGecko.Net handles deserialization, rate limits, endpoint discovery — building from scratch is reinventing the wheel |
| System.Threading.Channels | Hangfire/Quartz | Channels are simpler for single async job, no DB polling overhead, matches existing BackgroundService pattern |
| EFCore.BulkExtensions | SaveChanges loop | BulkExtensions 18x faster for batch inserts, critical for 1460+ rows (4 years daily) |
| Job metadata in DB | In-memory state | DB persistence survives restarts, enables observability, allows future multi-instance scaling |

**Installation:**

```bash
# Add CoinGecko.Net client
dotnet add TradingBot.ApiService package CoinGecko.Net

# Add Polly for retry
dotnet add TradingBot.ApiService package Polly

# Add EFCore bulk extensions for PostgreSQL
dotnet add TradingBot.ApiService package EFCore.BulkExtensions
```

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── Application/
│   └── Services/
│       └── HistoricalData/
│           ├── CoinGeckoDataService.cs       # NEW: CoinGecko API wrapper
│           ├── DataIngestionService.cs       # NEW: Orchestrates ingestion + gap detection
│           ├── GapDetectionService.cs        # NEW: PostgreSQL gap detection
│           └── Models/
│               ├── IngestionJob.cs           # NEW: Job entity (DB)
│               ├── IngestionJobStatus.cs     # NEW: Enum (Pending/Running/Completed/Failed)
│               ├── DataStatusResponse.cs     # NEW: GET /data/status response DTO
│               └── IngestResponse.cs         # NEW: POST /data/ingest response DTO
├── Infrastructure/
│   └── CoinGecko/
│       ├── ServiceCollectionExtensions.cs    # NEW: DI registration
│       └── CoinGeckoOptions.cs               # NEW: Config (API key, base URL)
├── BuildingBlocks/
│   └── BackgroundJobs/
│       ├── JobQueue.cs                       # NEW: Channel<T> wrapper
│       └── DataIngestionBackgroundService.cs # NEW: BackgroundService consumer
├── Endpoints/
│   └── DataEndpoints.cs                      # NEW: Minimal API endpoints
```

### Pattern 1: Async Job Queue with System.Threading.Channels

**What:** Use bounded Channel&lt;T&gt; for async job queue with backpressure, BackgroundService as consumer, job metadata persisted in PostgreSQL.

**When to use:** Single long-running async job (data ingestion), need job status tracking, want to prevent multiple concurrent jobs.

**Example:**

```csharp
// JobQueue wrapper around Channel<T>
public class JobQueue<TJobRequest>
{
    private readonly Channel<TJobRequest> _channel;

    public JobQueue(int capacity = 1)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<TJobRequest>(options);
    }

    public async ValueTask<bool> TryEnqueueAsync(TJobRequest job, CancellationToken ct = default)
    {
        return await _channel.Writer.WaitToWriteAsync(ct)
            && _channel.Writer.TryWrite(job);
    }

    public IAsyncEnumerable<TJobRequest> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

// BackgroundService consumer
public class DataIngestionBackgroundService(
    JobQueue<IngestionJobRequest> jobQueue,
    IServiceProvider services,
    ILogger<DataIngestionBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobRequest in jobQueue.ReadAllAsync(stoppingToken))
        {
            await using var scope = services.CreateAsyncScope();
            var ingestionService = scope.ServiceProvider
                .GetRequiredService<DataIngestionService>();

            try
            {
                await ingestionService.RunIngestionAsync(jobRequest.JobId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ingestion job {JobId} failed", jobRequest.JobId);
            }
        }
    }
}

// Endpoint: POST /api/backtest/data/ingest
app.MapPost("/api/backtest/data/ingest", async (
    JobQueue<IngestionJobRequest> jobQueue,
    TradingBotDbContext db,
    bool force = false) =>
{
    // Check for running job
    var runningJob = await db.IngestionJobs
        .Where(j => j.Status == IngestionJobStatus.Running)
        .FirstOrDefaultAsync();

    if (runningJob != null)
    {
        return Results.Conflict(new { error = "Ingestion already running", jobId = runningJob.Id });
    }

    // Create job entity
    var job = new IngestionJob
    {
        Id = Guid.CreateVersion7(),
        Status = IngestionJobStatus.Pending,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-4)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Force = force,
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.IngestionJobs.Add(job);
    await db.SaveChangesAsync();

    // Enqueue job
    await jobQueue.TryEnqueueAsync(new IngestionJobRequest(job.Id));

    // Estimate completion time (30 calls/min, 4 years = ~1460 days, ~49 min)
    var estimatedMinutes = Math.Ceiling(1460 / 30.0);
    var estimatedCompletion = DateTimeOffset.UtcNow.AddMinutes(estimatedMinutes);

    return Results.Ok(new IngestResponse(job.Id, estimatedCompletion));
});
```

**Key points:**
- Bounded channel capacity = 1 (single job at a time)
- BoundedChannelFullMode.Wait applies backpressure (blocks POST if queue full)
- Job metadata persisted in DB before enqueuing (survives restarts)
- BackgroundService creates scope per job (scoped DbContext)

### Pattern 2: CoinGecko API Client with Rate Limiting

**What:** Use CoinGecko.Net library with manual throttling to stay under free tier limit (30 calls/min).

**When to use:** Fetching historical OHLC data from CoinGecko, need rate limit compliance.

**Example:**

```csharp
public class CoinGeckoDataService
{
    private readonly ICoinGeckoClient _client;
    private readonly ILogger<CoinGeckoDataService> _logger;
    private readonly SemaphoreSlim _rateLimiter;

    public CoinGeckoDataService(
        ICoinGeckoClient client,
        ILogger<CoinGeckoDataService> logger)
    {
        _client = client;
        _logger = logger;
        // Throttle to 25 calls/min (safe margin under 30 limit)
        _rateLimiter = new SemaphoreSlim(25, 25);
    }

    public async Task<List<DailyPrice>> FetchOhlcDataAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct)
    {
        var fromTimestamp = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            .ToUnixTimeSeconds();
        var toTimestamp = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            .ToUnixTimeSeconds();

        await _rateLimiter.WaitAsync(ct);

        try
        {
            // CoinGecko.Net OHLC endpoint
            var result = await _client.CoinsClient.GetCoinOhlcByIdAsync(
                id: "bitcoin",
                vsCurrency: "usd",
                days: "max", // Fetch all available within range
                cancellationToken: ct);

            if (!result.IsSuccess)
            {
                throw new Exception($"CoinGecko API error: {result.ErrorMessage}");
            }

            // Map to DailyPrice entities
            var dailyPrices = result.Data
                .Where(candle => candle.TimeStamp >= fromTimestamp && candle.TimeStamp <= toTimestamp)
                .Select(candle => new DailyPrice
                {
                    Date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(candle.TimeStamp).UtcDateTime),
                    Symbol = "BTC",
                    Open = candle.Open,
                    High = candle.High,
                    Low = candle.Low,
                    Close = candle.Close,
                    Volume = candle.Volume ?? 0m,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(candle.TimeStamp)
                })
                .ToList();

            _logger.LogInformation("Fetched {Count} OHLC candles from CoinGecko", dailyPrices.Count);

            return dailyPrices;
        }
        finally
        {
            // Release rate limit slot after 2.4 seconds (25 calls/60 sec)
            _ = Task.Delay(TimeSpan.FromSeconds(2.4), ct)
                .ContinueWith(_ => _rateLimiter.Release(), ct);
        }
    }
}
```

**Key points:**
- SemaphoreSlim(25, 25) limits concurrent calls to 25
- Task.Delay(2.4s) + Release spaces calls at 25/min (safe under 30 limit)
- CoinGecko.Net returns typed result with success/error handling
- Map CoinGecko response to existing DailyPrice entity

**Note:** CoinGecko free tier OHLC endpoint uses automatic granularity (can't specify "daily" without paid plan). For 4-year range (31+ days), granularity is 4 days per candle. To get true daily data on free tier, use `/coins/{id}/market_chart/range` endpoint with chunked requests (90-day windows).

**Alternative approach for true daily data:**

```csharp
// Chunk 4-year range into 90-day windows for daily granularity
var windows = ChunkDateRange(startDate, endDate, 90);

foreach (var window in windows)
{
    await _rateLimiter.WaitAsync(ct);

    var result = await _client.CoinsClient.GetCoinMarketChartRangeByIdAsync(
        id: "bitcoin",
        vsCurrency: "usd",
        from: window.Start.ToUnixTimeSeconds(),
        to: window.End.ToUnixTimeSeconds(),
        cancellationToken: ct);

    // Extract daily close prices from result
    // ...
}
```

### Pattern 3: PostgreSQL Gap Detection with generate_series

**What:** Use PostgreSQL's generate_series to create complete calendar day sequence, LEFT JOIN with DailyPrice to find missing dates.

**When to use:** Detecting gaps in time series data after ingestion.

**Example:**

```csharp
public class GapDetectionService
{
    private readonly TradingBotDbContext _db;
    private readonly ILogger<GapDetectionService> _logger;

    public GapDetectionService(
        TradingBotDbContext db,
        ILogger<GapDetectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<DateOnly>> DetectGapsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string symbol = "BTC",
        CancellationToken ct = default)
    {
        // Raw SQL: generate_series LEFT JOIN to find missing dates
        var gaps = await _db.Database
            .SqlQuery<DateOnly>($@"
                SELECT gs.date::date
                FROM generate_series(
                    {startDate}::date,
                    {endDate}::date,
                    '1 day'::interval
                ) AS gs(date)
                LEFT JOIN ""DailyPrices"" dp
                    ON gs.date::date = dp.""Date""
                    AND dp.""Symbol"" = {symbol}
                WHERE dp.""Date"" IS NULL
                ORDER BY gs.date
            ")
            .ToListAsync(ct);

        _logger.LogInformation("Detected {GapCount} missing dates between {Start} and {End}",
            gaps.Count, startDate, endDate);

        return gaps;
    }

    public async Task<DataCoverageStats> GetCoverageStatsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string symbol = "BTC",
        CancellationToken ct = default)
    {
        var totalDays = (endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).Days + 1;

        var storedDays = await _db.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= endDate)
            .CountAsync(ct);

        var gaps = await DetectGapsAsync(startDate, endDate, symbol, ct);

        return new DataCoverageStats
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalExpectedDays = totalDays,
            TotalStoredDays = storedDays,
            GapCount = gaps.Count,
            GapDates = gaps,
            CoveragePercent = (decimal)storedDays / totalDays * 100m
        };
    }
}
```

**Key points:**
- generate_series creates complete date sequence (all calendar days)
- LEFT JOIN finds dates NOT in DailyPrices table
- Works efficiently for large date ranges (4 years = 1460 days)
- Returns sorted list of missing DateOnly values

### Pattern 4: Bulk Insert with EFCore.BulkExtensions

**What:** Use BulkInsert extension for high-performance batch inserts, avoid SaveChanges loop.

**When to use:** Inserting 100+ rows (4 years = ~1460 daily prices), performance critical.

**Example:**

```csharp
public class DataIngestionService
{
    private readonly TradingBotDbContext _db;
    private readonly CoinGeckoDataService _coinGecko;
    private readonly GapDetectionService _gapDetection;
    private readonly ILogger<DataIngestionService> _logger;

    public async Task RunIngestionAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.IngestionJobs.FindAsync([jobId], ct);
        if (job == null) throw new InvalidOperationException($"Job {jobId} not found");

        job.Status = IngestionJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Fetch OHLC data from CoinGecko
            var prices = await _coinGecko.FetchOhlcDataAsync(
                job.StartDate,
                job.EndDate,
                ct);

            // Bulk insert with conflict handling
            await _db.BulkInsertOrUpdateAsync(prices, new BulkConfig
            {
                UpdateByProperties = new List<string> { nameof(DailyPrice.Date), nameof(DailyPrice.Symbol) },
                PropertiesToIncludeOnUpdate = job.Force
                    ? new List<string> { nameof(DailyPrice.Open), nameof(DailyPrice.High), nameof(DailyPrice.Low), nameof(DailyPrice.Close), nameof(DailyPrice.Volume) }
                    : new List<string>() // Skip update if not force mode
            }, cancellationToken: ct);

            _logger.LogInformation("Bulk inserted {Count} daily prices", prices.Count);

            // Detect gaps
            var gaps = await _gapDetection.DetectGapsAsync(job.StartDate, job.EndDate, "BTC", ct);

            if (gaps.Any())
            {
                _logger.LogWarning("Detected {GapCount} gaps after ingestion, attempting auto-fill", gaps.Count);

                // Auto-fill: fetch missing dates individually
                foreach (var gapDate in gaps)
                {
                    try
                    {
                        var gapData = await _coinGecko.FetchOhlcDataAsync(gapDate, gapDate, ct);
                        if (gapData.Any())
                        {
                            await _db.DailyPrices.AddAsync(gapData.First(), ct);
                            await _db.SaveChangesAsync(ct);
                            _logger.LogInformation("Filled gap for date {Date}", gapDate);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fill gap for date {Date}", gapDate);
                    }
                }

                // Re-check gaps after auto-fill
                gaps = await _gapDetection.DetectGapsAsync(job.StartDate, job.EndDate, "BTC", ct);
            }

            // Mark job complete
            job.Status = gaps.Any() ? IngestionJobStatus.CompletedWithGaps : IngestionJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.RecordsFetched = prices.Count;
            job.GapsDetected = gaps.Count;
            job.ErrorMessage = gaps.Any() ? $"{gaps.Count} gaps could not be filled" : null;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Ingestion job {JobId} completed: {Status}", jobId, job.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion job {JobId} failed", jobId);

            job.Status = IngestionJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = ex.Message;

            await _db.SaveChangesAsync(ct);
            throw;
        }
    }
}
```

**Key points:**
- BulkInsertOrUpdateAsync handles upsert logic (insert new, update existing if force=true)
- UpdateByProperties defines composite key (Date + Symbol)
- Auto-fill attempts individual fetches for missing dates
- Job status reflects outcome (Completed vs CompletedWithGaps vs Failed)

### Pattern 5: Retry with Exponential Backoff (Polly)

**What:** Use Polly for retry policy with exponential backoff when CoinGecko API fails.

**When to use:** Transient failures (429 rate limit, 503 service unavailable).

**Example:**

```csharp
// DI registration in ServiceCollectionExtensions
public static IServiceCollection AddCoinGecko(this IServiceCollection services, IConfiguration config)
{
    services.AddOptions<CoinGeckoOptions>()
        .Bind(config.GetSection("CoinGecko"));

    services.AddHttpClient<ICoinGeckoClient, CoinGeckoClient>()
        .AddResilienceHandler("coingecko-retry", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential, // 2s, 4s, 8s
                UseJitter = true // Add randomness to avoid thundering herd
            });
        });

    services.AddScoped<CoinGeckoDataService>();

    return services;
}
```

**Key points:**
- Exponential backoff: 2s → 4s → 8s delays
- Jitter prevents synchronized retries
- Max 3 attempts before failing
- Integrated with HttpClient via AddResilienceHandler (Polly v8 pattern)

### Pattern 6: Data Status Endpoint with Rich Response

**What:** GET /api/backtest/data/status returns comprehensive data availability info.

**When to use:** User needs to verify data completeness before running backtest.

**Example:**

```csharp
app.MapGet("/api/backtest/data/status", async (
    TradingBotDbContext db,
    GapDetectionService gapDetection) =>
{
    var symbol = "BTC";

    // Get date range from stored data
    var minDate = await db.DailyPrices
        .Where(p => p.Symbol == symbol)
        .MinAsync(p => (DateOnly?)p.Date);

    var maxDate = await db.DailyPrices
        .Where(p => p.Symbol == symbol)
        .MaxAsync(p => (DateOnly?)p.Date);

    if (minDate == null || maxDate == null)
    {
        return Results.Ok(new DataStatusResponse
        {
            Symbol = symbol,
            HasData = false,
            Message = "No data available. Run POST /api/backtest/data/ingest first."
        });
    }

    // Get coverage stats
    var stats = await gapDetection.GetCoverageStatsAsync(minDate.Value, maxDate.Value, symbol);

    // Get last ingestion job
    var lastJob = await db.IngestionJobs
        .OrderByDescending(j => j.CreatedAt)
        .FirstOrDefaultAsync();

    // Freshness: data is fresh if last date is within 2 days of now
    var daysSinceLastData = (DateOnly.FromDateTime(DateTime.UtcNow) - maxDate.Value).Days;
    var freshness = daysSinceLastData switch
    {
        <= 2 => "Fresh",
        <= 7 => "Recent",
        _ => "Stale"
    };

    return Results.Ok(new DataStatusResponse
    {
        Symbol = symbol,
        HasData = true,
        StartDate = minDate.Value,
        EndDate = maxDate.Value,
        TotalDaysStored = stats.TotalStoredDays,
        GapCount = stats.GapCount,
        GapDates = stats.GapDates.Take(20).ToList(), // First 20 gaps
        CoveragePercent = stats.CoveragePercent,
        Freshness = freshness,
        DaysSinceLastData = daysSinceLastData,
        LastIngestion = lastJob == null ? null : new IngestionJobSummary
        {
            JobId = lastJob.Id,
            Status = lastJob.Status.ToString(),
            CompletedAt = lastJob.CompletedAt,
            RecordsFetched = lastJob.RecordsFetched,
            GapsDetected = lastJob.GapsDetected
        },
        DataSource = "CoinGecko Free API",
        Message = stats.GapCount > 0
            ? $"{stats.GapCount} gaps detected. Run ingestion with force=true to retry, or gaps may prevent backtest."
            : "Data complete. Ready for backtesting."
    });
});
```

**Key points:**
- Rich response includes date range, coverage %, gaps, freshness, last job
- Freshness indicator (Fresh/Recent/Stale) based on days since last data
- Message guides user on next action (ingest, force re-ingest, or ready)
- Limits gap dates to first 20 (avoid huge responses)

### Pattern 7: Job Status Polling Endpoint

**What:** Dedicated GET /api/backtest/data/ingest/{jobId} for polling job progress.

**When to use:** User needs real-time status of running ingestion job.

**Example:**

```csharp
app.MapGet("/api/backtest/data/ingest/{jobId:guid}", async (
    Guid jobId,
    TradingBotDbContext db) =>
{
    var job = await db.IngestionJobs.FindAsync(jobId);

    if (job == null)
    {
        return Results.NotFound(new { error = "Job not found" });
    }

    return Results.Ok(new JobStatusResponse
    {
        JobId = job.Id,
        Status = job.Status.ToString(),
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        RecordsFetched = job.RecordsFetched,
        GapsDetected = job.GapsDetected,
        ErrorMessage = job.ErrorMessage,
        ProgressPercent = job.Status == IngestionJobStatus.Running
            ? CalculateProgress(job)
            : job.Status == IngestionJobStatus.Completed ? 100 : 0
    });
});

private static int CalculateProgress(IngestionJob job)
{
    // Estimate progress: assume 1 day fetched per second (rough heuristic)
    if (job.StartedAt == null) return 0;

    var elapsed = DateTimeOffset.UtcNow - job.StartedAt.Value;
    var totalDays = (job.EndDate.ToDateTime(TimeOnly.MinValue) - job.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
    var estimatedDaysPerSecond = 1; // Rough estimate

    var daysFetched = (int)(elapsed.TotalSeconds * estimatedDaysPerSecond);
    var progress = Math.Min((int)((double)daysFetched / totalDays * 100), 99); // Cap at 99 until actually done

    return progress;
}
```

**Key points:**
- Dedicated endpoint (not folded into /data/status) for cleaner separation
- Progress % estimate based on elapsed time (rough heuristic, not precise)
- Returns job metadata (status, timestamps, records, gaps, errors)
- Clients poll this endpoint every 5-10 seconds during ingestion

### Anti-Patterns to Avoid

- **SaveChanges in loop:** Never insert 1460 rows one-by-one. Use BulkInsert.
- **No rate limiting:** CoinGecko free tier enforces 30 calls/min. Exceeding triggers 429 errors and potential IP ban.
- **Blocking job queue:** Don't make POST /api/backtest/data/ingest wait for completion. Return job ID immediately, use background worker.
- **Missing gap detection:** Don't assume CoinGecko returns complete data. Always validate with generate_series.
- **In-memory job state only:** Persist job metadata in DB. In-memory state is lost on restart.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CoinGecko API client | Manual HttpClient + JSON deserialization | CoinGecko.Net library | Handles endpoint discovery, rate limits, typed responses, versioning |
| Async job queue | Custom queue with locks | System.Threading.Channels | Native .NET, backpressure built-in, async/await optimized |
| Bulk inserts | SaveChanges loop | EFCore.BulkExtensions | 18x faster, handles upsert logic, supports PostgreSQL |
| Gap detection | Manual date iteration | PostgreSQL generate_series | Database-native, efficient for large ranges, battle-tested |
| Retry logic | Manual try-catch with delays | Polly resilience library | Exponential backoff, jitter, circuit breaker, widely used |

**Key insight:** CoinGecko API integration + async job processing + time series gap detection are solved problems in .NET ecosystem. Use proven libraries and patterns. Don't reinvent HTTP clients, job queues, or retry logic.

## Common Pitfalls

### Pitfall 1: CoinGecko Free Tier Limitations

**What goes wrong:** CoinGecko free tier OHLC endpoint (/coins/{id}/ohlc) uses automatic granularity. For 4-year range (31+ days), it returns 4-day candles, not daily candles.

**Why it happens:** "interval=daily" parameter is paid-only. Free tier auto-adjusts granularity: 1-2 days = 30min, 3-30 days = 4hr, 31+ days = 4 days.

**How to avoid:** Use /coins/{id}/market_chart/range endpoint instead, chunk requests into 90-day windows (daily granularity for up to 90 days). Accept higher API call count (~16 chunks for 4 years).

**Warning signs:**
- Only ~365 rows for 4-year range (expected 1460)
- Dates are spaced 4 days apart
- Gap detection shows 75% missing dates

**Code pattern:**

```csharp
// CORRECT: Chunk into 90-day windows for daily granularity
public async Task<List<DailyPrice>> FetchDailyDataAsync(
    DateOnly startDate,
    DateOnly endDate,
    CancellationToken ct)
{
    var allPrices = new List<DailyPrice>();
    var chunks = ChunkDateRange(startDate, endDate, chunkSizeDays: 90);

    foreach (var chunk in chunks)
    {
        await _rateLimiter.WaitAsync(ct);

        var result = await _client.CoinsClient.GetCoinMarketChartRangeByIdAsync(
            id: "bitcoin",
            vsCurrency: "usd",
            from: chunk.Start.ToUnixTimeSeconds(),
            to: chunk.End.ToUnixTimeSeconds(),
            cancellationToken: ct);

        // Extract daily close prices, populate OHLC from closest data points
        // ...
    }

    return allPrices;
}

private static List<DateRange> ChunkDateRange(DateOnly start, DateOnly end, int chunkSizeDays)
{
    var chunks = new List<DateRange>();
    var current = start;

    while (current <= end)
    {
        var chunkEnd = current.AddDays(chunkSizeDays - 1);
        if (chunkEnd > end) chunkEnd = end;

        chunks.Add(new DateRange(current, chunkEnd));
        current = chunkEnd.AddDays(1);
    }

    return chunks;
}
```

### Pitfall 2: Rate Limit Exceeded (429 Errors)

**What goes wrong:** Too many concurrent requests to CoinGecko API trigger 429 rate limit errors, job fails.

**Why it happens:** Free tier limit is 30 calls/min. Without throttling, code makes rapid requests and exceeds limit.

**How to avoid:** Use SemaphoreSlim to limit concurrent calls, add delay between releases (2.4 sec for 25 calls/min safe margin).

**Warning signs:**
- HTTP 429 status in logs
- "Too many requests" error messages
- Job fails partway through ingestion

**Code pattern:**

```csharp
// CORRECT: Throttle with semaphore + delay
private readonly SemaphoreSlim _rateLimiter = new(25, 25);

await _rateLimiter.WaitAsync(ct);

try
{
    var result = await _client.CoinsClient.GetCoinOhlcByIdAsync(...);
    // ...
}
finally
{
    // Release after 2.4 seconds (25 calls/min)
    _ = Task.Delay(TimeSpan.FromSeconds(2.4), ct)
        .ContinueWith(_ => _rateLimiter.Release(), ct);
}

// WRONG: No rate limiting
for (var chunk in chunks)
{
    var result = await _client.CoinsClient.GetCoinOhlcByIdAsync(...); // Rapid-fire requests
}
```

### Pitfall 3: Concurrent Job Execution

**What goes wrong:** Multiple ingestion jobs run simultaneously, causing database conflicts, rate limit issues, and data corruption.

**Why it happens:** POST /api/backtest/data/ingest doesn't check for running jobs, or check has race condition.

**How to avoid:** Check for running job in transaction before creating new job. Use bounded channel with capacity=1 to enforce single job.

**Warning signs:**
- Multiple "Running" jobs in database
- Bulk insert conflicts (duplicate key violations)
- Inconsistent data (partial overwrites)

**Code pattern:**

```csharp
// CORRECT: Transaction + bounded channel
using var transaction = await db.Database.BeginTransactionAsync(ct);

var runningJob = await db.IngestionJobs
    .Where(j => j.Status == IngestionJobStatus.Running)
    .FirstOrDefaultAsync(ct);

if (runningJob != null)
{
    return Results.Conflict(new { error = "Ingestion already running", jobId = runningJob.Id });
}

var newJob = new IngestionJob { /* ... */ };
db.IngestionJobs.Add(newJob);
await db.SaveChangesAsync(ct);

await transaction.CommitAsync(ct);

// Bounded channel with capacity=1 enforces single job
var enqueued = await jobQueue.TryEnqueueAsync(new IngestionJobRequest(newJob.Id), ct);
if (!enqueued)
{
    // Queue full (shouldn't happen due to DB check, but defensive)
    return Results.Conflict(new { error = "Job queue full" });
}

// WRONG: No concurrency check
var job = new IngestionJob { /* ... */ };
db.IngestionJobs.Add(job);
await db.SaveChangesAsync(ct);
await jobQueue.TryEnqueueAsync(new IngestionJobRequest(job.Id), ct); // Race condition
```

### Pitfall 4: Gap Detection False Positives

**What goes wrong:** Gap detection reports missing dates that are actually present, or misses actual gaps.

**Why it happens:** Wrong date range in generate_series, wrong composite key (Symbol not included), timezone issues (Date vs DateTimeOffset).

**How to avoid:** Use DateOnly for consistency, include Symbol in WHERE clause, test with known gaps.

**Warning signs:**
- Gap count doesn't match manual DB query
- Gaps reported for dates that exist in DailyPrices
- Weekend dates flagged as gaps (not applicable for BTC 24/7 trading)

**Code pattern:**

```csharp
// CORRECT: DateOnly, Symbol filter, inclusive range
var gaps = await _db.Database
    .SqlQuery<DateOnly>($@"
        SELECT gs.date::date
        FROM generate_series(
            {startDate}::date,
            {endDate}::date,
            '1 day'::interval
        ) AS gs(date)
        LEFT JOIN ""DailyPrices"" dp
            ON gs.date::date = dp.""Date""
            AND dp.""Symbol"" = {symbol}
        WHERE dp.""Date"" IS NULL
        ORDER BY gs.date
    ")
    .ToListAsync(ct);

// WRONG: Missing Symbol filter, wrong date type
var gaps = await _db.Database
    .SqlQuery<DateTime>($@"
        SELECT gs.date
        FROM generate_series({startDate}, {endDate}, '1 day') AS gs(date)
        LEFT JOIN ""DailyPrices"" dp ON gs.date = dp.""Timestamp""
        WHERE dp.""Timestamp"" IS NULL
    ")
    .ToListAsync(ct); // Doesn't filter by Symbol, uses Timestamp instead of Date
```

### Pitfall 5: Partial Ingestion Without Resume

**What goes wrong:** Ingestion job fails partway through (e.g., CoinGecko API down), next run starts from scratch instead of resuming.

**Why it happens:** Job doesn't track progress, always starts from job.StartDate.

**How to avoid:** Check for existing data in range before fetching, only fetch missing chunks (incremental).

**Warning signs:**
- Re-fetching same data on retry
- Slow ingestion after first run
- BulkInsert conflicts on retry

**Code pattern:**

```csharp
// CORRECT: Incremental fetch (only missing chunks)
public async Task<List<DailyPrice>> FetchIncrementalAsync(
    DateOnly startDate,
    DateOnly endDate,
    CancellationToken ct)
{
    // Detect gaps in requested range
    var gaps = await _gapDetection.DetectGapsAsync(startDate, endDate, "BTC", ct);

    if (!gaps.Any())
    {
        _logger.LogInformation("No gaps detected, skipping fetch");
        return new List<DailyPrice>();
    }

    // Group consecutive gaps into chunks to minimize API calls
    var chunks = GroupIntoConsecutiveRanges(gaps);

    var allPrices = new List<DailyPrice>();

    foreach (var chunk in chunks)
    {
        var prices = await FetchOhlcDataAsync(chunk.Start, chunk.End, ct);
        allPrices.AddRange(prices);
    }

    return allPrices;
}

// WRONG: Always fetch full range
public async Task<List<DailyPrice>> FetchFullAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct)
{
    return await FetchOhlcDataAsync(startDate, endDate, ct); // Re-fetches existing data
}
```

## Code Examples

Verified patterns from official sources and codebase:

### Complete Ingestion Flow

```csharp
// Full ingestion orchestration with error handling
public async Task RunIngestionAsync(Guid jobId, CancellationToken ct)
{
    var job = await _db.IngestionJobs.FindAsync([jobId], ct);
    if (job == null) throw new InvalidOperationException($"Job {jobId} not found");

    job.Status = IngestionJobStatus.Running;
    job.StartedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    try
    {
        // Step 1: Fetch OHLC data from CoinGecko (chunked for daily granularity)
        var prices = await _coinGecko.FetchOhlcDataAsync(
            job.StartDate,
            job.EndDate,
            ct);

        _logger.LogInformation("Fetched {Count} OHLC candles for job {JobId}", prices.Count, jobId);

        // Step 2: Bulk insert or update (upsert)
        await _db.BulkInsertOrUpdateAsync(prices, new BulkConfig
        {
            UpdateByProperties = new List<string> { nameof(DailyPrice.Date), nameof(DailyPrice.Symbol) },
            PropertiesToIncludeOnUpdate = job.Force
                ? new List<string> { nameof(DailyPrice.Open), nameof(DailyPrice.High), nameof(DailyPrice.Low), nameof(DailyPrice.Close), nameof(DailyPrice.Volume) }
                : new List<string>() // Skip update if not force
        }, cancellationToken: ct);

        _logger.LogInformation("Bulk inserted {Count} daily prices for job {JobId}", prices.Count, jobId);

        // Step 3: Detect gaps
        var gaps = await _gapDetection.DetectGapsAsync(job.StartDate, job.EndDate, "BTC", ct);

        if (gaps.Any())
        {
            _logger.LogWarning("Job {JobId}: Detected {GapCount} gaps, attempting auto-fill", jobId, gaps.Count);

            // Step 4: Auto-fill gaps (individual fetches)
            var filledCount = 0;

            foreach (var gapDate in gaps)
            {
                try
                {
                    var gapData = await _coinGecko.FetchOhlcDataAsync(gapDate, gapDate, ct);

                    if (gapData.Any())
                    {
                        await _db.DailyPrices.AddAsync(gapData.First(), ct);
                        await _db.SaveChangesAsync(ct);
                        filledCount++;
                        _logger.LogDebug("Job {JobId}: Filled gap for date {Date}", jobId, gapDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Failed to fill gap for date {Date}", jobId, gapDate);
                }
            }

            _logger.LogInformation("Job {JobId}: Auto-filled {FilledCount} of {GapCount} gaps", jobId, filledCount, gaps.Count);

            // Re-check gaps after auto-fill
            gaps = await _gapDetection.DetectGapsAsync(job.StartDate, job.EndDate, "BTC", ct);
        }

        // Step 5: Mark job complete
        job.Status = gaps.Any() ? IngestionJobStatus.CompletedWithGaps : IngestionJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.RecordsFetched = prices.Count;
        job.GapsDetected = gaps.Count;
        job.ErrorMessage = gaps.Any() ? $"{gaps.Count} gaps could not be filled: {string.Join(", ", gaps.Take(10))}" : null;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Job {JobId} completed: {Status}, {RecordsFetched} records, {GapsDetected} gaps",
            jobId, job.Status, job.RecordsFetched, job.GapsDetected);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Job {JobId} failed with error: {Error}", jobId, ex.Message);

        job.Status = IngestionJobStatus.Failed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.ErrorMessage = ex.Message;

        await _db.SaveChangesAsync(ct);
        throw;
    }
}
```

### Endpoint: GET /api/backtest/data/status

```csharp
// Data status endpoint with comprehensive response
app.MapGet("/api/backtest/data/status", async (
    TradingBotDbContext db,
    GapDetectionService gapDetection) =>
{
    var symbol = "BTC";

    // Get date range from stored data
    var minDate = await db.DailyPrices
        .Where(p => p.Symbol == symbol)
        .MinAsync(p => (DateOnly?)p.Date);

    var maxDate = await db.DailyPrices
        .Where(p => p.Symbol == symbol)
        .MaxAsync(p => (DateOnly?)p.Date);

    if (minDate == null || maxDate == null)
    {
        return Results.Ok(new DataStatusResponse
        {
            Symbol = symbol,
            HasData = false,
            Message = "No data available. Run POST /api/backtest/data/ingest to fetch historical data."
        });
    }

    // Get coverage stats
    var stats = await gapDetection.GetCoverageStatsAsync(minDate.Value, maxDate.Value, symbol);

    // Get last ingestion job
    var lastJob = await db.IngestionJobs
        .OrderByDescending(j => j.CreatedAt)
        .FirstOrDefaultAsync();

    // Freshness: data is fresh if last date is within 2 days of now
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var daysSinceLastData = (today - maxDate.Value).Days;
    var freshness = daysSinceLastData switch
    {
        <= 2 => "Fresh",
        <= 7 => "Recent",
        _ => "Stale"
    };

    return Results.Ok(new DataStatusResponse
    {
        Symbol = symbol,
        HasData = true,
        StartDate = minDate.Value,
        EndDate = maxDate.Value,
        TotalDaysStored = stats.TotalStoredDays,
        GapCount = stats.GapCount,
        GapDates = stats.GapDates.Take(20).ToList(), // Limit to first 20 gaps
        CoveragePercent = stats.CoveragePercent,
        Freshness = freshness,
        DaysSinceLastData = daysSinceLastData,
        LastIngestion = lastJob == null ? null : new IngestionJobSummary
        {
            JobId = lastJob.Id,
            Status = lastJob.Status.ToString(),
            CompletedAt = lastJob.CompletedAt,
            RecordsFetched = lastJob.RecordsFetched,
            GapsDetected = lastJob.GapsDetected,
            ErrorMessage = lastJob.ErrorMessage
        },
        DataSource = "CoinGecko Free API",
        Message = stats.GapCount > 0
            ? $"Warning: {stats.GapCount} gaps detected. Run ingestion with force=true to retry missing dates."
            : "Data is complete and ready for backtesting."
    });
})
.WithName("GetDataStatus")
.WithOpenApi();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual HttpClient JSON parsing | CoinGecko.Net typed client | 2020+ | Type safety, endpoint discovery, rate limit handling built-in |
| BlockingCollection job queue | System.Threading.Channels | .NET Core 3.0 (2019) | Async/await optimized, backpressure, simpler API |
| SaveChanges loop | EFCore.BulkExtensions | EF Core 2.0+ (2017) | 18x performance boost, upsert logic |
| Hangfire/Quartz for simple jobs | BackgroundService + Channel | .NET Core 2.1 (2018) | Simpler for single job, no external DB polling |
| Polly v7 imperative API | Polly v8 resilience pipelines | 2023 | Declarative, composable, better telemetry |

**Deprecated/outdated:**
- Using Hangfire for simple async jobs (overkill, adds DB polling overhead)
- SaveChanges in loop for bulk data (18x slower than BulkExtensions)
- Manual retry logic with Thread.Sleep (Polly is superior)

## Open Questions

1. **CoinGecko OHLC vs Market Chart Endpoint**
   - What we know: OHLC endpoint returns 4-day granularity for 4-year range on free tier. Market chart endpoint returns daily for up to 90-day windows.
   - What's unclear: Which endpoint provides more reliable OHLC data (Open/High/Low/Close)? Market chart returns prices array, not OHLC.
   - Recommendation: Use market chart endpoint chunked into 90-day windows. Extract daily close prices. For OHLC, use close price for all four values (Open=High=Low=Close) as approximation, or fetch intraday data and compute true OHLC. Document limitation: "Free tier uses close price for OHLC approximation; paid tier provides true OHLC."

2. **Job Polling Endpoint: Dedicated vs Folded into /data/status**
   - What we know: User needs to poll job status after POST /ingest. Two options: dedicated GET /ingest/{jobId} or query param on GET /data/status?jobId={id}.
   - What's unclear: User preference for API design.
   - Recommendation: Dedicated endpoint GET /api/backtest/data/ingest/{jobId}. Cleaner separation of concerns (status vs job tracking), easier to document, follows RESTful resource pattern.

3. **Estimated Completion Time Accuracy**
   - What we know: POST /ingest returns estimated completion time. With 25 calls/min throttle and ~16 chunks (90-day windows for 4 years), estimate is ~40 minutes.
   - What's unclear: Should estimate account for gap auto-fill time (unpredictable), or only initial fetch?
   - Recommendation: Estimate initial fetch only (40 min for 4 years). Document: "Estimated time for initial fetch; gap auto-fill may add 5-10 minutes." Progress % in job status endpoint can exceed 100% if gap fill takes longer.

4. **Force Re-Ingestion: Full Update vs Selective**
   - What we know: force=true flag re-fetches and updates existing dates. BulkConfig PropertiesToIncludeOnUpdate controls which fields update.
   - What's unclear: Should force update all fields (Open/High/Low/Close/Volume), or only missing fields?
   - Recommendation: Update all OHLC fields on force. Rationale: "force" implies "re-fetch from source of truth," not "patch missing data." User explicitly requested re-ingestion, so trust CoinGecko as authoritative.

5. **Gap Auto-Fill: Batch vs Individual**
   - What we know: Gaps can be filled individually (1 API call per gap date) or batched (group consecutive gaps into ranges).
   - What's unclear: Trade-off between API call efficiency vs code complexity.
   - Recommendation: Start with individual fetches (simple, works for scattered gaps). Optimize to batch consecutive gaps in future if needed. Gaps should be rare after initial ingestion, so individual approach is acceptable.

## Sources

### Primary (HIGH confidence)

- [CoinGecko.Net GitHub Repository](https://github.com/JKorf/CoinGecko.Net) - Official library documentation and examples
- [CoinGecko API Documentation - Rate Limits](https://docs.coingecko.com/docs/common-errors-rate-limit) - Free tier 30 calls/min limit
- [CoinGecko API - OHLC Endpoint](https://docs.coingecko.com/reference/coins-id-ohlc) - OHLC endpoint parameters and granularity
- [Microsoft Learn - Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) - Official System.Threading.Channels documentation
- [Microsoft Learn - Background Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0) - IHostedService and BackgroundService patterns
- [PostgreSQL generate_series Documentation](https://www.postgresql.org/docs/current/functions-srf.html) - Time series generation for gap detection
- [Detecting Gaps in Time-Series Data in PostgreSQL](https://www.endpointdev.com/blog/2020/10/postgresql-finding-gaps-in-time-series-data/) - Gap detection patterns
- [EFCore.BulkExtensions GitHub](https://github.com/borisdj/EFCore.BulkExtensions) - Bulk insert performance and API reference
- [Polly v8 Retry Strategy](https://www.pollydocs.org/strategies/retry.html) - Exponential backoff configuration
- Codebase: `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/BuildingBlocks/TimeBackgroundService.cs` - Existing BackgroundService pattern
- Codebase: `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Models/DailyPrice.cs` - Existing DailyPrice entity

### Secondary (MEDIUM confidence)

- [CoinGecko Historical Data Guide](https://www.coingecko.com/learn/download-bitcoin-historical-data) - Best practices for historical data ingestion
- [How to Fill Missing Dates in PostgreSQL](https://ubiq.co/database-blog/fill-missing-dates-using-postgresql-generate_series/) - Generate series patterns
- [Fast SQL Bulk Inserts With C# and EF Core](https://www.milanjovanovic.tech/blog/fast-sql-bulk-inserts-with-csharp-and-ef-core) - Performance comparison
- [Building High-Performance .NET Apps With C# Channels](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels) - Channel patterns and examples
- [Job Offloading Pattern with Channels](https://nikiforovall.blog/dotnet/async/2024/04/21/job-offloading-pattern.html) - Background job processing patterns

### Tertiary (LOW confidence)

- [ConcurrentQueue vs Channels Performance Comparison](https://medium.com/@mahmednisar/concurrentqueue-vs-channels-in-net-2025-the-performance-battle-you-need-to-see-e9949ec106e2) - Benchmarks for job queues

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - CoinGecko.Net is proven library, Channels are native .NET, EFCore.BulkExtensions widely used
- Architecture: HIGH - BackgroundService pattern established in codebase, bulk insert patterns verified, gap detection proven with generate_series
- Pitfalls: MEDIUM - CoinGecko free tier limitations documented, but chunking strategy needs validation with actual API responses; rate limit throttling is heuristic (25 calls/min safe margin)

**Research date:** 2026-02-13
**Valid until:** 2026-03-13 (30 days - CoinGecko API stable, .NET 10 LTS, patterns are established)

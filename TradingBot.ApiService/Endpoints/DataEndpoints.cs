using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services.HistoricalData;
using TradingBot.ApiService.Application.Services.HistoricalData.Models;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Endpoints;

public static class DataEndpoints
{
    public static WebApplication MapDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backtest/data");
        group.MapPost("/ingest", IngestAsync);
        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/ingest/{jobId:guid}", GetJobStatusAsync);
        return app;
    }

    private static async Task<IResult> IngestAsync(
        [FromQuery] bool force,
        TradingBotDbContext db,
        IngestionJobQueue jobQueue,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Check for running or pending job
        var existingJob = await db.IngestionJobs
            .Where(j => j.Status == IngestionJobStatus.Running || j.Status == IngestionJobStatus.Pending)
            .FirstOrDefaultAsync(ct);

        if (existingJob != null)
        {
            logger.LogWarning("Ingestion job already running or pending: {JobId}", existingJob.Id);
            return Results.Conflict(new { error = "Ingestion already running", jobId = existingJob.Id });
        }

        // Create new ingestion job for last 4 years
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-4));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var job = new Models.IngestionJob
        {
            Id = IngestionJobId.New(),
            StartDate = startDate,
            EndDate = endDate,
            Force = force,
            Status = IngestionJobStatus.Pending
        };

        db.IngestionJobs.Add(job);
        await db.SaveChangesAsync(ct);

        // Try to enqueue job
        if (!jobQueue.TryEnqueue(job.Id))
        {
            // Queue full (defensive - shouldn't happen given DB check)
            job.Status = IngestionJobStatus.Failed;
            job.ErrorMessage = "Failed to enqueue job - queue full";
            await db.SaveChangesAsync(ct);

            logger.LogError("Failed to enqueue ingestion job {JobId} - queue full", job.Id);
            return Results.Conflict(new { error = "Failed to enqueue job - queue full", jobId = job.Id });
        }

        // Estimate completion time
        // 4 years ≈ 1460 days, 90-day chunks ≈ 17 API calls, 2.5s per call ≈ 43 seconds
        // Add overhead for processing → estimate ~2 minutes
        var estimatedCompletion = DateTimeOffset.UtcNow.AddMinutes(2);

        logger.LogInformation("Ingestion job created: {JobId}, StartDate: {StartDate}, EndDate: {EndDate}, Force: {Force}",
            job.Id, startDate, endDate, force);

        var response = new IngestResponse(
            job.Id,
            estimatedCompletion,
            $"Ingestion job created. Poll GET /api/backtest/data/ingest/{job.Id} for status.");

        return Results.Accepted($"/api/backtest/data/ingest/{job.Id}", response);
    }

    private static async Task<IResult> GetStatusAsync(
        TradingBotDbContext db,
        GapDetectionService gapDetection,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        const string symbol = "BTC";

        // Query min/max date from DailyPrices
        var priceData = await db.DailyPrices
            .Where(p => p.Symbol == symbol)
            .GroupBy(p => p.Symbol)
            .Select(g => new
            {
                MinDate = g.Min(p => p.Date),
                MaxDate = g.Max(p => p.Date),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        if (priceData == null || priceData.Count == 0)
        {
            logger.LogInformation("No historical data found for {Symbol}", symbol);
            return Results.Ok(new DataStatusResponse
            {
                Symbol = symbol,
                HasData = false,
                Message = "No data available. Run POST /api/backtest/data/ingest to fetch historical data."
            });
        }

        // Get coverage stats
        var coverageStats = await gapDetection.GetCoverageStatsAsync(
            priceData.MinDate,
            priceData.MaxDate,
            symbol,
            ct);

        // Calculate freshness
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSinceLastData = today.DayNumber - priceData.MaxDate.DayNumber;
        var freshness = daysSinceLastData switch
        {
            <= 2 => "Fresh",
            <= 7 => "Recent",
            _ => "Stale"
        };

        // Get last ingestion job
        var lastJob = await db.IngestionJobs
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        IngestionJobSummary? lastIngestion = null;
        if (lastJob != null)
        {
            lastIngestion = new IngestionJobSummary
            {
                JobId = lastJob.Id,
                Status = lastJob.Status.ToString(),
                CompletedAt = lastJob.CompletedAt,
                RecordsFetched = lastJob.RecordsFetched,
                GapsDetected = lastJob.GapsDetected,
                ErrorMessage = lastJob.ErrorMessage
            };
        }

        var message = coverageStats.GapCount > 0
            ? $"Data has {coverageStats.GapCount} gap(s). Consider running POST /api/backtest/data/ingest?force=true to fill gaps."
            : "Data is complete and ready for backtesting.";

        var response = new DataStatusResponse
        {
            Symbol = symbol,
            HasData = true,
            StartDate = priceData.MinDate,
            EndDate = priceData.MaxDate,
            TotalDaysStored = coverageStats.TotalStoredDays,
            GapCount = coverageStats.GapCount,
            GapDates = coverageStats.GapDates.Take(20).ToList(),
            CoveragePercent = coverageStats.CoveragePercent,
            Freshness = freshness,
            DaysSinceLastData = daysSinceLastData,
            LastIngestion = lastIngestion,
            Message = message
        };

        logger.LogInformation("Data status for {Symbol}: {TotalDays} days, {Coverage}% coverage, {Freshness}",
            symbol, coverageStats.TotalStoredDays, coverageStats.CoveragePercent, freshness);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetJobStatusAsync(
        IngestionJobId jobId,
        TradingBotDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var job = await db.IngestionJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job == null)
        {
            logger.LogWarning("Ingestion job not found: {JobId}", jobId);
            return Results.NotFound(new { error = "Job not found" });
        }

        // Calculate progress percentage
        var progressPercent = job.Status switch
        {
            IngestionJobStatus.Failed => 0,
            IngestionJobStatus.Completed => 100,
            IngestionJobStatus.CompletedWithGaps => 100,
            IngestionJobStatus.Pending => 0,
            IngestionJobStatus.Running => CalculateRunningProgress(job),
            _ => 0
        };

        var response = new JobStatusResponse
        {
            JobId = job.Id,
            Status = job.Status.ToString(),
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            StartDate = job.StartDate,
            EndDate = job.EndDate,
            Force = job.Force,
            RecordsFetched = job.RecordsFetched,
            GapsDetected = job.GapsDetected,
            ErrorMessage = job.ErrorMessage,
            ProgressPercent = progressPercent
        };

        logger.LogInformation("Job status for {JobId}: {Status}, Progress: {Progress}%",
            jobId, job.Status, progressPercent);

        return Results.Ok(response);
    }

    private static int CalculateRunningProgress(Models.IngestionJob job)
    {
        if (job.StartedAt == null)
        {
            return 0;
        }

        var totalDays = (job.EndDate.DayNumber - job.StartDate.DayNumber) + 1;
        var totalChunks = Math.Ceiling(totalDays / 90.0);
        var estimatedTotalSeconds = totalChunks * 2.5;  // 2.5s per API call

        var elapsed = (DateTimeOffset.UtcNow - job.StartedAt.Value).TotalSeconds;
        var progress = (int)(elapsed / estimatedTotalSeconds * 100);

        // Cap at 99 until actually done
        return Math.Min(progress, 99);
    }
}

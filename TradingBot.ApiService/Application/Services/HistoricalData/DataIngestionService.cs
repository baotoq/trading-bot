using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services.HistoricalData.Models;
using TradingBot.ApiService.Infrastructure.CoinGecko;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Services.HistoricalData;

/// <summary>
/// Orchestrates historical data ingestion: fetch from CoinGecko, bulk upsert, gap detection, and auto-fill.
/// </summary>
public class DataIngestionService(
    TradingBotDbContext db,
    CoinGeckoClient coinGecko,
    GapDetectionService gapDetection,
    ILogger<DataIngestionService> logger)
{
    /// <summary>
    /// Runs a complete ingestion job: fetch data, bulk insert, detect gaps, auto-fill, update job status.
    /// </summary>
    public async Task RunIngestionAsync(IngestionJobId jobId, CancellationToken ct = default)
    {
        var job = await db.IngestionJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
        {
            throw new InvalidOperationException($"Ingestion job {jobId} not found");
        }

        // Mark job as running
        job.Status = IngestionJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            logger.LogInformation(
                "Starting ingestion job {JobId}: {StartDate} to {EndDate}, Force={Force}",
                jobId, job.StartDate, job.EndDate, job.Force);

            List<DateOnly> initialGaps;

            // Check for existing data if not in force mode
            if (!job.Force)
            {
                initialGaps = await gapDetection.DetectGapsAsync(
                    job.StartDate,
                    job.EndDate,
                    "BTC",
                    ct);

                if (initialGaps.Count == 0)
                {
                    logger.LogInformation(
                        "Job {JobId}: No gaps detected, data already complete",
                        jobId);

                    job.Status = IngestionJobStatus.Completed;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    job.RecordsFetched = 0;
                    job.GapsDetected = 0;
                    await db.SaveChangesAsync(ct);
                    return;
                }

                logger.LogInformation(
                    "Job {JobId}: Detected {GapCount} initial gaps, fetching data",
                    jobId, initialGaps.Count);
            }

            // Fetch data from CoinGecko
            var prices = await coinGecko.FetchDailyDataAsync(job.StartDate, job.EndDate, ct);

            if (prices.Count == 0)
            {
                logger.LogWarning("Job {JobId}: No data returned from CoinGecko", jobId);
                job.Status = IngestionJobStatus.CompletedWithGaps;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.RecordsFetched = 0;
                job.GapsDetected = (job.EndDate.DayNumber - job.StartDate.DayNumber) + 1;
                job.ErrorMessage = "No data returned from CoinGecko API";
                await db.SaveChangesAsync(ct);
                return;
            }

            // Bulk upsert prices
            var bulkConfig = new BulkConfig
            {
                UpdateByProperties = new List<string>
                {
                    nameof(DailyPrice.Date),
                    nameof(DailyPrice.Symbol)
                }
            };

            if (job.Force)
            {
                // Force mode: update existing rows
                bulkConfig.PropertiesToIncludeOnUpdate = new List<string>
                {
                    nameof(DailyPrice.Open),
                    nameof(DailyPrice.High),
                    nameof(DailyPrice.Low),
                    nameof(DailyPrice.Close),
                    nameof(DailyPrice.Volume),
                    nameof(DailyPrice.Timestamp)
                };
            }
            else
            {
                // Incremental mode: only insert new rows, skip updates
                bulkConfig.PropertiesToIncludeOnUpdate = new List<string>();
            }

            await db.BulkInsertOrUpdateAsync(prices, bulkConfig, cancellationToken: ct);

            logger.LogInformation(
                "Job {JobId}: Bulk upserted {Count} daily prices",
                jobId, prices.Count);

            job.RecordsFetched = prices.Count;

            // Detect gaps after initial insert
            var remainingGaps = await gapDetection.DetectGapsAsync(
                job.StartDate,
                job.EndDate,
                "BTC",
                ct);

            logger.LogInformation(
                "Job {JobId}: {GapCount} gaps remain after bulk insert",
                jobId, remainingGaps.Count);

            // Auto-fill gaps (try to fetch individual missing dates)
            if (remainingGaps.Count > 0)
            {
                var filledCount = 0;

                logger.LogInformation(
                    "Job {JobId}: Attempting to auto-fill {GapCount} gaps",
                    jobId, remainingGaps.Count);

                foreach (var gapDate in remainingGaps)
                {
                    try
                    {
                        var gapPrices = await coinGecko.FetchDailyDataAsync(gapDate, gapDate, ct);

                        if (gapPrices.Count > 0)
                        {
                            var gapPrice = gapPrices[0];

                            // Check if already exists (race condition or duplicate from chunked fetch)
                            var exists = await db.DailyPrices.FindAsync(
                                [gapDate, "BTC"],
                                ct);

                            if (exists == null)
                            {
                                await db.DailyPrices.AddAsync(gapPrice, ct);
                                await db.SaveChangesAsync(ct);
                                filledCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Job {JobId}: Failed to fill gap for date {Date}",
                            jobId, gapDate);
                    }
                }

                logger.LogInformation(
                    "Job {JobId}: Auto-filled {FilledCount}/{TotalGaps} gaps",
                    jobId, filledCount, remainingGaps.Count);

                // Re-detect gaps after auto-fill
                remainingGaps = await gapDetection.DetectGapsAsync(
                    job.StartDate,
                    job.EndDate,
                    "BTC",
                    ct);
            }

            // Set final job status
            if (remainingGaps.Count > 0)
            {
                job.Status = IngestionJobStatus.CompletedWithGaps;
                job.GapsDetected = remainingGaps.Count;
                job.ErrorMessage = $"Data incomplete: {remainingGaps.Count} dates missing after auto-fill attempts";

                logger.LogWarning(
                    "Job {JobId} completed with gaps: {GapCount} missing dates",
                    jobId, remainingGaps.Count);
            }
            else
            {
                job.Status = IngestionJobStatus.Completed;
                job.GapsDetected = 0;

                logger.LogInformation(
                    "Job {JobId} completed successfully: {RecordCount} records, no gaps",
                    jobId, job.RecordsFetched);
            }

            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Job {JobId} failed: {ErrorMessage}",
                jobId, ex.Message);

            job.Status = IngestionJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;

            await db.SaveChangesAsync(ct);

            // Do not rethrow - background service should continue processing other jobs
        }
    }
}

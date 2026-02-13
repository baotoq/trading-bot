namespace TradingBot.ApiService.Application.Services.HistoricalData;

/// <summary>
/// Background service that consumes ingestion jobs from the queue and processes them.
/// Runs as a long-running service, processing one job at a time.
/// </summary>
public class DataIngestionBackgroundService(
    IngestionJobQueue jobQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<DataIngestionBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DataIngestionBackgroundService started, waiting for jobs");

        try
        {
            await foreach (var jobId in jobQueue.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation("Processing ingestion job {JobId}", jobId);

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var ingestionService = scope.ServiceProvider.GetRequiredService<DataIngestionService>();

                    await ingestionService.RunIngestionAsync(jobId, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Unhandled exception processing job {JobId}. Service will continue.",
                        jobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            logger.LogInformation("DataIngestionBackgroundService stopping due to cancellation");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "DataIngestionBackgroundService failed with unhandled exception");
            throw;
        }

        logger.LogInformation("DataIngestionBackgroundService stopped");
    }
}

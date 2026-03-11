using System.Diagnostics;

namespace TradingBot.ApiService.BuildingBlocks;

[DebuggerStepThrough]
public abstract class TimeBackgroundService(
        ILogger<TimeBackgroundService> logger
    ) : BackgroundService
{
    protected abstract Task ProcessAsync(CancellationToken cancellationToken);
    protected abstract TimeSpan Interval { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.BeginScope(new Dictionary<string, object?>()
        {
            ["BackgroundService"] = GetType().FullName,
            ["Interval"] = Interval
        });

        logger.LogInformation("{BackgroundService} is starting with {Interval}", GetType().Name, Interval);
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    logger.LogInformation("{BackgroundService} started iteration", GetType().Name);
                    await ProcessAsync(stoppingToken);
                    logger.LogInformation("{BackgroundService} completed iteration successfully", GetType().Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{BackgroundService} error while processing", GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{BackgroundService} is stopping", GetType().Name);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "{BackgroundService} error occurred", GetType().Name);
        }
        finally
        {
            logger.LogInformation("{BackgroundService} stopped", GetType().Name);
        }
    }
}
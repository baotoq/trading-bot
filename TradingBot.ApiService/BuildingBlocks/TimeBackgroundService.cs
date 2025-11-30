using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

namespace TradingBot.ApiService.BuildingBlocks;

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

        logger.LogInformation("{BackgroundService} is starting with {Interval}", GetType().FullName, Interval);
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    logger.LogDebug("{BackgroundService} started iteration", GetType().FullName);
                    await ProcessAsync(stoppingToken);
                    logger.LogDebug("{BackgroundService} completed iteration successfully", GetType().FullName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{BackgroundService} error while processing", GetType().FullName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{BackgroundService} is stopping", GetType().FullName);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "{BackgroundService} error occured", GetType().FullName);
        }
        finally
        {
            logger.LogInformation("{BackgroundService} stopped", GetType().FullName);
        }
    }
}
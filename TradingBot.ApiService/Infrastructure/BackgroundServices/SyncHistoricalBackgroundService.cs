using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.IntegrationEvents;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that syncs historical candlestick data to the database every 5 minutes
/// This reduces the need to repeatedly fetch from the Binance API
/// </summary>
public class SyncHistoricalBackgroundService(
    IServiceProvider services,
    ILogger<SyncHistoricalBackgroundService> logger
) : TimeBackgroundService(logger)
{
    private readonly CandleInterval[] _intervals = ["4h"];
    private const string Symbol = "BTCUSDT";
    private readonly Lock _lock = new();
    private DateTime _lastSyncTime = DateTime.MinValue;

    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(5);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using (_lock.EnterScope())
        {
            // Prevent concurrent syncs
            if (DateTime.UtcNow - _lastSyncTime < TimeSpan.FromMinutes(4))
            {
                logger.LogDebug("Skipping sync - last sync was less than 4 minutes ago");
                return;
            }

            _lastSyncTime = DateTime.UtcNow;
        }

        try
        {
            logger.LogInformation("Starting historical data sync for {Symbol} at {Time}", Symbol, DateTime.UtcNow);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var syncTasks = _intervals.Select(interval =>
                bus.PublishAsync(new HistoricalDataSyncRequestedIntegrationEvent
                {
                    Symbol = Symbol,
                    Interval = interval
                }, cancellationToken));

            await Task.WhenAll(syncTasks);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Completed historical data sync for {Symbol}", Symbol);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing historical data for {Symbol}", Symbol);
        }
    }
}

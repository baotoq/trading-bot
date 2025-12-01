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
    private readonly string[] _intervals = ["1m", "5m", "15m", "4h"];
    private const string Symbol = "BTCUSDT";
    private readonly Lock _lock = new();
    private DateTime _lastSyncTime = DateTime.MinValue;

    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(3);

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
            logger.LogInformation("Starting historical data sync for {Symbol} at {Time}",
                Symbol, DateTime.UtcNow);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var syncTasks = _intervals.Select(interval =>
                bus.PublishAsync(new HistoricalDataSyncRequestedIntegrationEvent(Symbol, interval), cancellationToken));

            await Task.WhenAll(syncTasks);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Completed historical data sync for {Symbol}", Symbol);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing historical data for {Symbol}", Symbol);
        }
    }

    private async Task SyncIntervalAsync(
        string interval,
        ApplicationDbContext context,
        IHistoricalDataService historicalService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the last candle we have in the database for this symbol/interval
            var lastCandle = await context.Candles
                .Where(c => c.Symbol == Symbol && c.Interval == interval)
                .OrderByDescending(c => c.OpenTime)
                .FirstOrDefaultAsync(cancellationToken);

            DateTimeOffset startTime;
            if (lastCandle != null)
            {
                // Fetch from the last candle we have (with 1-minute overlap to ensure no gaps)
                startTime = lastCandle.OpenTime.AddMinutes(-1);
                logger.LogDebug("Last candle for {Symbol} {Interval}: {Time}",
                    Symbol, interval, lastCandle.OpenTime);
            }
            else
            {
                // Initial sync - get last 1000 candles (Binance API limit)
                startTime = GetStartTimeForInterval(interval);
                logger.LogInformation("Initial sync for {Symbol} {Interval} from {StartTime}",
                    Symbol, interval, startTime);
            }

            var endTime = DateTime.UtcNow;

            // Fetch historical data from Binance
            var candles = await historicalService.GetHistoricalDataAsync(
                Symbol,
                interval,
                startTime,
                endTime,
                limit: 1000,
                cancellationToken: cancellationToken);

            if (candles.Count == 0)
            {
                logger.LogDebug("No new candles fetched for {Symbol} {Interval}", Symbol, interval);
                return;
            }

            // Convert to entities and upsert
            var entities = candles.Select(c => new Candle
            {
                Symbol = Symbol,
                Interval = interval,
                OpenTime = c.OpenTime,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume,
                CloseTime = c.CloseTime,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            // Upsert candles (update if exists, insert if not)
            foreach (var entity in entities)
            {
                var existing = await context.Candles
                    .FirstOrDefaultAsync(c =>
                            c.Symbol == entity.Symbol &&
                            c.Interval == entity.Interval &&
                            c.OpenTime == entity.OpenTime,
                        cancellationToken);

                if (existing != null)
                {
                    // Update existing candle (in case it was incomplete/updated)
                    existing.Open = entity.Open;
                    existing.High = entity.High;
                    existing.Low = entity.Low;
                    existing.Close = entity.Close;
                    existing.Volume = entity.Volume;
                    existing.CloseTime = entity.CloseTime;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Insert new candle
                    await context.Candles.AddAsync(entity, cancellationToken);
                }
            }

            var savedCount = await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Synced {Count} candles for {Symbol} {Interval} (saved {SavedCount} changes)",
                candles.Count, Symbol, interval, savedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing {Interval} data for {Symbol}", interval, Symbol);
        }
    }

    private static DateTime GetStartTimeForInterval(string interval)
    {
        // Calculate appropriate start time based on interval to get ~1000 candles
        return interval switch
        {
            "1m" => DateTime.UtcNow.AddHours(-17), // ~1000 minutes
            "5m" => DateTime.UtcNow.AddDays(-3.5), // ~1000 * 5 minutes
            "15m" => DateTime.UtcNow.AddDays(-10), // ~1000 * 15 minutes
            "4h" => DateTime.UtcNow.AddDays(-166), // ~1000 * 4 hours
            _ => DateTime.UtcNow.AddDays(-30)
        };
    }
}

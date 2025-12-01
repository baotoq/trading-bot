using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks.Pubsub;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.IntegrationEvents;

public record HistoricalDataSyncRequestedIntegrationEvent : IntegrationEvent
{
    public string Symbol { get; set; }
    public string Interval { get; set; }

    public HistoricalDataSyncRequestedIntegrationEvent()
    {
    }

    public HistoricalDataSyncRequestedIntegrationEvent(string symbol, string interval)
    {
        Symbol = symbol;
        Interval = interval;
    }
}

public class HistoricalDataSyncRequestedIntegrationEventHandler(
        ILogger<HistoricalDataSyncRequestedIntegrationEventHandler> logger,
        ApplicationDbContext context,
        IHistoricalDataService historicalService
    ) : IIntegrationEventHandler<HistoricalDataSyncRequestedIntegrationEvent>
{
    public async Task Handle(HistoricalDataSyncRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // Get the last candle we have in the database for this symbol/interval
        var lastCandle = await context.Candles
            .Where(c => c.Symbol == notification.Symbol && c.Interval == notification.Interval)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        DateTimeOffset startTime;
        if (lastCandle != null)
        {
            // Fetch from the last candle we have (with 1-minute overlap to ensure no gaps)
            startTime = lastCandle.OpenTime.AddMinutes(-1);
            logger.LogDebug("Last candle for {Symbol} {Interval}: {Time}",
                notification.Symbol, notification.Interval, lastCandle.OpenTime);
        }
        else
        {
            // Initial sync - get last 1000 candles (Binance API limit)
            startTime = GetStartTimeForInterval(notification.Interval);
            logger.LogInformation("Initial sync for {Symbol} {Interval} from {StartTime}",
                notification.Symbol, notification.Interval, startTime);
        }

        var endTime = DateTime.UtcNow;

        // Fetch historical data from Binance
        var candles = await historicalService.GetHistoricalDataAsync(
            notification.Symbol,
            notification.Interval,
            startTime,
            endTime,
            limit: 1000,
            cancellationToken: cancellationToken);

        if (candles.Count == 0)
        {
            logger.LogDebug("No new candles fetched for {Symbol} {Interval}", notification.Symbol, notification.Interval);
            return;
        }

        // Convert to entities and upsert
        var entities = candles.Select(c => new Domain.Candle
        {
            Id = Guid.CreateVersion7(c.OpenTime),
            Symbol = notification.Symbol,
            Interval = notification.Interval,
            OpenTime = c.OpenTime,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume,
            CloseTime = c.CloseTime,
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
            candles.Count, notification.Symbol, notification.Interval, savedCount);
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
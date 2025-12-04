using Binance.Net.Interfaces.Clients;
using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.DistributedLocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Candles.IntegrationEvents;

public record HistoricalDataSyncRequestedIntegrationEvent : IntegrationEvent
{
    public required Symbol Symbol { get; init; }
    public required CandleInterval Interval { get; init; }
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.Parse("2025-12-01T00:00:00Z");
}

public class HistoricalDataSyncRequestedIntegrationEventHandler(
        ILogger<HistoricalDataSyncRequestedIntegrationEventHandler> logger,
        ApplicationDbContext context,
        IDistributedLock lockStore,
        IBinanceRestClient binanceClient
    ) : IIntegrationEventHandler<HistoricalDataSyncRequestedIntegrationEvent>
{
    public async Task Handle(HistoricalDataSyncRequestedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        await using var lockResponse = await lockStore.AcquireLockAsync(@event.Symbol + @event.Interval, TimeSpan.FromSeconds(60), cancellationToken);

        if (!lockResponse.Success)
        {
            logger.LogWarning("Could not acquire lock for historical data sync of {Symbol}, another process may be handling it", @event.Symbol);
            return;
        }

        // Get the last candle we have in the database for this symbol/interval
        var lastCandle = await context.Candles
            .Where(c => c.Symbol == @event.Symbol && c.Interval == @event.Interval)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(cancellationToken);

        DateTimeOffset startTime = @event.StartTime;
        if (lastCandle != null)
        {
            // Fetch from the last candle we have (with 1-minute overlap to ensure no gaps)
            startTime = lastCandle.OpenTime.AddMinutes(-1);
            logger.LogInformation("Last candle for {Symbol} {Interval}: {Time}",
                @event.Symbol, @event.Interval, lastCandle.OpenTime);
        }

        var result = await binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
            @event.Symbol,
            @event.Interval.ToKlineInterval(),
            startTime.DateTime,
            null,
            1000,
            cancellationToken);

        if (!result.Success)
        {
            logger.LogError("Failed to fetch historical data: {Error}", result.Error?.Message);
            return;
        }

        // Convert to entities and upsert
        var entities = result.Data.Select(c => new Domain.Candle
        {
            Symbol = @event.Symbol,
            Interval = @event.Interval,
            OpenTime = c.OpenTime,
            OpenPrice = c.OpenPrice,
            HighPrice = c.HighPrice,
            LowPrice = c.LowPrice,
            ClosePrice = c.ClosePrice,
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
                existing.OpenPrice = entity.OpenPrice;
                existing.HighPrice = entity.HighPrice;
                existing.LowPrice = entity.LowPrice;
                existing.ClosePrice = entity.ClosePrice;
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
            entities.Count, @event.Symbol, @event.Interval, savedCount);
    }
}
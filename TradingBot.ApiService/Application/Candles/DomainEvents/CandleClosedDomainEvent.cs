using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Candles.DomainEvents;

public record CandleClosedDomainEvent(
    Symbol Symbol,
    CandleInterval Interval,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal OpenPrice,
    decimal ClosePrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal Volume
) : IDomainEvent;

public class CaptureCandleOnCandleClosedHandler(
        ILogger<CaptureCandleOnCandleClosedHandler> logger,
        ApplicationDbContext context,
        IMediator mediator,
        ICandleCacheService cacheService,
        IInMemoryCandleCache memoryCache
    ) : INotificationHandler<CandleClosedDomainEvent>
{
    public async Task Handle(CandleClosedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation("Candle received {@Request}", @event);

        var candle = @event;

        // Check if candle already exists
        var existingCandle = await context.Candles
            .FirstOrDefaultAsync(c =>
                c.Symbol == candle.Symbol &&
                c.Interval == candle.Interval &&
                c.OpenTime == candle.OpenTime,
                cancellationToken);

        if (existingCandle != null)
        {
            // Update existing candle
            existingCandle.OpenPrice = candle.OpenPrice;
            existingCandle.HighPrice = candle.HighPrice;
            existingCandle.LowPrice = candle.LowPrice;
            existingCandle.ClosePrice = candle.ClosePrice;
            existingCandle.Volume = candle.Volume;
            existingCandle.CloseTime = candle.CloseTime;
            existingCandle.UpdatedAt = DateTime.UtcNow;

            logger.LogDebug("Updated existing candle for {Symbol} at {OpenTime}", candle.Symbol, candle.OpenTime);
        }
        else
        {
            // Insert new candle
            var newCandle = new Candle
            {
                Symbol = candle.Symbol,
                Interval = candle.Interval,
                OpenTime = candle.OpenTime,
                OpenPrice = candle.OpenPrice,
                HighPrice = candle.HighPrice,
                LowPrice = candle.LowPrice,
                ClosePrice = candle.ClosePrice,
                Volume = candle.Volume,
                CloseTime = candle.CloseTime
            };

            await context.Candles.AddAsync(newCandle);
            logger.LogInformation("New candle saved {@Candle}", newCandle);

            existingCandle = newCandle;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cacheService.InvalidateCandlesAsync(candle.Symbol, candle.Interval, cancellationToken);
        memoryCache.Invalidate(candle.Symbol, candle.Interval);

        await mediator.Publish(new CandleCapturedDomainEvent(existingCandle), cancellationToken);
    }
}
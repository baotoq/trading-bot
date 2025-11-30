using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.HistoricalData;

/// <summary>
/// Get historical candles from database cache
/// </summary>
public static class GetCachedCandles
{
    public record Query(
        string Symbol,
        string Interval,
        DateTimeOffset? StartTime = null,
        DateTimeOffset? EndTime = null,
        int? Limit = null
    ) : IRequest<Result>;

    public record Result(
        string Symbol,
        string Interval,
        int Count,
        DateTimeOffset? FirstCandleTime,
        DateTimeOffset? LastCandleTime,
        List<Candle> Candles
    );

    internal sealed class Handler(ApplicationDbContext context, ILogger<Handler> logger) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var start = request.StartTime ?? DateTime.UtcNow.AddDays(-7);
            var end = request.EndTime ?? DateTime.UtcNow;

            var query = context.Candles
                .Where(c => c.Symbol == request.Symbol && c.Interval == request.Interval)
                .Where(c => c.OpenTime >= start && c.OpenTime <= end)
                .OrderBy(c => c.OpenTime);

            var entities = request.Limit.HasValue
                ? await query.Take(request.Limit.Value).ToListAsync(cancellationToken)
                : await query.ToListAsync(cancellationToken);

            var candles = entities.Select(e => new Candle
            {
                OpenTime = e.OpenTime,
                Open = e.Open,
                High = e.High,
                Low = e.Low,
                Close = e.Close,
                Volume = e.Volume,
                CloseTime = e.CloseTime
            }).ToList();

            logger.LogInformation(
                "Retrieved {Count} cached candles for {Symbol} ({Interval})",
                candles.Count, request.Symbol, request.Interval);

            return new Result(
                request.Symbol,
                request.Interval,
                candles.Count,
                candles.FirstOrDefault()?.OpenTime,
                candles.LastOrDefault()?.OpenTime,
                candles
            );
        }
    }
}


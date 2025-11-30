using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.HistoricalData;

/// <summary>
/// Get statistics about cached historical data
/// </summary>
public static class GetCacheStats
{
    public record Query : IRequest<Result>;

    public record Result(
        List<IntervalStats> Stats,
        int TotalCandles,
        DateTimeOffset? OldestCandle,
        DateTimeOffset? NewestCandle
    );

    public record IntervalStats(
        string Symbol,
        string Interval,
        int Count,
        DateTimeOffset? FirstCandle,
        DateTimeOffset? LastCandle
    );

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var stats = await context.Candles
                .GroupBy(c => new { c.Symbol, c.Interval })
                .Select(g => new IntervalStats(
                    g.Key.Symbol,
                    g.Key.Interval,
                    g.Count(),
                    g.Min(c => c.OpenTime),
                    g.Max(c => c.OpenTime)
                ))
                .ToListAsync(cancellationToken);

            var totalCandles = await context.Candles.CountAsync(cancellationToken);
            var oldestCandle = await context.Candles
                .OrderBy(c => c.OpenTime)
                .Select(c => c.OpenTime)
                .FirstOrDefaultAsync(cancellationToken);
            var newestCandle = await context.Candles
                .OrderByDescending(c => c.OpenTime)
                .Select(c => c.OpenTime)
                .FirstOrDefaultAsync(cancellationToken);

            return new Result(stats, totalCandles, oldestCandle, newestCandle);
        }
    }
}


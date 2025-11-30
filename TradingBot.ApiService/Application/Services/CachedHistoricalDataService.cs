using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Historical data service that reads from database cache first, falls back to API
/// This significantly reduces API calls and improves performance
/// </summary>
public class CachedHistoricalDataService : IHistoricalDataService
{
    private readonly ApplicationDbContext _context;
    private readonly IHistoricalDataService _apiService;
    private readonly ILogger<CachedHistoricalDataService> _logger;

    public CachedHistoricalDataService(
        ApplicationDbContext context,
        HistoricalDataService apiService,
        ILogger<CachedHistoricalDataService> logger)
    {
        _context = context;
        _apiService = apiService;
        _logger = logger;
    }

    public async Task<List<Candle>> GetHistoricalDataAsync(
        string symbol,
        string interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = startTime ?? DateTimeOffset.UtcNow.AddDays(-30);
            var end = endTime ?? DateTimeOffset.UtcNow;

            // Try to get from database first
            var query = _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == interval)
                .Where(c => c.OpenTime >= start && c.OpenTime <= end)
                .OrderBy(c => c.OpenTime);

            var entities = limit.HasValue
                ? await query.Take(limit.Value).ToListAsync(cancellationToken)
                : await query.ToListAsync(cancellationToken);

            if (entities.Count > 0)
            {
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

                _logger.LogInformation(
                    "Retrieved {Count} candles from cache for {Symbol} ({Interval})",
                    candles.Count, symbol, interval);

                // If we have enough data in cache, return it
                if (!limit.HasValue || candles.Count >= limit.Value)
                {
                    return candles;
                }
            }

            // Fallback to API if cache is empty or insufficient
            _logger.LogInformation(
                "Cache miss or insufficient data for {Symbol} ({Interval}), fetching from API",
                symbol, interval);

            return await _apiService.GetHistoricalDataAsync(
                symbol, interval, startTime, endTime, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);

            // Fallback to API on any error
            return await _apiService.GetHistoricalDataAsync(
                symbol, interval, startTime, endTime, limit, cancellationToken);
        }
    }
}


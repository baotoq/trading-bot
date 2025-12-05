using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

public interface IInMemoryCandleCache
{
    Task<List<Candle>> GetCandlesAsync(Symbol symbol, CandleInterval interval, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task PreloadDataAsync(CancellationToken cancellationToken = default);
    void Invalidate(Symbol symbol, CandleInterval interval);
}

public class InMemoryCandleCache : IInMemoryCandleCache
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InMemoryCandleCache> _logger;
    private readonly ConcurrentDictionary<string, List<Candle>> _cache = new();
    private readonly SemaphoreSlim _preloadLock = new(1, 1);
    private bool _isPreloaded = false;

    public InMemoryCandleCache(ApplicationDbContext context, ILogger<InMemoryCandleCache> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Candle>> GetCandlesAsync(
        Symbol symbol,
        CandleInterval interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        if (!_isPreloaded)
        {
            await PreloadDataAsync(cancellationToken);
        }

        var key = GetCacheKey(symbol, interval);

        if (_cache.TryGetValue(key, out var allCandles))
        {
            var filtered = allCandles
                .Where(c => c.OpenTime >= startDate && c.OpenTime <= endDate)
                .OrderBy(c => c.OpenTime)
                .ToList();

            _logger.LogDebug("Retrieved {Count} candles from memory cache for {Symbol} {Interval} ({Start} to {End})",
                filtered.Count, symbol, interval, startDate, endDate);

            return filtered;
        }

        _logger.LogWarning("No cached data found for {Symbol} {Interval}", symbol, interval);
        return new List<Candle>();
    }

    public async Task PreloadDataAsync(CancellationToken cancellationToken = default)
    {
        await _preloadLock.WaitAsync(cancellationToken);
        try
        {
            if (_isPreloaded)
            {
                _logger.LogDebug("Data already preloaded, skipping");
                return;
            }

            _logger.LogInformation("Preloading 3 months of candle data into memory...");

            var threeMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-3);
            var now = DateTimeOffset.UtcNow;

            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT" };
            var intervals = new[] { "5m", "15m", "4h", "1d" };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var symbol in symbols)
            {
                foreach (var interval in intervals)
                {
                    var key = GetCacheKey(symbol, interval);

                    var candles = await _context.Candles
                        .AsNoTracking()
                        .Where(c => c.Symbol == symbol &&
                                   c.Interval == interval &&
                                   c.OpenTime >= threeMonthsAgo &&
                                   c.OpenTime <= now)
                        .OrderBy(c => c.OpenTime)
                        .ToListAsync(cancellationToken);

                    if (candles.Count > 0)
                    {
                        _cache[key] = candles;
                        _logger.LogInformation("Loaded {Count} candles for {Symbol} {Interval} into memory",
                            candles.Count, symbol, interval);
                    }
                }
            }

            stopwatch.Stop();
            _isPreloaded = true;

            var totalCandles = _cache.Values.Sum(c => c.Count);
            var memorySizeMB = totalCandles * 200 / 1024.0 / 1024.0;

            _logger.LogInformation(
                "Preload complete: {TotalCandles} candles loaded for {SymbolCount} symbols × {IntervalCount} intervals in {ElapsedMs}ms (~{SizeMB:F2} MB)",
                totalCandles, symbols.Length, intervals.Length, stopwatch.ElapsedMilliseconds, memorySizeMB);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading candle data");
        }
        finally
        {
            _preloadLock.Release();
        }
    }

    public void Invalidate(Symbol symbol, CandleInterval interval)
    {
        var key = GetCacheKey(symbol, interval);

        if (_cache.TryRemove(key, out _))
        {
            _logger.LogInformation("Invalidated memory cache for {Symbol} {Interval}", symbol, interval);

            Task.Run(async () =>
            {
                try
                {
                    var threeMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-3);
                    var now = DateTimeOffset.UtcNow;

                    var candles = await _context.Candles
                        .AsNoTracking()
                        .Where(c => c.Symbol == symbol &&
                                   c.Interval == interval &&
                                   c.OpenTime >= threeMonthsAgo &&
                                   c.OpenTime <= now)
                        .OrderBy(c => c.OpenTime)
                        .ToListAsync();

                    if (candles.Count > 0)
                    {
                        _cache[key] = candles;
                        _logger.LogInformation("Reloaded {Count} candles for {Symbol} {Interval}",
                            candles.Count, symbol, interval);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading candles for {Symbol} {Interval}", symbol, interval);
                }
            });
        }
    }

    private static string GetCacheKey(Symbol symbol, CandleInterval interval)
    {
        return $"{symbol}:{interval}";
    }
}

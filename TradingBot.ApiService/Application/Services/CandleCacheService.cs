using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ICandleCacheService
{
    Task<List<Candle>?> GetCandlesAsync(Symbol symbol, CandleInterval interval, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task SetCandlesAsync(Symbol symbol, CandleInterval interval, DateTimeOffset startDate, DateTimeOffset endDate, List<Candle> candles, CancellationToken cancellationToken = default);
    Task InvalidateCandlesAsync(Symbol symbol, CandleInterval interval, CancellationToken cancellationToken = default);
}

public class CandleCacheService : ICandleCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CandleCacheService> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public CandleCacheService(IDistributedCache cache, ILogger<CandleCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Candle>?> GetCandlesAsync(
        Symbol symbol,
        CandleInterval interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(symbol, interval, startDate, endDate);

        try
        {
            var cachedData = await _cache.GetStringAsync(key, cancellationToken);

            if (cachedData != null)
            {
                _logger.LogDebug("Cache hit for {Key}", key);
                return JsonSerializer.Deserialize<List<Candle>>(cachedData);
            }

            _logger.LogDebug("Cache miss for {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from cache for {Key}", key);
            return null;
        }
    }

    public async Task SetCandlesAsync(
        Symbol symbol,
        CandleInterval interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        List<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(symbol, interval, startDate, endDate);

        try
        {
            var serialized = JsonSerializer.Serialize(candles);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };

            await _cache.SetStringAsync(key, serialized, options, cancellationToken);
            _logger.LogDebug("Cached {Count} candles for {Key}", candles.Count, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing to cache for {Key}", key);
        }
    }

    public async Task InvalidateCandlesAsync(Symbol symbol, CandleInterval interval, CancellationToken cancellationToken = default)
    {
        var pattern = $"candles:{symbol}:{interval}:*";
        _logger.LogInformation("Invalidating cache for pattern {Pattern}", pattern);

        try
        {
            await _cache.RemoveAsync(pattern, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating cache for {Pattern}", pattern);
        }
    }

    private static string GetCacheKey(Symbol symbol, CandleInterval interval, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        return $"candles:{symbol}:{interval}:{startDate:yyyyMMddHHmmss}:{endDate:yyyyMMddHHmmss}";
    }
}

using System.Net.Http.Json;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;

/// <summary>
/// VNDirect dchart API implementation of <see cref="IEtfPriceProvider"/>.
/// Uses stale-while-revalidate pattern: returns stale data immediately and
/// triggers a fire-and-forget background refresh.
/// Caches results in Redis with 48-hour freshness and 30-day physical TTL.
/// </summary>
public class VNDirectPriceProvider(
    HttpClient httpClient,
    IDistributedCache cache,
    ILogger<VNDirectPriceProvider> logger) : IEtfPriceProvider
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(48);
    private static readonly TimeSpan PhysicalTtl = TimeSpan.FromDays(30);
    private const string CacheKeyPrefix = "price:etf:";
    private const string Currency = "VND";

    /// <summary>
    /// Vietnamese stock exchange prices are quoted in thousands of VND.
    /// For example, a dchart close of 20.29 means 20,290 VND per unit.
    /// </summary>
    private const decimal ThousandsToVndMultiplier = 1_000m;

    /// <inheritdoc />
    public async Task<PriceFeedResult> GetPriceAsync(string vnDirectTicker, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{vnDirectTicker}";
        var cached = await ReadCacheAsync(cacheKey, ct);

        if (cached is not null)
        {
            var age = DateTimeOffset.UtcNow - cached.FetchedAt;

            if (age <= FreshnessWindow)
            {
                return PriceFeedResult.Fresh(cached.Price, cached.FetchedAt, Currency);
            }

            // Stale: return immediately, refresh in background (fire-and-forget)
            _ = RefreshInBackgroundAsync(vnDirectTicker, cacheKey);
            return PriceFeedResult.Stale(cached.Price, cached.FetchedAt, Currency);
        }

        // Cache empty: must await — throw if provider fails
        var entry = await FetchAndCacheAsync(vnDirectTicker, cacheKey, ct);
        return PriceFeedResult.Fresh(entry.Price, entry.FetchedAt, Currency);
    }

    /// <summary>
    /// Fire-and-forget background refresh. Logs errors but never throws.
    /// Uses CancellationToken.None because it should not be cancelled by the original request.
    /// </summary>
    private async Task RefreshInBackgroundAsync(string vnDirectTicker, string cacheKey)
    {
        try
        {
            await FetchAndCacheAsync(vnDirectTicker, cacheKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Background refresh failed for ETF {Ticker}, stale cache will be used until next request",
                vnDirectTicker);
        }
    }

    /// <summary>
    /// Fetches latest close price from VNDirect dchart API and caches the result.
    /// </summary>
    private async Task<PriceFeedEntry> FetchAndCacheAsync(
        string vnDirectTicker, string cacheKey, CancellationToken ct)
    {
        var to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds(); // 3 days back for safety (weekends/holidays)
        var url = $"dchart/history?resolution=D&symbol={vnDirectTicker}&from={from}&to={to}";

        var response = await httpClient.GetFromJsonAsync<VNDirectDchartResponse>(url, ct);

        if (response?.Status != "ok" || response.Close is null || response.Close.Length == 0)
        {
            throw new InvalidOperationException(
                $"VNDirect dchart returned no valid data for '{vnDirectTicker}' (status: {response?.Status})");
        }

        // Close prices from dchart are in thousands of VND (e.g., 20.29 = 20,290 VND).
        // Multiply by 1000 to convert to actual VND.
        var latestCloseThousands = response.Close[^1];
        var priceVnd = latestCloseThousands * ThousandsToVndMultiplier;

        var entry = PriceFeedEntry.Create(priceVnd, Currency);
        await WriteCacheAsync(cacheKey, entry, ct);

        logger.LogInformation(
            "Fetched ETF price for {Ticker} from VNDirect: {Price} VND",
            vnDirectTicker, priceVnd);

        return entry;
    }

    private async Task<PriceFeedEntry?> ReadCacheAsync(string key, CancellationToken ct)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null) return null;
        return MessagePackSerializer.Deserialize<PriceFeedEntry>(bytes, MessagePackSerializerOptions.Standard);
    }

    private async Task WriteCacheAsync(string key, PriceFeedEntry entry, CancellationToken ct)
    {
        var bytes = MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
        await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PhysicalTtl
        }, ct);
    }
}

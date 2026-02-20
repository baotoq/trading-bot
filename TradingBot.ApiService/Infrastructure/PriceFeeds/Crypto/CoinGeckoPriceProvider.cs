using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Infrastructure.CoinGecko;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;

/// <summary>
/// CoinGecko implementation of <see cref="ICryptoPriceProvider"/>.
/// Fetches live crypto prices via /simple/price endpoint with batch support.
/// Caches results in Redis with 5-minute freshness and 30-day physical TTL.
/// </summary>
public class CoinGeckoPriceProvider(
    HttpClient httpClient,
    IDistributedCache cache,
    IOptionsMonitor<CoinGeckoOptions> options,
    ILogger<CoinGeckoPriceProvider> logger) : ICryptoPriceProvider
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PhysicalTtl = TimeSpan.FromDays(30);
    private const string CacheKeyPrefix = "price:crypto:";
    private const string Currency = "USD";

    /// <inheritdoc />
    public async Task<PriceFeedResult> GetPriceAsync(string coinGeckoId, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{coinGeckoId}";
        var cached = await ReadCacheAsync(cacheKey, ct);

        if (cached is not null)
        {
            var age = DateTimeOffset.UtcNow - cached.FetchedAt;

            if (age <= FreshnessWindow)
            {
                return PriceFeedResult.Fresh(cached.Price, cached.FetchedAt, Currency);
            }

            // Stale: try to refresh, fall back to stale if provider fails
            try
            {
                var prices = await FetchFromApiAsync([coinGeckoId], ct);
                if (prices.TryGetValue(coinGeckoId, out var freshPrice))
                {
                    var entry = await WriteCacheAsync(cacheKey, freshPrice, ct);
                    return PriceFeedResult.Fresh(entry.Price, entry.FetchedAt, Currency);
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "CoinGecko API failed for {CoinId}, returning stale cached price from {FetchedAt}",
                    coinGeckoId, cached.FetchedAt);
            }

            return PriceFeedResult.Stale(cached.Price, cached.FetchedAt, Currency);
        }

        // Cache empty: must fetch — let exception propagate if provider is down
        var fetchedPrices = await FetchFromApiAsync([coinGeckoId], ct);

        if (!fetchedPrices.TryGetValue(coinGeckoId, out var price))
        {
            throw new InvalidOperationException(
                $"CoinGecko returned no price data for '{coinGeckoId}'");
        }

        var newEntry = await WriteCacheAsync(cacheKey, price, ct);
        return PriceFeedResult.Fresh(newEntry.Price, newEntry.FetchedAt, Currency);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, PriceFeedResult>> GetPricesAsync(
        IEnumerable<string> coinGeckoIds, CancellationToken ct)
    {
        var idList = coinGeckoIds.ToList();
        var results = new Dictionary<string, PriceFeedResult>(idList.Count);
        var idsToFetch = new List<string>();

        // Check cache for each ID
        foreach (var id in idList)
        {
            var cacheKey = $"{CacheKeyPrefix}{id}";
            var cached = await ReadCacheAsync(cacheKey, ct);

            if (cached is not null)
            {
                var age = DateTimeOffset.UtcNow - cached.FetchedAt;

                if (age <= FreshnessWindow)
                {
                    results[id] = PriceFeedResult.Fresh(cached.Price, cached.FetchedAt, Currency);
                    continue;
                }

                // Stale — mark for fetch but keep stale as fallback
                results[id] = PriceFeedResult.Stale(cached.Price, cached.FetchedAt, Currency);
            }

            idsToFetch.Add(id);
        }

        if (idsToFetch.Count == 0)
        {
            return results;
        }

        // Batch-fetch all missing/stale IDs in a single API call
        try
        {
            var fetched = await FetchFromApiAsync(idsToFetch, ct);

            foreach (var (id, price) in fetched)
            {
                var cacheKey = $"{CacheKeyPrefix}{id}";
                var entry = await WriteCacheAsync(cacheKey, price, ct);
                results[id] = PriceFeedResult.Fresh(entry.Price, entry.FetchedAt, Currency);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "CoinGecko batch API failed for {CoinIds}, returning stale/missing results",
                string.Join(",", idsToFetch));

            // For IDs that had no stale cache and were not fetched, throw
            var missingIds = idsToFetch.Where(id => !results.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"CoinGecko API failed and no cached data exists for: {string.Join(", ", missingIds)}");
            }
        }

        return results;
    }

    /// <summary>
    /// Fetches prices from CoinGecko /simple/price endpoint for a batch of coin IDs.
    /// </summary>
    private async Task<Dictionary<string, decimal>> FetchFromApiAsync(
        List<string> coinGeckoIds, CancellationToken ct)
    {
        var ids = string.Join(",", coinGeckoIds);
        var url = $"simple/price?ids={ids}&vs_currencies=usd&include_last_updated_at=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add CoinGecko demo API key header if configured
        var apiKey = options.CurrentValue.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Add("x-cg-demo-api-key", apiKey);
        }

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, CoinPriceData>>(ct)
            ?? throw new InvalidOperationException("CoinGecko returned null response");

        logger.LogInformation("Fetched crypto prices for {CoinIds} from CoinGecko", ids);

        return data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Usd);
    }

    private async Task<PriceFeedEntry?> ReadCacheAsync(string key, CancellationToken ct)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null) return null;
        return MessagePackSerializer.Deserialize<PriceFeedEntry>(bytes, MessagePackSerializerOptions.Standard);
    }

    private async Task<PriceFeedEntry> WriteCacheAsync(string key, decimal price, CancellationToken ct)
    {
        var entry = PriceFeedEntry.Create(price, Currency);
        var bytes = MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
        await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PhysicalTtl
        }, ct);
        return entry;
    }

    /// <summary>
    /// JSON DTO for CoinGecko /simple/price response per coin.
    /// </summary>
    private record CoinPriceData(
        [property: JsonPropertyName("usd")] decimal Usd,
        [property: JsonPropertyName("last_updated_at")] long LastUpdatedAt);
}

using System.Net.Http.Json;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

/// <summary>
/// open.er-api.com implementation of <see cref="IExchangeRateProvider"/>.
/// Fetches USD/VND exchange rate with 12-hour freshness and 30-day physical TTL.
/// Uses wait-for-fetch pattern (not stale-while-revalidate) because exchange rate
/// is used in currency conversion where accuracy matters more than latency.
/// </summary>
public class OpenErApiProvider(
    HttpClient httpClient,
    IDistributedCache cache,
    ILogger<OpenErApiProvider> logger) : IExchangeRateProvider
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(12);
    private static readonly TimeSpan PhysicalTtl = TimeSpan.FromDays(30);
    private const string CacheKey = "price:exchangerate:usd-vnd";
    private const string Currency = "VND";

    /// <inheritdoc />
    public async Task<PriceFeedResult> GetUsdToVndRateAsync(CancellationToken ct)
    {
        var cached = await ReadCacheAsync(ct);

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
                var entry = await FetchAndCacheAsync(ct);
                return PriceFeedResult.Fresh(entry.Price, entry.FetchedAt, Currency);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(
                    ex,
                    "Exchange rate API failed, returning stale rate from {FetchedAt}",
                    cached.FetchedAt);

                return PriceFeedResult.Stale(cached.Price, cached.FetchedAt, Currency);
            }
        }

        // Cache empty: must fetch — let exception propagate if provider is down
        var freshEntry = await FetchAndCacheAsync(ct);
        return PriceFeedResult.Fresh(freshEntry.Price, freshEntry.FetchedAt, Currency);
    }

    /// <summary>
    /// Fetches USD/VND rate from open.er-api.com and caches the result.
    /// </summary>
    private async Task<PriceFeedEntry> FetchAndCacheAsync(CancellationToken ct)
    {
        var response = await httpClient.GetFromJsonAsync<OpenErApiResponse>("v6/latest/USD", ct);

        if (response?.Result != "success" || response.Rates is null)
        {
            throw new InvalidOperationException(
                $"Exchange rate API returned non-success result: {response?.Result}");
        }

        if (!response.Rates.TryGetValue("VND", out var rate))
        {
            throw new InvalidOperationException("VND rate not found in exchange rate response");
        }

        var entry = PriceFeedEntry.Create(rate, Currency);
        var bytes = MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
        await cache.SetAsync(CacheKey, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PhysicalTtl
        }, ct);

        logger.LogInformation("Fetched USD/VND exchange rate: {Rate}", rate);

        return entry;
    }

    private async Task<PriceFeedEntry?> ReadCacheAsync(CancellationToken ct)
    {
        var bytes = await cache.GetAsync(CacheKey, ct);
        if (bytes is null) return null;
        return MessagePackSerializer.Deserialize<PriceFeedEntry>(bytes, MessagePackSerializerOptions.Standard);
    }
}

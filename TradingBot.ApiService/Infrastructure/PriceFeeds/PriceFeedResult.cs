namespace TradingBot.ApiService.Infrastructure.PriceFeeds;

/// <summary>
/// Return type for all price feed providers.
/// Contains the price, when it was fetched, whether it's stale, and the currency.
/// </summary>
public record PriceFeedResult(decimal Price, DateTimeOffset FetchedAt, bool IsStale, string Currency)
{
    /// <summary>
    /// Creates a fresh (non-stale) price result.
    /// </summary>
    public static PriceFeedResult Fresh(decimal price, DateTimeOffset fetchedAt, string currency)
        => new(price, fetchedAt, IsStale: false, currency);

    /// <summary>
    /// Creates a stale price result (cache hit but past freshness window).
    /// </summary>
    public static PriceFeedResult Stale(decimal price, DateTimeOffset fetchedAt, string currency)
        => new(price, fetchedAt, IsStale: true, currency);
}

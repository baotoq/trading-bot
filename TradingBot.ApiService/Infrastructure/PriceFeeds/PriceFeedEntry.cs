using MessagePack;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds;

/// <summary>
/// MessagePack-serializable cache record for storing a price with its fetch timestamp in Redis.
/// Uses long for FetchedAt to avoid DateTimeOffset MessagePack resolver issues.
/// </summary>
[MessagePackObject]
public record PriceFeedEntry(
    [property: Key(0)] decimal Price,
    [property: Key(1)] long FetchedAtUnixSeconds,
    [property: Key(2)] string Currency)
{
    /// <summary>
    /// Computed property to get FetchedAt as DateTimeOffset.
    /// </summary>
    [IgnoreMember]
    public DateTimeOffset FetchedAt => DateTimeOffset.FromUnixTimeSeconds(FetchedAtUnixSeconds);

    /// <summary>
    /// Creates a new PriceFeedEntry with the current UTC time as the fetch timestamp.
    /// </summary>
    public static PriceFeedEntry Create(decimal price, string currency) => new(
        Price: price,
        FetchedAtUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Currency: currency);
}

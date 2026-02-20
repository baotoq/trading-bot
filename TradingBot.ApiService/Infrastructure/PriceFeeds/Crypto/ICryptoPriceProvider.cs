namespace TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;

/// <summary>
/// Provides live crypto prices from CoinGecko with Redis caching.
/// Supports single and batch price fetching by CoinGecko coin ID.
/// </summary>
public interface ICryptoPriceProvider
{
    /// <summary>
    /// Gets the current USD price for a single coin.
    /// </summary>
    /// <param name="coinGeckoId">CoinGecko coin ID (e.g., "bitcoin", "ethereum")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Price result with staleness information</returns>
    Task<PriceFeedResult> GetPriceAsync(string coinGeckoId, CancellationToken ct);

    /// <summary>
    /// Gets current USD prices for multiple coins in a single API call.
    /// </summary>
    /// <param name="coinGeckoIds">CoinGecko coin IDs</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary of coin ID to price result</returns>
    Task<Dictionary<string, PriceFeedResult>> GetPricesAsync(IEnumerable<string> coinGeckoIds, CancellationToken ct);
}

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;

/// <summary>
/// Provides live VN ETF prices from VNDirect dchart API with Redis caching.
/// Prices are returned in VND.
/// </summary>
public interface IEtfPriceProvider
{
    /// <summary>
    /// Gets the latest close price for a VN ETF.
    /// </summary>
    /// <param name="vnDirectTicker">VNDirect ticker symbol (e.g., "E1VFVN30")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Price result in VND with staleness information</returns>
    Task<PriceFeedResult> GetPriceAsync(string vnDirectTicker, CancellationToken ct);
}

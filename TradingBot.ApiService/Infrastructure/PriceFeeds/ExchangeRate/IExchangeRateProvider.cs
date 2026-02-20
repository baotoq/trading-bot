namespace TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

/// <summary>
/// Provides USD/VND exchange rate from open.er-api.com with Redis caching.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Gets the current USD-to-VND exchange rate.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Price result where Price is the VND value of 1 USD</returns>
    Task<PriceFeedResult> GetUsdToVndRateAsync(CancellationToken ct);
}

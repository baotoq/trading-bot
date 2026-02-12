namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Service for fetching, storing, and calculating price data for smart multiplier logic.
/// Bootstraps historical data on first run, fetches daily updates, and provides cached calculations.
/// </summary>
public interface IPriceDataService
{
    /// <summary>
    /// Bootstraps historical price data for a symbol (e.g., 200 days).
    /// Should be called once on app startup. Idempotent - skips if data already exists.
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., "BTC")</param>
    /// <param name="ct">Cancellation token</param>
    Task BootstrapHistoricalDataAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Fetches and stores the latest daily candle(s) for a symbol.
    /// Fills any gaps between last stored date and today.
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., "BTC")</param>
    /// <param name="ct">Cancellation token</param>
    Task FetchAndStoreDailyCandleAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Gets the highest closing price from the last N days (default: 30).
    /// Used for calculating price drop percentage for multiplier tiers.
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., "BTC")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Highest close price in lookback period, or 0 if insufficient data</returns>
    Task<decimal> Get30DayHighAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Gets the simple moving average of closing prices over N days (default: 200).
    /// Used for detecting bear market conditions (price below MA200).
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., "BTC")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200-day SMA of close prices, or 0 if insufficient data</returns>
    Task<decimal> Get200DaySmaAsync(string symbol, CancellationToken ct = default);
}

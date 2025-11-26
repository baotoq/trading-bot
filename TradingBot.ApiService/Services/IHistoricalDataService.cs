using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services;

/// <summary>
/// Service for fetching historical candlestick data
/// </summary>
public interface IHistoricalDataService
{
    /// <summary>
    /// Get historical candlestick data for backtesting
    /// </summary>
    Task<List<Candle>> GetHistoricalDataAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}


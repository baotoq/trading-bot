using TradingBot.ApiService.Application.Models;

namespace TradingBot.ApiService.Application.Services;

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
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}



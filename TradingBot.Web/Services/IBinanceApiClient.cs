using Refit;
using TradingBot.Web.Models;

namespace TradingBot.Web.Services;

/// <summary>
/// Refit interface for Binance API client
/// </summary>
public interface IBinanceApiClient
{
    /// <summary>
    /// Test Binance API connectivity
    /// </summary>
    [Get("/binance/ping")]
    Task<PingResponse> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get 24-hour price statistics for a specific symbol
    /// </summary>
    [Get("/binance/ticker/{symbol}")]
    Task<BinanceTickerData> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get 24-hour price statistics for all symbols
    /// </summary>
    [Get("/binance/tickers")]
    Task<BinanceTickerData[]> GetAllTickersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get order book for a specific symbol
    /// </summary>
    [Get("/binance/orderbook/{symbol}")]
    Task<BinanceOrderBookData> GetOrderBookAsync(string symbol, [Query] int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get account information and balances
    /// </summary>
    [Get("/binance/account")]
    Task<BinanceAccountInfo> GetAccountInfoAsync(CancellationToken cancellationToken = default);
}



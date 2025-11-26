using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services;

public interface IBinanceService
{
    /// <summary>
    /// Get ticker information for a specific symbol
    /// </summary>
    Task<BinanceTickerData?> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get ticker information for all symbols
    /// </summary>
    Task<IEnumerable<BinanceTickerData>> GetAllTickersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get order book for a specific symbol
    /// </summary>
    Task<BinanceOrderBookData?> GetOrderBookAsync(string symbol, int limit = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get account information (requires API credentials)
    /// </summary>
    Task<BinanceAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current exchange information and trading rules
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}



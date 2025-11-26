using Refit;
using TradingBot.Web.Models;

namespace TradingBot.Web.Services;

/// <summary>
/// Wrapper for Refit client with better error handling
/// Provides nullable return types for better error handling in UI
/// </summary>
public class BinanceApiClientWrapper
{
    private readonly IBinanceApiClient _client;
    private readonly ILogger<BinanceApiClientWrapper> _logger;

    public BinanceApiClientWrapper(IBinanceApiClient client, ILogger<BinanceApiClientWrapper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<BinanceTickerData?> GetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetTickerAsync(symbol, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Ticker not found for symbol: {Symbol}", symbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticker for symbol: {Symbol}", symbol);
            return null;
        }
    }

    public async Task<BinanceTickerData[]?> GetAllTickersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAllTickersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tickers");
            return null;
        }
    }

    public async Task<BinanceOrderBookData?> GetOrderBookAsync(string symbol, int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetOrderBookAsync(symbol, limit, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Order book not found for symbol: {Symbol}", symbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order book for symbol: {Symbol}", symbol);
            return null;
        }
    }

    public async Task<BinanceAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAccountInfoAsync(cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Unauthorized access to account info - API keys may not be configured");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account info");
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            return response?.Connected ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection");
            return false;
        }
    }
}


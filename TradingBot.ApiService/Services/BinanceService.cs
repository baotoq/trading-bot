using Binance.Net.Interfaces.Clients;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services;

public class BinanceService : IBinanceService
{
    private readonly IBinanceRestClient _restClient;
    private readonly ILogger<BinanceService> _logger;

    public BinanceService(IBinanceRestClient restClient, ILogger<BinanceService> logger)
    {
        _restClient = restClient;
        _logger = logger;
    }

    public async Task<BinanceTickerData?> GetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _restClient.SpotApi.ExchangeData.GetTickerAsync(symbol, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError("Failed to get ticker for {Symbol}: {Error}", symbol, result.Error?.Message);
                return null;
            }

            var ticker = result.Data;
            return new BinanceTickerData
            {
                Symbol = ticker.Symbol,
                LastPrice = ticker.LastPrice,
                PriceChange = ticker.PriceChange,
                PriceChangePercent = ticker.PriceChangePercent,
                HighPrice = ticker.HighPrice,
                LowPrice = ticker.LowPrice,
                Volume = ticker.Volume,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticker for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IEnumerable<BinanceTickerData>> GetAllTickersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _restClient.SpotApi.ExchangeData.GetTickersAsync(ct: cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError("Failed to get all tickers: {Error}", result.Error?.Message);
                return [];
            }

            return result.Data.Select(ticker => new BinanceTickerData
            {
                Symbol = ticker.Symbol,
                LastPrice = ticker.LastPrice,
                PriceChange = ticker.PriceChange,
                PriceChangePercent = ticker.PriceChangePercent,
                HighPrice = ticker.HighPrice,
                LowPrice = ticker.LowPrice,
                Volume = ticker.Volume,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tickers");
            return [];
        }
    }

    public async Task<BinanceOrderBookData?> GetOrderBookAsync(string symbol, int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _restClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, limit, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError("Failed to get order book for {Symbol}: {Error}", symbol, result.Error?.Message);
                return null;
            }

            var orderBook = result.Data;
            return new BinanceOrderBookData
            {
                Symbol = symbol,
                Bids = orderBook.Bids.Select(b => new OrderBookEntry { Price = b.Price, Quantity = b.Quantity }).ToList(),
                Asks = orderBook.Asks.Select(a => new OrderBookEntry { Price = a.Price, Quantity = a.Quantity }).ToList(),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order book for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<BinanceAccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _restClient.SpotApi.Account.GetAccountInfoAsync(ct: cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogError("Failed to get account info: {Error}", result.Error?.Message);
                return null;
            }

            var account = result.Data;
            return new BinanceAccountInfo
            {
                Balances = account.Balances
                    .Where(b => b.Total > 0)
                    .Select(b => new BinanceBalance
                    {
                        Asset = b.Asset,
                        Free = b.Available,
                        Locked = b.Locked
                    })
                    .ToList(),
                CanTrade = account.CanTrade,
                CanWithdraw = account.CanWithdraw,
                CanDeposit = account.CanDeposit,
                UpdateTime = account.UpdateTime
            };
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
            var result = await _restClient.SpotApi.ExchangeData.PingAsync(cancellationToken);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Binance connection");
            return false;
        }
    }
}


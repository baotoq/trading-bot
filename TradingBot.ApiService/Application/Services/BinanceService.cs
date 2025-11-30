using Binance.Net.Interfaces.Clients;
using TradingBot.ApiService.Application.Models;

namespace TradingBot.ApiService.Application.Services;

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

    public async Task<BinanceOrderBookData?> GetOrderBookAsync(string symbol, int limit = 20,
        CancellationToken cancellationToken = default)
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
                Bids =
                    orderBook.Bids.Select(b => new OrderBookEntry { Price = b.Price, Quantity = b.Quantity })
                        .ToList(),
                Asks = orderBook.Asks.Select(a => new OrderBookEntry { Price = a.Price, Quantity = a.Quantity })
                    .ToList(),
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
                    .Select(b => new BinanceBalance { Asset = b.Asset, Free = b.Available, Locked = b.Locked })
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

    public async Task<OrderResult?> PlaceSpotOrderAsync(
        string symbol,
        OrderSide side,
        OrderType type,
        decimal quantity,
        decimal? price = null,
        decimal? stopPrice = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var binanceSide = side == OrderSide.Buy
                ? global::Binance.Net.Enums.OrderSide.Buy
                : global::Binance.Net.Enums.OrderSide.Sell;

            var binanceType = type switch
            {
                OrderType.Market => global::Binance.Net.Enums.SpotOrderType.Market,
                OrderType.Limit => global::Binance.Net.Enums.SpotOrderType.Limit,
                OrderType.StopLoss => global::Binance.Net.Enums.SpotOrderType.StopLoss,
                OrderType.StopLossLimit => global::Binance.Net.Enums.SpotOrderType.StopLossLimit,
                OrderType.TakeProfit => global::Binance.Net.Enums.SpotOrderType.TakeProfit,
                OrderType.TakeProfitLimit => global::Binance.Net.Enums.SpotOrderType.TakeProfitLimit,
                _ => global::Binance.Net.Enums.SpotOrderType.Market
            };

            var result = await _restClient.SpotApi.Trading.PlaceOrderAsync(
                symbol,
                binanceSide,
                binanceType,
                quantity,
                price: price,
                stopPrice: stopPrice,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to place order: {Error}", result.Error?.Message);
                return null;
            }

            var order = result.Data;
            return new OrderResult
            {
                OrderId = order.Id,
                Symbol = order.Symbol,
                Side = side,
                Type = type,
                Quantity = order.Quantity,
                Price = order.Price > 0 ? order.Price : order.Quantity > 0 ? order.QuoteQuantity / order.Quantity : 0m,
                Status = MapOrderStatus(order.Status),
                Timestamp = order.CreateTime,
                Message = $"Order placed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing spot order");
            return null;
        }
    }

    private OrderStatus MapOrderStatus(global::Binance.Net.Enums.OrderStatus binanceStatus)
    {
        return binanceStatus switch
        {
            global::Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
            global::Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
            global::Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
            global::Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
            global::Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
            global::Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
            _ => OrderStatus.New
        };
    }
}


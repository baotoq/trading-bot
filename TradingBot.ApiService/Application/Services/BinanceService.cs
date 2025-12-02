using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;

namespace TradingBot.ApiService.Application.Services;

public class BinanceService : IBinanceService
{
    private readonly BinanceRestClient _client;
    private readonly ILogger<BinanceService> _logger;
    private readonly IConfiguration _configuration;

    public BinanceService(ILogger<BinanceService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var apiKey = _configuration["Binance:ApiKey"];
        var apiSecret = _configuration["Binance:ApiSecret"];

        _client = new BinanceRestClient(options =>
        {
            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            }
        });
    }

    public async Task<BinanceOrderResult> PlaceFuturesOrderAsync(
        string symbol,
        OrderSide side,
        FuturesOrderType orderType,
        decimal quantity,
        decimal? price = null,
        decimal? stopPrice = null,
        TimeInForce? timeInForce = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Placing futures order: {Symbol} {Side} {Type} Qty={Quantity} Price={Price} StopPrice={StopPrice}",
            symbol, side, orderType, quantity, price, stopPrice);

        try
        {
            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol: symbol,
                side: side,
                type: orderType,
                quantity: quantity,
                price: price,
                timeInForce: timeInForce,
                stopPrice: stopPrice,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                _logger.LogInformation(
                    "Order placed successfully: OrderId={OrderId}, Status={Status}",
                    result.Data.Id, result.Data.Status);

                return new BinanceOrderResult
                {
                    OrderId = result.Data.Id,
                    Symbol = result.Data.Symbol,
                    Side = result.Data.Side,
                    Type = result.Data.Type,
                    Quantity = result.Data.Quantity,
                    Price = result.Data.Price,
                    Status = MapOrderStatus(result.Data.Status),
                    IsSuccess = true,
                    Timestamp = result.Data.CreateTime
                };
            }
            else
            {
                _logger.LogError("Order placement failed: {Error}", result.Error?.Message);
                return new BinanceOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = result.Error?.Message ?? "Unknown error",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception placing futures order for {Symbol}", symbol);
            return new BinanceOrderResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> CancelFuturesOrderAsync(string symbol, long orderId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Canceling futures order: {Symbol} OrderId={OrderId}", symbol, orderId);

        try
        {
            var result = await _client.UsdFuturesApi.Trading.CancelOrderAsync(
                symbol: symbol,
                orderId: orderId,
                ct: cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Order canceled successfully: OrderId={OrderId}", orderId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to cancel order {OrderId}: {Error}", orderId, result.Error?.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception canceling order {OrderId}", orderId);
            return false;
        }
    }

    public async Task<BinanceAccountBalance> GetFuturesAccountBalanceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching futures account balance");

        try
        {
            var result = await _client.UsdFuturesApi.Account.GetBalancesAsync(ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var usdtBalance = result.Data.FirstOrDefault(b => b.Asset == "USDT");

                if (usdtBalance != null)
                {
                    return new BinanceAccountBalance
                    {
                        TotalWalletBalance = usdtBalance.WalletBalance,
                        AvailableBalance = usdtBalance.AvailableBalance,
                        TotalUnrealizedProfit = usdtBalance.CrossUnrealizedPnl ?? 0m,
                        TotalMarginBalance = usdtBalance.CrossWalletBalance
                    };
                }
            }

            _logger.LogWarning("Failed to fetch account balance: {Error}", result.Error?.Message);
            return new BinanceAccountBalance();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching account balance");
            return new BinanceAccountBalance();
        }
    }

    public async Task<decimal> GetFundingRateAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching funding rate for {Symbol}", symbol);

        try
        {
            var result = await _client.UsdFuturesApi.ExchangeData.GetFundingRatesAsync(
                symbol: symbol,
                limit: 1,
                ct: cancellationToken);

            if (result.Success && result.Data != null && result.Data.Any())
            {
                var fundingRate = result.Data.First().FundingRate;
                _logger.LogInformation("Funding rate for {Symbol}: {Rate}%", symbol, fundingRate * 100);
                return fundingRate;
            }

            _logger.LogWarning("Failed to fetch funding rate: {Error}", result.Error?.Message);
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching funding rate for {Symbol}", symbol);
            return 0m;
        }
    }

    public async Task<bool> SetLeverageAsync(string symbol, int leverage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting leverage for {Symbol} to {Leverage}x", symbol, leverage);

        try
        {
            var result = await _client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(
                symbol: symbol,
                leverage: leverage,
                ct: cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Leverage set successfully for {Symbol}: {Leverage}x", symbol, leverage);
                return true;
            }
            else
            {
                _logger.LogError("Failed to set leverage for {Symbol}: {Error}", symbol, result.Error?.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting leverage for {Symbol}", symbol);
            return false;
        }
    }

    public async Task<BinancePositionInfo?> GetPositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching position info for {Symbol}", symbol);

        try
        {
            var result = await _client.UsdFuturesApi.Account.GetPositionInformationAsync(
                symbol: symbol,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var position = result.Data.FirstOrDefault(p => p.Symbol == symbol);

                if (position != null && position.Quantity != 0)
                {
                    return new BinancePositionInfo
                    {
                        Symbol = position.Symbol,
                        PositionAmount = position.Quantity,
                        EntryPrice = position.EntryPrice,
                        UnrealizedProfit = position.UnrealizedPnl,
                        Leverage = position.Leverage,
                        LiquidationPrice = position.LiquidationPrice
                    };
                }

                return null; // No open position
            }

            _logger.LogWarning("Failed to fetch position info: {Error}", result.Error?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching position for {Symbol}", symbol);
            return null;
        }
    }

    private static OrderStatus MapOrderStatus(Binance.Net.Enums.OrderStatus binanceStatus)
    {
        return binanceStatus switch
        {
            Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
            Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
            Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
            Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
            Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
            Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
            _ => OrderStatus.New
        };
    }
}

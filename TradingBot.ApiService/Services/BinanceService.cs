using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Application.Options;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IBinanceService
{
    Task<BinanceOrderResult> PlaceFuturesOrderAsync(
        Symbol symbol,
        OrderSide side,
        FuturesOrderType orderType,
        decimal quantity,
        decimal? price = null,
        decimal? stopPrice = null,
        TimeInForce? timeInForce = null,
        CancellationToken cancellationToken = default);

    Task<bool> CancelFuturesOrderAsync(Symbol symbol, long orderId, CancellationToken cancellationToken = default);

    Task<BinanceAccountBalance> GetFuturesAccountBalanceAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetFundingRateAsync(Symbol symbol, CancellationToken cancellationToken = default);

    Task<bool> SetLeverageAsync(Symbol symbol, int leverage, CancellationToken cancellationToken = default);

    Task<BinancePositionInfo?> GetPositionAsync(Symbol symbol, CancellationToken cancellationToken = default);

    // Spot trading methods for delta-neutral hedging
    Task<BinanceSpotOrderResult> PlaceSpotOrderAsync(
        Symbol symbol,
        OrderSide side,
        SpotOrderType orderType,
        decimal quantity,
        decimal? price = null,
        CancellationToken cancellationToken = default);

    Task<decimal> GetSpotBalanceAsync(string asset, CancellationToken cancellationToken = default);

    Task<decimal> GetCurrentPriceAsync(Symbol symbol, CancellationToken cancellationToken = default);

    Task<FundingRateInfo> GetFundingRateInfoAsync(Symbol symbol, CancellationToken cancellationToken = default);

    Task<List<FundingRateInfo>> GetAllFundingRatesAsync(CancellationToken cancellationToken = default);
}

public class BinanceOrderResult
{
    public long OrderId { get; set; }
    public Symbol Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public FuturesOrderType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public OrderStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class BinanceAccountBalance
{
    public decimal TotalWalletBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal TotalUnrealizedProfit { get; set; }
    public decimal TotalMarginBalance { get; set; }
}

public class BinancePositionInfo
{
    public Symbol Symbol { get; set; } = string.Empty;
    public decimal PositionAmount { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal UnrealizedProfit { get; set; }
    public int Leverage { get; set; }
    public decimal LiquidationPrice { get; set; }
}

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Rejected,
    Expired
}

public class BinanceSpotOrderResult
{
    public long OrderId { get; set; }
    public Symbol Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public SpotOrderType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal ExecutedQuantity { get; set; }
    public OrderStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class FundingRateInfo
{
    public Symbol Symbol { get; set; } = string.Empty;
    public decimal FundingRate { get; set; }
    public DateTime NextFundingTime { get; set; }
    public int MinutesToNextFunding { get; set; }
    public decimal EstimatedAnnualizedRate { get; set; }
    public decimal MarkPrice { get; set; }
}

public class BinanceService : IBinanceService
{
    private readonly BinanceRestClient _client;
    private readonly ILogger<BinanceService> _logger;

    public BinanceService(ILogger<BinanceService> logger, IOptions<BinanceOptions> binanceOptions)
    {
        _logger = logger;

        _client = new BinanceRestClient(options =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(binanceOptions.Value.ApiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(binanceOptions.Value.ApiSecret);
            options.ApiCredentials = new ApiCredentials(binanceOptions.Value.ApiKey, binanceOptions.Value.ApiSecret);
        });
    }

    public async Task<BinanceOrderResult> PlaceFuturesOrderAsync(
        Symbol symbol,
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
                symbol: symbol.Value,
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

    public async Task<bool> CancelFuturesOrderAsync(Symbol symbol, long orderId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Canceling futures order: {Symbol} OrderId={OrderId}", symbol, orderId);

        try
        {
            var result = await _client.UsdFuturesApi.Trading.CancelOrderAsync(
                symbol: symbol.Value,
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

    public async Task<decimal> GetFundingRateAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching funding rate for {Symbol}", symbol);

        try
        {
            var result = await _client.UsdFuturesApi.ExchangeData.GetFundingRatesAsync(
                symbol: symbol.Value,
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

    public async Task<bool> SetLeverageAsync(Symbol symbol, int leverage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting leverage for {Symbol} to {Leverage}x", symbol, leverage);

        try
        {
            var result = await _client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(
                symbol: symbol.Value,
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

    public async Task<BinancePositionInfo?> GetPositionAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching position info for {Symbol}", symbol);

        try
        {
            var result = await _client.UsdFuturesApi.Account.GetPositionInformationAsync(
                symbol: symbol.Value,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var position = result.Data.FirstOrDefault(p => p.Symbol == symbol.Value);

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

    public async Task<BinanceSpotOrderResult> PlaceSpotOrderAsync(
        Symbol symbol,
        OrderSide side,
        SpotOrderType orderType,
        decimal quantity,
        decimal? price = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Placing spot order: {Symbol} {Side} {Type} Qty={Quantity} Price={Price}",
            symbol, side, orderType, quantity, price);

        try
        {
            var result = await _client.SpotApi.Trading.PlaceOrderAsync(
                symbol: symbol.Value,
                side: side,
                type: orderType,
                quantity: quantity,
                price: price,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                _logger.LogInformation(
                    "Spot order placed successfully: OrderId={OrderId}, Status={Status}",
                    result.Data.Id, result.Data.Status);

                return new BinanceSpotOrderResult
                {
                    OrderId = result.Data.Id,
                    Symbol = result.Data.Symbol,
                    Side = result.Data.Side,
                    Type = result.Data.Type,
                    Quantity = result.Data.Quantity,
                    ExecutedQuantity = result.Data.QuantityFilled,
                    Price = result.Data.Price,
                    Status = MapOrderStatus(result.Data.Status),
                    IsSuccess = true,
                    Timestamp = result.Data.CreateTime
                };
            }
            else
            {
                _logger.LogError("Spot order placement failed: {Error}", result.Error?.Message);
                return new BinanceSpotOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = result.Error?.Message ?? "Unknown error",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception placing spot order for {Symbol}", symbol);
            return new BinanceSpotOrderResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<decimal> GetSpotBalanceAsync(string asset, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching spot balance for {Asset}", asset);

        try
        {
            var result = await _client.SpotApi.Account.GetAccountInfoAsync(ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var balance = result.Data.Balances.FirstOrDefault(b => b.Asset == asset);
                return balance?.Available ?? 0m;
            }

            _logger.LogWarning("Failed to fetch spot balance: {Error}", result.Error?.Message);
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching spot balance for {Asset}", asset);
            return 0m;
        }
    }

    public async Task<decimal> GetCurrentPriceAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current price for {Symbol}", symbol);

        try
        {
            var result = await _client.SpotApi.ExchangeData.GetPriceAsync(
                symbol: symbol.Value,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                return result.Data.Price;
            }

            _logger.LogWarning("Failed to fetch price: {Error}", result.Error?.Message);
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching price for {Symbol}", symbol);
            return 0m;
        }
    }

    public async Task<FundingRateInfo> GetFundingRateInfoAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching funding rate info for {Symbol}", symbol);

        try
        {
            var result = await _client.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(
                symbol: symbol.Value,
                ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var data = result.Data;
                var nextFundingTime = data.NextFundingTime ?? DateTime.UtcNow.AddHours(8);
                var minutesToNext = (int)(nextFundingTime - DateTime.UtcNow).TotalMinutes;

                return new FundingRateInfo
                {
                    Symbol = symbol,
                    FundingRate = data.FundingRate ?? 0m,
                    NextFundingTime = nextFundingTime,
                    MinutesToNextFunding = Math.Max(0, minutesToNext),
                    EstimatedAnnualizedRate = (data.FundingRate ?? 0m) * 3 * 365,
                    MarkPrice = data.MarkPrice
                };
            }

            _logger.LogWarning("Failed to fetch funding rate info: {Error}", result.Error?.Message);
            return new FundingRateInfo { Symbol = symbol };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching funding rate info for {Symbol}", symbol);
            return new FundingRateInfo { Symbol = symbol };
        }
    }

    public async Task<List<FundingRateInfo>> GetAllFundingRatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all funding rates");

        try
        {
            var result = await _client.UsdFuturesApi.ExchangeData.GetMarkPricesAsync(ct: cancellationToken);

            if (result.Success && result.Data != null)
            {
                var fundingRates = result.Data
                    .Where(d => d.Symbol.EndsWith("USDT")) // Only USDT perpetuals
                    .Select(data =>
                    {
                        var nextFundingTime = data.NextFundingTime ?? DateTime.UtcNow.AddHours(8);
                        var minutesToNext = (int)(nextFundingTime - DateTime.UtcNow).TotalMinutes;

                        return new FundingRateInfo
                        {
                            Symbol = data.Symbol,
                            FundingRate = data.FundingRate ?? 0m,
                            NextFundingTime = nextFundingTime,
                            MinutesToNextFunding = Math.Max(0, minutesToNext),
                            EstimatedAnnualizedRate = (data.FundingRate ?? 0m) * 3 * 365,
                            MarkPrice = data.MarkPrice
                        };
                    })
                    .ToList();

                _logger.LogInformation("Fetched funding rates for {Count} symbols", fundingRates.Count);
                return fundingRates;
            }

            _logger.LogWarning("Failed to fetch all funding rates: {Error}", result.Error?.Message);
            return new List<FundingRateInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching all funding rates");
            return new List<FundingRateInfo>();
        }
    }
}

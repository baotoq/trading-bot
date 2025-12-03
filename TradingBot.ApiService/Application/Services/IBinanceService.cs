using Binance.Net.Enums;
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
    public DateTime Timestamp { get; set; }
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

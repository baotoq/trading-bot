using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Domain;

public class Position : AuditedEntity
{
    public Guid Id { get; set; }
    public required Symbol Symbol { get; set; }
    public TradeSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public decimal TakeProfit2 { get; set; }
    public decimal? TakeProfit3 { get; set; }
    public decimal RiskAmount { get; set; }
    public int Leverage { get; set; }
    public PositionStatus Status { get; set; }

    // Order IDs from Binance
    public long? EntryOrderId { get; set; }
    public long? StopLossOrderId { get; set; }
    public long? TakeProfit1OrderId { get; set; }
    public long? TakeProfit2OrderId { get; set; }
    public long? TakeProfit3OrderId { get; set; }

    // Position tracking
    public decimal RemainingQuantity { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public bool IsBreakEven { get; set; }
    public DateTime? EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string? ExitReason { get; set; }

    // Strategy information
    public string Strategy { get; set; } = string.Empty;
    public string SignalReason { get; set; } = string.Empty;
}

public enum PositionStatus
{
    PendingEntry,
    Open,
    PartiallyFilled,
    Closed,
    Cancelled,
    Failed
}

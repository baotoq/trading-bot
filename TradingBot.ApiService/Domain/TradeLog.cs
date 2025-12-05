using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Domain;

public class TradeLog : AuditedEntity
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public required Symbol Symbol { get; set; }
    public TradeSide Side { get; set; }

    // Entry details
    public DateTimeOffset EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public decimal TakeProfit2 { get; set; }
    public decimal? TakeProfit3 { get; set; }

    // Exit details
    public DateTime? ExitTime { get; set; }
    public decimal? ExitPrice { get; set; }
    public string? ExitReason { get; set; }

    // Performance
    public decimal RealizedPnL { get; set; }
    public decimal RealizedPnLPercent { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public decimal Fees { get; set; }
    public decimal Slippage { get; set; }

    // Market conditions at entry
    public decimal AtrAtEntry { get; set; }
    public decimal FundingRateAtEntry { get; set; }
    public decimal VolumeAtEntry { get; set; }
    public decimal RsiAtEntry { get; set; }
    public decimal MacdAtEntry { get; set; }

    // Strategy details
    public string Strategy { get; set; } = string.Empty;
    public string SignalReason { get; set; } = string.Empty;
    public Dictionary<string, decimal> Indicators { get; set; } = new();

    // Trade outcome
    public bool IsWin { get; set; }
    public int HoldingTimeMinutes { get; set; }

    public Position? Position { get; set; }
}

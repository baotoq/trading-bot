namespace TradingBot.ApiService.Domain;

public class TradingSignal
{
    public required Symbol Symbol { get; set; }
    public SignalType Type { get; set; }
    public decimal Price { get; set; }
    public decimal Confidence { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, decimal> Indicators { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }

    // Additional properties for entry management
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit1 { get; set; }
    public decimal? TakeProfit2 { get; set; }
    public decimal? TakeProfit3 { get; set; }
    public decimal? PositionSize { get; set; }
    public decimal? RiskAmount { get; set; }
}

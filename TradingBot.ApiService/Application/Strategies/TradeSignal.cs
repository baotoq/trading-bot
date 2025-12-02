namespace TradingBot.ApiService.Application.Strategies;

public class TradeSignal
{
    public required string Symbol { get; set; }
    public SignalType SignalType { get; set; }
    public DateTimeOffset Time { get; set; }
    public decimal Price { get; set; }
    public decimal FastEma { get; set; }
    public decimal SlowEma { get; set; }
    public decimal Volume { get; set; }
    public string Reason { get; set; } = string.Empty;
}
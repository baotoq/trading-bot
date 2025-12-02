namespace TradingBot.ApiService.Application.Strategies;

public class Trade
{
    public DateTimeOffset EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTimeOffset ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal EntryFee { get; set; }
    public decimal ExitFee { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitPercentage { get; set; }
}
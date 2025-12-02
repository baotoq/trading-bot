namespace TradingBot.ApiService.Application.Strategies;

public class BacktestResult
{
    public decimal InitialCapital { get; set; }
    public decimal CurrentCapital { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalReturn { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal AverageProfit { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
    public List<Trade> Trades { get; set; } = new();
}
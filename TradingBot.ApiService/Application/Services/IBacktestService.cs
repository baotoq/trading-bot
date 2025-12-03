using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IBacktestService
{
    Task<BacktestResult> RunBacktestAsync(
        Symbol symbol,
        string strategyName,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default);

    Task<ComparisonResult> CompareStrategiesAsync(
        Symbol symbol,
        List<string> strategies,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default);
}

public class BacktestResult
{
    public string StrategyName { get; set; } = string.Empty;
    public Symbol Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetProfitPercent { get; set; }

    // Trade Statistics
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }

    // Profit Metrics
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }

    // Risk Metrics
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal AverageRiskRewardRatio { get; set; }

    // Trade Details
    public List<BacktestTrade> Trades { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class BacktestTrade
{
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public TradeSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ProfitLossPercent { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public string ExitReason { get; set; } = string.Empty;
    public bool IsWin { get; set; }
}

public class ComparisonResult
{
    public Symbol Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BacktestResult> Results { get; set; } = new();
    public string BestStrategy { get; set; } = string.Empty;
    public string BestByWinRate { get; set; } = string.Empty;
    public string BestByProfitFactor { get; set; } = string.Empty;
    public string BestByDrawdown { get; set; } = string.Empty;
}

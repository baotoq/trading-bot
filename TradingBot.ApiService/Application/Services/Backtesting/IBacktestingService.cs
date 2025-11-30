namespace TradingBot.ApiService.Application.Services.Backtesting;

public interface IBacktestingService
{
    /// <summary>
    /// Run backtest for a strategy
    /// </summary>
    Task<BacktestResult> RunBacktestAsync(
        string strategyName,
        string symbol,
        string interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        decimal initialCapital = 10000m,
        decimal positionSize = 0.1m,
        CancellationToken cancellationToken = default);
}

public record BacktestResult
{
    public string StrategyName { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public decimal InitialCapital { get; init; }
    public decimal FinalCapital { get; init; }
    public decimal NetProfit { get; init; }
    public decimal ReturnPercentage { get; init; }
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public decimal WinRate { get; init; }
    public decimal AverageProfit { get; init; }
    public decimal AverageLoss { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal MaxDrawdownPercentage { get; init; }
    public decimal SharpeRatio { get; init; }
    public List<Trade> Trades { get; init; } = [];
    public List<EquityCurvePoint> EquityCurve { get; init; } = [];
}

public record Trade
{
    public DateTimeOffset EntryTime { get; init; }
    public decimal EntryPrice { get; init; }
    public DateTimeOffset ExitTime { get; init; }
    public decimal ExitPrice { get; init; }
    public TradeType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal Profit { get; init; }
    public decimal ProfitPercentage { get; init; }
    public string Signal { get; init; } = string.Empty;
}

public enum TradeType
{
    Long,
    Short
}

public record EquityCurvePoint
{
    public DateTimeOffset Time { get; init; }
    public decimal Equity { get; init; }
    public decimal DrawdownPercentage { get; init; }
}



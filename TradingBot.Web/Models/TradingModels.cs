namespace TradingBot.Web.Models;

public record TradingSignal
{
    public string Symbol { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Confidence { get; init; }
    public string Strategy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object> Indicators { get; init; } = new();
}

public record BacktestResult
{
    public string StrategyName { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
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
    public DateTime EntryTime { get; init; }
    public decimal EntryPrice { get; init; }
    public DateTime ExitTime { get; init; }
    public decimal ExitPrice { get; init; }
    public string Type { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Profit { get; init; }
    public decimal ProfitPercentage { get; init; }
    public string Signal { get; init; } = string.Empty;
}

public record EquityCurvePoint
{
    public DateTime Time { get; init; }
    public decimal Equity { get; init; }
    public decimal DrawdownPercentage { get; init; }
}

public record OrderResult
{
    public long OrderId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
}


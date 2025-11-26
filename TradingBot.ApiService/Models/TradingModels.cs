namespace TradingBot.ApiService.Models;

/// <summary>
/// Trading signal indicating buy, sell, or hold action
/// </summary>
public record TradingSignal
{
    public string Symbol { get; init; } = string.Empty;
    public SignalType Type { get; init; }
    public decimal Price { get; init; }
    public decimal Confidence { get; init; } // 0.0 to 1.0
    public string Strategy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Indicators { get; init; } = new();
}

public enum SignalType
{
    Buy,
    Sell,
    Hold,
    StrongBuy,
    StrongSell
}

/// <summary>
/// Candlestick/OHLCV data
/// </summary>
public record Candle
{
    public DateTime OpenTime { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public DateTime CloseTime { get; init; }
}

/// <summary>
/// Order placement request
/// </summary>
public record OrderRequest
{
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal? Price { get; init; } // For limit orders
    public decimal? StopPrice { get; init; } // For stop orders
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    StopLoss,
    StopLossLimit,
    TakeProfit,
    TakeProfitLimit
}

/// <summary>
/// Order execution result
/// </summary>
public record OrderResult
{
    public long OrderId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
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

/// <summary>
/// Strategy configuration
/// </summary>
public record StrategyConfig
{
    public string Name { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = "1h"; // 1m, 5m, 15m, 1h, 4h, 1d
    public bool Enabled { get; init; } = true;
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Strategy performance metrics
/// </summary>
public record StrategyPerformance
{
    public string StrategyName { get; init; } = string.Empty;
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public decimal WinRate { get; init; }
    public decimal TotalProfit { get; init; }
    public decimal TotalLoss { get; init; }
    public decimal NetProfit { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal SharpeRatio { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}


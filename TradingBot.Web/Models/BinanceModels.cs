namespace TradingBot.Web.Models;

public record BinanceTickerData
{
    public string Symbol { get; init; } = string.Empty;
    public decimal LastPrice { get; init; }
    public decimal PriceChange { get; init; }
    public decimal PriceChangePercent { get; init; }
    public decimal HighPrice { get; init; }
    public decimal LowPrice { get; init; }
    public decimal Volume { get; init; }
    public DateTime Timestamp { get; init; }
}

public record BinanceOrderBookData
{
    public string Symbol { get; init; } = string.Empty;
    public List<OrderBookEntry> Bids { get; init; } = [];
    public List<OrderBookEntry> Asks { get; init; } = [];
    public DateTime Timestamp { get; init; }
}

public record OrderBookEntry
{
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
}

public record BinanceAccountInfo
{
    public List<BinanceBalance> Balances { get; init; } = [];
    public bool CanTrade { get; init; }
    public bool CanWithdraw { get; init; }
    public bool CanDeposit { get; init; }
    public DateTime UpdateTime { get; init; }
}

public record BinanceBalance
{
    public string Asset { get; init; } = string.Empty;
    public decimal Free { get; init; }
    public decimal Locked { get; init; }
    public decimal Total => Free + Locked;
}

public record PingResponse
{
    public bool Connected { get; init; }
    public DateTime Timestamp { get; init; }
}



namespace TradingBot.ApiService.Models;

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




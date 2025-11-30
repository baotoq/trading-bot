namespace TradingBot.ApiService.Application.Models;

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




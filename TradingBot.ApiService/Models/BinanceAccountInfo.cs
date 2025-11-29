namespace TradingBot.ApiService.Models;

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




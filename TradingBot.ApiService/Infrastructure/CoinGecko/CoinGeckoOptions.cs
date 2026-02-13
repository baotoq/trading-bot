namespace TradingBot.ApiService.Infrastructure.CoinGecko;

public class CoinGeckoOptions
{
    /// <summary>
    /// Optional API key for CoinGecko paid tier.
    /// Free tier works without a key.
    /// </summary>
    public string? ApiKey { get; set; }
}

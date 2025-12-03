namespace TradingBot.ApiService.Application.Options;

public class BinanceOptions
{
    public const string SectionName = "Binance";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public bool TestMode { get; set; }
    public bool IsEnabled => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret);
}

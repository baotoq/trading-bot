namespace TradingBot.ApiService.Configuration;

public class HyperliquidOptions
{
    private const string TestnetUrl = "https://api.hyperliquid-testnet.xyz";
    private const string MainnetUrl = "https://api.hyperliquid.xyz";

    public bool IsTestnet { get; set; } = true;
    public string WalletAddress { get; set; } = string.Empty;

    public string ApiUrl => IsTestnet ? TestnetUrl : MainnetUrl;

    public override string ToString()
    {
        return $"Hyperliquid [IsTestnet: {IsTestnet}, ApiUrl: {ApiUrl}, WalletAddress: {WalletAddress}]";
    }
}

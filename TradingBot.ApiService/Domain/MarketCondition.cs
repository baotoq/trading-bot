namespace TradingBot.ApiService.Domain;

public class MarketCondition
{
    public string Symbol { get; set; } = string.Empty;
    public MarketRegime Regime { get; set; }
    public decimal Atr { get; set; }
    public decimal AtrPercent { get; set; }
    public decimal FundingRate { get; set; }
    public TradeSide? Bias { get; set; }
    public bool IsVolatile { get; set; }
    public bool IsLowVolatility { get; set; }
    public bool CanTrade { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }

    // Trend information
    public bool IsBullish { get; set; }
    public bool IsBearish { get; set; }
    public bool IsNeutral { get; set; }
}

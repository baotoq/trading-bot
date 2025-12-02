namespace TradingBot.ApiService.Application.Requests;

/// <summary>
/// Request to execute a trade with risk management parameters
/// </summary>
/// <param name="Symbol">Trading symbol (e.g., BTCUSDT)</param>
/// <param name="AccountEquity">Total account equity in USDT</param>
/// <param name="RiskPercent">Risk per trade as percentage (2% to 4% max, default 2.5%)</param>
public record ExecuteTradeRequest(
    string Symbol,
    decimal AccountEquity,
    decimal RiskPercent = 2.5m)
{
    /// <summary>
    /// Validates that risk percent is within acceptable range (2% to 4%)
    /// </summary>
    public bool IsValid() => RiskPercent >= 2m && RiskPercent <= 4m;
}

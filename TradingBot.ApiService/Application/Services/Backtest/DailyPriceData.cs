namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Input price data record for a single day in backtest simulation.
/// </summary>
public record DailyPriceData(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

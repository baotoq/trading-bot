namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Response DTO for POST /api/backtest.
/// Wraps BacktestResult with resolved config and date metadata.
/// </summary>
public record BacktestResponse(
    BacktestConfig Config,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    BacktestResult Result);

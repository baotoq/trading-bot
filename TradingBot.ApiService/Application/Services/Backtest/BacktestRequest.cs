namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Request DTO for POST /api/backtest.
/// All fields are nullable - they default to production DcaOptions when null.
/// </summary>
public record BacktestRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    decimal? BaseDailyAmount = null,
    int? HighLookbackDays = null,
    int? BearMarketMaPeriod = null,
    decimal? BearBoostFactor = null,
    decimal? MaxMultiplierCap = null,
    List<MultiplierTierInput>? Tiers = null);

/// <summary>
/// Multiplier tier override input for backtest requests.
/// </summary>
public record MultiplierTierInput(
    decimal DropPercentage,
    decimal Multiplier);

namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Backtest-specific configuration record.
/// Mirrors DcaOptions multiplier fields without schedule/infrastructure fields.
/// </summary>
public record BacktestConfig(
    decimal BaseDailyAmount,
    int HighLookbackDays,
    int BearMarketMaPeriod,
    decimal BearBoostFactor,
    decimal MaxMultiplierCap,
    IReadOnlyList<MultiplierTierConfig> Tiers);

/// <summary>
/// Backtest-specific multiplier tier configuration.
/// Separate from Configuration.MultiplierTier to avoid coupling backtest DTOs to mutable configuration classes.
/// </summary>
public record MultiplierTierConfig(
    decimal DropPercentage,
    decimal Multiplier);

using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Backtest-specific configuration record.
/// Mirrors DcaOptions multiplier fields without schedule/infrastructure fields.
/// </summary>
public record BacktestConfig(
    UsdAmount BaseDailyAmount,
    int HighLookbackDays,
    int BearMarketMaPeriod,
    Multiplier BearBoostFactor,
    Multiplier MaxMultiplierCap,
    IReadOnlyList<MultiplierTierConfig> Tiers);

/// <summary>
/// Backtest-specific multiplier tier configuration.
/// Separate from Configuration.MultiplierTier to avoid coupling backtest DTOs to mutable configuration classes.
/// </summary>
public record MultiplierTierConfig(
    Percentage DropPercentage,
    Multiplier Multiplier);

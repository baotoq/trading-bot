namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Request DTO for POST /api/backtest/sweep.
/// Accepts parameter ranges to generate combinations, or a preset name.
/// </summary>
public record SweepRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Preset = null,
    List<decimal>? BaseAmounts = null,
    List<int>? HighLookbackDays = null,
    List<int>? BearMarketMaPeriods = null,
    List<decimal>? BearBoosts = null,
    List<decimal>? MaxMultiplierCaps = null,
    List<TierSet>? TierSets = null,
    string RankBy = "efficiency",
    int MaxCombinations = 1000,
    bool Validate = false);

/// <summary>
/// A set of multiplier tier configurations to sweep.
/// </summary>
public record TierSet(List<MultiplierTierInput> Tiers);

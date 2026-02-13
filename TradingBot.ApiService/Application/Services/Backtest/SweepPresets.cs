namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Provides predefined parameter sweep presets.
/// </summary>
public static class SweepPresets
{
    /// <summary>
    /// Get a preset by name.
    /// </summary>
    /// <param name="name">Preset name: "conservative" or "full".</param>
    /// <returns>SweepRequest with parameter lists filled.</returns>
    /// <exception cref="ArgumentException">Thrown if preset name not recognized.</exception>
    public static SweepRequest GetPreset(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "conservative" => ConservativePreset(),
            "full" => FullPreset(),
            _ => throw new ArgumentException(
                $"Unknown preset '{name}'. Available presets: conservative, full",
                nameof(name))
        };
    }

    /// <summary>
    /// Conservative preset with ~24 combinations.
    /// </summary>
    private static SweepRequest ConservativePreset()
    {
        var standardTiers = new TierSet(
        [
            new MultiplierTierInput(10m, 1.5m),
            new MultiplierTierInput(20m, 2.0m),
            new MultiplierTierInput(30m, 2.5m)
        ]);

        return new SweepRequest(
            BaseAmounts: [10m, 15m, 20m],
            HighLookbackDays: [21, 30],
            BearMarketMaPeriods: [200],
            BearBoosts: [1.0m, 1.5m],
            MaxMultiplierCaps: [3.0m, 4.0m],
            TierSets: [standardTiers]);
    }

    /// <summary>
    /// Full preset with ~2160 combinations.
    /// </summary>
    private static SweepRequest FullPreset()
    {
        var tier1 = new TierSet(
        [
            new MultiplierTierInput(10m, 1.5m),
            new MultiplierTierInput(20m, 2.0m),
            new MultiplierTierInput(30m, 2.5m)
        ]);

        var tier2 = new TierSet(
        [
            new MultiplierTierInput(15m, 1.8m),
            new MultiplierTierInput(25m, 2.5m),
            new MultiplierTierInput(35m, 3.0m)
        ]);

        var tier3 = new TierSet(
        [
            new MultiplierTierInput(10m, 1.3m),
            new MultiplierTierInput(20m, 1.8m),
            new MultiplierTierInput(30m, 2.3m)
        ]);

        return new SweepRequest(
            BaseAmounts: [10m, 15m, 20m, 25m, 30m],
            HighLookbackDays: [14, 21, 30, 60],
            BearMarketMaPeriods: [100, 150, 200],
            BearBoosts: [1.0m, 1.25m, 1.5m, 2.0m],
            MaxMultiplierCaps: [3.0m, 4.0m, 5.0m],
            TierSets: [tier1, tier2, tier3]);
    }
}

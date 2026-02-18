using Microsoft.Extensions.Options;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Configuration;

public class DcaOptions
{
    public UsdAmount BaseDailyAmount { get; set; }
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int HighLookbackDays { get; set; } = 30;
    public int BearMarketMaPeriod { get; set; } = 200;
    public Multiplier BearBoostFactor { get; set; } = Multiplier.From(1.5m);
    public Multiplier MaxMultiplierCap { get; set; } = Multiplier.From(4.5m);
    public bool DryRun { get; set; } = false;
    public List<MultiplierTier> MultiplierTiers { get; set; } = [];
}

public class MultiplierTier
{
    public Percentage DropPercentage { get; set; }
    public Multiplier Multiplier { get; set; }
}

public class DcaOptionsValidator : IValidateOptions<DcaOptions>
{
    public ValidateOptionsResult Validate(string? name, DcaOptions options)
    {
        var errors = new List<string>();

        // BaseDailyAmount, BearBoostFactor, MaxMultiplierCap positivity enforced by value objects at binding

        if (options.DailyBuyHour < 0 || options.DailyBuyHour > 23)
        {
            errors.Add("DailyBuyHour must be between 0 and 23");
        }

        if (options.DailyBuyMinute < 0 || options.DailyBuyMinute > 59)
        {
            errors.Add("DailyBuyMinute must be between 0 and 59");
        }

        if (options.HighLookbackDays <= 0)
        {
            errors.Add("HighLookbackDays must be greater than 0");
        }

        if (options.BearMarketMaPeriod <= 0)
        {
            errors.Add("BearMarketMaPeriod must be greater than 0");
        }

        // Cross-field business rule: cap should not reduce (must be >= 1)
        if (options.MaxMultiplierCap.Value < 1)
        {
            errors.Add("MaxMultiplierCap must be at least 1.0 (no multiplier reduction)");
        }

        // Validate multiplier tiers
        if (options.MultiplierTiers.Any())
        {
            // Multiplier > 0 enforced by value object at binding; keep ascending sort check
            // Check that tiers are sorted by DropPercentage ascending
            var sortedTiers = options.MultiplierTiers.OrderBy(t => t.DropPercentage.Value).ToList();
            if (!options.MultiplierTiers.SequenceEqual(sortedTiers, new MultiplierTierComparer()))
            {
                errors.Add("MultiplierTiers must be sorted by DropPercentage in ascending order");
            }
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }

    private class MultiplierTierComparer : IEqualityComparer<MultiplierTier>
    {
        public bool Equals(MultiplierTier? x, MultiplierTier? y)
        {
            if (x == null || y == null) return false;
            return x.DropPercentage == y.DropPercentage && x.Multiplier == y.Multiplier;
        }

        public int GetHashCode(MultiplierTier obj)
        {
            return HashCode.Combine(obj.DropPercentage, obj.Multiplier);
        }
    }
}

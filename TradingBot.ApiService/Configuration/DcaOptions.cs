using Microsoft.Extensions.Options;

namespace TradingBot.ApiService.Configuration;

public class DcaOptions
{
    public decimal BaseDailyAmount { get; set; }
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int HighLookbackDays { get; set; } = 30;
    public int BearMarketMaPeriod { get; set; } = 200;
    public decimal BearBoostFactor { get; set; } = 1.5m;
    public bool DryRun { get; set; } = false;
    public List<MultiplierTier> MultiplierTiers { get; set; } = [];
}

public class MultiplierTier
{
    public decimal DropPercentage { get; set; }
    public decimal Multiplier { get; set; }
}

public class DcaOptionsValidator : IValidateOptions<DcaOptions>
{
    public ValidateOptionsResult Validate(string? name, DcaOptions options)
    {
        var errors = new List<string>();

        if (options.BaseDailyAmount <= 0)
        {
            errors.Add("BaseDailyAmount must be greater than 0");
        }

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

        if (options.BearBoostFactor <= 0)
        {
            errors.Add("BearBoostFactor must be greater than 0");
        }

        // Validate multiplier tiers
        if (options.MultiplierTiers.Any())
        {
            // Check that all multipliers are positive
            var invalidMultipliers = options.MultiplierTiers.Where(t => t.Multiplier <= 0).ToList();
            if (invalidMultipliers.Any())
            {
                errors.Add("All MultiplierTiers must have Multiplier > 0");
            }

            // Check that drop percentages are positive
            var invalidDrops = options.MultiplierTiers.Where(t => t.DropPercentage < 0).ToList();
            if (invalidDrops.Any())
            {
                errors.Add("All MultiplierTiers must have DropPercentage >= 0");
            }

            // Check that tiers are sorted by DropPercentage ascending
            var sortedTiers = options.MultiplierTiers.OrderBy(t => t.DropPercentage).ToList();
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

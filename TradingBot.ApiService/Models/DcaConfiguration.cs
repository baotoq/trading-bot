using ErrorOr;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class DcaConfiguration : AggregateRoot<DcaConfigurationId>
{
    protected DcaConfiguration() { }

    public UsdAmount BaseDailyAmount { get; private set; }
    public int DailyBuyHour { get; private set; }
    public int DailyBuyMinute { get; private set; }
    public int HighLookbackDays { get; private set; }
    public bool DryRun { get; private set; }
    public int BearMarketMaPeriod { get; private set; }
    public Multiplier BearBoostFactor { get; private set; }
    public Multiplier MaxMultiplierCap { get; private set; }
    // Stored as jsonb -- keep raw decimals per research (Pitfall 3: jsonb STJ serialization)
    public List<MultiplierTierData> MultiplierTiers { get; private set; } = [];

    public static DcaConfiguration Create(
        DcaConfigurationId id,
        UsdAmount baseDailyAmount,
        int dailyBuyHour,
        int dailyBuyMinute,
        int highLookbackDays,
        bool dryRun,
        int bearMarketMaPeriod,
        Multiplier bearBoostFactor,
        Multiplier maxMultiplierCap,
        List<MultiplierTierData> multiplierTiers)
    {
        // Factory method stays throwing per locked decision
        var scheduleErrors = ValidateScheduleErrors(dailyBuyHour, dailyBuyMinute);
        if (scheduleErrors.Count > 0)
            throw new ArgumentException(scheduleErrors[0].Description);

        var tierErrors = ValidateTierErrors(multiplierTiers);
        if (tierErrors.Count > 0)
            throw new ArgumentException(tierErrors[0].Description);

        var config = new DcaConfiguration
        {
            Id = id,
            BaseDailyAmount = baseDailyAmount,
            DailyBuyHour = dailyBuyHour,
            DailyBuyMinute = dailyBuyMinute,
            HighLookbackDays = highLookbackDays,
            DryRun = dryRun,
            BearMarketMaPeriod = bearMarketMaPeriod,
            BearBoostFactor = bearBoostFactor,
            MaxMultiplierCap = maxMultiplierCap,
            MultiplierTiers = multiplierTiers
        };

        config.AddDomainEvent(new DcaConfigurationCreatedEvent(id, DateTimeOffset.UtcNow));
        return config;
    }

    public void UpdateDailyAmount(UsdAmount amount)
    {
        BaseDailyAmount = amount;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id, DateTimeOffset.UtcNow));
    }

    public ErrorOr<Updated> UpdateSchedule(int hour, int minute)
    {
        var errors = ValidateScheduleErrors(hour, minute);
        if (errors.Count > 0)
            return errors;

        DailyBuyHour = hour;
        DailyBuyMinute = minute;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id, DateTimeOffset.UtcNow));
        return Result.Updated;
    }

    public ErrorOr<Updated> UpdateTiers(List<MultiplierTierData> tiers)
    {
        var errors = ValidateTierErrors(tiers);
        if (errors.Count > 0)
            return errors;

        MultiplierTiers = tiers;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id, DateTimeOffset.UtcNow));
        return Result.Updated;
    }

    public ErrorOr<Updated> UpdateBearMarket(int maPeriod, Multiplier boostFactor)
    {
        if (maPeriod <= 0)
            return DcaConfigurationErrors.InvalidMaPeriod;

        BearMarketMaPeriod = maPeriod;
        BearBoostFactor = boostFactor;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id, DateTimeOffset.UtcNow));
        return Result.Updated;
    }

    public ErrorOr<Updated> UpdateSettings(int highLookbackDays, bool dryRun, Multiplier maxMultiplierCap)
    {
        if (highLookbackDays <= 0)
            return DcaConfigurationErrors.InvalidHighLookbackDays;

        HighLookbackDays = highLookbackDays;
        DryRun = dryRun;
        MaxMultiplierCap = maxMultiplierCap;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id, DateTimeOffset.UtcNow));
        return Result.Updated;
    }

    private static List<Error> ValidateScheduleErrors(int hour, int minute)
    {
        var errors = new List<Error>();
        if (hour < 0 || hour > 23)
            errors.Add(DcaConfigurationErrors.InvalidScheduleHour);
        if (minute < 0 || minute > 59)
            errors.Add(DcaConfigurationErrors.InvalidScheduleMinute);
        return errors;
    }

    private static List<Error> ValidateTierErrors(List<MultiplierTierData> tiers)
    {
        var errors = new List<Error>();

        if (tiers == null || tiers.Count == 0)
            return errors;

        if (!tiers.OrderBy(t => t.DropPercentage).SequenceEqual(tiers))
            errors.Add(DcaConfigurationErrors.TiersNotAscending);

        if (tiers.Any(t => t.Multiplier <= 0 || t.Multiplier > 20))
            errors.Add(DcaConfigurationErrors.TierMultiplierOutOfRange);

        if (tiers.Select(t => t.DropPercentage).Distinct().Count() != tiers.Count)
            errors.Add(DcaConfigurationErrors.TierDropPercentageDuplicate);

        return errors;
    }
}

public record MultiplierTierData(decimal DropPercentage, decimal Multiplier);

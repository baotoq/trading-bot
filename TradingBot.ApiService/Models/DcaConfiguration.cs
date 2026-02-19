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
        ValidateSchedule(dailyBuyHour, dailyBuyMinute);
        ValidateTiers(multiplierTiers);

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

        config.AddDomainEvent(new DcaConfigurationCreatedEvent(id));
        return config;
    }

    public void UpdateDailyAmount(UsdAmount amount)
    {
        BaseDailyAmount = amount;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateSchedule(int hour, int minute)
    {
        ValidateSchedule(hour, minute);
        DailyBuyHour = hour;
        DailyBuyMinute = minute;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateTiers(List<MultiplierTierData> tiers)
    {
        ValidateTiers(tiers);
        MultiplierTiers = tiers;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateBearMarket(int maPeriod, Multiplier boostFactor)
    {
        if (maPeriod <= 0)
            throw new ArgumentException("MA period must be greater than 0", nameof(maPeriod));

        BearMarketMaPeriod = maPeriod;
        BearBoostFactor = boostFactor;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateSettings(int highLookbackDays, bool dryRun, Multiplier maxMultiplierCap)
    {
        if (highLookbackDays <= 0)
            throw new ArgumentException("High lookback days must be greater than 0", nameof(highLookbackDays));

        HighLookbackDays = highLookbackDays;
        DryRun = dryRun;
        MaxMultiplierCap = maxMultiplierCap;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    private static void ValidateSchedule(int hour, int minute)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentException("Hour must be between 0 and 23", nameof(hour));
        if (minute < 0 || minute > 59)
            throw new ArgumentException("Minute must be between 0 and 59", nameof(minute));
    }

    private static void ValidateTiers(List<MultiplierTierData> tiers)
    {
        if (tiers == null || tiers.Count == 0)
            return;

        if (!tiers.OrderBy(t => t.DropPercentage).SequenceEqual(tiers))
            throw new ArgumentException("Tiers must be ordered by ascending drop percentage", nameof(tiers));

        if (tiers.Any(t => t.Multiplier <= 0 || t.Multiplier > 20))
            throw new ArgumentException("Tier multipliers must be between 0 (exclusive) and 20 (inclusive)", nameof(tiers));

        if (tiers.Select(t => t.DropPercentage).Distinct().Count() != tiers.Count)
            throw new ArgumentException("Tier drop percentages must be unique", nameof(tiers));
    }
}

public record MultiplierTierData(decimal DropPercentage, decimal Multiplier);

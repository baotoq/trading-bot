using ErrorOr;

namespace TradingBot.ApiService.Models;

public static class DcaConfigurationErrors
{
    public static readonly Error InvalidScheduleHour = Error.Validation(
        "InvalidScheduleHour", "Hour must be between 0 and 23");

    public static readonly Error InvalidScheduleMinute = Error.Validation(
        "InvalidScheduleMinute", "Minute must be between 0 and 59");

    public static readonly Error TiersNotAscending = Error.Validation(
        "TiersNotAscending", "Tiers must be ordered by ascending drop percentage");

    public static readonly Error TierMultiplierOutOfRange = Error.Validation(
        "TierMultiplierOutOfRange", "Tier multipliers must be between 0 (exclusive) and 20 (inclusive)");

    public static readonly Error TierDropPercentageDuplicate = Error.Validation(
        "TierDropPercentageDuplicate", "Tier drop percentages must be unique");

    public static readonly Error InvalidMaPeriod = Error.Validation(
        "InvalidMaPeriod", "MA period must be greater than 0");

    public static readonly Error InvalidHighLookbackDays = Error.Validation(
        "InvalidHighLookbackDays", "High lookback days must be greater than 0");
}

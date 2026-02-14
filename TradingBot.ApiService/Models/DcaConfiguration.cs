using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Models;

public class DcaConfiguration : AuditedEntity
{
    // Fixed ID for singleton pattern
    public Guid Id { get; init; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public decimal BaseDailyAmount { get; set; }
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int HighLookbackDays { get; set; }
    public bool DryRun { get; set; }
    public int BearMarketMaPeriod { get; set; }
    public decimal BearBoostFactor { get; set; }
    public decimal MaxMultiplierCap { get; set; }
    public List<MultiplierTierData> MultiplierTiers { get; set; } = [];
}

public record MultiplierTierData(decimal DropPercentage, decimal Multiplier);

using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class DcaConfiguration : BaseEntity<DcaConfigurationId>
{

    public UsdAmount BaseDailyAmount { get; set; }
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int HighLookbackDays { get; set; }
    public bool DryRun { get; set; }
    public int BearMarketMaPeriod { get; set; }
    public Multiplier BearBoostFactor { get; set; }
    public Multiplier MaxMultiplierCap { get; set; }
    // Stored as jsonb -- keep raw decimals per research (Pitfall 3: jsonb STJ serialization)
    public List<MultiplierTierData> MultiplierTiers { get; set; } = [];
}

public record MultiplierTierData(decimal DropPercentage, decimal Multiplier);

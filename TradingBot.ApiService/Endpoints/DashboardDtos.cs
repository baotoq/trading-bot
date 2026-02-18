using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Endpoints;

// Portfolio overview
public record PortfolioResponse(
    Quantity TotalBtc,
    UsdAmount TotalCost,
    Price AverageCostBasis,
    Price CurrentPrice,
    decimal UnrealizedPnl,         // Stays decimal: can be negative (no value object fits)
    decimal UnrealizedPnlPercent,  // Stays decimal: can be negative
    int TotalPurchaseCount,
    DateTimeOffset? FirstPurchaseDate,
    DateTimeOffset? LastPurchaseDate
);

// Purchase history with cursor pagination
public record PurchaseHistoryResponse(
    List<PurchaseDto> Items,
    string? NextCursor,
    bool HasMore
);

public record PurchaseDto(
    Guid Id,
    DateTimeOffset ExecutedAt,
    Price Price,
    UsdAmount Cost,
    Quantity Quantity,
    string MultiplierTier,
    Multiplier Multiplier,
    Percentage DropPercentage
);

// Live status
public record LiveStatusResponse(
    string HealthStatus,         // "Healthy", "Warning", "Error"
    string? HealthMessage,
    DateTimeOffset? NextBuyTime,
    DateTimeOffset? LastPurchaseTime,
    Price? LastPurchasePrice,
    Quantity? LastPurchaseBtc,
    string? LastPurchaseTier
);

// Price chart data
public record PriceChartResponse(
    List<PricePointDto> Prices,
    List<PurchaseMarkerDto> Purchases,
    Price AverageCostBasis
);

public record PricePointDto(string Date, Price Price);

public record PurchaseMarkerDto(string Date, Price Price, Quantity BtcAmount, string Tier);

// DCA config for backtest form pre-fill
public record DcaConfigResponse(
    UsdAmount BaseDailyAmount,
    int HighLookbackDays,
    int BearMarketMaPeriod,
    Multiplier BearBoostFactor,
    Multiplier MaxMultiplierCap,
    List<MultiplierTierDto> Tiers);

public record MultiplierTierDto(Percentage DropPercentage, Multiplier Multiplier);

// Configuration management (full config with all fields)
public record ConfigResponse(
    UsdAmount BaseDailyAmount,
    int DailyBuyHour,
    int DailyBuyMinute,
    int HighLookbackDays,
    bool DryRun,
    int BearMarketMaPeriod,
    Multiplier BearBoostFactor,
    Multiplier MaxMultiplierCap,
    List<MultiplierTierDto> Tiers);

public record UpdateConfigRequest(
    UsdAmount BaseDailyAmount,
    int DailyBuyHour,
    int DailyBuyMinute,
    int HighLookbackDays,
    bool DryRun,
    int BearMarketMaPeriod,
    Multiplier BearBoostFactor,
    Multiplier MaxMultiplierCap,
    List<MultiplierTierDto> Tiers);

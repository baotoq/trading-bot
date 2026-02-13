namespace TradingBot.ApiService.Endpoints;

// Portfolio overview
public record PortfolioResponse(
    decimal TotalBtc,
    decimal TotalCost,
    decimal AverageCostBasis,
    decimal CurrentPrice,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlPercent,
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
    decimal Price,
    decimal Cost,
    decimal Quantity,
    string MultiplierTier,
    decimal Multiplier,
    decimal DropPercentage
);

// Live status
public record LiveStatusResponse(
    string HealthStatus,         // "Healthy", "Warning", "Error"
    string? HealthMessage,
    DateTimeOffset? NextBuyTime,
    DateTimeOffset? LastPurchaseTime,
    decimal? LastPurchasePrice,
    decimal? LastPurchaseBtc,
    string? LastPurchaseTier
);

// Price chart data
public record PriceChartResponse(
    List<PricePointDto> Prices,
    List<PurchaseMarkerDto> Purchases,
    decimal AverageCostBasis
);

public record PricePointDto(string Date, decimal Price);

public record PurchaseMarkerDto(string Date, decimal Price, decimal BtcAmount, string Tier);

namespace TradingBot.ApiService.Endpoints;

// Portfolio summary
public record PortfolioSummaryResponse(
    decimal TotalValueUsd,
    decimal TotalValueVnd,
    decimal TotalCostUsd,
    decimal TotalCostVnd,
    decimal UnrealizedPnlUsd,
    decimal UnrealizedPnlVnd,
    decimal? UnrealizedPnlPercent,
    List<AllocationDto> Allocations,
    DateTimeOffset? ExchangeRateUpdatedAt
);

public record AllocationDto(string AssetType, decimal ValueUsd, decimal ValueVnd, decimal Percentage);

// Per-asset breakdown
public record PortfolioAssetResponse(
    Guid Id,
    string Name,
    string Ticker,
    string AssetType,
    string NativeCurrency,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal CurrentValueUsd,
    decimal CurrentValueVnd,
    decimal UnrealizedPnlUsd,
    decimal? UnrealizedPnlPercent,
    DateTimeOffset? PriceUpdatedAt,
    bool IsPriceStale
);

// Transaction request
public record CreateTransactionRequest(
    DateOnly Date,
    decimal Quantity,
    decimal PricePerUnit,
    string Currency,
    string Type,
    decimal? Fee
);

// Transaction response
public record TransactionResponse(
    Guid Id,
    DateOnly Date,
    decimal Quantity,
    decimal PricePerUnit,
    string Currency,
    string Type,
    decimal? Fee,
    string Source
);

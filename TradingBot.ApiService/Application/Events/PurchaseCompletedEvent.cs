using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(
    Guid PurchaseId,
    decimal BtcAmount,
    decimal Price,
    decimal UsdSpent,
    decimal RemainingUsdc,
    decimal CurrentBtcBalance,
    DateTimeOffset ExecutedAt,
    // Multiplier metadata for rich notifications
    decimal Multiplier,
    string? MultiplierTier,
    decimal DropPercentage,
    decimal High30Day,
    decimal Ma200Day,
    bool IsDryRun
) : IDomainEvent;

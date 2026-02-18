using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(
    PurchaseId PurchaseId,
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

using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(
    PurchaseId PurchaseId,
    Quantity BtcAmount,
    Price Price,
    UsdAmount UsdSpent,
    decimal RemainingUsdc,      // Stays decimal: can legitimately be 0 when depleted
    Quantity CurrentBtcBalance,
    DateTimeOffset ExecutedAt,
    // Multiplier metadata for rich notifications
    Multiplier Multiplier,
    string? MultiplierTier,
    Percentage DropPercentage,
    decimal High30Day,          // Stays decimal: uses 0 sentinel for "data unavailable"
    decimal Ma200Day,           // Stays decimal: uses 0 sentinel for "data unavailable"
    bool IsDryRun
) : IDomainEvent;

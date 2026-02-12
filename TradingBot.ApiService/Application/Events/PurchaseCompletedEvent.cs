using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(
    Guid PurchaseId,
    decimal BtcAmount,
    decimal Price,
    decimal UsdSpent,
    decimal RemainingUsdc,
    decimal CurrentBtcBalance,
    DateTimeOffset ExecutedAt
) : IDomainEvent;

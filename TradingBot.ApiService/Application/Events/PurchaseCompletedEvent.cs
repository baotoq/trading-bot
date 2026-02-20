using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(
    PurchaseId PurchaseId,
    decimal Price,
    decimal Quantity,
    decimal Cost,
    decimal Multiplier,
    bool IsDryRun,
    decimal TotalBtc,
    decimal TotalCost,
    int PurchaseCount,
    DateTimeOffset OccurredAt) : IDomainEvent;

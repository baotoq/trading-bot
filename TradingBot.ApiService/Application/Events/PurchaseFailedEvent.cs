using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseFailedEvent(
    PurchaseId PurchaseId,
    string? FailureReason,
    DateTimeOffset OccurredAt) : IDomainEvent;

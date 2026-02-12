using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseFailedEvent(
    string ErrorType,
    string ErrorMessage,
    int RetryCount,
    DateTimeOffset FailedAt
) : IDomainEvent;

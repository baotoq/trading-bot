using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseSkippedEvent(
    string Reason,
    decimal? CurrentBalance,
    decimal? RequiredAmount,
    DateTimeOffset SkippedAt
) : IDomainEvent;

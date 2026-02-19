using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public interface IDomainEventPublisher
{
    Task PublishDirectAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

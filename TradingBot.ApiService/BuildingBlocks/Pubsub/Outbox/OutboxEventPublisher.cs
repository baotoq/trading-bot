using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxEventPublisher(IOutboxStore outboxStore) : IEventPublisher
{
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await outboxStore.AddAsync(new OutboxMessage
        {
            EventName = typeof(T).Name,
            Payload = JsonSerializer.Serialize(@event),
            ProcessingStatus = ProcessingStatus.Pending
        }, cancellationToken);
    }
}
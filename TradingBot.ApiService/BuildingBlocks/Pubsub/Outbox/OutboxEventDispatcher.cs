using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxEventDispatcher(DbContext context) : IEventDispatcher
{
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await context.Set<OutboxMessage>().AddAsync(new OutboxMessage
        {
            EventName = typeof(T).Name,
            Data = JsonSerializer.Serialize(@event),
            ProcessingStatus = ProcessingStatus.Pending
        }, cancellationToken);
    }
}
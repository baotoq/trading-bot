using Dapr.Client;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public class DaprEventDispatcher(PubSubRegistry registry, DaprClient client) : IEventDispatcher
{
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await client.PublishEventAsync(registry.Name, typeof(T).Name.ToLower(), @event, cancellationToken);
    }
}
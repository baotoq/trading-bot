using Dapr.Client;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public class DaprMessageBroker(PubSubRegistry registry, DaprClient daprClient) : IMessageBroker
{
    public async Task PublishAsync<TData>(
        string topic,
        TData data,
        CancellationToken cancellationToken = default)
    {
        await daprClient.PublishEventAsync(
            registry.Name,
            topic,
            data,
            cancellationToken);
    }
}

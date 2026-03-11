using Dapr.Client;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public class DaprEventPublisher(IMessageBroker messageBroker) : IEventPublisher
{
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await messageBroker.PublishAsync(typeof(T).Name, @event, cancellationToken);
    }
}
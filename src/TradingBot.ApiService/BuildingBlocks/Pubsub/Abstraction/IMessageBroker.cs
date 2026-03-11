namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;

public interface IMessageBroker
{
    Task PublishAsync<TData>(
        string topic,
        TData data,
        CancellationToken cancellationToken = default);
}
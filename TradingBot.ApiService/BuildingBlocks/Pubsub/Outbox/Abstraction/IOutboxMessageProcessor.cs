namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

public interface IOutboxMessageProcessor
{
    Task ProcessOutboxMessagesAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
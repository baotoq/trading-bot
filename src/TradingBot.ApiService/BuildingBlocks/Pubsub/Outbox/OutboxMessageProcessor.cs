using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxMessageProcessor(
        ILogger<OutboxMessageProcessor> logger,
        IOutboxStore outboxStore,
        IMessageBroker messageBroker
    ) : IOutboxMessageProcessor
{
    public async Task ProcessOutboxMessagesAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (message.RetryCount >= 3)
            {
                logger.LogWarning("Message {MessageId} {EventName} exceeded max retry count, moving to dead-letter", message.Id, message.EventName);
                await outboxStore.MoveToDeadLetterAsync(message, null, cancellationToken);
                return;
            }

            logger.LogInformation("Processing outbox message {EventName} {MessageId}", message.EventName, message.Id);

            await outboxStore.MarkAsAsync(message.Id, ProcessingStatus.Processing, cancellationToken);
            await messageBroker.PublishAsync(message.EventName.ToLower(), message.Payload, cancellationToken);
            await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);

            logger.LogInformation("Successfully processed outbox message {EventName} {MessageId}", message.EventName, message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
            await outboxStore.IncrementRetryAsync(message.Id, cancellationToken);
        }
    }
}
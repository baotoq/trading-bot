using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public interface IOutboxMessageProcessorService
{
    Task ProcessOutboxMessagesAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public class OutboxMessageProcessorService(
        ILogger<OutboxMessageProcessorService> logger,
        DbContext context,
        DaprClient daprClient,
        PubSubRegistry registry
    ) : IOutboxMessageProcessorService
{
    public async Task ProcessOutboxMessagesAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            logger.LogInformation("Processing outbox message {EventName} {MessageId}", message.EventName, message.Id);

            message.ProcessingStatus = ProcessingStatus.Published;
            await daprClient.PublishEventAsync(registry.Name, message.EventName.ToLower(), message.Data, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Successfully processed outbox message {EventName} {MessageId}", message.EventName, message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
            message.ProcessingStatus = ProcessingStatus.Failed;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
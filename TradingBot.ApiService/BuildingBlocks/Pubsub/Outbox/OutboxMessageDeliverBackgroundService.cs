using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxMessageDeliverBackgroundService(
        Type dbContext,
        IServiceProvider services,
        ILogger<OutboxMessageDeliverBackgroundService> logger
    ) : TimeBackgroundService(logger)
{
    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService(dbContext) as DbContext;
        var dapr = scope.ServiceProvider.GetRequiredService<DaprClient>();
        var registry = scope.ServiceProvider.GetRequiredService<PubSubRegistry>();

        var pendingMessages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessingStatus == ProcessingStatus.Pending)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                logger.LogInformation("Processing outbox message {EventName} {MessageId}", message.EventName, message.Id);

                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                message.ProcessingStatus = ProcessingStatus.Published;
                await dapr.PublishEventAsync(registry.Name, message.EventName.ToLower(), message.Data, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Successfully processed outbox message {EventName} {MessageId}", message.EventName, message.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
                message.ProcessingStatus = ProcessingStatus.Failed;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
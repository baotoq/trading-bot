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
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessorService>();

        var pendingMessages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessingStatus == ProcessingStatus.Pending)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            await outboxProcessor.ProcessOutboxMessagesAsync(message, cancellationToken);
        }
    }
}
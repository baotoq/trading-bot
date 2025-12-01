using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxMessageBackgroundService(
        IServiceProvider services,
        ILogger<OutboxMessageBackgroundService> logger
    ) : TimeBackgroundService(logger)
{
    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();

        var pendingMessages = await outboxStore.GetUnprocessedAsync(100, cancellationToken);

        foreach (var message in pendingMessages)
        {
            await outboxProcessor.ProcessOutboxMessagesAsync(message, cancellationToken);
        }
    }
}
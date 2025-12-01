namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

public interface IOutboxStore
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task MarkAsAsync(Guid messageId, ProcessingStatus status, CancellationToken cancellationToken = default);
    Task IncrementRetryAsync(Guid messageId, CancellationToken cancellationToken = default);
}
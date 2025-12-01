using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;

public class EfCoreOutboxStore(DbContext dbContext) : IOutboxStore
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<OutboxMessage>().AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetUnprocessedAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessingStatus == ProcessingStatus.Pending)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Set<OutboxMessage>()
            .FindAsync([messageId], cancellationToken);

        if (message != null)
        {
            message.MarkAsPublished();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsAsync(Guid messageId, ProcessingStatus status, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Set<OutboxMessage>()
            .FindAsync([messageId], cancellationToken);

        if (message != null)
        {
            message.ProcessingStatus = status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task IncrementRetryAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Set<OutboxMessage>()
            .FindAsync([messageId], cancellationToken);

        if (message != null)
        {
            message.RetryCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
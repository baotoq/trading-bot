using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class DomainEventPublisher(DbContext dbContext, JsonSerializerOptions jsonSerializerOptions) : IDomainEventPublisher
{
    public async Task PublishDirectAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var outboxMessage = new OutboxMessage
        {
            EventName = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonSerializerOptions),
            ProcessingStatus = ProcessingStatus.Pending,
        };

        await dbContext.Set<OutboxMessage>().AddAsync(outboxMessage, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}

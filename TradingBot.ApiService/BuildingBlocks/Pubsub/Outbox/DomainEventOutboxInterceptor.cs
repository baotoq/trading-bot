using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class DomainEventOutboxInterceptor(JsonSerializerOptions jsonOptions) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            InsertOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            InsertOutboxMessages(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void InsertOutboxMessages(Microsoft.EntityFrameworkCore.DbContext context)
    {
        var aggregates = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var outboxMessages = new List<OutboxMessage>();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                outboxMessages.Add(new OutboxMessage
                {
                    EventName = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions),
                    ProcessingStatus = ProcessingStatus.Pending
                });
            }

            aggregate.ClearDomainEvents();
        }

        if (outboxMessages.Count > 0)
            context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}

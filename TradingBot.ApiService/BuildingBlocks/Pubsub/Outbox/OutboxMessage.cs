namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class AuditedEntity
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class OutboxMessage : AuditedEntity
{
    public Guid Id { get; init; }
    public required string EventName { get; init; }
    public required string Data { get; init; }
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;
    public DateTimeOffset? PublishedAt { get; init; }

    public OutboxMessage()
    {
        Id = Guid.CreateVersion7(CreatedAt);
    }
}

public enum ProcessingStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}
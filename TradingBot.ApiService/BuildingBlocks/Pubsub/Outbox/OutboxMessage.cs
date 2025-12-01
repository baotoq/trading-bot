namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class OutboxMessage : AuditedEntity
{
    public Guid Id { get; init; }
    public required string EventName { get; init; }
    public required string Payload { get; init; }
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;
    public DateTimeOffset? PublishedAt { get; set; }
    public int RetryCount { get; set; } = 0;

    public OutboxMessage()
    {
        Id = Guid.CreateVersion7(CreatedAt);
    }

    public void MarkAsPublished()
    {
        ProcessingStatus = ProcessingStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
    }
}

public enum ProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    Failed = 3
}
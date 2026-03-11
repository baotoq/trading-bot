using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public class DeadLetterMessage : AuditedEntity
{
    public Guid Id { get; init; }
    public required string EventName { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset FailedAt { get; init; }
    public string? LastError { get; set; }
    public int RetryCount { get; init; }

    public DeadLetterMessage()
    {
        Id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
    }
}

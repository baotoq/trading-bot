namespace TradingBot.ApiService.Infrastructure;

public class AuditedEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OutboxMessage : AuditedEntity
{
    public Guid Id { get; init; }
    public string Topic { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public ProcessingStatus ProcessingStatus { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public OutboxMessage()
    {
        Id = Guid.CreateVersion7(CreatedAt);
    }
}

public enum ProcessingStatus
{
    Pending,
    Published,
    Failed
}
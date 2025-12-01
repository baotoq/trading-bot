namespace TradingBot.ApiService.BuildingBlocks;

public class AuditedEntity
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

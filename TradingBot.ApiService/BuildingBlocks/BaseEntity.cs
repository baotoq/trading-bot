namespace TradingBot.ApiService.BuildingBlocks;

public abstract class BaseEntity<TId> : AuditedEntity
{
    public TId Id { get; init; } = default!;
}

// Backward-compatible alias -- removed in Plan 02 when entities switch to BaseEntity<TId>
public abstract class BaseEntity : BaseEntity<Guid>
{
    public BaseEntity()
    {
        Id = Guid.CreateVersion7(CreatedAt);
    }
}

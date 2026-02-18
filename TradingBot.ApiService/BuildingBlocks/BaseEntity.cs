namespace TradingBot.ApiService.BuildingBlocks;

public abstract class BaseEntity<TId> : AuditedEntity
{
    public TId Id { get; init; } = default!;
}

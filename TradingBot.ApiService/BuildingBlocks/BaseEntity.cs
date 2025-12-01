namespace TradingBot.ApiService.BuildingBlocks;

public abstract class BaseEntity : AuditedEntity
{
    public Guid Id { get; init; }

    public BaseEntity()
    {
        Id = Guid.CreateVersion7(CreatedAt);
    }
}

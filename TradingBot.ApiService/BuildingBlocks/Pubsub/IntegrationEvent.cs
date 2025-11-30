using MediatR;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub;

public interface IIntegrationEvent : INotification
{
}

public record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredOn { get; }

    public IntegrationEvent()
    {
        OccurredOn = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7(OccurredOn);
    }
}

public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent> where TEvent : IntegrationEvent
{
}
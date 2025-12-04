using MediatR;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;

public interface IIntegrationEvent : INotification
{
}

public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent> where TEvent : IntegrationEvent
{
    new Task Handle(TEvent @event, CancellationToken cancellationToken);
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredOn { get; init; }

    public IntegrationEvent()
    {
        OccurredOn = DateTimeOffset.UtcNow;
        Id = Guid.CreateVersion7(OccurredOn);
    }
}

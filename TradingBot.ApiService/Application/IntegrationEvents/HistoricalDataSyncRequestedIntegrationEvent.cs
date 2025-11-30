using TradingBot.ApiService.BuildingBlocks.Pubsub;

namespace TradingBot.ApiService.Application.IntegrationEvents;

public record HistoricalDataSyncRequestedIntegrationEvent : IntegrationEvent
{
    public string Symbol { get; init; }
    public string Interval { get; init; }

    public HistoricalDataSyncRequestedIntegrationEvent(string symbol, string interval)
    {
        Symbol = symbol;
        Interval = interval;
    }
}

public class HistoricalDataSyncRequestedIntegrationEventHandler(ILogger<HistoricalDataSyncRequestedIntegrationEventHandler> logger) : IIntegrationEventHandler<HistoricalDataSyncRequestedIntegrationEvent>
{
    public async Task Handle(HistoricalDataSyncRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Hello");
    }
}
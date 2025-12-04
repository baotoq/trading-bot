using TradingBot.ApiService.Application.IntegrationEvents;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

public class SyncHistoricalBackgroundService(
    IServiceProvider services,
    ILogger<SyncHistoricalBackgroundService> logger
) : TimeBackgroundService(logger)
{
    private readonly Dictionary<CandleInterval, DateTimeOffset> _startTimes = new()
    {
        { "15m", DateTimeOffset.Parse("2025-12-01T00:00:00Z") },
        { "4h", DateTimeOffset.Parse("2025-01-01T00:00:00Z") },
        { "1d", DateTimeOffset.Parse("2025-01-01T00:00:00Z") },
    };
    private readonly Symbol[] _symbols = [
        "BTCUSDT",
    ];

    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(30);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting historical data sync at {Time}", DateTime.UtcNow);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            List<Task> syncTasks = [];
            foreach (var symbol in _symbols)
            {
                syncTasks.AddRange(_startTimes.Keys.Select(async interval =>
                {
                    var startTime = _startTimes[interval];

                    var @event = new HistoricalDataSyncRequestedIntegrationEvent
                    {
                        Symbol = symbol,
                        Interval = interval,
                        StartTime = startTime,
                    };

                    return bus.PublishAsync(@event, cancellationToken);
                }));

                logger.LogInformation("Publishing historical data sync request for {Symbol}", symbol);
            }

            await Task.WhenAll(syncTasks);

            await context.SaveChangesAsync(cancellationToken);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing historical data");
        }
    }
}

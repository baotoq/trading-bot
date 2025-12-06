using TradingBot.ApiService.Application.Candles.IntegrationEvents;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

public class SyncHistoricalBackgroundService(
    IServiceProvider services,
    ILogger<SyncHistoricalBackgroundService> logger
) : TimeBackgroundService(logger)
{
    // Minimum 3 months of historical data for all intervals
    private static readonly DateTimeOffset ThreeMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-3);

    private readonly Dictionary<CandleInterval, DateTimeOffset> _startTimes = new()
    {
        // Short timeframes: 3 months of data
        { "5m", ThreeMonthsAgo },
        { "15m", ThreeMonthsAgo },
        // Longer timeframes: 1 year of data for better trend analysis
        { "4h", DateTimeOffset.UtcNow.AddYears(-1) },
        { "1d", DateTimeOffset.UtcNow.AddYears(-1) },
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

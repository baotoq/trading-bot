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
    private readonly CandleInterval[] _intervals = ["1h", "4h"];
    private const string Symbol = "BTCUSDT";

    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(10);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting historical data sync for {Symbol} at {Time}", Symbol, DateTime.UtcNow);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var syncTasks = _intervals.Select(interval =>
                bus.PublishAsync(new HistoricalDataSyncRequestedIntegrationEvent
                {
                    Symbol = Symbol,
                    Interval = interval
                }, cancellationToken));

            await Task.WhenAll(syncTasks);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Completed historical data sync for {Symbol}", Symbol);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing historical data for {Symbol}", Symbol);
        }
    }
}

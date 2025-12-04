using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

public class AutoMonitorBackgroundService(
    IServiceProvider services,
    ILogger<AutoMonitorBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            logger.LogInformation("Starting auto-monitor for BTCUSDT");

            await using var scope = services.CreateAsyncScope();
            var realtimeCandleService = scope.ServiceProvider.GetRequiredService<IRealtimeCandleService>();

            var symbol = new Symbol("BTCUSDT");
            var interval = new CandleInterval("1m");

            await realtimeCandleService.StartMonitoringAsync(symbol, interval, stoppingToken);

            logger.LogInformation("Successfully started monitoring {Symbol} on {Interval} interval", symbol, interval);

            // Optionally, you can also monitor 5m for futures/scalping strategies
            // var interval5m = new CandleInterval("5m");
            // await realtimeCandleService.StartMonitoringAsync(symbol, interval5m, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start auto-monitoring for BTCUSDT");
        }

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

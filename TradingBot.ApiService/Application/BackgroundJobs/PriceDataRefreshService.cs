using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.BackgroundJobs;

/// <summary>
/// Background service for daily price data refresh and historical data bootstrap.
/// Bootstraps 200 days of historical data on startup, then refreshes daily candle at 00:05 UTC.
/// Errors are logged but never crash the service to maintain DCA scheduler stability.
/// </summary>
public class PriceDataRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<PriceDataRefreshService> logger) : BackgroundService
{
    private const string Symbol = "BTC";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Step 1: Bootstrap historical data on startup
        await BootstrapOnStartupAsync(stoppingToken);

        // Step 2: Daily refresh loop at 00:05 UTC
        await DailyRefreshLoopAsync(stoppingToken);
    }

    private async Task BootstrapOnStartupAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting price data bootstrap for {Symbol}", Symbol);

            await using var scope = scopeFactory.CreateAsyncScope();
            var priceDataService = scope.ServiceProvider.GetRequiredService<IPriceDataService>();

            await priceDataService.BootstrapHistoricalDataAsync(Symbol, stoppingToken);

            logger.LogInformation("Price data bootstrap completed successfully for {Symbol}", Symbol);
        }
        catch (Exception ex)
        {
            // Log error but don't crash — DCA can work with existing data or manual refresh
            logger.LogError(ex, "Failed to bootstrap price data for {Symbol} on startup. DCA will continue with existing data.",
                Symbol);
        }
    }

    private async Task DailyRefreshLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next run time: 00:05 UTC today or tomorrow
                var targetTime = new TimeOnly(0, 5); // 5 minutes past midnight to let exchange finalize daily candle
                var now = DateTimeOffset.UtcNow;
                var todayTarget = new DateTimeOffset(
                    DateOnly.FromDateTime(now.UtcDateTime.Date).ToDateTime(targetTime),
                    TimeSpan.Zero
                );

                // If already past target time today, schedule for tomorrow
                var nextRun = todayTarget > now ? todayTarget : todayTarget.AddDays(1);
                var delay = nextRun - now;

                logger.LogInformation("Next price data refresh at {NextRun} UTC (in {Delay})",
                    nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                // Perform daily refresh
                await RefreshDailyCandleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                logger.LogInformation("Price data refresh service stopping");
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue loop — transient failures shouldn't stop daily refresh
                logger.LogError(ex, "Error in daily price refresh loop for {Symbol}. Will retry next cycle.", Symbol);

                // Wait 1 hour before retrying to avoid tight loop on persistent errors
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RefreshDailyCandleAsync(CancellationToken stoppingToken)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            logger.LogInformation("Starting daily candle refresh for {Symbol} on {Date}", Symbol, today);

            await using var scope = scopeFactory.CreateAsyncScope();
            var priceDataService = scope.ServiceProvider.GetRequiredService<IPriceDataService>();

            await priceDataService.FetchAndStoreDailyCandleAsync(Symbol, stoppingToken);

            logger.LogInformation("Daily candle refresh completed successfully for {Symbol} on {Date}", Symbol, today);
        }
        catch (Exception ex)
        {
            // Log error but don't rethrow — allow loop to continue
            logger.LogError(ex, "Failed to refresh daily candle for {Symbol}", Symbol);
        }
    }
}

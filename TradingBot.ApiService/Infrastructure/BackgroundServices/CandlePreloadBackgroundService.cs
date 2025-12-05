using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

public class CandlePreloadBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CandlePreloadBackgroundService> _logger;

    public CandlePreloadBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CandlePreloadBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            _logger.LogInformation("Starting candle data preload...");

            using var scope = _serviceProvider.CreateScope();
            var memoryCache = scope.ServiceProvider.GetRequiredService<IInMemoryCandleCache>();

            await memoryCache.PreloadDataAsync(stoppingToken);

            _logger.LogInformation("Candle data preload completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload candle data");
        }
    }
}

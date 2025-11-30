using Microsoft.EntityFrameworkCore;

namespace TradingBot.ApiService.Persistent;

public class EfCoreMigrationHostedService(
        IServiceProvider services,
        ILogger<EfCoreMigrationHostedService> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting EF Migration Hosted Service");
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync(stoppingToken);
        await context.Database.MigrateAsync(stoppingToken);

        logger.LogInformation("EF Migration Hosted Service completed");
    }
}
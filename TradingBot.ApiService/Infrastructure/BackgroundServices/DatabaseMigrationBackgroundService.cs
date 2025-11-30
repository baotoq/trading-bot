using Microsoft.EntityFrameworkCore;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

public class DatabaseMigrationBackgroundService(
        IServiceProvider services,
        ILogger<DatabaseMigrationBackgroundService> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting EF Migration Background Service");
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync(stoppingToken);
        await context.Database.MigrateAsync(stoppingToken);

        logger.LogInformation("EF Migration Background Service completed");
    }
}
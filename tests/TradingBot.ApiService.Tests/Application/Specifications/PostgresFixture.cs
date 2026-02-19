using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Tests.Application.Specifications;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("tradingbot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public TradingBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .EnableSensitiveDataLogging()  // For SQL verification during debugging
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options;
        return new TradingBotDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

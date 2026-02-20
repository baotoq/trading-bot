using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.PriceFeeds;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;
using TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

namespace TradingBot.ApiService.Tests.Endpoints;

/// <summary>
/// WebApplicationFactory for integration testing endpoint handlers.
/// Spins up a Testcontainers PostgreSQL database and mocks external services.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("tradingbot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    // Mocks exposed so test classes can configure return values per test
    public ICryptoPriceProvider CryptoMock { get; } = Substitute.For<ICryptoPriceProvider>();
    public IEtfPriceProvider EtfMock { get; } = Substitute.For<IEtfPriceProvider>();
    public IExchangeRateProvider ExchangeRateMock { get; } = Substitute.For<IExchangeRateProvider>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set test environment to suppress development-only features if needed
        builder.UseEnvironment("Testing");

        // Provide required environment values so Aspire-style connection string resolution
        // doesn't throw on missing config — we'll override the DB registration below.
        builder.UseSetting("ConnectionStrings:tradingbotdb", _container.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379"); // overridden below
        builder.UseSetting("Dashboard:ApiKey", "test-api-key");

        // Provide placeholder secrets so validation doesn't crash on startup
        builder.UseSetting("Hyperliquid:PrivateKey", "0x" + new string('1', 64));
        builder.UseSetting("Hyperliquid:WalletAddress", "0x" + new string('1', 40));
        builder.UseSetting("Telegram:BotToken", "0:test");
        builder.UseSetting("Telegram:ChatId", "0");

        builder.ConfigureServices(services =>
        {
            // ── 1. Replace DB context with Testcontainers PostgreSQL ──────────────────
            // Aspire registers DbContextOptions<TradingBotDbContext> via AddNpgsqlDbContext.
            // We remove that registration and add our own pointing at the test container.
            services.RemoveAll<DbContextOptions<TradingBotDbContext>>();
            services.RemoveAll<TradingBotDbContext>();

            services.AddDbContext<TradingBotDbContext>(opts =>
                opts.UseNpgsql(_container.GetConnectionString()));

            // ── 2. Replace Redis distributed cache with in-memory cache ───────────────
            // Aspire's AddRedisDistributedCache registers StackExchange.Redis; replace with
            // the simple in-memory implementation so tests run without a Redis sidecar.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            // ── 3. Mock price feed providers ──────────────────────────────────────────
            services.RemoveAll<ICryptoPriceProvider>();
            services.RemoveAll<IEtfPriceProvider>();
            services.RemoveAll<IExchangeRateProvider>();

            services.AddSingleton(CryptoMock);
            services.AddSingleton(EtfMock);
            services.AddSingleton(ExchangeRateMock);

            // ── 4. Mock Dapr/message broker (no Dapr sidecar in test) ─────────────────
            services.RemoveAll<IMessageBroker>();
            services.AddSingleton(Substitute.For<IMessageBroker>());

            // ── 5. Remove all background services (they hit external APIs) ────────────
            services.RemoveAll<IHostedService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Run EF Core migrations against the test database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition so the Testcontainers PostgreSQL instance is shared
/// across all endpoint test classes (started once per test run).
/// </summary>
[CollectionDefinition("Endpoints")]
public class EndpointsCollection : ICollectionFixture<CustomWebApplicationFactory>;

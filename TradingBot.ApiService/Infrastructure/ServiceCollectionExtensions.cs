using TradingBot.ApiService.Application.IntegrationEvents;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

namespace TradingBot.ApiService.Infrastructure;

public static class ServiceCollectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddPersistentServices()
        {
            builder.AddNpgsqlDbContext<ApplicationDbContext>("tradingbotdb");
            builder.Services.AddHostedService<EfCoreMigrationHostedService>();
            builder.Services.AddHostedService<SyncHistoricalHostedService>();
        }

        public void AddPubSubServices()
        {
            var registry = builder.Services.AddPubSub();

            registry.Subscribe<HistoricalDataSyncRequestedIntegrationEvent>();
        }
    }
}
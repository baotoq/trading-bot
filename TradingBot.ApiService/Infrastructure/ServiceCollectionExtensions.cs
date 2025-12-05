using TradingBot.ApiService.Application.Candles.IntegrationEvents;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;
using TradingBot.ApiService.Infrastructure.BackgroundServices;

namespace TradingBot.ApiService.Infrastructure;

public static class ServiceCollectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddPersistentServices()
        {
            builder.AddNpgsqlDbContext<ApplicationDbContext>("tradingbotdb");
            builder.Services.AddHostedService<DatabaseMigrationBackgroundService>();
            builder.Services.AddHostedService<SyncHistoricalBackgroundService>();
            builder.Services.AddHostedService<AutoMonitorBackgroundService>();
            builder.Services.AddHostedService<CandlePreloadBackgroundService>();
        }

        public void AddPubSubServices()
        {
            builder.Services.AddDaprPubSub()
                .Subscribe<HistoricalDataSyncRequestedIntegrationEvent>();

            builder.Services.AddOutboxPublishingWithEfCore<ApplicationDbContext>();

        }
    }
}
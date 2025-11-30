using TradingBot.ApiService.Application.IntegrationEvents;
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
            builder.Services.AddHostedService<OutboxMessageDeliverBackgroundService>();
            builder.Services.AddHostedService<SyncHistoricalBackgroundService>();
        }

        public void AddPubSubServices()
        {
            builder.Services.AddPubSub()
                .Subscribe<HistoricalDataSyncRequestedIntegrationEvent>();

            builder.Services.AddOutboxEfCore<ApplicationDbContext>();
        }
    }
}
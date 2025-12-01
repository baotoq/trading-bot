using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOutboxEfCore<T>() where T : DbContext
        {
            services.AddHostedService(p => new OutboxMessageDeliverBackgroundService(
                typeof(T),
                p.GetRequiredService<IServiceProvider>(),
                p.GetRequiredService<ILogger<OutboxMessageDeliverBackgroundService>>())
            );
            services.AddScoped<IEventDispatcher>(p => new OutboxEventDispatcher(p.GetRequiredService(typeof(T)) as DbContext));
            services.AddScoped<IOutboxMessageProcessorService, OutboxMessageProcessorService>();
            return services;
        }
    }
}
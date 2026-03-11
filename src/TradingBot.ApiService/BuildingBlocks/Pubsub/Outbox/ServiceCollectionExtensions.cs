using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;

public record OutboxProcessorOptions
{
}

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOutboxPublishingWithEfCore<TDbContext>(
            Action<OutboxProcessorOptions>? configureOptions = null)
            where TDbContext : DbContext
        {
            services.AddHostedService<OutboxMessageBackgroundService>();

            services.AddScoped<IOutboxStore>(sp => new EfCoreOutboxStore(sp.GetRequiredService<TDbContext>()));
            services.AddScoped<IEventPublisher, OutboxEventPublisher>();
            services.AddScoped<IOutboxMessageProcessor, OutboxMessageProcessor>();
            services.AddScoped<IDomainEventPublisher>(sp => new DomainEventPublisher(
                sp.GetRequiredService<TDbContext>(),
                sp.GetRequiredService<JsonSerializerOptions>()));

            services.AddSingleton(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            services.ConfigureOutboxOptions(configureOptions);

            return services;
        }

        private void ConfigureOutboxOptions(Action<OutboxProcessorOptions>? configureOptions)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<OutboxProcessorOptions>(options => { });
            }
        }
    }
}
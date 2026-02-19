using Dapr.Client;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public PubSubRegistry AddDaprPubSub()
        {
            var registry = new PubSubRegistry();

            services.AddDaprClient();
            services.AddSingleton(registry);
            services.AddScoped<IEventPublisher, DaprEventPublisher>();
            services.AddScoped<IMessageBroker, DaprMessageBroker>();

            return registry;
        }
    }

    extension(PubSubRegistry registry)
    {
        public PubSubRegistry Subscribe<TEvent>() where TEvent : class
        {
            registry.Add<TEvent>();
            return registry;
        }
    }
}
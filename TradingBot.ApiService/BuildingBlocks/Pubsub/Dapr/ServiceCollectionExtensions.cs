using Dapr.Client;
using TradingBot.ApiService.Application;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public PubSubRegistry AddPubSub()
        {
            var registry = new PubSubRegistry();

            services.AddDaprClient();
            services.AddSingleton(registry);
            services.AddScoped<IEventDispatcher, DaprEventDispatcher>();

            return registry;
        }
    }

    extension(PubSubRegistry registry)
    {
        public void Subscribe<TEvent>() where  TEvent : IntegrationEvent
        {
            registry.Add<TEvent>();
        }
    }
}
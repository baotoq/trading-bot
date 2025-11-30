using Dapr.Client;
using TradingBot.ApiService.Application;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public static class PubSubServiceExtensions
{
    public static PubSubRegistry AddPubSub(this IServiceCollection services)
    {
        var registry = new PubSubRegistry();

        services.AddDaprClient();
        services.AddSingleton(registry);
        services.AddScoped<IEventDispatcher>(p => new DaprEventDispatcher(registry, p.GetRequiredService<DaprClient>()));

        return registry;
    }

    public static void Subscribe<TEvent>(this PubSubRegistry registry) where  TEvent : IntegrationEvent
    {
        registry.Add<TEvent>();
    }
}
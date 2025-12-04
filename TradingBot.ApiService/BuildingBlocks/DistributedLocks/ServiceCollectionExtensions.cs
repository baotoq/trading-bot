using Dapr.DistributedLock.Extensions;

namespace TradingBot.ApiService.BuildingBlocks.DistributedLocks;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDistributedLock()
        {
            services.AddDaprDistributedLock();
            services.AddScoped<IDistributedLock, DaprDistributedLock>();

            return services;
        }
    }
}

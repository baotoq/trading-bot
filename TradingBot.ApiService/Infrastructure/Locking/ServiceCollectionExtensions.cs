using TradingBot.ApiService.BuildingBlocks.DistributedLocks;

namespace TradingBot.ApiService.Infrastructure.Locking;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPostgresDistributedLock()
        {
            services.AddScoped<IDistributedLock, PostgresDistributedLock>();
            return services;
        }
    }
}

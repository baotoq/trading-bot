namespace TradingBot.ApiService.BuildingBlocks.DistributedLocks;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDistributedLock()
        {
            // Implementation will be registered from Infrastructure/Locking
            return services;
        }
    }
}

using Medallion.Threading.Postgres;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.DistributedLocks;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Infrastructure.Locking;

public class PostgresDistributedLock : IDistributedLock
{
    private readonly PostgresDistributedSynchronizationProvider _provider;

    public PostgresDistributedLock(TradingBotDbContext dbContext)
    {
        var connectionString = dbContext.Database.GetDbConnection().ConnectionString;
        _provider = new PostgresDistributedSynchronizationProvider(connectionString!);
    }

    public async Task<LockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var lockKey = new PostgresAdvisoryLockKey(key);
        var distributedLock = _provider.CreateLock(lockKey);
        var lockHandle = await distributedLock.TryAcquireAsync(ttl, cancellationToken);

        if (lockHandle != null)
        {
            return new LockResponse(success: true, lockHandle);
        }

        return new LockResponse(success: false, lockHandle: null);
    }
}

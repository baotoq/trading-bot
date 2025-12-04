using Dapr.DistributedLock;

namespace TradingBot.ApiService.BuildingBlocks.DistributedLocks;

public interface IDistributedLock
{
    Task<LockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public class LockResponse : IAsyncDisposable
{
    public bool Success => true;

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public class DaprDistributedLock(DaprDistributedLockClient dapr) : IDistributedLock
{
    public static readonly string StoreName = "redislock";

    public async Task<LockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return new();
        //return await dapr.TryLockAsync(StoreName, key, "api", 60, cancellationToken);
    }
}

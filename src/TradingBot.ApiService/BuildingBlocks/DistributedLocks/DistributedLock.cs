namespace TradingBot.ApiService.BuildingBlocks.DistributedLocks;

public interface IDistributedLock
{
    Task<LockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public class LockResponse(bool success, IAsyncDisposable? lockHandle) : IAsyncDisposable
{
    public bool Success { get; } = success;
    private readonly IAsyncDisposable? _lockHandle = lockHandle;

    public ValueTask DisposeAsync()
    {
        return _lockHandle?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

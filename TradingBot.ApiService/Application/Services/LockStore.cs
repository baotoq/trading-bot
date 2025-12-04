using Dapr.Client;

namespace TradingBot.ApiService.Application.Services;

public interface ILockStore
{
    Task<TryLockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public class LockStore(DaprClient daprClient) : ILockStore
{
    public static readonly string StoreName = "lockstore";

    public async Task<TryLockResponse> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return new TryLockResponse
        {
            Success = true
        };
        return await daprClient.Lock("lockstore", key, key, ttl.Seconds, cancellationToken);
    }
}

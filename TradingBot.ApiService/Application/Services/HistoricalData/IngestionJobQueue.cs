using System.Threading.Channels;

namespace TradingBot.ApiService.Application.Services.HistoricalData;

/// <summary>
/// Bounded queue for ingestion jobs. Ensures only one job runs at a time.
/// Uses Channel<T> with capacity=1, dropping additional enqueue attempts while job is running.
/// </summary>
public class IngestionJobQueue
{
    private readonly Channel<Guid> _channel;

    public IngestionJobQueue()
    {
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    /// <summary>
    /// Attempts to enqueue a job. Returns false if queue is full (job already running).
    /// </summary>
    public bool TryEnqueue(Guid jobId)
    {
        return _channel.Writer.TryWrite(jobId);
    }

    /// <summary>
    /// Reads all jobs from the queue as they become available.
    /// Blocks until a job is enqueued.
    /// </summary>
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

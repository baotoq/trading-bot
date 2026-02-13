using TradingBot.ApiService.Application.Services.HistoricalData.Models;
using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Models;

/// <summary>
/// Tracks historical data ingestion jobs with status, progress, and error details.
/// </summary>
public class IngestionJob : AuditedEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Pending;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool Force { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int RecordsFetched { get; set; }

    public int GapsDetected { get; set; }

    public string? ErrorMessage { get; set; }
}

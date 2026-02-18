using TradingBot.ApiService.Application.Services.HistoricalData.Models;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

/// <summary>
/// Tracks historical data ingestion jobs with status, progress, and error details.
/// </summary>
public class IngestionJob : BaseEntity<IngestionJobId>
{

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

namespace TradingBot.ApiService.Application.Services.HistoricalData.Models;

public record JobStatusResponse
{
    public Guid JobId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool Force { get; init; }
    public int RecordsFetched { get; init; }
    public int GapsDetected { get; init; }
    public string? ErrorMessage { get; init; }
    public int ProgressPercent { get; init; }  // 0-100
}

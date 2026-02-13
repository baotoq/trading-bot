namespace TradingBot.ApiService.Application.Services.HistoricalData.Models;

public record DataStatusResponse
{
    public required string Symbol { get; init; }
    public bool HasData { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int TotalDaysStored { get; init; }
    public int GapCount { get; init; }
    public List<DateOnly> GapDates { get; init; } = [];  // First 20 gaps
    public decimal CoveragePercent { get; init; }
    public string Freshness { get; init; } = "";  // "Fresh" / "Recent" / "Stale"
    public int DaysSinceLastData { get; init; }
    public IngestionJobSummary? LastIngestion { get; init; }
    public string DataSource { get; init; } = "CoinGecko Free API";
    public required string Message { get; init; }
}

public record IngestionJobSummary
{
    public Guid JobId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int RecordsFetched { get; init; }
    public int GapsDetected { get; init; }
    public string? ErrorMessage { get; init; }
}

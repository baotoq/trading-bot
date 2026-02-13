namespace TradingBot.ApiService.Application.Services.HistoricalData.Models;

public record DataCoverageStats(
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalExpectedDays,
    int TotalStoredDays,
    int GapCount,
    List<DateOnly> GapDates,
    decimal CoveragePercent
);

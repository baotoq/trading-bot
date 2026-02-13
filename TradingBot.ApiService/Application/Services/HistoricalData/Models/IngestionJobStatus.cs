namespace TradingBot.ApiService.Application.Services.HistoricalData.Models;

public enum IngestionJobStatus
{
    Pending,
    Running,
    Completed,
    CompletedWithGaps,
    Failed
}

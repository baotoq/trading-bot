namespace TradingBot.ApiService.Application.Services.HistoricalData.Models;

public record IngestResponse(Guid JobId, DateTimeOffset EstimatedCompletion, string Message);

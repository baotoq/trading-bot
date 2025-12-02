using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IMarketAnalysisService
{
    Task<MarketCondition> AnalyzeMarketConditionAsync(string symbol, CancellationToken cancellationToken = default);
    Task<bool> CheckTrendAlignmentAsync(string symbol, TradeSide side, CancellationToken cancellationToken = default);
}

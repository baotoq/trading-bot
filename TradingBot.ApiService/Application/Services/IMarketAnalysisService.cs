using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IMarketAnalysisService
{
    Task<MarketCondition> AnalyzeMarketConditionAsync(Symbol symbol, CancellationToken cancellationToken = default);
    Task<bool> CheckTrendAlignmentAsync(Symbol symbol, TradeSide side, CancellationToken cancellationToken = default);
}

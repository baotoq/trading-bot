using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IMarketAnalysisService
{
    Task<bool> CheckTrendAlignmentAsync(Symbol symbol, TradeSide side, CancellationToken cancellationToken = default);
}

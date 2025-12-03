using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

public interface IStrategy
{
    string Name { get; }
    Task<TradingSignal> AnalyzeAsync(Symbol symbol, CancellationToken cancellationToken = default);
}

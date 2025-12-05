using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

public enum StrategyName
{
    EmaMomentumScalper,
    BollingerSqueeze,
    RsiDivergence,
    BtcSpotDca,
    BtcSpotTrend,
    FundingRateArbitrage
}

public interface IStrategyFactory
{
    IStrategy GetStrategy(StrategyName strategyName);
    StrategyMetadata GetMetadata(StrategyName strategyName);
    IEnumerable<StrategyMetadata> GetAllStrategies();

    // Helper methods for string conversion (for API/configuration)
    StrategyName ParseStrategyName(string strategyName);
    bool TryParseStrategyName(string strategyName, out StrategyName result);
}

public class StrategyMetadata
{
    public required StrategyName Name { get; init; }
    public required string DisplayName { get; init; }
    public required CandleInterval DefaultInterval { get; init; }
    public required string Description { get; init; }
    public required StrategyType Type { get; init; }
}

public enum StrategyType
{
    FuturesScalping,
    SpotLongTerm,
    FundingArbitrage
}

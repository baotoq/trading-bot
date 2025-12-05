using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

public class StrategyFactory : IStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly Dictionary<StrategyName, StrategyMetadata> _strategies = new()
    {
        [StrategyName.EmaMomentumScalper] = new()
        {
            Name = StrategyName.EmaMomentumScalper,
            DisplayName = "EMA Momentum Scalper",
            DefaultInterval = new CandleInterval("5m"),
            Description = "Short-term scalping strategy using EMA crossovers with momentum confirmation",
            Type = StrategyType.FuturesScalping
        },
        [StrategyName.BollingerSqueeze] = new()
        {
            Name = StrategyName.BollingerSqueeze,
            DisplayName = "Bollinger Squeeze",
            DefaultInterval = new CandleInterval("5m"),
            Description = "Volatility breakout strategy using Bollinger Band squeeze",
            Type = StrategyType.FuturesScalping
        },
        [StrategyName.RsiDivergence] = new()
        {
            Name = StrategyName.RsiDivergence,
            DisplayName = "RSI Divergence",
            DefaultInterval = new CandleInterval("5m"),
            Description = "Divergence detection strategy at support/resistance levels",
            Type = StrategyType.FuturesScalping
        },
        [StrategyName.BtcSpotDca] = new()
        {
            Name = StrategyName.BtcSpotDca,
            DisplayName = "BTC Spot DCA",
            DefaultInterval = new CandleInterval("4h"),
            Description = "Dollar-cost averaging strategy for long-term BTC accumulation",
            Type = StrategyType.SpotLongTerm
        },
        [StrategyName.BtcSpotTrend] = new()
        {
            Name = StrategyName.BtcSpotTrend,
            DisplayName = "BTC Spot Trend",
            DefaultInterval = new CandleInterval("4h"),
            Description = "Swing trading strategy with trend following for BTC spot",
            Type = StrategyType.SpotLongTerm
        },
        [StrategyName.FundingRateArbitrage] = new()
        {
            Name = StrategyName.FundingRateArbitrage,
            DisplayName = "Funding Rate Arbitrage",
            DefaultInterval = new CandleInterval("1h"),
            Description = "Low-risk strategy that profits from funding rate payments by entering positions before settlement",
            Type = StrategyType.FundingArbitrage
        }
    };

    public StrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IStrategy GetStrategy(StrategyName strategyName)
    {
        if (!_strategies.ContainsKey(strategyName))
        {
            throw new ArgumentException($"Strategy '{strategyName}' is not registered");
        }

        return ResolveStrategy(strategyName);
    }

    public StrategyMetadata GetMetadata(StrategyName strategyName)
    {
        if (_strategies.TryGetValue(strategyName, out var metadata))
        {
            return metadata;
        }

        throw new ArgumentException(
            $"Unknown strategy: '{strategyName}'. Available strategies: {string.Join(", ", _strategies.Keys)}");
    }

    public IEnumerable<StrategyMetadata> GetAllStrategies()
    {
        return _strategies.Values;
    }

    public StrategyName ParseStrategyName(string strategyName)
    {
        if (TryParseStrategyName(strategyName, out var result))
        {
            return result;
        }

        throw new ArgumentException(
            $"Unknown strategy: '{strategyName}'. Available strategies: {string.Join(", ", Enum.GetNames<StrategyName>())}");
    }

    public bool TryParseStrategyName(string strategyName, out StrategyName result)
    {
        // Normalize: remove spaces, trim
        var normalized = strategyName.Replace(" ", "").Trim();

        // Try exact enum parse (case-insensitive)
        if (Enum.TryParse<StrategyName>(normalized, ignoreCase: true, out result))
        {
            return true;
        }

        // Try fuzzy matching with enum names
        foreach (StrategyName strategy in Enum.GetValues<StrategyName>())
        {
            var enumName = strategy.ToString();

            // Check if normalized input contains enum name or vice versa (case-insensitive)
            if (normalized.Contains(enumName, StringComparison.OrdinalIgnoreCase) ||
                enumName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                result = strategy;
                return true;
            }

            // Check display name matching
            if (_strategies.TryGetValue(strategy, out var metadata))
            {
                var displayName = metadata.DisplayName.Replace(" ", "");
                if (displayName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    result = strategy;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private IStrategy ResolveStrategy(StrategyName strategyName)
    {
        return strategyName switch
        {
            StrategyName.EmaMomentumScalper => _serviceProvider.GetRequiredService<EmaMomentumScalperStrategy>(),
            StrategyName.BollingerSqueeze => _serviceProvider.GetRequiredService<BollingerSqueezeStrategy>(),
            StrategyName.RsiDivergence => _serviceProvider.GetRequiredService<RsiDivergenceStrategy>(),
            StrategyName.BtcSpotDca => _serviceProvider.GetRequiredService<BtcSpotDcaStrategy>(),
            StrategyName.BtcSpotTrend => _serviceProvider.GetRequiredService<BtcSpotTrendStrategy>(),
            StrategyName.FundingRateArbitrage => _serviceProvider.GetRequiredService<FundingRateArbitrageStrategy>(),
            _ => throw new ArgumentException($"Strategy '{strategyName}' is registered but not implemented")
        };
    }
}

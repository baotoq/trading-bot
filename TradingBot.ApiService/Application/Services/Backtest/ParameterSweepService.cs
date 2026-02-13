using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Service for parameter sweep execution.
/// Generates cartesian product of parameter ranges and executes backtests in parallel.
/// </summary>
public class ParameterSweepService(ILogger<ParameterSweepService> logger)
{
    /// <summary>
    /// Generate all parameter combinations via cartesian product.
    /// </summary>
    public IReadOnlyList<BacktestConfig> GenerateCombinations(SweepRequest request, BacktestConfig defaults)
    {
        // Use defaults for any null/empty parameter lists
        var baseAmounts = request.BaseAmounts?.Count > 0 ? request.BaseAmounts : new List<decimal> { defaults.BaseDailyAmount };
        var highLookbackDays = request.HighLookbackDays?.Count > 0 ? request.HighLookbackDays : new List<int> { defaults.HighLookbackDays };
        var bearMarketMaPeriods = request.BearMarketMaPeriods?.Count > 0 ? request.BearMarketMaPeriods : new List<int> { defaults.BearMarketMaPeriod };
        var bearBoosts = request.BearBoosts?.Count > 0 ? request.BearBoosts : new List<decimal> { defaults.BearBoostFactor };
        var maxMultiplierCaps = request.MaxMultiplierCaps?.Count > 0 ? request.MaxMultiplierCaps : new List<decimal> { defaults.MaxMultiplierCap };

        // Convert TierSets to MultiplierTierConfig lists
        var tierSets = request.TierSets?.Count > 0
            ? request.TierSets.Select(ts => ts.Tiers.Select(t => new MultiplierTierConfig(t.DropPercentage, t.Multiplier)).ToList()).ToList()
            : new List<List<MultiplierTierConfig>> { defaults.Tiers.ToList() };

        // Cartesian product using chained LINQ SelectMany
        var combinations = baseAmounts
            .SelectMany(baseAmount =>
                highLookbackDays.Select(lookback => (baseAmount, lookback)))
            .SelectMany(x =>
                bearMarketMaPeriods.Select(maPeriod => (x.baseAmount, x.lookback, maPeriod)))
            .SelectMany(x =>
                bearBoosts.Select(boost => (x.baseAmount, x.lookback, x.maPeriod, boost)))
            .SelectMany(x =>
                maxMultiplierCaps.Select(cap => (x.baseAmount, x.lookback, x.maPeriod, x.boost, cap)))
            .SelectMany(x =>
                tierSets.Select(tiers => new BacktestConfig(
                    x.baseAmount,
                    x.lookback,
                    x.maPeriod,
                    x.boost,
                    x.cap,
                    tiers)))
            .ToList();

        logger.LogInformation("Generated {Count} parameter combinations", combinations.Count);

        return combinations;
    }

    /// <summary>
    /// Execute sweep for all configurations in parallel and rank results.
    /// </summary>
    public async Task<(List<SweepResultEntry> Results, List<SweepResultDetailEntry> TopResults)> ExecuteSweepAsync(
        IReadOnlyList<BacktestConfig> configs,
        IReadOnlyList<DailyPriceData> priceData,
        string rankBy,
        CancellationToken ct)
    {
        const int topN = 5;

        // Determine parallelism: min 4, max 16, default to processor count
        int maxParallelism = Math.Clamp(Environment.ProcessorCount, 4, 16);

        logger.LogInformation("Executing {Count} backtests with parallelism {MaxParallelism}", configs.Count, maxParallelism);

        // Execute backtests in parallel with batching
        var allResults = new List<(BacktestConfig config, BacktestResult result)>();

        for (int i = 0; i < configs.Count; i += maxParallelism)
        {
            ct.ThrowIfCancellationRequested();

            var batch = configs.Skip(i).Take(maxParallelism).ToList();

            var batchResults = await Task.WhenAll(
                batch.Select(config => Task.Run(() =>
                {
                    var result = BacktestSimulator.Run(config, priceData);
                    return (config, result);
                }, ct)));

            allResults.AddRange(batchResults);

            logger.LogInformation("Completed batch {Current}/{Total} configurations", Math.Min(i + maxParallelism, configs.Count), configs.Count);
        }

        // Rank results by chosen metric
        var ranked = RankResults(allResults, rankBy);

        // Build result entries (summary metrics only)
        var results = ranked.Select((item, index) => new SweepResultEntry(
            Rank: index + 1,
            Config: item.config,
            SmartDca: item.result.SmartDca,
            FixedDcaSameBase: item.result.FixedDcaSameBase,
            Comparison: item.result.Comparison,
            WalkForward: null)).ToList();

        // Build top N detail entries (with full purchase logs and tier breakdown)
        var topResults = ranked.Take(topN).Select((item, index) => new SweepResultDetailEntry(
            Rank: index + 1,
            Config: item.config,
            SmartDca: item.result.SmartDca,
            FixedDcaSameBase: item.result.FixedDcaSameBase,
            Comparison: item.result.Comparison,
            TierBreakdown: item.result.TierBreakdown,
            PurchaseLog: item.result.PurchaseLog,
            WalkForward: null)).ToList();

        logger.LogInformation("Sweep completed: {Count} combinations, ranked by {RankBy}", allResults.Count, rankBy);

        return (results, topResults);
    }

    /// <summary>
    /// Rank results by the specified metric.
    /// </summary>
    private List<(BacktestConfig config, BacktestResult result)> RankResults(
        List<(BacktestConfig config, BacktestResult result)> results,
        string rankBy)
    {
        return rankBy.ToLowerInvariant() switch
        {
            "efficiency" => results.OrderByDescending(r => r.result.Comparison.EfficiencyRatio).ToList(),
            "costbasis" => results.OrderBy(r => r.result.SmartDca.AvgCostBasis).ToList(),
            "extrabtc" => results.OrderByDescending(r => r.result.Comparison.ExtraBtcPercentSameBase).ToList(),
            "returnpct" => results.OrderByDescending(r => r.result.SmartDca.ReturnPercent).ToList(),
            _ => throw new ArgumentException($"Unknown rankBy value '{rankBy}'. Valid options: efficiency, costbasis, extrabtc, returnpct", nameof(rankBy))
        };
    }
}

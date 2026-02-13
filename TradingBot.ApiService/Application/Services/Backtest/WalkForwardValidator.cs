using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Walk-forward validation for backtest overfitting detection.
/// Splits data into train/test periods and detects performance degradation.
/// </summary>
public static class WalkForwardValidator
{
    /// <summary>
    /// Validate a single configuration using train/test split.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <param name="fullData">Full historical price data.</param>
    /// <param name="trainRatio">Ratio for train split (default 0.70 = 70% train, 30% test).</param>
    /// <returns>Walk-forward entry with degradation metrics, or null if insufficient data.</returns>
    public static WalkForwardEntry? Validate(
        BacktestConfig config,
        IReadOnlyList<DailyPriceData> fullData,
        decimal trainRatio = 0.70m)
    {
        // Split data
        int trainSize = (int)(fullData.Count * trainRatio);
        var trainData = fullData.Take(trainSize).ToList();
        var testData = fullData.Skip(trainSize).ToList();

        // Guard: require minimum 30 days in each period
        if (trainData.Count < 30 || testData.Count < 30)
        {
            return null; // Insufficient data for meaningful validation
        }

        // Run backtests on train and test periods
        var trainResult = BacktestSimulator.Run(config, trainData);
        var testResult = BacktestSimulator.Run(config, testData);

        // Calculate degradation
        decimal returnDegradation = testResult.SmartDca.ReturnPercent - trainResult.SmartDca.ReturnPercent;
        decimal efficiencyDegradation = testResult.Comparison.EfficiencyRatio - trainResult.Comparison.EfficiencyRatio;

        // Detect overfitting: 20 percentage points drop in return OR 0.3 drop in efficiency ratio
        bool overfitWarning = returnDegradation < -20m || efficiencyDegradation < -0.3m;

        return new WalkForwardEntry(
            TrainReturnPercent: trainResult.SmartDca.ReturnPercent,
            TestReturnPercent: testResult.SmartDca.ReturnPercent,
            ReturnDegradation: returnDegradation,
            TrainEfficiency: trainResult.Comparison.EfficiencyRatio,
            TestEfficiency: testResult.Comparison.EfficiencyRatio,
            EfficiencyDegradation: efficiencyDegradation,
            OverfitWarning: overfitWarning);
    }

    /// <summary>
    /// Validate all configurations in a sweep.
    /// </summary>
    /// <param name="results">Backtest results with configs.</param>
    /// <param name="fullData">Full historical price data.</param>
    /// <param name="trainRatio">Ratio for train split.</param>
    /// <returns>Per-result walk-forward entries and summary.</returns>
    public static (List<WalkForwardEntry?> PerResult, WalkForwardSummary Summary) ValidateAll(
        IReadOnlyList<(BacktestConfig config, BacktestResult result)> results,
        IReadOnlyList<DailyPriceData> fullData,
        decimal trainRatio = 0.70m)
    {
        // Calculate split point for summary
        int trainSize = (int)(fullData.Count * trainRatio);
        var trainEnd = fullData[trainSize - 1].Date;
        var testStart = fullData[trainSize].Date;

        // Validate each configuration
        var perResult = results
            .Select(r => Validate(r.config, fullData, trainRatio))
            .ToList();

        // Build summary
        int totalValidated = perResult.Count(e => e != null);
        int overfitCount = perResult.Count(e => e?.OverfitWarning == true);

        var summary = new WalkForwardSummary(
            TrainRatio: trainRatio,
            TrainEnd: trainEnd,
            TestStart: testStart,
            OverfitCount: overfitCount,
            TotalValidated: totalValidated);

        return (perResult, summary);
    }
}

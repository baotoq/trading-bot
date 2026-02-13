using TradingBot.ApiService.Application.Services.Backtest;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Pure static backtest simulation engine.
/// Replays a smart DCA strategy day-by-day against historical price data,
/// computing multipliers via MultiplierCalculator and tracking running totals.
/// </summary>
public static class BacktestSimulator
{
    /// <summary>
    /// Runs a backtest simulation for the given configuration and price data.
    /// </summary>
    /// <param name="config">Backtest configuration including multiplier tiers and parameters.</param>
    /// <param name="priceData">Historical price data array (each entry = one purchase day).</param>
    /// <returns>Complete backtest result with smart DCA, fixed DCA baselines, and comparison metrics.</returns>
    public static BacktestResult Run(BacktestConfig config, IReadOnlyList<DailyPriceData> priceData)
    {
        throw new NotImplementedException();
    }
}

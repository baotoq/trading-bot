using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

public interface IBacktestService
{
    Task<BacktestResult> RunBacktestAsync(
        Symbol symbol,
        string strategyName,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default);

    Task<ComparisonResult> CompareStrategiesAsync(
        Symbol symbol,
        List<string> strategies,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default);
}

public class BacktestResult
{
    public string StrategyName { get; set; } = string.Empty;
    public Symbol Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetProfitPercent { get; set; }

    // Trade Statistics
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }

    // Profit Metrics
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }

    // Risk Metrics
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal AverageRiskRewardRatio { get; set; }

    // Trade Details
    public List<BacktestTrade> Trades { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class BacktestTrade
{
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public TradeSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ProfitLossPercent { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public string ExitReason { get; set; } = string.Empty;
    public bool IsWin { get; set; }
}

public class ComparisonResult
{
    public Symbol Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BacktestResult> Results { get; set; } = new();
    public string BestStrategy { get; set; } = string.Empty;
    public string BestByWinRate { get; set; } = string.Empty;
    public string BestByProfitFactor { get; set; } = string.Empty;
    public string BestByDrawdown { get; set; } = string.Empty;
}

public class BacktestService : IBacktestService
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BacktestService> _logger;

    public BacktestService(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        IServiceProvider serviceProvider,
        ILogger<BacktestService> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<BacktestResult> RunBacktestAsync(
        Symbol symbol,
        string strategyName,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Running backtest for {Strategy} on {Symbol} from {Start} to {End}",
            strategyName, symbol, startDate, endDate);

        var result = new BacktestResult
        {
            StrategyName = strategyName,
            Symbol = symbol,
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalCapital = initialCapital
        };

        try
        {
            // Determine timeframe based on strategy
            var interval = strategyName.ToLowerInvariant() switch
            {
                var s when s.Contains("dca") || s.Contains("trend") || s.Contains("spot") => "4h",
                _ => "5m"
            };

            // Get historical candles
            var candles = await _context.Candles
                .Where(c => c.Symbol == symbol &&
                           c.Interval == interval &&
                           c.OpenTime >= startDate &&
                           c.OpenTime <= endDate)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            var minCandles = interval == "4h" ? 200 : 100;
            if (candles.Count < minCandles)
            {
                result.Warnings.Add($"Insufficient data: {candles.Count} candles (minimum {minCandles} required for {interval} timeframe)");
                return result;
            }

            _logger.LogInformation("Processing {Count} candles on {Interval} timeframe", candles.Count, interval);

            // Get strategy instance
            IStrategy strategy = strategyName.ToLowerInvariant() switch
            {
                "ema" or "emascalper" or "ema momentum scalper" =>
                    _serviceProvider.GetRequiredService<EmaMomentumScalperStrategy>(),
                "btc spot dca" or "dca" or "btcspotdca" =>
                    _serviceProvider.GetRequiredService<BtcSpotDcaStrategy>(),
                "btc spot trend" or "trend" or "btcspottrend" =>
                    _serviceProvider.GetRequiredService<BtcSpotTrendStrategy>(),
                _ => throw new ArgumentException($"Unknown strategy: {strategyName}")
            };

            var currentCapital = initialCapital;
            var equity = initialCapital;
            var maxEquity = initialCapital;
            var maxDrawdown = 0m;
            BacktestTrade? openTrade = null;

            // Simulate trading on each candle (start after minimum required candles)
            for (int i = minCandles; i < candles.Count; i++)
            {
                var currentCandle = candles[i];
                var historicalCandles = candles.Take(i + 1).ToList();
                var isDcaStrategy = strategyName.ToLowerInvariant().Contains("dca");

                // Skip if we have an open trade
                if (openTrade != null)
                {
                    // Check if stop-loss or take-profit hit
                    // For DCA strategy, we don't check exits here (handled by strategy signals)
                    var exitResult = isDcaStrategy
                        ? (false, 0m, string.Empty)
                        : CheckExit(openTrade, currentCandle);

                    if (exitResult.Item1)
                    {
                        openTrade.ExitTime = currentCandle.OpenTime.DateTime;
                        openTrade.ExitPrice = exitResult.Item2;
                        openTrade.ExitReason = exitResult.Item3;

                        // Calculate P&L
                        var profitLoss = openTrade.Side == TradeSide.Long
                            ? (openTrade.ExitPrice - openTrade.EntryPrice) * openTrade.Quantity
                            : (openTrade.EntryPrice - openTrade.ExitPrice) * openTrade.Quantity;

                        openTrade.ProfitLoss = profitLoss;
                        openTrade.ProfitLossPercent = (profitLoss / (openTrade.EntryPrice * openTrade.Quantity)) * 100;
                        openTrade.IsWin = profitLoss > 0;

                        currentCapital += profitLoss;
                        equity = currentCapital;

                        // Track max drawdown
                        if (equity > maxEquity)
                        {
                            maxEquity = equity;
                        }

                        var currentDrawdown = ((maxEquity - equity) / maxEquity) * 100;
                        if (currentDrawdown > maxDrawdown)
                        {
                            maxDrawdown = currentDrawdown;
                        }

                        result.Trades.Add(openTrade);
                        openTrade = null;
                        continue;
                    }
                }

                // Look for new signal if no open trade
                if (openTrade == null)
                {
                    try
                    {
                        // For 5m strategies, need to check trend alignment (requires 15m data)
                        if (interval == "5m")
                        {
                            var has15mData = await _context.Candles
                                .AnyAsync(c => c.Symbol == symbol &&
                                              c.Interval == "15m" &&
                                              c.OpenTime <= currentCandle.OpenTime,
                                         cancellationToken);

                            if (!has15mData)
                            {
                                continue; // Skip if no 15m data for trend filter
                            }
                        }

                        var signal = await GenerateSignal(strategy, symbol, historicalCandles, cancellationToken);

                        if (signal.Type != SignalType.Hold && signal.Confidence >= 0.5m)
                        {
                            // Calculate position parameters
                            var isLong = signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy;
                            var entryPrice = signal.Price;

                            // Use strategy's stop loss if provided, otherwise calculate default
                            decimal stopLoss;
                            if (signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
                            {
                                stopLoss = signal.StopLoss.Value;
                            }
                            else
                            {
                                // DCA strategy: no stop loss, use 15% for position sizing purposes only
                                var atr = _indicatorService.CalculateATR(historicalCandles.TakeLast(50).ToList(), 14);
                                var stopLossDistance2 = isDcaStrategy ? entryPrice * 0.15m : 1.5m * atr;
                                stopLoss = isLong
                                    ? entryPrice - stopLossDistance2
                                    : entryPrice + stopLossDistance2;
                            }

                            // Position sizing
                            var riskAmount = currentCapital * (riskPercent / 100);
                            var stopLossDistance = Math.Abs(entryPrice - stopLoss);
                            var stopLossPercent = (stopLossDistance / entryPrice);
                            var positionSize = riskAmount / stopLossPercent;
                            var quantity = positionSize / entryPrice;

                            openTrade = new BacktestTrade
                            {
                                EntryTime = currentCandle.OpenTime.DateTime,
                                Side = isLong ? TradeSide.Long : TradeSide.Short,
                                EntryPrice = entryPrice,
                                Quantity = quantity
                            };

                            _logger.LogDebug(
                                "Opened {Side} position at {Price} with {Qty} qty",
                                openTrade.Side, entryPrice, quantity);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error generating signal at candle {Time}", currentCandle.OpenTime);
                    }
                }
            }

            // Close any remaining open trade at last candle
            if (openTrade != null)
            {
                var lastCandle = candles.Last();
                openTrade.ExitTime = lastCandle.OpenTime.DateTime;
                openTrade.ExitPrice = lastCandle.ClosePrice;
                openTrade.ExitReason = "End of backtest period";

                var profitLoss = openTrade.Side == TradeSide.Long
                    ? (openTrade.ExitPrice - openTrade.EntryPrice) * openTrade.Quantity
                    : (openTrade.EntryPrice - openTrade.ExitPrice) * openTrade.Quantity;

                openTrade.ProfitLoss = profitLoss;
                openTrade.ProfitLossPercent = (profitLoss / (openTrade.EntryPrice * openTrade.Quantity)) * 100;
                openTrade.IsWin = profitLoss > 0;

                currentCapital += profitLoss;
                result.Trades.Add(openTrade);
            }

            // Calculate final statistics
            result.FinalCapital = currentCapital;
            result.NetProfit = currentCapital - initialCapital;
            result.NetProfitPercent = ((currentCapital - initialCapital) / initialCapital) * 100;
            result.TotalTrades = result.Trades.Count;
            result.WinningTrades = result.Trades.Count(t => t.IsWin);
            result.LosingTrades = result.Trades.Count(t => !t.IsWin);
            result.WinRate = result.TotalTrades > 0
                ? (decimal)result.WinningTrades / result.TotalTrades * 100
                : 0;

            result.GrossProfit = result.Trades.Where(t => t.IsWin).Sum(t => t.ProfitLoss);
            result.GrossLoss = Math.Abs(result.Trades.Where(t => !t.IsWin).Sum(t => t.ProfitLoss));
            result.ProfitFactor = result.GrossLoss > 0 ? result.GrossProfit / result.GrossLoss : 0;

            result.AverageWin = result.WinningTrades > 0
                ? result.Trades.Where(t => t.IsWin).Average(t => t.ProfitLoss)
                : 0;
            result.AverageLoss = result.LosingTrades > 0
                ? result.Trades.Where(t => !t.IsWin).Average(t => Math.Abs(t.ProfitLoss))
                : 0;

            result.LargestWin = result.Trades.Any() ? result.Trades.Max(t => t.ProfitLoss) : 0;
            result.LargestLoss = result.Trades.Any() ? result.Trades.Min(t => t.ProfitLoss) : 0;
            result.MaxDrawdownPercent = maxDrawdown;

            // Calculate Sharpe Ratio (simplified)
            if (result.Trades.Any())
            {
                var returns = result.Trades.Select(t => t.ProfitLossPercent).ToList();
                var avgReturn = returns.Average();
                var stdDev = (decimal)Math.Sqrt((double)returns.Average(r => (r - avgReturn) * (r - avgReturn)));
                result.SharpeRatio = stdDev > 0 ? avgReturn / stdDev : 0;
            }

            _logger.LogInformation(
                "Backtest complete: {Trades} trades, {WinRate}% win rate, {Profit}% profit",
                result.TotalTrades, result.WinRate, result.NetProfitPercent);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running backtest");
            result.Warnings.Add($"Error: {ex.Message}");
            return result;
        }
    }

    public async Task<ComparisonResult> CompareStrategiesAsync(
        Symbol symbol,
        List<string> strategies,
        DateTime startDate,
        DateTime endDate,
        decimal initialCapital = 10000m,
        decimal riskPercent = 1.5m,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Comparing {Count} strategies on {Symbol} from {Start} to {End}",
            strategies.Count, symbol, startDate, endDate);

        var comparison = new ComparisonResult
        {
            Symbol = symbol,
            StartDate = startDate,
            EndDate = endDate
        };

        foreach (var strategy in strategies)
        {
            var backtestResult = await RunBacktestAsync(
                symbol, strategy, startDate, endDate, initialCapital, riskPercent, cancellationToken);

            comparison.Results.Add(backtestResult);
        }

        // Determine best strategies by different metrics
        if (comparison.Results.Any())
        {
            comparison.BestStrategy = comparison.Results
                .OrderByDescending(r => r.NetProfitPercent)
                .First().StrategyName;

            comparison.BestByWinRate = comparison.Results
                .OrderByDescending(r => r.WinRate)
                .First().StrategyName;

            comparison.BestByProfitFactor = comparison.Results
                .OrderByDescending(r => r.ProfitFactor)
                .First().StrategyName;

            comparison.BestByDrawdown = comparison.Results
                .OrderBy(r => r.MaxDrawdownPercent)
                .First().StrategyName;
        }

        return comparison;
    }

    private async Task<TradingSignal> GenerateSignal(
        IStrategy strategy,
        Symbol symbol,
        List<Candle> candles,
        CancellationToken cancellationToken)
    {
        // This is a simplified version - in production you'd need to properly
        // simulate the strategy's analysis method with historical data
        return await strategy.AnalyzeAsync(symbol, cancellationToken);
    }

    private (bool ShouldExit, decimal ExitPrice, string Reason) CheckExit(BacktestTrade trade, Candle currentCandle)
    {
        if (trade.Side == TradeSide.Long)
        {
            // For simplicity, using a 2% stop-loss and 4% take-profit
            var stopLoss = trade.EntryPrice * 0.98m;
            var takeProfit = trade.EntryPrice * 1.04m;

            if (currentCandle.LowPrice <= stopLoss)
                return (true, stopLoss, "Stop-Loss");

            if (currentCandle.HighPrice >= takeProfit)
                return (true, takeProfit, "Take-Profit");
        }
        else // Short
        {
            var stopLoss = trade.EntryPrice * 1.02m;
            var takeProfit = trade.EntryPrice * 0.96m;

            if (currentCandle.HighPrice >= stopLoss)
                return (true, stopLoss, "Stop-Loss");

            if (currentCandle.LowPrice <= takeProfit)
                return (true, takeProfit, "Take-Profit");
        }

        return (false, 0, string.Empty);
    }
}

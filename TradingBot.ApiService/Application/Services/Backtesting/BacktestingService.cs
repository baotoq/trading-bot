using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services.Strategy;

namespace TradingBot.ApiService.Application.Services.Backtesting;

public class BacktestingService : IBacktestingService
{
    private readonly IHistoricalDataService _historicalDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BacktestingService> _logger;

    public BacktestingService(
        IHistoricalDataService historicalDataService,
        IServiceProvider serviceProvider,
        ILogger<BacktestingService> logger)
    {
        _historicalDataService = historicalDataService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<BacktestResult> RunBacktestAsync(
        string strategyName,
        string symbol,
        string interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        decimal initialCapital = 10000m,
        decimal positionSize = 0.1m,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting backtest: Strategy={Strategy}, Symbol={Symbol}, Interval={Interval}, Period={Start} to {End}",
            strategyName, symbol, interval, startDate, endDate);

        // Fetch historical data
        var candles = await _historicalDataService.GetHistoricalDataAsync(
            symbol,
            interval,
            startDate,
            endDate,
            cancellationToken: cancellationToken);

        if (candles.Count < 50)
        {
            _logger.LogWarning("Insufficient data for backtesting");
            return CreateEmptyResult(strategyName, symbol, interval, startDate, endDate, initialCapital);
        }

        // Get strategy instance
        var strategy = GetStrategy(strategyName);
        if (strategy == null)
        {
            _logger.LogError("Strategy not found: {Strategy}", strategyName);
            return CreateEmptyResult(strategyName, symbol, interval, startDate, endDate, initialCapital);
        }

        // Run backtest simulation
        var trades = new List<Trade>();
        var equityCurve = new List<EquityCurvePoint>();

        decimal capital = initialCapital;
        decimal? position = null; // null = no position, value = entry price
        DateTimeOffset? entryTime = null;
        decimal maxEquity = initialCapital;
        decimal maxDrawdown = 0;

        for (int i = 50; i < candles.Count; i++) // Start after enough data for indicators
        {
            var historicalCandles = candles.Take(i + 1).ToList();
            var currentCandle = candles[i];

            // Generate signal
            var signal = await strategy.AnalyzeAsync(symbol, historicalCandles, cancellationToken);

            // Track equity
            var currentEquity = capital;
            if (position.HasValue)
            {
                var unrealizedPnL = (currentCandle.Close - position.Value) * (capital * positionSize / position.Value);
                currentEquity = capital + unrealizedPnL;
            }

            // Update max drawdown
            if (currentEquity > maxEquity)
            {
                maxEquity = currentEquity;
            }
            var drawdown = (maxEquity - currentEquity) / maxEquity;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }

            equityCurve.Add(new EquityCurvePoint
            {
                Time = currentCandle.CloseTime,
                Equity = currentEquity,
                DrawdownPercentage = drawdown * 100
            });

            // Execute trades based on signals
            if (position == null && (signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy))
            {
                // Enter long position
                position = currentCandle.Close;
                entryTime = currentCandle.CloseTime;

                _logger.LogDebug(
                    "Entering long at {Price} on {Time}. Signal: {Signal}",
                    position, entryTime, signal.Reason);
            }
            else if (position.HasValue && (signal.Type == SignalType.Sell || signal.Type == SignalType.StrongSell))
            {
                // Exit long position
                var exitPrice = currentCandle.Close;
                var quantity = capital * positionSize / position.Value;
                var profit = (exitPrice - position.Value) * quantity;
                var profitPercentage = (exitPrice - position.Value) / position.Value * 100;

                trades.Add(new Trade
                {
                    EntryTime = entryTime!.Value,
                    EntryPrice = position.Value,
                    ExitTime = currentCandle.CloseTime,
                    ExitPrice = exitPrice,
                    Type = TradeType.Long,
                    Quantity = quantity,
                    Profit = profit,
                    ProfitPercentage = profitPercentage,
                    Signal = signal.Reason
                });

                capital += profit;
                position = null;
                entryTime = null;

                _logger.LogDebug(
                    "Exiting long at {Price} on {Time}. Profit: {Profit:F2} ({Percentage:F2}%)",
                    exitPrice, currentCandle.CloseTime, profit, profitPercentage);
            }
        }

        // Close any open position at the end
        if (position.HasValue)
        {
            var lastCandle = candles.Last();
            var exitPrice = lastCandle.Close;
            var quantity = capital * positionSize / position.Value;
            var profit = (exitPrice - position.Value) * quantity;
            var profitPercentage = (exitPrice - position.Value) / position.Value * 100;

            trades.Add(new Trade
            {
                EntryTime = entryTime!.Value,
                EntryPrice = position.Value,
                ExitTime = lastCandle.CloseTime,
                ExitPrice = exitPrice,
                Type = TradeType.Long,
                Quantity = quantity,
                Profit = profit,
                ProfitPercentage = profitPercentage,
                Signal = "Position closed at end of backtest"
            });

            capital += profit;
        }

        // Calculate metrics
        var winningTrades = trades.Count(t => t.Profit > 0);
        var losingTrades = trades.Count(t => t.Profit < 0);
        var winRate = trades.Count > 0 ? (decimal)winningTrades / trades.Count * 100 : 0;

        var totalProfit = trades.Where(t => t.Profit > 0).Sum(t => t.Profit);
        var totalLoss = Math.Abs(trades.Where(t => t.Profit < 0).Sum(t => t.Profit));
        var profitFactor = totalLoss > 0 ? totalProfit / totalLoss : totalProfit;

        var avgProfit = winningTrades > 0 ? trades.Where(t => t.Profit > 0).Average(t => t.Profit) : 0;
        var avgLoss = losingTrades > 0 ? trades.Where(t => t.Profit < 0).Average(t => t.Profit) : 0;

        var netProfit = capital - initialCapital;
        var returnPercentage = (capital - initialCapital) / initialCapital * 100;

        // Calculate Sharpe Ratio (simplified)
        var returns = trades.Select(t => t.ProfitPercentage).ToList();
        var sharpeRatio = CalculateSharpeRatio(returns);

        var result = new BacktestResult
        {
            StrategyName = strategyName,
            Symbol = symbol,
            Interval = interval,
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalCapital = capital,
            NetProfit = netProfit,
            ReturnPercentage = returnPercentage,
            TotalTrades = trades.Count,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate,
            AverageProfit = avgProfit,
            AverageLoss = avgLoss,
            ProfitFactor = profitFactor,
            MaxDrawdown = maxDrawdown * initialCapital,
            MaxDrawdownPercentage = maxDrawdown * 100,
            SharpeRatio = sharpeRatio,
            Trades = trades,
            EquityCurve = equityCurve
        };

        _logger.LogInformation(
            "Backtest completed: Net Profit={Profit:F2} ({Return:F2}%), Trades={Trades}, Win Rate={WinRate:F2}%",
            netProfit, returnPercentage, trades.Count, winRate);

        return result;
    }

    private IStrategy? GetStrategy(string strategyName)
    {
        return strategyName.ToLower() switch
        {
            "ma crossover" => _serviceProvider.GetService<MovingAverageCrossoverStrategy>(),
            "rsi" => _serviceProvider.GetService<RSIStrategy>(),
            "macd" => _serviceProvider.GetService<MACDStrategy>(),
            _ => null
        };
    }

    private decimal CalculateSharpeRatio(List<decimal> returns)
    {
        if (returns.Count < 2)
            return 0;

        var avgReturn = returns.Average();
        var stdDev = (decimal)Math.Sqrt((double)returns.Sum(r => (r - avgReturn) * (r - avgReturn)) / returns.Count);

        if (stdDev == 0)
            return 0;

        // Assuming risk-free rate of 0 for simplicity
        return avgReturn / stdDev;
    }

    private BacktestResult CreateEmptyResult(
        string strategyName,
        string symbol,
        string interval,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        decimal initialCapital)
    {
        return new BacktestResult
        {
            StrategyName = strategyName,
            Symbol = symbol,
            Interval = interval,
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalCapital = initialCapital
        };
    }
}



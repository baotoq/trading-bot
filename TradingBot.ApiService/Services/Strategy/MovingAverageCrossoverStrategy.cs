using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services.Strategy;

/// <summary>
/// Moving Average Crossover Strategy
/// Buy when fast MA crosses above slow MA (Golden Cross)
/// Sell when fast MA crosses below slow MA (Death Cross)
/// </summary>
public class MovingAverageCrossoverStrategy : BaseStrategy
{
    public override string Name => "MA Crossover";
    public override string Description => "Buy on golden cross (fast MA > slow MA), sell on death cross";

    private readonly int _fastPeriod;
    private readonly int _slowPeriod;

    public MovingAverageCrossoverStrategy(
        ILogger<MovingAverageCrossoverStrategy> logger,
        int fastPeriod = 9,
        int slowPeriod = 21) : base(logger)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
    }

    public override async Task<TradingSignal> AnalyzeAsync(
        string symbol, 
        List<Candle> candles, 
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // For async pattern

        if (candles.Count < _slowPeriod + 1)
        {
            return CreateHoldSignal(symbol, candles.Last().Close, "Insufficient data");
        }

        // Calculate current MAs
        var fastMA = CalculateSMA(candles, _fastPeriod);
        var slowMA = CalculateSMA(candles, _slowPeriod);

        // Calculate previous MAs
        var previousCandles = candles.SkipLast(1).ToList();
        var prevFastMA = CalculateSMA(previousCandles, _fastPeriod);
        var prevSlowMA = CalculateSMA(previousCandles, _slowPeriod);

        var currentPrice = candles.Last().Close;
        var crossoverStrength = Math.Abs(fastMA - slowMA) / slowMA;

        // Golden Cross - Bullish signal
        if (prevFastMA <= prevSlowMA && fastMA > slowMA)
        {
            _logger.LogInformation(
                "Golden Cross detected for {Symbol}: Fast MA ({Fast}) crossed above Slow MA ({Slow})",
                symbol, fastMA, slowMA);

            return new TradingSignal
            {
                Symbol = symbol,
                Type = crossoverStrength > 0.02m ? SignalType.StrongBuy : SignalType.Buy,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.7m + crossoverStrength * 10),
                Strategy = Name,
                Reason = $"Golden Cross: Fast MA ({fastMA:F2}) crossed above Slow MA ({slowMA:F2})",
                Indicators = new Dictionary<string, object>
                {
                    ["FastMA"] = fastMA,
                    ["SlowMA"] = slowMA,
                    ["CrossoverStrength"] = crossoverStrength
                }
            };
        }

        // Death Cross - Bearish signal
        if (prevFastMA >= prevSlowMA && fastMA < slowMA)
        {
            _logger.LogInformation(
                "Death Cross detected for {Symbol}: Fast MA ({Fast}) crossed below Slow MA ({Slow})",
                symbol, fastMA, slowMA);

            return new TradingSignal
            {
                Symbol = symbol,
                Type = crossoverStrength > 0.02m ? SignalType.StrongSell : SignalType.Sell,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.7m + crossoverStrength * 10),
                Strategy = Name,
                Reason = $"Death Cross: Fast MA ({fastMA:F2}) crossed below Slow MA ({slowMA:F2})",
                Indicators = new Dictionary<string, object>
                {
                    ["FastMA"] = fastMA,
                    ["SlowMA"] = slowMA,
                    ["CrossoverStrength"] = crossoverStrength
                }
            };
        }

        // No crossover - Hold
        var trend = fastMA > slowMA ? "bullish" : "bearish";
        return CreateHoldSignal(
            symbol, 
            currentPrice, 
            $"No crossover. Trend: {trend}, Fast MA: {fastMA:F2}, Slow MA: {slowMA:F2}",
            new Dictionary<string, object>
            {
                ["FastMA"] = fastMA,
                ["SlowMA"] = slowMA,
                ["Trend"] = trend
            });
    }

    private TradingSignal CreateHoldSignal(
        string symbol, 
        decimal price, 
        string reason,
        Dictionary<string, object>? indicators = null)
    {
        return new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Hold,
            Price = price,
            Confidence = 0.5m,
            Strategy = Name,
            Reason = reason,
            Indicators = indicators ?? new()
        };
    }
}



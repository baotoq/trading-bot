using TradingBot.ApiService.Application.Models;

namespace TradingBot.ApiService.Application.Services.Strategy;

/// <summary>
/// MACD (Moving Average Convergence Divergence) Strategy
/// Buy when MACD crosses above signal line
/// Sell when MACD crosses below signal line
/// </summary>
public class MACDStrategy : BaseStrategy
{
    public override string Name => "MACD";
    public override string Description => "Buy on bullish MACD crossover, sell on bearish crossover";

    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly int _signalPeriod;

    public MACDStrategy(
        ILogger<MACDStrategy> logger,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9) : base(logger)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _signalPeriod = signalPeriod;
    }

    public override async Task<TradingSignal> AnalyzeAsync(
        string symbol,
        List<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (candles.Count < _slowPeriod + _signalPeriod)
        {
            return CreateHoldSignal(symbol, candles.Last().Close, "Insufficient data");
        }

        var (macd, signal, histogram) = CalculateMACD(candles, _fastPeriod, _slowPeriod, _signalPeriod);

        // Calculate previous values to detect crossover
        var previousCandles = candles.SkipLast(1).ToList();
        var (prevMacd, prevSignal, prevHistogram) =
            CalculateMACD(previousCandles, _fastPeriod, _slowPeriod, _signalPeriod);

        var currentPrice = candles.Last().Close;

        _logger.LogInformation(
            "MACD for {Symbol}: MACD={MACD:F4}, Signal={Signal:F4}, Histogram={Histogram:F4}",
            symbol, macd, signal, histogram);

        // Bullish crossover - MACD crosses above signal
        if (prevMacd <= prevSignal && macd > signal && histogram > 0)
        {
            var strength = Math.Abs(histogram) / Math.Abs(signal);
            return new TradingSignal
            {
                Symbol = symbol,
                Type = strength > 0.05m ? SignalType.StrongBuy : SignalType.Buy,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.65m + strength * 5),
                Strategy = Name,
                Reason = $"Bullish MACD crossover: MACD ({macd:F4}) > Signal ({signal:F4})",
                Indicators = new Dictionary<string, object>
                {
                    ["MACD"] = macd, ["Signal"] = signal, ["Histogram"] = histogram, ["Strength"] = strength
                }
            };
        }

        // Bearish crossover - MACD crosses below signal
        if (prevMacd >= prevSignal && macd < signal && histogram < 0)
        {
            var strength = Math.Abs(histogram) / Math.Abs(signal);
            return new TradingSignal
            {
                Symbol = symbol,
                Type = strength > 0.05m ? SignalType.StrongSell : SignalType.Sell,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.65m + strength * 5),
                Strategy = Name,
                Reason = $"Bearish MACD crossover: MACD ({macd:F4}) < Signal ({signal:F4})",
                Indicators = new Dictionary<string, object>
                {
                    ["MACD"] = macd, ["Signal"] = signal, ["Histogram"] = histogram, ["Strength"] = strength
                }
            };
        }

        // No crossover - Hold
        var trend = histogram > 0 ? "bullish" : "bearish";
        return CreateHoldSignal(
            symbol,
            currentPrice,
            $"No crossover. Trend: {trend}, Histogram: {histogram:F4}",
            new Dictionary<string, object>
            {
                ["MACD"] = macd, ["Signal"] = signal, ["Histogram"] = histogram, ["Trend"] = trend
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



using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services.Strategy;

/// <summary>
/// Combined Multi-Indicator Strategy - BEST FOR CRYPTO
/// Combines RSI, MACD, and Moving Averages for higher confidence signals
/// Requires multiple confirmations before generating buy/sell signals
/// </summary>
public class CombinedStrategy : BaseStrategy
{
    public override string Name => "Combined Multi-Indicator";
    public override string Description => "Best crypto strategy: Combines RSI, MACD, and MA for high-confidence signals";

    public CombinedStrategy(ILogger<CombinedStrategy> logger) : base(logger)
    {
    }

    public override async Task<TradingSignal> AnalyzeAsync(
        string symbol,
        List<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (candles.Count < 50) // Need enough data for all indicators
        {
            return CreateHoldSignal(symbol, candles.Last().Close, "Insufficient data", new());
        }

        var currentPrice = candles.Last().Close;

        // Calculate all indicators
        var rsi = CalculateRSI(candles, 14);
        var (macd, signal, histogram) = CalculateMACD(candles, 12, 26, 9);
        var fastMA = CalculateSMA(candles, 9);
        var slowMA = CalculateSMA(candles, 21);
        var (upperBB, middleBB, lowerBB) = CalculateBollingerBands(candles, 20, 2);

        // Calculate previous values for crossover detection
        var previousCandles = candles.SkipLast(1).ToList();
        var prevFastMA = CalculateSMA(previousCandles, 9);
        var prevSlowMA = CalculateSMA(previousCandles, 21);
        var (prevMacd, prevSignal, _) = CalculateMACD(previousCandles, 12, 26, 9);

        var indicators = new Dictionary<string, object>
        {
            ["RSI"] = rsi,
            ["MACD"] = macd,
            ["Signal"] = signal,
            ["Histogram"] = histogram,
            ["FastMA"] = fastMA,
            ["SlowMA"] = slowMA,
            ["UpperBB"] = upperBB,
            ["MiddleBB"] = middleBB,
            ["LowerBB"] = lowerBB,
            ["CurrentPrice"] = currentPrice
        };

        // Count bullish signals
        var bullishSignals = 0;
        var bullishReasons = new List<string>();

        // 1. RSI oversold
        if (rsi < 30)
        {
            bullishSignals++;
            bullishReasons.Add($"RSI oversold ({rsi:F2})");
        }

        // 2. MACD bullish crossover or positive histogram
        if (macd > signal && histogram > 0)
        {
            bullishSignals++;
            bullishReasons.Add("MACD bullish");
        }

        // 3. Price above moving averages
        if (currentPrice > fastMA && currentPrice > slowMA)
        {
            bullishSignals++;
            bullishReasons.Add("Price above MAs");
        }

        // 4. MA golden cross
        if (prevFastMA <= prevSlowMA && fastMA > slowMA)
        {
            bullishSignals += 2; // Double weight for crossover
            bullishReasons.Add("Golden Cross");
        }

        // 5. Price near lower Bollinger Band (oversold)
        if (currentPrice < lowerBB * 1.02m)
        {
            bullishSignals++;
            bullishReasons.Add("Near lower BB");
        }

        // Count bearish signals
        var bearishSignals = 0;
        var bearishReasons = new List<string>();

        // 1. RSI overbought
        if (rsi > 70)
        {
            bearishSignals++;
            bearishReasons.Add($"RSI overbought ({rsi:F2})");
        }

        // 2. MACD bearish crossover or negative histogram
        if (macd < signal && histogram < 0)
        {
            bearishSignals++;
            bearishReasons.Add("MACD bearish");
        }

        // 3. Price below moving averages
        if (currentPrice < fastMA && currentPrice < slowMA)
        {
            bearishSignals++;
            bearishReasons.Add("Price below MAs");
        }

        // 4. MA death cross
        if (prevFastMA >= prevSlowMA && fastMA < slowMA)
        {
            bearishSignals += 2; // Double weight for crossover
            bearishReasons.Add("Death Cross");
        }

        // 5. Price near upper Bollinger Band (overbought)
        if (currentPrice > upperBB * 0.98m)
        {
            bearishSignals++;
            bearishReasons.Add("Near upper BB");
        }

        _logger.LogInformation(
            "Combined Strategy for {Symbol}: Bullish={Bullish}, Bearish={Bearish}, RSI={RSI:F2}, Price={Price}",
            symbol, bullishSignals, bearishSignals, rsi, currentPrice);

        // Strong Buy: 4+ bullish signals
        if (bullishSignals >= 4)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.StrongBuy,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.6m + (bullishSignals * 0.08m)),
                Strategy = Name,
                Reason = $"Strong buy: {string.Join(", ", bullishReasons)}",
                Indicators = indicators
            };
        }

        // Buy: 3 bullish signals
        if (bullishSignals >= 3)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.Buy,
                Price = currentPrice,
                Confidence = 0.75m,
                Strategy = Name,
                Reason = $"Buy: {string.Join(", ", bullishReasons)}",
                Indicators = indicators
            };
        }

        // Strong Sell: 4+ bearish signals
        if (bearishSignals >= 4)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.StrongSell,
                Price = currentPrice,
                Confidence = Math.Min(0.95m, 0.6m + (bearishSignals * 0.08m)),
                Strategy = Name,
                Reason = $"Strong sell: {string.Join(", ", bearishReasons)}",
                Indicators = indicators
            };
        }

        // Sell: 3 bearish signals
        if (bearishSignals >= 3)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.Sell,
                Price = currentPrice,
                Confidence = 0.75m,
                Strategy = Name,
                Reason = $"Sell: {string.Join(", ", bearishReasons)}",
                Indicators = indicators
            };
        }

        // Hold: Mixed or weak signals
        return CreateHoldSignal(
            symbol,
            currentPrice,
            $"Mixed signals - Bullish: {bullishSignals}, Bearish: {bearishSignals}",
            indicators);
    }

    private TradingSignal CreateHoldSignal(
        string symbol,
        decimal price,
        string reason,
        Dictionary<string, object> indicators)
    {
        return new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Hold,
            Price = price,
            Confidence = 0.5m,
            Strategy = Name,
            Reason = reason,
            Indicators = indicators
        };
    }
}


using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

public class RsiDivergenceStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<RsiDivergenceStrategy> _logger;

    public string Name => "RSI Divergence";

    public RsiDivergenceStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<RsiDivergenceStrategy> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _logger = logger;
    }

    public async Task<TradingSignal> AnalyzeAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing {Symbol} with {Strategy}", symbol, Name);

        var signal = new TradingSignal
        {
            Symbol = symbol,
            Strategy = Name,
            Timestamp = DateTime.UtcNow,
            Type = SignalType.Hold
        };

        try
        {
            // Get 5m candles for analysis
            var fiveMinCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "5m")
                .OrderByDescending(c => c.OpenTime)
                .Take(100)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (fiveMinCandles.Count < 50)
            {
                signal.Reason = "Insufficient 5m candle data";
                signal.Confidence = 0;
                return signal;
            }

            // Calculate RSI values for all candles
            var rsiValues = new List<decimal>();
            for (int i = 15; i <= fiveMinCandles.Count; i++)
            {
                var subset = fiveMinCandles.Take(i).ToList();
                var rsi = _indicatorService.CalculateRSI(subset, 14);
                rsiValues.Add(rsi);
            }

            var currentRsi = rsiValues.Last();
            var currentPrice = fiveMinCandles.Last().ClosePrice;

            // Find support and resistance levels
            var supportLevels = _indicatorService.FindSupportLevels(fiveMinCandles, 50);
            var resistanceLevels = _indicatorService.FindResistanceLevels(fiveMinCandles, 50);

            // Calculate additional indicators
            var ema21 = _indicatorService.CalculateEMA(fiveMinCandles, 21);
            var atr = _indicatorService.CalculateATR(fiveMinCandles, 14);
            var avgVolume = _indicatorService.CalculateAverageVolume(fiveMinCandles, 20);
            var currentVolume = fiveMinCandles.Last().Volume;

            // Store indicators
            signal.Indicators["RSI"] = currentRsi;
            signal.Indicators["EMA21"] = ema21;
            signal.Indicators["ATR"] = atr;
            signal.Indicators["Volume"] = currentVolume;
            signal.Indicators["AvgVolume"] = avgVolume;
            signal.Price = currentPrice;

            // Check for divergences
            var bullishDivergence = _indicatorService.DetectBullishDivergence(
                fiveMinCandles.Skip(fiveMinCandles.Count - rsiValues.Count).ToList(),
                rsiValues,
                20);

            var bearishDivergence = _indicatorService.DetectBearishDivergence(
                fiveMinCandles.Skip(fiveMinCandles.Count - rsiValues.Count).ToList(),
                rsiValues,
                20);

            // Check if price is near support/resistance
            var nearSupport = supportLevels.Any(s => Math.Abs(currentPrice - s) / s < 0.01m); // Within 1%
            var nearResistance = resistanceLevels.Any(r => Math.Abs(currentPrice - r) / r < 0.01m); // Within 1%

            // LONG SIGNAL: Bullish divergence at support
            if (bullishDivergence && nearSupport && currentRsi < 40)
            {
                var closestSupport = supportLevels
                    .OrderBy(s => Math.Abs(currentPrice - s))
                    .First();

                signal = await CheckLongDivergence(
                    signal, currentPrice, closestSupport, resistanceLevels,
                    ema21, currentRsi, currentVolume, avgVolume, atr, cancellationToken);
            }
            // SHORT SIGNAL: Bearish divergence at resistance
            else if (bearishDivergence && nearResistance && currentRsi > 60)
            {
                var closestResistance = resistanceLevels
                    .OrderBy(r => Math.Abs(currentPrice - r))
                    .First();

                signal = await CheckShortDivergence(
                    signal, currentPrice, closestResistance, supportLevels,
                    ema21, currentRsi, currentVolume, avgVolume, atr, cancellationToken);
            }
            else
            {
                var reasons = new List<string>();
                if (!bullishDivergence && !bearishDivergence)
                    reasons.Add("No divergence detected");
                if (!nearSupport && !nearResistance)
                    reasons.Add("Price not near S/R levels");
                if (bullishDivergence && currentRsi >= 40)
                    reasons.Add($"RSI too high for bullish entry ({currentRsi:F2})");
                if (bearishDivergence && currentRsi <= 60)
                    reasons.Add($"RSI too low for bearish entry ({currentRsi:F2})");

                signal.Reason = string.Join(" | ", reasons);
                signal.Confidence = 0;
            }

            _logger.LogInformation(
                "Signal for {Symbol}: {Type} (Confidence: {Confidence}%) - {Reason}",
                symbol, signal.Type, signal.Confidence * 100, signal.Reason);

            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing {Symbol}", symbol);
            signal.Reason = $"Error: {ex.Message}";
            signal.Confidence = 0;
            return signal;
        }
    }

    private async Task<TradingSignal> CheckLongDivergence(
        TradingSignal signal,
        decimal currentPrice,
        decimal supportLevel,
        List<decimal> resistanceLevels,
        decimal ema21,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume,
        decimal atr,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;

        // PRIMARY SIGNALS
        reasons.Add("✓ Bullish RSI divergence detected");
        reasons.Add($"✓ Price at support level (${supportLevel:F2})");
        reasons.Add($"✓ RSI oversold ({rsi:F2})");

        // CONFIRMATION SIGNALS
        if (currentPrice > ema21)
        {
            confirmations++;
            reasons.Add("✓ Price above EMA(21) - trend confirmation");
        }

        if (currentVolume > avgVolume * 1.3m)
        {
            confirmations++;
            reasons.Add($"✓ Volume confirmation ({currentVolume / avgVolume:F2}x avg)");
        }

        // Check if candle is closing above support (breakout confirmation)
        var candleClosedAboveSupport = currentPrice > supportLevel;
        if (candleClosedAboveSupport)
        {
            confirmations++;
            reasons.Add("✓ Candle closing above support level");
        }

        // INVALIDATION CHECKS
        if (rsi > 35)
        {
            signal.Reason = $"RSI not oversold enough ({rsi:F2} > 35)";
            signal.Confidence = 0;
            return signal;
        }

        // Need at least 1 confirmation
        if (confirmations < 1)
        {
            signal.Reason = "Insufficient confirmations for divergence play";
            signal.Confidence = 0.4m;
            return signal;
        }

        // VALID LONG DIVERGENCE SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongBuy : SignalType.Buy;
        signal.Confidence = confirmations switch
        {
            3 => 0.90m,
            2 => 0.75m,
            1 => 0.60m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        // Set entry and targets
        signal.EntryPrice = currentPrice;
        signal.StopLoss = supportLevel - (atr * 1.5m); // Stop below support + ATR buffer

        // Target next resistance level or 3R if no resistance found
        var nextResistance = resistanceLevels
            .Where(r => r > currentPrice)
            .OrderBy(r => r)
            .FirstOrDefault();

        if (nextResistance > 0)
        {
            signal.TakeProfit1 = nextResistance;
        }
        else
        {
            var riskDistance = currentPrice - signal.StopLoss;
            signal.TakeProfit1 = currentPrice + (riskDistance * 3m); // 3R target
        }

        return signal;
    }

    private async Task<TradingSignal> CheckShortDivergence(
        TradingSignal signal,
        decimal currentPrice,
        decimal resistanceLevel,
        List<decimal> supportLevels,
        decimal ema21,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume,
        decimal atr,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;

        // PRIMARY SIGNALS
        reasons.Add("✓ Bearish RSI divergence detected");
        reasons.Add($"✓ Price at resistance level (${resistanceLevel:F2})");
        reasons.Add($"✓ RSI overbought ({rsi:F2})");

        // CONFIRMATION SIGNALS
        if (currentPrice < ema21)
        {
            confirmations++;
            reasons.Add("✓ Price below EMA(21) - trend confirmation");
        }

        if (currentVolume > avgVolume * 1.3m)
        {
            confirmations++;
            reasons.Add($"✓ Volume confirmation ({currentVolume / avgVolume:F2}x avg)");
        }

        // Check if candle is closing below resistance (breakdown confirmation)
        var candleClosedBelowResistance = currentPrice < resistanceLevel;
        if (candleClosedBelowResistance)
        {
            confirmations++;
            reasons.Add("✓ Candle closing below resistance level");
        }

        // INVALIDATION CHECKS
        if (rsi < 65)
        {
            signal.Reason = $"RSI not overbought enough ({rsi:F2} < 65)";
            signal.Confidence = 0;
            return signal;
        }

        // Need at least 1 confirmation
        if (confirmations < 1)
        {
            signal.Reason = "Insufficient confirmations for divergence play";
            signal.Confidence = 0.4m;
            return signal;
        }

        // VALID SHORT DIVERGENCE SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongSell : SignalType.Sell;
        signal.Confidence = confirmations switch
        {
            3 => 0.90m,
            2 => 0.75m,
            1 => 0.60m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        // Set entry and targets
        signal.EntryPrice = currentPrice;
        signal.StopLoss = resistanceLevel + (atr * 1.5m); // Stop above resistance + ATR buffer

        // Target next support level or 3R if no support found
        var nextSupport = supportLevels
            .Where(s => s < currentPrice)
            .OrderByDescending(s => s)
            .FirstOrDefault();

        if (nextSupport > 0)
        {
            signal.TakeProfit1 = nextSupport;
        }
        else
        {
            var riskDistance = signal.StopLoss - currentPrice;
            signal.TakeProfit1 = currentPrice - (riskDistance * 3m); // 3R target
        }

        return signal;
    }
}

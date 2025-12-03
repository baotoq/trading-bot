using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

public class EmaMomentumScalperStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IMarketAnalysisService _marketAnalysisService;
    private readonly ILogger<EmaMomentumScalperStrategy> _logger;

    public string Name => "EMA Momentum Scalper";

    public EmaMomentumScalperStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        IMarketAnalysisService marketAnalysisService,
        ILogger<EmaMomentumScalperStrategy> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _marketAnalysisService = marketAnalysisService;
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
            // PHASE 1: Check trend filter on 15m chart
            var trendAlignedLong = await _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, cancellationToken);
            var trendAlignedShort = await _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, cancellationToken);

            if (!trendAlignedLong && !trendAlignedShort)
            {
                signal.Reason = "No clear trend alignment on 15m chart";
                signal.Confidence = 0;
                return signal;
            }

            // PHASE 2: Get 5m candles for entry signal detection
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

            // Calculate indicators on 5m chart
            var ema9 = _indicatorService.CalculateEMA(fiveMinCandles, 9);
            var ema21 = _indicatorService.CalculateEMA(fiveMinCandles, 21);
            var rsi = _indicatorService.CalculateRSI(fiveMinCandles, 14);
            var (macd, macdSignal, histogram) = _indicatorService.CalculateMACD(fiveMinCandles);
            var (upperBB, middleBB, lowerBB) = _indicatorService.CalculateBollingerBands(fiveMinCandles, 20, 2);
            var avgVolume = _indicatorService.CalculateAverageVolume(fiveMinCandles, 20);
            var currentVolume = fiveMinCandles.Last().Volume;
            var currentPrice = fiveMinCandles.Last().ClosePrice;
            var swingHigh = _indicatorService.GetSwingHigh(fiveMinCandles, 10);
            var swingLow = _indicatorService.GetSwingLow(fiveMinCandles, 10);

            // Store indicators
            signal.Indicators["EMA9"] = ema9;
            signal.Indicators["EMA21"] = ema21;
            signal.Indicators["RSI"] = rsi;
            signal.Indicators["MACD"] = macd;
            signal.Indicators["MACDSignal"] = macdSignal;
            signal.Indicators["Histogram"] = histogram;
            signal.Indicators["UpperBB"] = upperBB;
            signal.Indicators["MiddleBB"] = middleBB;
            signal.Indicators["LowerBB"] = lowerBB;
            signal.Indicators["Volume"] = currentVolume;
            signal.Indicators["AvgVolume"] = avgVolume;
            signal.Price = currentPrice;

            // Check for EMA crossover (compare last 2 candles)
            var prevCandles = fiveMinCandles.SkipLast(1).ToList();
            if (prevCandles.Count < 50)
            {
                signal.Reason = "Insufficient data for crossover detection";
                return signal;
            }

            var prevEma9 = _indicatorService.CalculateEMA(prevCandles, 9);
            var prevEma21 = _indicatorService.CalculateEMA(prevCandles, 21);

            bool bullishCrossover = prevEma9 <= prevEma21 && ema9 > ema21;
            bool bearishCrossover = prevEma9 >= prevEma21 && ema9 < ema21;

            // LONG SIGNAL DETECTION
            if (trendAlignedLong && bullishCrossover)
            {
                signal = await CheckLongSignal(
                    signal, currentPrice, ema9, ema21, rsi, macd, macdSignal, histogram,
                    currentVolume, avgVolume, swingHigh, swingLow, middleBB, upperBB, cancellationToken);
            }
            // SHORT SIGNAL DETECTION
            else if (trendAlignedShort && bearishCrossover)
            {
                signal = await CheckShortSignal(
                    signal, currentPrice, ema9, ema21, rsi, macd, macdSignal, histogram,
                    currentVolume, avgVolume, swingLow, middleBB, lowerBB, cancellationToken);
            }
            else
            {
                signal.Reason = "No EMA crossover detected";
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

    private async Task<TradingSignal> CheckLongSignal(
        TradingSignal signal,
        decimal currentPrice,
        decimal ema9,
        decimal ema21,
        decimal rsi,
        decimal macd,
        decimal macdSignal,
        decimal histogram,
        decimal currentVolume,
        decimal avgVolume,
        decimal swingHigh,
        decimal swingLow,
        decimal middleBB,
        decimal upperBB,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;
        var invalidations = new List<string>();

        // PRIMARY SIGNALS (All must be TRUE)
        bool priceAboveEmas = currentPrice > ema9 && currentPrice > ema21;
        bool macdBullish = macd > macdSignal || histogram > 0;
        bool rsiInRange = rsi > 50 && rsi < 75;

        if (!priceAboveEmas)
        {
            signal.Reason = "Price not above both EMAs";
            return signal;
        }
        reasons.Add("✓ Bullish EMA crossover");

        if (!macdBullish)
        {
            signal.Reason = "MACD not bullish";
            return signal;
        }
        reasons.Add("✓ MACD bullish");

        if (!rsiInRange)
        {
            signal.Reason = $"RSI out of range ({rsi:F2})";
            return signal;
        }
        reasons.Add($"✓ RSI momentum confirmed ({rsi:F2})");

        // CONFIRMATION SIGNALS (2 of 3 required)
        if (currentVolume > avgVolume * 1.5m)
        {
            confirmations++;
            reasons.Add($"✓ High volume ({currentVolume / avgVolume:F2}x avg)");
        }

        if (currentPrice > swingHigh)
        {
            confirmations++;
            reasons.Add($"✓ Breakout above swing high (${swingHigh:F2})");
        }

        if (currentPrice > middleBB)
        {
            confirmations++;
            reasons.Add("✓ Price above Bollinger middle band");
        }

        if (confirmations < 2)
        {
            signal.Reason = $"Insufficient confirmations ({confirmations}/3 required)";
            signal.Confidence = 0.3m;
            return signal;
        }

        // INVALIDATION CHECKS
        if (rsi > 80)
        {
            invalidations.Add("RSI extremely overbought");
        }

        if (currentPrice > upperBB)
        {
            invalidations.Add("Price overextended above upper BB");
        }

        // Check for MACD histogram declining (divergence warning)
        var candles = await _context.Candles
            .Where(c => c.Symbol == signal.Symbol && c.Interval == "5m")
            .OrderByDescending(c => c.OpenTime)
            .Take(5)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count >= 4)
        {
            var histograms = new List<decimal>();
            foreach (var subset in Enumerable.Range(0, candles.Count - 3))
            {
                var testCandles = candles.Take(candles.Count - subset).ToList();
                var (_, _, h) = _indicatorService.CalculateMACD(testCandles);
                histograms.Add(h);
            }

            if (histograms.Count >= 3)
            {
                bool declining = histograms[0] > histograms[1] && histograms[1] > histograms[2];
                if (declining)
                {
                    invalidations.Add("MACD histogram declining");
                }
            }
        }

        if (invalidations.Any())
        {
            signal.Reason = $"Invalidated: {string.Join(", ", invalidations)}";
            signal.Confidence = 0;
            return signal;
        }

        // VALID LONG SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongBuy : SignalType.Buy;
        signal.Confidence = confirmations switch
        {
            3 => 0.95m,
            2 => 0.75m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        // Calculate entry, stop loss, and take profit levels for LONG
        signal.EntryPrice = currentPrice;

        // Stop loss: Below swing low or 2% below entry (whichever is tighter)
        var swingLowStop = swingLow * 0.998m; // Slightly below swing low
        var percentageStop = currentPrice * 0.98m; // 2% below entry
        signal.StopLoss = Math.Min(swingLowStop, percentageStop);

        var riskAmount = signal.EntryPrice.Value - signal.StopLoss.Value;

        // Take profit levels using risk:reward ratios
        signal.TakeProfit1 = signal.EntryPrice.Value + (riskAmount * 1.5m); // 1.5R
        signal.TakeProfit2 = signal.EntryPrice.Value + (riskAmount * 2.5m); // 2.5R
        signal.TakeProfit3 = signal.EntryPrice.Value + (riskAmount * 4.0m); // 4R

        return signal;
    }

    private async Task<TradingSignal> CheckShortSignal(
        TradingSignal signal,
        decimal currentPrice,
        decimal ema9,
        decimal ema21,
        decimal rsi,
        decimal macd,
        decimal macdSignal,
        decimal histogram,
        decimal currentVolume,
        decimal avgVolume,
        decimal swingLow,
        decimal middleBB,
        decimal lowerBB,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;
        var invalidations = new List<string>();

        // PRIMARY SIGNALS (All must be TRUE)
        bool priceBelowEmas = currentPrice < ema9 && currentPrice < ema21;
        bool macdBearish = macd < macdSignal || histogram < 0;
        bool rsiInRange = rsi < 50 && rsi > 25;

        if (!priceBelowEmas)
        {
            signal.Reason = "Price not below both EMAs";
            return signal;
        }
        reasons.Add("✓ Bearish EMA crossover");

        if (!macdBearish)
        {
            signal.Reason = "MACD not bearish";
            return signal;
        }
        reasons.Add("✓ MACD bearish");

        if (!rsiInRange)
        {
            signal.Reason = $"RSI out of range ({rsi:F2})";
            return signal;
        }
        reasons.Add($"✓ RSI momentum confirmed ({rsi:F2})");

        // CONFIRMATION SIGNALS (2 of 3 required)
        if (currentVolume > avgVolume * 1.5m)
        {
            confirmations++;
            reasons.Add($"✓ High volume ({currentVolume / avgVolume:F2}x avg)");
        }

        if (currentPrice < swingLow)
        {
            confirmations++;
            reasons.Add($"✓ Breakdown below swing low (${swingLow:F2})");
        }

        if (currentPrice < middleBB)
        {
            confirmations++;
            reasons.Add("✓ Price below Bollinger middle band");
        }

        if (confirmations < 2)
        {
            signal.Reason = $"Insufficient confirmations ({confirmations}/3 required)";
            signal.Confidence = 0.3m;
            return signal;
        }

        // INVALIDATION CHECKS
        if (rsi < 20)
        {
            invalidations.Add("RSI extremely oversold");
        }

        if (currentPrice < lowerBB)
        {
            invalidations.Add("Price overextended below lower BB");
        }

        // Check for MACD histogram rising (divergence warning)
        var candles = await _context.Candles
            .Where(c => c.Symbol == signal.Symbol && c.Interval == "5m")
            .OrderByDescending(c => c.OpenTime)
            .Take(5)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count >= 4)
        {
            var histograms = new List<decimal>();
            foreach (var subset in Enumerable.Range(0, candles.Count - 3))
            {
                var testCandles = candles.Take(candles.Count - subset).ToList();
                var (_, _, h) = _indicatorService.CalculateMACD(testCandles);
                histograms.Add(h);
            }

            if (histograms.Count >= 3)
            {
                bool rising = histograms[0] < histograms[1] && histograms[1] < histograms[2];
                if (rising)
                {
                    invalidations.Add("MACD histogram rising");
                }
            }
        }

        if (invalidations.Any())
        {
            signal.Reason = $"Invalidated: {string.Join(", ", invalidations)}";
            signal.Confidence = 0;
            return signal;
        }

        // VALID SHORT SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongSell : SignalType.Sell;
        signal.Confidence = confirmations switch
        {
            3 => 0.95m,
            2 => 0.75m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        // Calculate entry, stop loss, and take profit levels for SHORT
        signal.EntryPrice = currentPrice;

        // Stop loss: Above recent swing or 2% above entry (whichever is closer)
        var swingBasedStop = swingLow * 1.002m; // Slightly above swing low (acts as resistance)
        var percentageStop = currentPrice * 1.02m; // 2% above entry
        signal.StopLoss = Math.Min(swingBasedStop, percentageStop);

        var riskAmount = signal.StopLoss.Value - signal.EntryPrice.Value;

        // Take profit levels using risk:reward ratios
        signal.TakeProfit1 = signal.EntryPrice.Value - (riskAmount * 1.5m); // 1.5R
        signal.TakeProfit2 = signal.EntryPrice.Value - (riskAmount * 2.5m); // 2.5R
        signal.TakeProfit3 = signal.EntryPrice.Value - (riskAmount * 4.0m); // 4R

        return signal;
    }
}

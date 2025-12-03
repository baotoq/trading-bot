using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

/// <summary>
/// BTC Spot Trend Strategy - Active swing trading strategy for BTC spot:
/// 1. Trend following using EMA crossovers on 4h/1d timeframes
/// 2. LONG-only positions (spot trading)
/// 3. Entry on strong momentum breakouts
/// 4. Exit on trend reversal or target hit
/// 5. Wider stop losses (8-12%) suitable for spot swing trading
/// 6. Hold winners through minor pullbacks
///
/// Designed for: 4h candles, swing trading, spot market
/// Expected: 45-55% win rate, 2.5R-4R average win, 2-6 trades/month
/// </summary>
public class BtcSpotTrendStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<BtcSpotTrendStrategy> _logger;

    public string Name => "BTC Spot Trend";

    public BtcSpotTrendStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<BtcSpotTrendStrategy> logger)
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
            // Use 4h candles for swing trading
            var candles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "4h")
                .OrderByDescending(c => c.OpenTime)
                .Take(200)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (candles.Count < 200)
            {
                signal.Reason = "Insufficient candle data (need 200 for analysis)";
                signal.Confidence = 0;
                return signal;
            }

            // Calculate indicators
            var ema9 = _indicatorService.CalculateEMA(candles, 9);
            var ema21 = _indicatorService.CalculateEMA(candles, 21);
            var ema50 = _indicatorService.CalculateEMA(candles, 50);
            var ema200 = _indicatorService.CalculateEMA(candles, 200);
            var rsi = _indicatorService.CalculateRSI(candles, 14);
            var (macd, macdSignal, histogram) = _indicatorService.CalculateMACD(candles);
            var (upperBB, middleBB, lowerBB) = _indicatorService.CalculateBollingerBands(candles, 20, 2);
            var avgVolume = _indicatorService.CalculateAverageVolume(candles, 20);
            var currentVolume = candles.Last().Volume;
            var currentPrice = candles.Last().ClosePrice;
            var swingHigh = _indicatorService.GetSwingHigh(candles, 20);
            var swingLow = _indicatorService.GetSwingLow(candles, 20);

            // Store indicators
            signal.Indicators["EMA9"] = ema9;
            signal.Indicators["EMA21"] = ema21;
            signal.Indicators["EMA50"] = ema50;
            signal.Indicators["EMA200"] = ema200;
            signal.Indicators["RSI"] = rsi;
            signal.Indicators["MACD"] = macd;
            signal.Indicators["MACDSignal"] = macdSignal;
            signal.Indicators["Histogram"] = histogram;
            signal.Indicators["UpperBB"] = upperBB;
            signal.Indicators["MiddleBB"] = middleBB;
            signal.Indicators["LowerBB"] = lowerBB;
            signal.Indicators["Volume"] = currentVolume;
            signal.Indicators["AvgVolume"] = avgVolume;
            signal.Indicators["SwingHigh"] = swingHigh;
            signal.Indicators["SwingLow"] = swingLow;
            signal.Price = currentPrice;

            // ============================================
            // PHASE 1: Market Regime Filter
            // ============================================
            bool inBullMarket = currentPrice > ema200;
            bool emaUptrend = ema50 > ema200;

            if (!inBullMarket)
            {
                signal.Reason = $"Not in bull market (Price ${currentPrice:F2} < 200 EMA ${ema200:F2}) - No long entries";
                signal.Confidence = 0;
                return signal;
            }

            // ============================================
            // PHASE 2: Check for EMA Crossover
            // ============================================
            var prevCandles = candles.SkipLast(1).ToList();
            if (prevCandles.Count < 200)
            {
                signal.Reason = "Insufficient data for crossover detection";
                return signal;
            }

            var prevEma9 = _indicatorService.CalculateEMA(prevCandles, 9);
            var prevEma21 = _indicatorService.CalculateEMA(prevCandles, 21);
            var prevEma50 = _indicatorService.CalculateEMA(prevCandles, 50);

            bool bullishCrossover = prevEma9 <= prevEma21 && ema9 > ema21;
            bool bearishCrossover = prevEma9 >= prevEma21 && ema9 < ema21;

            // ============================================
            // PHASE 3: LONG ENTRY SIGNAL
            // ============================================
            if (bullishCrossover && inBullMarket)
            {
                signal = await CheckLongEntrySignal(
                    signal, currentPrice, ema9, ema21, ema50, ema200,
                    rsi, macd, macdSignal, histogram,
                    currentVolume, avgVolume, swingHigh, swingLow,
                    middleBB, upperBB, lowerBB, emaUptrend,
                    cancellationToken);
            }
            // ============================================
            // PHASE 4: EXIT SIGNAL (Trend Reversal)
            // ============================================
            else if (bearishCrossover || currentPrice < ema50)
            {
                signal = CheckExitSignal(signal, currentPrice, ema9, ema21, ema50, rsi, histogram);
            }
            else
            {
                signal.Reason = "No entry/exit signal - Waiting for setup";
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

    private async Task<TradingSignal> CheckLongEntrySignal(
        TradingSignal signal,
        decimal currentPrice,
        decimal ema9,
        decimal ema21,
        decimal ema50,
        decimal ema200,
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
        decimal lowerBB,
        bool emaUptrend,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;
        var invalidations = new List<string>();

        // ============================================
        // PRIMARY SIGNALS (All must be TRUE)
        // ============================================

        // 1. Price above all short-term EMAs
        bool priceAboveEmas = currentPrice > ema9 && currentPrice > ema21;
        if (!priceAboveEmas)
        {
            signal.Reason = "Price not above both short-term EMAs";
            return signal;
        }
        reasons.Add("✓ Bullish EMA(9) x EMA(21) crossover");

        // 2. MACD bullish
        bool macdBullish = macd > macdSignal || histogram > 0;
        if (!macdBullish)
        {
            signal.Reason = "MACD not bullish";
            return signal;
        }
        reasons.Add("✓ MACD bullish momentum");

        // 3. RSI in momentum zone (not overbought, not oversold)
        bool rsiInRange = rsi > 50 && rsi < 70;
        if (!rsiInRange)
        {
            signal.Reason = $"RSI out of momentum range ({rsi:F2})";
            return signal;
        }
        reasons.Add($"✓ RSI momentum confirmed ({rsi:F2})");

        // ============================================
        // CONFIRMATION SIGNALS (3+ for StrongBuy, 2+ for Buy)
        // ============================================

        // 1. Strong uptrend alignment
        if (currentPrice > ema50 && emaUptrend)
        {
            confirmations++;
            reasons.Add($"✓ Strong uptrend (Price > EMA50 > EMA200)");
        }

        // 2. High volume breakout
        if (currentVolume > avgVolume * 1.8m)
        {
            confirmations++;
            reasons.Add($"✓ High volume breakout ({currentVolume / avgVolume:F2}x avg)");
        }

        // 3. Breakout above swing high
        if (currentPrice > swingHigh)
        {
            confirmations++;
            reasons.Add($"✓ Breakout above swing high (${swingHigh:F2})");
        }

        // 4. Price above Bollinger middle band
        if (currentPrice > middleBB)
        {
            confirmations++;
            reasons.Add("✓ Price above Bollinger middle band");
        }

        // 5. MACD histogram increasing
        var candles = await _context.Candles
            .Where(c => c.Symbol == signal.Symbol && c.Interval == "4h")
            .OrderByDescending(c => c.OpenTime)
            .Take(3)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count >= 3)
        {
            var hist1 = _indicatorService.CalculateMACD(candles.Take(candles.Count - 2).ToList()).Item3;
            var hist2 = _indicatorService.CalculateMACD(candles.Take(candles.Count - 1).ToList()).Item3;
            var hist3 = histogram;

            if (hist3 > hist2 && hist2 > hist1)
            {
                confirmations++;
                reasons.Add("✓ MACD histogram accelerating");
            }
        }

        if (confirmations < 2)
        {
            signal.Reason = $"Insufficient confirmations ({confirmations}/5 required)";
            signal.Confidence = 0.4m;
            return signal;
        }

        // ============================================
        // INVALIDATION CHECKS
        // ============================================

        if (rsi > 75)
        {
            invalidations.Add("RSI overbought (> 75)");
        }

        if (currentPrice > upperBB * 1.02m)
        {
            invalidations.Add("Price overextended above upper BB");
        }

        if (invalidations.Any())
        {
            signal.Reason = $"Invalidated: {string.Join(", ", invalidations)}";
            signal.Confidence = 0;
            return signal;
        }

        // ============================================
        // VALID LONG ENTRY
        // ============================================

        signal.Type = confirmations >= 3 ? SignalType.StrongBuy : SignalType.Buy;
        signal.Confidence = confirmations switch
        {
            >= 4 => 0.95m,
            3 => 0.80m,
            2 => 0.65m,
            _ => 0.50m
        };
        signal.Reason = string.Join(" | ", reasons);

        // ============================================
        // POSITION MANAGEMENT
        // ============================================

        signal.EntryPrice = currentPrice;

        // Stop loss: 10% below entry or below swing low (whichever is closer)
        var percentageStop = currentPrice * 0.90m; // 10% below
        var swingStop = swingLow * 0.98m; // 2% below swing low
        signal.StopLoss = Math.Max(swingStop, percentageStop); // Use tighter stop

        var riskAmount = signal.EntryPrice.Value - signal.StopLoss.Value;

        // Take profit levels for swing trading
        signal.TakeProfit1 = signal.EntryPrice.Value + (riskAmount * 1.5m); // 1.5R (~15%)
        signal.TakeProfit2 = signal.EntryPrice.Value + (riskAmount * 3.0m); // 3R (~30%)
        signal.TakeProfit3 = signal.EntryPrice.Value + (riskAmount * 5.0m); // 5R (~50%)

        reasons.Add($"Entry: ${signal.EntryPrice:F2} | SL: ${signal.StopLoss:F2} | TP1: ${signal.TakeProfit1:F2} | TP2: ${signal.TakeProfit2:F2}");

        return signal;
    }

    private TradingSignal CheckExitSignal(
        TradingSignal signal,
        decimal currentPrice,
        decimal ema9,
        decimal ema21,
        decimal ema50,
        decimal rsi,
        decimal histogram)
    {
        var reasons = new List<string>();
        var exitConfirmations = 0;

        // 1. Bearish EMA crossover
        if (ema9 < ema21)
        {
            exitConfirmations++;
            reasons.Add("⚠️ Bearish EMA(9) x EMA(21) crossover");
        }

        // 2. Price below 50 EMA
        if (currentPrice < ema50)
        {
            exitConfirmations++;
            reasons.Add($"⚠️ Price broke below 50 EMA (${ema50:F2})");
        }

        // 3. RSI bearish
        if (rsi < 45)
        {
            exitConfirmations++;
            reasons.Add($"⚠️ RSI turned bearish ({rsi:F2})");
        }

        // 4. MACD bearish
        if (histogram < 0)
        {
            exitConfirmations++;
            reasons.Add("⚠️ MACD histogram negative");
        }

        if (exitConfirmations >= 2)
        {
            signal.Type = SignalType.Sell;
            signal.Confidence = exitConfirmations >= 3 ? 0.85m : 0.70m;
            signal.Reason = $"TREND REVERSAL ({exitConfirmations}/4 signals) | " + string.Join(" | ", reasons);
            signal.EntryPrice = currentPrice;
            signal.StopLoss = null;
            signal.TakeProfit1 = null;
            signal.TakeProfit2 = null;
            signal.TakeProfit3 = null;
        }
        else
        {
            signal.Reason = "Minor pullback - Hold position";
            signal.Confidence = 0;
        }

        return signal;
    }
}

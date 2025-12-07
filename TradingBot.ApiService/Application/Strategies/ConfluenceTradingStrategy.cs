using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

/// <summary>
/// Confluence Trading Strategy: A multi-timeframe strategy combining EMA Ribbon, VWAP,
/// Volume Profile, and candlestick patterns for high-probability trade entries.
///
/// Algorithm:
/// 1. Identify trend on 15-min chart (EMA Ribbon 9/21/50 aligned + VWAP slope)
/// 2. Wait for pullback on 5-min chart to key support/resistance levels
/// 3. Look for confluence signals (2+ confirmations required)
/// 4. Execute with two-tiered take profit strategy
/// </summary>
public class ConfluenceTradingStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<ConfluenceTradingStrategy> _logger;

    // Configuration constants
    private const int MinCandles15m = 60;
    private const int MinCandles5m = 100;
    private const decimal EmaRibbonThreshold = 0.001m; // 0.1% minimum separation
    private const decimal VwapSlopeThreshold = 0.01m; // 0.01% slope threshold
    private const decimal ConfluenceZoneWidth = 0.005m; // 0.5% zone width
    private const decimal StopLossPercent = 0.0015m; // 0.15% below swing low
    private const int RequiredConfirmations = 2;

    public string Name => "Confluence Trading";

    public ConfluenceTradingStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<ConfluenceTradingStrategy> logger)
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
            Timestamp = DateTimeOffset.UtcNow,
            Type = SignalType.Hold
        };

        try
        {
            // STEP 1: Identify trend on 15-min chart
            var trendAnalysis = await AnalyzeTrendOn15Min(symbol, cancellationToken);
            if (trendAnalysis == null)
            {
                signal.Reason = "Insufficient 15-min data for trend analysis";
                signal.Confidence = 0;
                return signal;
            }

            if (trendAnalysis.TrendDirection == TrendDirection.None)
            {
                signal.Reason = trendAnalysis.Reason;
                signal.Confidence = 0;
                return signal;
            }

            // Store 15m indicators
            signal.Indicators["15m_EMA9"] = trendAnalysis.Ema9;
            signal.Indicators["15m_EMA21"] = trendAnalysis.Ema21;
            signal.Indicators["15m_EMA50"] = trendAnalysis.Ema50;
            signal.Indicators["15m_VWAP"] = trendAnalysis.Vwap;
            signal.Indicators["15m_VWAPSlope"] = trendAnalysis.VwapSlope;

            // STEP 2 & 3: Analyze 5-min for pullback and confluence signals
            var entryAnalysis = await AnalyzeEntryOn5Min(
                symbol,
                trendAnalysis.TrendDirection,
                cancellationToken);

            if (entryAnalysis == null)
            {
                signal.Reason = "Insufficient 5-min data for entry analysis";
                signal.Confidence = 0;
                return signal;
            }

            // Store 5m indicators
            signal.Indicators["5m_EMA9"] = entryAnalysis.Ema9;
            signal.Indicators["5m_EMA21"] = entryAnalysis.Ema21;
            signal.Indicators["5m_RSI"] = entryAnalysis.Rsi;
            signal.Indicators["5m_VWAP"] = entryAnalysis.Vwap;
            signal.Indicators["5m_Volume"] = entryAnalysis.CurrentVolume;
            signal.Indicators["5m_AvgVolume"] = entryAnalysis.AverageVolume;
            signal.Price = entryAnalysis.CurrentPrice;

            if (!entryAnalysis.IsPullbackDetected)
            {
                signal.Reason = $"No pullback to confluence zone detected ({trendAnalysis.TrendDirection} trend active)";
                signal.Confidence = 0.2m;
                return signal;
            }

            if (entryAnalysis.ConfirmationCount < RequiredConfirmations)
            {
                signal.Reason = $"Insufficient confirmations: {entryAnalysis.ConfirmationCount}/{RequiredConfirmations} - {string.Join(", ", entryAnalysis.ConfirmationReasons)}";
                signal.Confidence = 0.4m;
                return signal;
            }

            // STEP 4: Generate signal with entry/exit levels
            signal = GenerateTradeSignal(signal, trendAnalysis, entryAnalysis);

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

    private async Task<TrendAnalysisResult?> AnalyzeTrendOn15Min(Symbol symbol, CancellationToken cancellationToken)
    {
        var candles = await _context.Candles
            .Where(c => c.Symbol == symbol && c.Interval == "15m")
            .OrderByDescending(c => c.OpenTime)
            .Take(MinCandles15m)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count < MinCandles15m)
        {
            _logger.LogWarning("Insufficient 15m candles: {Count}/{Required}", candles.Count, MinCandles15m);
            return null;
        }

        // Remove duplicates by OpenTime (handle real-time data duplication)
        candles = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.Last())
            .OrderBy(c => c.OpenTime)
            .ToList();

        var ema9 = _indicatorService.CalculateEMA(candles, 9);
        var ema21 = _indicatorService.CalculateEMA(candles, 21);
        var ema50 = _indicatorService.CalculateEMA(candles, 50);
        var vwap = _indicatorService.CalculateVWAP(candles);
        var vwapSlope = _indicatorService.CalculateVWAPSlope(candles, 5);
        var currentPrice = candles.Last().ClosePrice;

        var result = new TrendAnalysisResult
        {
            Ema9 = ema9,
            Ema21 = ema21,
            Ema50 = ema50,
            Vwap = vwap,
            VwapSlope = vwapSlope,
            CurrentPrice = currentPrice
        };

        // Check for LONG trend conditions
        bool emaRibbonBullish = ema9 > ema21 && ema21 > ema50;
        bool priceAboveEmaBand = currentPrice > ema9;
        bool vwapSlopingUp = vwapSlope > VwapSlopeThreshold;

        // Check for SHORT trend conditions
        bool emaRibbonBearish = ema9 < ema21 && ema21 < ema50;
        bool priceBelowEmaBand = currentPrice < ema9;
        bool vwapSlopingDown = vwapSlope < -VwapSlopeThreshold;

        // Validate EMA separation (avoid choppy/ranging markets)
        decimal ema9To21Separation = Math.Abs(ema9 - ema21) / ema21;
        decimal ema21To50Separation = Math.Abs(ema21 - ema50) / ema50;
        bool adequateSeparation = ema9To21Separation > EmaRibbonThreshold && ema21To50Separation > EmaRibbonThreshold;

        if (emaRibbonBullish && priceAboveEmaBand && vwapSlopingUp && adequateSeparation)
        {
            result.TrendDirection = TrendDirection.Long;
            result.Reason = $"15m Uptrend: EMA ribbon aligned (9>{21}>{50}), VWAP slope +{vwapSlope:F3}%";
        }
        else if (emaRibbonBearish && priceBelowEmaBand && vwapSlopingDown && adequateSeparation)
        {
            result.TrendDirection = TrendDirection.Short;
            result.Reason = $"15m Downtrend: EMA ribbon aligned (9<21<50), VWAP slope {vwapSlope:F3}%";
        }
        else
        {
            result.TrendDirection = TrendDirection.None;

            var reasons = new List<string>();
            if (!emaRibbonBullish && !emaRibbonBearish)
                reasons.Add("EMA ribbon not aligned");
            if (!priceAboveEmaBand && !priceBelowEmaBand)
                reasons.Add("Price within EMA band");
            if (Math.Abs(vwapSlope) <= VwapSlopeThreshold)
                reasons.Add($"VWAP slope flat ({vwapSlope:F3}%)");
            if (!adequateSeparation)
                reasons.Add("EMA separation too narrow (ranging market)");

            result.Reason = string.Join(", ", reasons);
        }

        return result;
    }

    private async Task<EntryAnalysisResult?> AnalyzeEntryOn5Min(
        Symbol symbol,
        TrendDirection trendDirection,
        CancellationToken cancellationToken)
    {
        var candles = await _context.Candles
            .Where(c => c.Symbol == symbol && c.Interval == "5m")
            .OrderByDescending(c => c.OpenTime)
            .Take(MinCandles5m)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);

        if (candles.Count < MinCandles5m)
        {
            _logger.LogWarning("Insufficient 5m candles: {Count}/{Required}", candles.Count, MinCandles5m);
            return null;
        }

        // Remove duplicates by OpenTime
        candles = candles
            .GroupBy(c => c.OpenTime)
            .Select(g => g.Last())
            .OrderBy(c => c.OpenTime)
            .ToList();

        var currentCandle = candles.Last();
        var currentPrice = currentCandle.ClosePrice;

        // Calculate indicators
        var ema9 = _indicatorService.CalculateEMA(candles, 9);
        var ema21 = _indicatorService.CalculateEMA(candles, 21);
        var rsi = _indicatorService.CalculateRSI(candles, 14);
        var rsiSeries = _indicatorService.CalculateRSISeries(candles, 14, 3);
        var vwap = _indicatorService.CalculateVWAP(candles);
        var avgVolume = _indicatorService.CalculateAverageVolume(candles, 20);
        var currentVolume = currentCandle.Volume;
        var hvns = _indicatorService.FindHighVolumeNodes(candles, 20, 3);
        var swingHigh = _indicatorService.GetSwingHigh(candles, 15);
        var swingLow = _indicatorService.GetSwingLow(candles, 15);
        var candlePattern = _indicatorService.DetectCandlestickPattern(candles);

        var result = new EntryAnalysisResult
        {
            CurrentPrice = currentPrice,
            Ema9 = ema9,
            Ema21 = ema21,
            Rsi = rsi,
            Vwap = vwap,
            CurrentVolume = currentVolume,
            AverageVolume = avgVolume,
            HighVolumeNodes = hvns,
            SwingHigh = swingHigh,
            SwingLow = swingLow,
            CandlePattern = candlePattern
        };

        // Check for pullback to confluence zone
        result.IsPullbackDetected = CheckPullbackToConfluenceZone(
            trendDirection,
            currentPrice,
            ema21,
            vwap,
            hvns,
            swingHigh,
            swingLow,
            out var confluenceLevel,
            out var confluenceType);

        result.ConfluenceLevel = confluenceLevel;
        result.ConfluenceType = confluenceType;

        if (!result.IsPullbackDetected)
            return result;

        // Check for confirmation signals
        CheckConfirmationSignals(
            trendDirection,
            result,
            rsi,
            rsiSeries,
            currentVolume,
            avgVolume,
            candlePattern);

        return result;
    }

    private bool CheckPullbackToConfluenceZone(
        TrendDirection direction,
        decimal currentPrice,
        decimal ema21,
        decimal vwap,
        List<decimal> hvns,
        decimal swingHigh,
        decimal swingLow,
        out decimal confluenceLevel,
        out string confluenceType)
    {
        confluenceLevel = 0;
        confluenceType = string.Empty;

        if (direction == TrendDirection.Long)
        {
            // For LONG: Check if price pulled back to support zone
            // Priority 1: HVN + EMA21 confluence (strongest)
            foreach (var hvn in hvns)
            {
                if (IsWithinZone(hvn, ema21, ConfluenceZoneWidth) &&
                    IsWithinZone(currentPrice, hvn, ConfluenceZoneWidth * 2))
                {
                    confluenceLevel = hvn;
                    confluenceType = "HVN + EMA21";
                    return true;
                }
            }

            // Priority 2: VWAP + Swing Low confluence
            if (IsWithinZone(vwap, swingLow, ConfluenceZoneWidth) &&
                IsWithinZone(currentPrice, vwap, ConfluenceZoneWidth * 2))
            {
                confluenceLevel = vwap;
                confluenceType = "VWAP + SwingLow";
                return true;
            }

            // Priority 3: EMA21 + HVN confluence
            foreach (var hvn in hvns)
            {
                if (IsWithinZone(ema21, hvn, ConfluenceZoneWidth) &&
                    IsWithinZone(currentPrice, ema21, ConfluenceZoneWidth * 2))
                {
                    confluenceLevel = ema21;
                    confluenceType = "EMA21 + HVN";
                    return true;
                }
            }

            // Fallback: Price at or near EMA21
            if (currentPrice >= ema21 * 0.995m && currentPrice <= ema21 * 1.005m)
            {
                confluenceLevel = ema21;
                confluenceType = "EMA21";
                return true;
            }
        }
        else if (direction == TrendDirection.Short)
        {
            // For SHORT: Check if price pulled back to resistance zone
            // Priority 1: HVN + EMA21 confluence (strongest)
            foreach (var hvn in hvns)
            {
                if (IsWithinZone(hvn, ema21, ConfluenceZoneWidth) &&
                    IsWithinZone(currentPrice, hvn, ConfluenceZoneWidth * 2))
                {
                    confluenceLevel = hvn;
                    confluenceType = "HVN + EMA21";
                    return true;
                }
            }

            // Priority 2: VWAP + Swing High confluence
            if (IsWithinZone(vwap, swingHigh, ConfluenceZoneWidth) &&
                IsWithinZone(currentPrice, vwap, ConfluenceZoneWidth * 2))
            {
                confluenceLevel = vwap;
                confluenceType = "VWAP + SwingHigh";
                return true;
            }

            // Priority 3: EMA21 + HVN confluence
            foreach (var hvn in hvns)
            {
                if (IsWithinZone(ema21, hvn, ConfluenceZoneWidth) &&
                    IsWithinZone(currentPrice, ema21, ConfluenceZoneWidth * 2))
                {
                    confluenceLevel = ema21;
                    confluenceType = "EMA21 + HVN";
                    return true;
                }
            }

            // Fallback: Price at or near EMA21
            if (currentPrice >= ema21 * 0.995m && currentPrice <= ema21 * 1.005m)
            {
                confluenceLevel = ema21;
                confluenceType = "EMA21";
                return true;
            }
        }

        return false;
    }

    private void CheckConfirmationSignals(
        TrendDirection direction,
        EntryAnalysisResult result,
        decimal rsi,
        List<decimal> rsiSeries,
        decimal currentVolume,
        decimal avgVolume,
        CandlestickPattern candlePattern)
    {
        result.ConfirmationReasons = new List<string>();
        result.ConfirmationCount = 0;

        if (direction == TrendDirection.Long)
        {
            // Confirmation 1: Bullish candlestick pattern
            if (candlePattern == CandlestickPattern.BullishEngulfing ||
                candlePattern == CandlestickPattern.Hammer ||
                candlePattern == CandlestickPattern.MorningStar ||
                candlePattern == CandlestickPattern.InvertedHammer)
            {
                result.ConfirmationCount++;
                result.ConfirmationReasons.Add($"✓ {candlePattern} pattern");
            }

            // Confirmation 2: RSI bouncing from 40-50 level and turning up
            if (rsi >= 40 && rsi <= 55 && rsiSeries.Count >= 2)
            {
                bool rsiTurningUp = rsiSeries[^1] > rsiSeries[^2];
                if (rsiTurningUp)
                {
                    result.ConfirmationCount++;
                    result.ConfirmationReasons.Add($"✓ RSI bouncing from {rsi:F1} and rising");
                }
            }

            // Confirmation 3: Increased buying volume
            if (currentVolume > avgVolume * 1.3m)
            {
                result.ConfirmationCount++;
                result.ConfirmationReasons.Add($"✓ Volume surge ({currentVolume / avgVolume:F1}x avg)");
            }
        }
        else if (direction == TrendDirection.Short)
        {
            // Confirmation 1: Bearish candlestick pattern
            if (candlePattern == CandlestickPattern.BearishEngulfing ||
                candlePattern == CandlestickPattern.ShootingStar ||
                candlePattern == CandlestickPattern.EveningStar ||
                candlePattern == CandlestickPattern.HangingMan)
            {
                result.ConfirmationCount++;
                result.ConfirmationReasons.Add($"✓ {candlePattern} pattern");
            }

            // Confirmation 2: RSI bouncing from 50-60 level and turning down
            if (rsi >= 45 && rsi <= 60 && rsiSeries.Count >= 2)
            {
                bool rsiTurningDown = rsiSeries[^1] < rsiSeries[^2];
                if (rsiTurningDown)
                {
                    result.ConfirmationCount++;
                    result.ConfirmationReasons.Add($"✓ RSI bouncing from {rsi:F1} and falling");
                }
            }

            // Confirmation 3: Increased selling volume
            if (currentVolume > avgVolume * 1.3m)
            {
                result.ConfirmationCount++;
                result.ConfirmationReasons.Add($"✓ Volume surge ({currentVolume / avgVolume:F1}x avg)");
            }
        }
    }

    private TradingSignal GenerateTradeSignal(
        TradingSignal signal,
        TrendAnalysisResult trendAnalysis,
        EntryAnalysisResult entryAnalysis)
    {
        var reasons = new List<string>
        {
            trendAnalysis.Reason,
            $"Pullback to {entryAnalysis.ConfluenceType} @ {entryAnalysis.ConfluenceLevel:F2}"
        };
        reasons.AddRange(entryAnalysis.ConfirmationReasons);

        if (trendAnalysis.TrendDirection == TrendDirection.Long)
        {
            signal.Type = entryAnalysis.ConfirmationCount >= 3 ? SignalType.StrongBuy : SignalType.Buy;

            // Entry: Open of next candle (simulated as current price)
            signal.EntryPrice = entryAnalysis.CurrentPrice;

            // Stop Loss: 0.1%-0.15% below recent swing low
            var stopLossDistance = entryAnalysis.SwingLow * StopLossPercent;
            signal.StopLoss = entryAnalysis.SwingLow - stopLossDistance;

            var riskDistance = signal.EntryPrice.Value - signal.StopLoss.Value;

            // TP1: Previous swing high (sell 50%)
            signal.TakeProfit1 = entryAnalysis.SwingHigh;

            // TP2: Trail below 9-EMA after TP1 hit
            // Calculate as extended target for initial setup
            signal.TakeProfit2 = signal.EntryPrice.Value + (riskDistance * 2.5m);

            // TP3: Extended trailing target
            signal.TakeProfit3 = signal.EntryPrice.Value + (riskDistance * 4m);
        }
        else // Short
        {
            signal.Type = entryAnalysis.ConfirmationCount >= 3 ? SignalType.StrongSell : SignalType.Sell;

            // Entry: Open of next candle
            signal.EntryPrice = entryAnalysis.CurrentPrice;

            // Stop Loss: 0.1%-0.15% above recent swing high
            var stopLossDistance = entryAnalysis.SwingHigh * StopLossPercent;
            signal.StopLoss = entryAnalysis.SwingHigh + stopLossDistance;

            var riskDistance = signal.StopLoss.Value - signal.EntryPrice.Value;

            // TP1: Previous swing low (cover 50%)
            signal.TakeProfit1 = entryAnalysis.SwingLow;

            // TP2: Trail above 9-EMA after TP1 hit
            signal.TakeProfit2 = signal.EntryPrice.Value - (riskDistance * 2.5m);

            // TP3: Extended trailing target
            signal.TakeProfit3 = signal.EntryPrice.Value - (riskDistance * 4m);
        }

        signal.Confidence = entryAnalysis.ConfirmationCount switch
        {
            3 => 0.90m,
            2 => 0.75m,
            _ => 0.50m
        };

        signal.Reason = string.Join(" | ", reasons);

        // Store additional metadata
        signal.Indicators["ConfluenceType"] = 0; // Placeholder for string
        signal.Indicators["ConfirmationCount"] = entryAnalysis.ConfirmationCount;

        return signal;
    }

    private static bool IsWithinZone(decimal price1, decimal price2, decimal zoneWidth)
    {
        if (price2 == 0) return false;
        var percentDiff = Math.Abs(price1 - price2) / price2;
        return percentDiff <= zoneWidth;
    }

    #region Result Classes

    private enum TrendDirection
    {
        None,
        Long,
        Short
    }

    private class TrendAnalysisResult
    {
        public TrendDirection TrendDirection { get; set; }
        public string Reason { get; set; } = string.Empty;
        public decimal Ema9 { get; set; }
        public decimal Ema21 { get; set; }
        public decimal Ema50 { get; set; }
        public decimal Vwap { get; set; }
        public decimal VwapSlope { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    private class EntryAnalysisResult
    {
        public decimal CurrentPrice { get; set; }
        public decimal Ema9 { get; set; }
        public decimal Ema21 { get; set; }
        public decimal Rsi { get; set; }
        public decimal Vwap { get; set; }
        public decimal CurrentVolume { get; set; }
        public decimal AverageVolume { get; set; }
        public List<decimal> HighVolumeNodes { get; set; } = new();
        public decimal SwingHigh { get; set; }
        public decimal SwingLow { get; set; }
        public CandlestickPattern CandlePattern { get; set; }
        public bool IsPullbackDetected { get; set; }
        public decimal ConfluenceLevel { get; set; }
        public string ConfluenceType { get; set; } = string.Empty;
        public int ConfirmationCount { get; set; }
        public List<string> ConfirmationReasons { get; set; } = new();
    }

    #endregion
}

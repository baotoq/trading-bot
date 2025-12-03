using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

/// <summary>
/// BTC Spot DCA Strategy - Long-term accumulation strategy combining:
/// 1. Regular DCA (Dollar-Cost Averaging) buy signals
/// 2. Enhanced buying on dips (when RSI oversold in bull market)
/// 3. Bull market filter (only buy above 200 EMA)
/// 4. NO stop loss - hold through volatility
/// 5. Exit only on major trend reversal
///
/// Designed for: 4h or 1d candles, LONG-only, spot trading
/// </summary>
public class BtcSpotDcaStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<BtcSpotDcaStrategy> _logger;

    public string Name => "BTC Spot DCA";

    public BtcSpotDcaStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<BtcSpotDcaStrategy> logger)
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
            // Use 4h candles for long-term trend analysis
            var candles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "4h")
                .OrderByDescending(c => c.OpenTime)
                .Take(200)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (candles.Count < 200)
            {
                signal.Reason = "Insufficient candle data (need 200 for 200 EMA)";
                signal.Confidence = 0;
                return signal;
            }

            // Calculate indicators
            var ema200 = _indicatorService.CalculateEMA(candles, 200);
            var ema50 = _indicatorService.CalculateEMA(candles, 50);
            var ema21 = _indicatorService.CalculateEMA(candles, 21);
            var rsi = _indicatorService.CalculateRSI(candles, 14);
            var (macd, macdSignal, histogram) = _indicatorService.CalculateMACD(candles);
            var (upperBB, middleBB, lowerBB) = _indicatorService.CalculateBollingerBands(candles, 20, 2);
            var avgVolume = _indicatorService.CalculateAverageVolume(candles, 20);
            var currentVolume = candles.Last().Volume;
            var currentPrice = candles.Last().ClosePrice;

            // Store indicators
            signal.Indicators["EMA200"] = ema200;
            signal.Indicators["EMA50"] = ema50;
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

            // ============================================
            // PHASE 1: Bull Market Filter (200 EMA)
            // ============================================
            bool inBullMarket = currentPrice > ema200;

            if (!inBullMarket)
            {
                // MAJOR TREND REVERSAL - Consider exiting positions
                signal.Type = SignalType.Sell;
                signal.Confidence = 0.90m;
                signal.Reason = $"⚠️ TREND REVERSAL: Price below 200 EMA (${ema200:F2}) - Consider exiting long positions";
                signal.EntryPrice = currentPrice;
                signal.StopLoss = null; // No stop loss in DCA strategy
                signal.TakeProfit1 = null;
                signal.TakeProfit2 = null;
                signal.TakeProfit3 = null;
                return signal;
            }

            // ============================================
            // PHASE 2: DCA Buy Signal Generation
            // ============================================
            var reasons = new List<string>();
            var signalType = SignalType.Buy;
            var confidence = 0.60m; // Base DCA confidence

            reasons.Add($"✓ Bull market confirmed (Price ${currentPrice:F2} > 200 EMA ${ema200:F2})");

            // ============================================
            // PHASE 3: Enhanced Buy Signal Detection
            // ============================================

            // STRONG BUY conditions (Dip buying opportunities)
            int enhancedConfirmations = 0;

            // 1. RSI oversold (< 40) - Great buying opportunity
            if (rsi < 40)
            {
                enhancedConfirmations++;
                reasons.Add($"✓✓ RSI oversold ({rsi:F2}) - Excellent dip buying opportunity");
            }
            else if (rsi < 50)
            {
                enhancedConfirmations++;
                reasons.Add($"✓ RSI in accumulation zone ({rsi:F2})");
            }

            // 2. Price near or below 50 EMA (pullback in uptrend)
            if (currentPrice < ema50 * 1.02m)
            {
                enhancedConfirmations++;
                reasons.Add($"✓✓ Price pullback to 50 EMA (${ema50:F2}) - Dip opportunity");
            }

            // 3. Price near lower Bollinger Band (oversold)
            if (currentPrice < lowerBB * 1.02m)
            {
                enhancedConfirmations++;
                reasons.Add($"✓✓ Price near lower BB (${lowerBB:F2}) - Oversold bounce expected");
            }

            // 4. MACD bullish divergence or positive histogram
            if (histogram > 0)
            {
                enhancedConfirmations++;
                reasons.Add("✓ MACD histogram positive - Bullish momentum");
            }

            // 5. High volume (> 1.5x average) - Institutional buying
            if (currentVolume > avgVolume * 1.5m)
            {
                enhancedConfirmations++;
                reasons.Add($"✓ High volume ({currentVolume / avgVolume:F2}x avg) - Strong buying interest");
            }

            // 6. Price above all key EMAs (strong uptrend)
            if (currentPrice > ema50 && currentPrice > ema21 && ema50 > ema200)
            {
                enhancedConfirmations++;
                reasons.Add("✓ Strong uptrend (Price > EMA21 > EMA50 > EMA200)");
            }

            // ============================================
            // PHASE 4: Signal Classification
            // ============================================

            if (enhancedConfirmations >= 4)
            {
                // STRONG BUY - Multiple dip-buying confirmations
                signalType = SignalType.StrongBuy;
                confidence = 0.95m;
                reasons.Insert(0, $"🚀 STRONG DIP BUY ({enhancedConfirmations}/6 confirmations)");
            }
            else if (enhancedConfirmations >= 2)
            {
                // BUY - Good accumulation opportunity
                signalType = SignalType.Buy;
                confidence = 0.75m;
                reasons.Insert(0, $"💰 GOOD BUY OPPORTUNITY ({enhancedConfirmations}/6 confirmations)");
            }
            else
            {
                // Regular DCA - Standard accumulation
                signalType = SignalType.Buy;
                confidence = 0.60m;
                reasons.Insert(0, "📊 REGULAR DCA BUY (Systematic accumulation)");
            }

            // ============================================
            // PHASE 5: Position Sizing Guidance
            // ============================================

            // Suggest position size based on signal strength
            string positionSizeGuidance = confidence switch
            {
                >= 0.90m => "Position Size: 20-25% (Strong dip opportunity)",
                >= 0.75m => "Position Size: 15-20% (Good entry)",
                _ => "Position Size: 10-15% (Regular DCA)"
            };
            reasons.Add(positionSizeGuidance);

            // ============================================
            // PHASE 6: Set Signal Properties
            // ============================================

            signal.Type = signalType;
            signal.Confidence = confidence;
            signal.Reason = string.Join(" | ", reasons);
            signal.EntryPrice = currentPrice;

            // NO STOP LOSS - Hold through volatility (spot trading)
            signal.StopLoss = null;

            // Take profit targets (optional for partial profit taking)
            // These are GUIDANCE only - main strategy is HODL
            signal.TakeProfit1 = currentPrice * 1.20m; // +20% - Optional partial profit
            signal.TakeProfit2 = currentPrice * 1.50m; // +50% - Optional partial profit
            signal.TakeProfit3 = currentPrice * 2.00m; // +100% - Long-term target

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
}

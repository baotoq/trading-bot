using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Strategies;

public class BollingerSqueezeStrategy : IStrategy
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<BollingerSqueezeStrategy> _logger;

    public string Name => "Bollinger Squeeze";

    public BollingerSqueezeStrategy(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<BollingerSqueezeStrategy> logger)
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

            // Calculate Bollinger Bands for current and previous periods
            var (upperBB, middleBB, lowerBB) = _indicatorService.CalculateBollingerBands(fiveMinCandles, 20, 2);

            // Calculate BB for last 20 candles to detect squeeze
            var bbWidths = new List<decimal>();
            for (int i = 20; i <= fiveMinCandles.Count; i++)
            {
                var subset = fiveMinCandles.Take(i).ToList();
                var (upper, _, lower) = _indicatorService.CalculateBollingerBands(subset, 20, 2);
                var width = (upper - lower) / middleBB * 100; // Width as percentage
                bbWidths.Add(width);
            }

            // Calculate average BB width and current width
            var avgBBWidth = bbWidths.Average();
            var currentBBWidth = bbWidths.Last();
            var isSqueezing = currentBBWidth < avgBBWidth * 0.7m; // Squeeze when width < 70% of average

            // Calculate volume
            var avgVolume = _indicatorService.CalculateAverageVolume(fiveMinCandles, 20);
            var currentVolume = fiveMinCandles.Last().Volume;
            var isHighVolume = currentVolume > avgVolume * 3m; // Breakout with 3x volume

            // Calculate EMA for trend confirmation
            var ema21 = _indicatorService.CalculateEMA(fiveMinCandles, 21);
            var ema50 = _indicatorService.CalculateEMA(fiveMinCandles, 50);

            // Calculate RSI
            var rsi = _indicatorService.CalculateRSI(fiveMinCandles, 14);

            var currentPrice = fiveMinCandles.Last().ClosePrice;
            var prevPrice = fiveMinCandles[^2].ClosePrice;

            // Store indicators
            signal.Indicators["UpperBB"] = upperBB;
            signal.Indicators["MiddleBB"] = middleBB;
            signal.Indicators["LowerBB"] = lowerBB;
            signal.Indicators["BBWidth"] = currentBBWidth;
            signal.Indicators["AvgBBWidth"] = avgBBWidth;
            signal.Indicators["Volume"] = currentVolume;
            signal.Indicators["AvgVolume"] = avgVolume;
            signal.Indicators["EMA21"] = ema21;
            signal.Indicators["EMA50"] = ema50;
            signal.Indicators["RSI"] = rsi;
            signal.Price = currentPrice;

            // Check if squeeze is present
            if (!isSqueezing)
            {
                signal.Reason = $"No squeeze detected (BB width: {currentBBWidth:F2}% vs avg: {avgBBWidth:F2}%)";
                signal.Confidence = 0;
                return signal;
            }

            _logger.LogInformation("Squeeze detected: BB width {Current}% vs avg {Avg}%",
                currentBBWidth, avgBBWidth);

            // Wait for breakout with volume
            if (!isHighVolume)
            {
                signal.Reason = $"Waiting for volume breakout (current: {currentVolume / avgVolume:F2}x avg)";
                signal.Confidence = 0.3m;
                return signal;
            }

            // Check for LONG breakout
            if (currentPrice > prevPrice && currentPrice > middleBB)
            {
                signal = await CheckLongBreakout(
                    signal, currentPrice, upperBB, middleBB, lowerBB,
                    ema21, ema50, rsi, currentVolume, avgVolume, cancellationToken);
            }
            // Check for SHORT breakout
            else if (currentPrice < prevPrice && currentPrice < middleBB)
            {
                signal = await CheckShortBreakout(
                    signal, currentPrice, upperBB, middleBB, lowerBB,
                    ema21, ema50, rsi, currentVolume, avgVolume, cancellationToken);
            }
            else
            {
                signal.Reason = "Squeeze present but no clear breakout direction";
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

    private async Task<TradingSignal> CheckLongBreakout(
        TradingSignal signal,
        decimal currentPrice,
        decimal upperBB,
        decimal middleBB,
        decimal lowerBB,
        decimal ema21,
        decimal ema50,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;

        // PRIMARY SIGNAL: Breakout above middle BB with high volume
        reasons.Add("✓ Bollinger Squeeze detected");
        reasons.Add($"✓ Volume breakout ({currentVolume / avgVolume:F2}x avg)");
        reasons.Add($"✓ Price breaking above middle BB (${middleBB:F2})");

        // CONFIRMATION SIGNALS
        if (currentPrice > ema21)
        {
            confirmations++;
            reasons.Add("✓ Price above EMA(21)");
        }

        if (ema21 > ema50)
        {
            confirmations++;
            reasons.Add("✓ Bullish EMA alignment");
        }

        if (rsi > 50 && rsi < 75)
        {
            confirmations++;
            reasons.Add($"✓ RSI momentum confirmed ({rsi:F2})");
        }

        // INVALIDATION CHECKS
        if (rsi > 80)
        {
            signal.Reason = "RSI extremely overbought";
            signal.Confidence = 0;
            return signal;
        }

        if (currentPrice > upperBB * 1.02m) // 2% above upper band
        {
            signal.Reason = "Price too far above upper BB";
            signal.Confidence = 0;
            return signal;
        }

        // Need at least 1 confirmation for valid signal
        if (confirmations < 1)
        {
            signal.Reason = "Insufficient confirmations for breakout";
            signal.Confidence = 0.4m;
            return signal;
        }

        // VALID LONG BREAKOUT SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongBuy : SignalType.Buy;
        signal.Confidence = confirmations switch
        {
            3 => 0.95m,
            2 => 0.80m,
            1 => 0.65m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        return signal;
    }

    private async Task<TradingSignal> CheckShortBreakout(
        TradingSignal signal,
        decimal currentPrice,
        decimal upperBB,
        decimal middleBB,
        decimal lowerBB,
        decimal ema21,
        decimal ema50,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var confirmations = 0;

        // PRIMARY SIGNAL: Breakdown below middle BB with high volume
        reasons.Add("✓ Bollinger Squeeze detected");
        reasons.Add($"✓ Volume breakout ({currentVolume / avgVolume:F2}x avg)");
        reasons.Add($"✓ Price breaking below middle BB (${middleBB:F2})");

        // CONFIRMATION SIGNALS
        if (currentPrice < ema21)
        {
            confirmations++;
            reasons.Add("✓ Price below EMA(21)");
        }

        if (ema21 < ema50)
        {
            confirmations++;
            reasons.Add("✓ Bearish EMA alignment");
        }

        if (rsi < 50 && rsi > 25)
        {
            confirmations++;
            reasons.Add($"✓ RSI momentum confirmed ({rsi:F2})");
        }

        // INVALIDATION CHECKS
        if (rsi < 20)
        {
            signal.Reason = "RSI extremely oversold";
            signal.Confidence = 0;
            return signal;
        }

        if (currentPrice < lowerBB * 0.98m) // 2% below lower band
        {
            signal.Reason = "Price too far below lower BB";
            signal.Confidence = 0;
            return signal;
        }

        // Need at least 1 confirmation for valid signal
        if (confirmations < 1)
        {
            signal.Reason = "Insufficient confirmations for breakdown";
            signal.Confidence = 0.4m;
            return signal;
        }

        // VALID SHORT BREAKOUT SIGNAL
        signal.Type = confirmations >= 3 ? SignalType.StrongSell : SignalType.Sell;
        signal.Confidence = confirmations switch
        {
            3 => 0.95m,
            2 => 0.80m,
            1 => 0.65m,
            _ => 0.5m
        };
        signal.Reason = string.Join(" | ", reasons);

        return signal;
    }
}

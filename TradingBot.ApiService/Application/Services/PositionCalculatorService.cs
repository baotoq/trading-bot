using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

public class PositionCalculatorService : IPositionCalculatorService
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<PositionCalculatorService> _logger;

    public PositionCalculatorService(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<PositionCalculatorService> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _logger = logger;
    }

    public async Task<PositionParameters> CalculatePositionParametersAsync(
        TradingSignal signal,
        decimal accountEquity,
        decimal riskPercent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calculating position parameters for {Symbol} {SignalType} with {RiskPercent}% risk",
            signal.Symbol, signal.Type, riskPercent);

        var parameters = new PositionParameters();

        try
        {
            // Get 5m candles for ATR calculation
            var fiveMinCandles = await _context.Candles
                .Where(c => c.Symbol == signal.Symbol && c.Interval == "5m")
                .OrderByDescending(c => c.OpenTime)
                .Take(50)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (fiveMinCandles.Count < 15)
            {
                parameters.IsValid = false;
                parameters.ValidationError = "Insufficient candle data for ATR calculation";
                return parameters;
            }

            var currentPrice = signal.Price;
            var atr = _indicatorService.CalculateATR(fiveMinCandles, 14);
            var swingHigh = _indicatorService.GetSwingHigh(fiveMinCandles, 10);
            var swingLow = _indicatorService.GetSwingLow(fiveMinCandles, 10);

            // STEP 1: Calculate Entry Price
            var isLong = signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy;
            var isShort = signal.Type == SignalType.Sell || signal.Type == SignalType.StrongSell;

            if (isLong)
            {
                // Entry slightly above market for limit order
                parameters.EntryPrice = currentPrice * 1.0002m; // +0.02%
            }
            else if (isShort)
            {
                // Entry slightly below market for limit order
                parameters.EntryPrice = currentPrice * 0.9998m; // -0.02%
            }
            else
            {
                parameters.IsValid = false;
                parameters.ValidationError = "Invalid signal type for position calculation";
                return parameters;
            }

            // STEP 2: Calculate Stop-Loss
            var atrStopDistance = 1.5m * atr;

            if (isLong)
            {
                // Stop-loss below entry
                var atrBasedStop = parameters.EntryPrice - atrStopDistance;
                var swingBasedStop = swingLow * 0.999m; // Slightly below swing low

                // Use the closer stop (more conservative)
                parameters.StopLoss = Math.Max(atrBasedStop, swingBasedStop);
            }
            else // Short
            {
                // Stop-loss above entry
                var atrBasedStop = parameters.EntryPrice + atrStopDistance;
                var swingBasedStop = swingHigh * 1.001m; // Slightly above swing high

                // Use the closer stop (more conservative)
                parameters.StopLoss = Math.Min(atrBasedStop, swingBasedStop);
            }

            parameters.StopLossDistance = Math.Abs(parameters.EntryPrice - parameters.StopLoss);
            parameters.StopLossPercent = (parameters.StopLossDistance / parameters.EntryPrice) * 100;

            // Validate stop-loss is not too wide (max 2% of account)
            if (parameters.StopLossPercent > 2.5m)
            {
                parameters.IsValid = false;
                parameters.ValidationError = $"Stop-loss too wide ({parameters.StopLossPercent:F2}% > 2.5%)";
                return parameters;
            }

            // STEP 3: Calculate Take-Profit Levels
            if (isLong)
            {
                parameters.TakeProfit1 = parameters.EntryPrice + (2 * parameters.StopLossDistance); // 2R
                parameters.TakeProfit2 = parameters.EntryPrice + (3 * parameters.StopLossDistance); // 3R
                parameters.TakeProfit3 = parameters.EntryPrice + (5 * parameters.StopLossDistance); // 5R (trailing)
            }
            else // Short
            {
                parameters.TakeProfit1 = parameters.EntryPrice - (2 * parameters.StopLossDistance); // 2R
                parameters.TakeProfit2 = parameters.EntryPrice - (3 * parameters.StopLossDistance); // 3R
                parameters.TakeProfit3 = parameters.EntryPrice - (5 * parameters.StopLossDistance); // 5R (trailing)
            }

            // Ensure TPs are valid (positive prices)
            if (parameters.TakeProfit1 <= 0 || parameters.TakeProfit2 <= 0 || parameters.TakeProfit3 <= 0)
            {
                parameters.IsValid = false;
                parameters.ValidationError = "Invalid take-profit calculation resulted in negative price";
                return parameters;
            }

            // STEP 4: Calculate Position Size
            parameters.RiskAmount = accountEquity * (riskPercent / 100);
            parameters.PositionSize = parameters.RiskAmount / (parameters.StopLossPercent / 100);
            parameters.Quantity = parameters.PositionSize / parameters.EntryPrice;

            // STEP 5: Determine Leverage
            // Get 15m candles for volatility assessment
            var fifteenMinCandles = await _context.Candles
                .Where(c => c.Symbol == signal.Symbol && c.Interval == "15m")
                .OrderByDescending(c => c.OpenTime)
                .Take(50)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (fifteenMinCandles.Count >= 15)
            {
                var atr15m = _indicatorService.CalculateATR(fifteenMinCandles, 14);
                var atrPercent15m = (atr15m / currentPrice) * 100;

                // Determine regime for leverage
                var ema21 = _indicatorService.CalculateEMA(fifteenMinCandles, 21);
                var ema50 = _indicatorService.CalculateEMA(fifteenMinCandles, 50);
                var isTrending = Math.Abs((ema21 - ema50) / ema50) > 0.002m; // > 0.2% spread

                if (atrPercent15m > 2.0m)
                {
                    // High volatility - use lower leverage
                    parameters.RecommendedLeverage = 5;
                }
                else if (isTrending)
                {
                    // Trending market with normal volatility
                    parameters.RecommendedLeverage = signal.Confidence > 0.8m ? 15 : 10;
                }
                else
                {
                    // Ranging market
                    parameters.RecommendedLeverage = 8;
                }
            }
            else
            {
                // Default conservative leverage
                parameters.RecommendedLeverage = 10;
            }

            // STEP 6: Calculate Margin Required
            parameters.MarginRequired = parameters.PositionSize / parameters.RecommendedLeverage;

            // Validate margin requirement
            if (parameters.MarginRequired > accountEquity * 0.2m)
            {
                parameters.IsValid = false;
                parameters.ValidationError = $"Position requires too much margin ({parameters.MarginRequired:F2} > 20% of equity)";
                return parameters;
            }

            parameters.IsValid = true;

            _logger.LogInformation(
                "Position calculated for {Symbol}: Entry=${Entry}, SL=${SL} ({SLPercent}%), " +
                "TP1=${TP1}, TP2=${TP2}, TP3=${TP3}, Size={Size}, Qty={Qty}, Leverage={Leverage}x",
                signal.Symbol, parameters.EntryPrice, parameters.StopLoss, parameters.StopLossPercent,
                parameters.TakeProfit1, parameters.TakeProfit2, parameters.TakeProfit3,
                parameters.PositionSize, parameters.Quantity, parameters.RecommendedLeverage);

            return parameters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating position parameters for {Symbol}", signal.Symbol);
            parameters.IsValid = false;
            parameters.ValidationError = $"Calculation error: {ex.Message}";
            return parameters;
        }
    }
}

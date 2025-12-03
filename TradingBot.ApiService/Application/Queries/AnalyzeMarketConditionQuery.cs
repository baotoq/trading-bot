using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Queries;

public record AnalyzeMarketConditionQuery(Symbol Symbol) : IRequest<MarketCondition>;

public class AnalyzeMarketConditionQueryHandler : IRequestHandler<AnalyzeMarketConditionQuery, MarketCondition>
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<AnalyzeMarketConditionQueryHandler> _logger;

    public AnalyzeMarketConditionQueryHandler(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<AnalyzeMarketConditionQueryHandler> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _logger = logger;
    }

    public async Task<MarketCondition> Handle(AnalyzeMarketConditionQuery request, CancellationToken cancellationToken)
    {
        var symbol = request.Symbol;
        _logger.LogInformation("Analyzing market condition for {Symbol}", symbol);

        var condition = new MarketCondition
        {
            Symbol = symbol,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Get 1H candles for last 24 hours (24 candles)
            var oneHourCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "1h")
                .OrderByDescending(c => c.OpenTime)
                .Take(50)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (oneHourCandles.Count < 50)
            {
                condition.CanTrade = false;
                condition.Reason = "Insufficient historical data for 1H analysis";
                return condition;
            }

            // Get 15m candles for ATR calculation
            var fifteenMinCandles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "15m")
                .OrderByDescending(c => c.OpenTime)
                .Take(50)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (fifteenMinCandles.Count < 50)
            {
                condition.CanTrade = false;
                condition.Reason = "Insufficient historical data for 15m analysis";
                return condition;
            }

            // Calculate ATR and volatility
            condition.Atr = _indicatorService.CalculateATR(fifteenMinCandles, 14);
            var currentPrice = fifteenMinCandles.Last().ClosePrice;
            condition.AtrPercent = (condition.Atr / currentPrice) * 100;

            // Check volatility levels
            condition.IsLowVolatility = condition.AtrPercent < 0.3m;
            condition.IsVolatile = condition.AtrPercent > 2.0m;

            if (condition.IsLowVolatility)
            {
                condition.CanTrade = false;
                condition.Reason = $"Low volatility detected (ATR: {condition.AtrPercent:F2}% < 0.3%)";
                return condition;
            }

            // Determine market regime using 1H chart
            var ema50 = _indicatorService.CalculateEMA(oneHourCandles, 50);
            var ema21 = _indicatorService.CalculateEMA(oneHourCandles, 21);
            var lastClose = oneHourCandles.Last().ClosePrice;

            // Trend determination
            var emaSpreadPercent = Math.Abs((ema21 - ema50) / ema50) * 100;

            if (emaSpreadPercent < 0.2m)
            {
                condition.Regime = MarketRegime.Ranging;
                condition.IsNeutral = true;
            }
            else if (condition.IsVolatile)
            {
                condition.Regime = MarketRegime.Volatile;
            }
            else
            {
                condition.Regime = MarketRegime.Trending;
            }

            // Determine trend direction
            condition.IsBullish = ema21 > ema50 && lastClose > ema21;
            condition.IsBearish = ema21 < ema50 && lastClose < ema21;
            condition.IsNeutral = !condition.IsBullish && !condition.IsBearish;

            // Note: Funding rate would come from Binance API
            // For now, we'll set it to 0 as placeholder
            condition.FundingRate = 0m; // TODO: Fetch from Binance API

            // Determine bias from funding rate
            if (condition.FundingRate > 0.1m)
            {
                condition.Bias = TradeSide.Short; // Longs over-leveraged
            }
            else if (condition.FundingRate < -0.1m)
            {
                condition.Bias = TradeSide.Long; // Shorts over-leveraged
            }

            // Final trading decision
            if (condition.IsNeutral)
            {
                condition.CanTrade = false;
                condition.Reason = "Market is neutral/ranging - no clear trend";
            }
            else
            {
                condition.CanTrade = true;
                condition.Reason = $"Market is {condition.Regime} with {(condition.IsBullish ? "bullish" : "bearish")} bias";
            }

            _logger.LogInformation(
                "Market condition for {Symbol}: Regime={Regime}, ATR={Atr}%, Trend={Trend}, CanTrade={CanTrade}",
                symbol, condition.Regime, condition.AtrPercent,
                condition.IsBullish ? "Bullish" : condition.IsBearish ? "Bearish" : "Neutral",
                condition.CanTrade);

            return condition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing market condition for {Symbol}", symbol);
            condition.CanTrade = false;
            condition.Reason = $"Error: {ex.Message}";
            return condition;
        }
    }
}

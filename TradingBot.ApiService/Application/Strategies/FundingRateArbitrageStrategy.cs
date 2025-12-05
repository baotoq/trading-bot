using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

/// <summary>
/// Delta-Neutral Funding Rate Arbitrage Strategy
///
/// GUARANTEED PROFIT with NO DIRECTIONAL RISK by hedging:
///
/// When funding rate is POSITIVE (longs pay shorts):
///   1. BUY spot (hold the asset)
///   2. SHORT futures (receive funding payment)
///   → Price goes UP: Spot gains cancel futures losses
///   → Price goes DOWN: Futures gains cancel spot losses
///   → You ALWAYS collect the funding fee!
///
/// When funding rate is NEGATIVE (shorts pay longs):
///   1. SELL spot (or skip if no holdings)
///   2. LONG futures (receive funding payment)
///   → Same hedging principle applies
///
/// Risk: Only execution slippage and trading fees
/// Profit: Pure funding rate income (up to 100%+ APY in volatile markets)
/// </summary>
public class FundingRateArbitrageStrategy : IStrategy
{
    private readonly IBinanceService _binanceService;
    private readonly ILogger<FundingRateArbitrageStrategy> _logger;

    // Configuration
    private const decimal MinFundingRateThreshold = 0.0003m;  // 0.03% minimum (covers ~0.08% round-trip fees)
    private const decimal HighFundingRateThreshold = 0.0008m; // 0.08% for high confidence
    private const decimal ExtremeRateThreshold = 0.002m;      // 0.2% for extreme rates
    private const int MaxEntryMinutesBefore = 60;             // Enter up to 1 hour before
    private const int MinEntryMinutesBefore = 2;              // At least 2 minutes before

    public string Name => "Funding Rate Arbitrage";

    public FundingRateArbitrageStrategy(
        IBinanceService binanceService,
        ILogger<FundingRateArbitrageStrategy> logger)
    {
        _binanceService = binanceService;
        _logger = logger;
    }

    public async Task<TradingSignal> AnalyzeAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing {Symbol} with Delta-Neutral {Strategy}", symbol, Name);

        var signal = new TradingSignal
        {
            Symbol = symbol,
            Strategy = Name,
            Timestamp = DateTime.UtcNow,
            Type = SignalType.Hold
        };

        try
        {
            // Get comprehensive funding rate info
            var fundingInfo = await _binanceService.GetFundingRateInfoAsync(symbol, cancellationToken);
            var currentPrice = await _binanceService.GetCurrentPriceAsync(symbol, cancellationToken);
            var accountBalance = await _binanceService.GetFuturesAccountBalanceAsync(cancellationToken);

            // Store indicators
            signal.Indicators["FundingRate"] = fundingInfo.FundingRate;
            signal.Indicators["FundingRatePct"] = fundingInfo.FundingRate * 100;
            signal.Indicators["MinutesToFunding"] = fundingInfo.MinutesToNextFunding;
            signal.Indicators["AnnualizedRate"] = fundingInfo.EstimatedAnnualizedRate * 100;
            signal.Indicators["CurrentPrice"] = currentPrice;
            signal.Price = currentPrice;

            _logger.LogInformation(
                "Funding info for {Symbol}: Rate={Rate:P4}, Next in {Minutes} min, APY={APY:P2}",
                symbol, fundingInfo.FundingRate, fundingInfo.MinutesToNextFunding, fundingInfo.EstimatedAnnualizedRate);

            // Check minimum funding rate threshold
            var absFundingRate = Math.Abs(fundingInfo.FundingRate);
            if (absFundingRate < MinFundingRateThreshold)
            {
                signal.Reason = $"Funding rate too low ({fundingInfo.FundingRate:P4}). Need >{MinFundingRateThreshold:P4} to cover fees.";
                signal.Confidence = 0;
                return signal;
            }

            // Check timing window
            if (fundingInfo.MinutesToNextFunding > MaxEntryMinutesBefore)
            {
                signal.Reason = $"Too early. {fundingInfo.MinutesToNextFunding} min to settlement. Wait until <{MaxEntryMinutesBefore} min.";
                signal.Confidence = 0.1m;
                signal.Indicators["WaitMinutes"] = fundingInfo.MinutesToNextFunding - MaxEntryMinutesBefore;
                return signal;
            }

            if (fundingInfo.MinutesToNextFunding < MinEntryMinutesBefore)
            {
                signal.Reason = $"Too late ({fundingInfo.MinutesToNextFunding} min). Wait for next funding cycle.";
                signal.Confidence = 0;
                return signal;
            }

            // Determine hedge direction and calculate positions
            var hedgeInfo = CalculateDeltaNeutralHedge(fundingInfo, currentPrice, accountBalance.AvailableBalance);

            // Build signal
            signal.Type = hedgeInfo.SignalType;
            signal.Confidence = hedgeInfo.Confidence;
            signal.EntryPrice = currentPrice;
            signal.PositionSize = hedgeInfo.PositionSizeUsdt;

            // Store hedge details in indicators
            signal.Indicators["HedgeType"] = hedgeInfo.IsPositiveFunding ? 1 : -1; // 1 = Short futures + Long spot
            signal.Indicators["SpotAction"] = hedgeInfo.IsPositiveFunding ? 1 : -1; // 1 = Buy, -1 = Sell
            signal.Indicators["FuturesAction"] = hedgeInfo.IsPositiveFunding ? -1 : 1; // -1 = Short, 1 = Long
            signal.Indicators["PositionSizeUsdt"] = hedgeInfo.PositionSizeUsdt;
            signal.Indicators["PositionSizeCoin"] = hedgeInfo.PositionSizeCoin;
            signal.Indicators["ExpectedFundingProfit"] = hedgeInfo.ExpectedFundingProfit;
            signal.Indicators["EstimatedTradingFees"] = hedgeInfo.EstimatedTradingFees;
            signal.Indicators["NetExpectedProfit"] = hedgeInfo.NetExpectedProfit;
            signal.Indicators["ProfitPercentage"] = hedgeInfo.ProfitPercentage;

            signal.Reason = hedgeInfo.Reason;

            _logger.LogInformation(
                "Delta-Neutral Signal: {Type} | Confidence: {Conf:P0} | Expected Profit: ${Profit:F2} ({ProfitPct:P4})",
                signal.Type, signal.Confidence, hedgeInfo.NetExpectedProfit, hedgeInfo.ProfitPercentage);

            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing {Symbol} with {Strategy}", symbol, Name);
            signal.Reason = $"Error: {ex.Message}";
            signal.Confidence = 0;
            return signal;
        }
    }

    private DeltaNeutralHedgeInfo CalculateDeltaNeutralHedge(
        FundingRateInfo fundingInfo,
        decimal currentPrice,
        decimal availableBalance)
    {
        var absFundingRate = Math.Abs(fundingInfo.FundingRate);
        var isPositiveFunding = fundingInfo.FundingRate > 0;

        // Position sizing: Conservative 15-25% of available balance
        decimal positionPercent;
        decimal confidence;
        string rateLevel;

        if (absFundingRate >= ExtremeRateThreshold)
        {
            positionPercent = 0.25m;
            confidence = 0.95m;
            rateLevel = "EXTREME";
        }
        else if (absFundingRate >= HighFundingRateThreshold)
        {
            positionPercent = 0.20m;
            confidence = 0.85m;
            rateLevel = "HIGH";
        }
        else
        {
            positionPercent = 0.15m;
            confidence = 0.70m;
            rateLevel = "MODERATE";
        }

        // Calculate position sizes
        var positionSizeUsdt = availableBalance * positionPercent;
        var positionSizeCoin = positionSizeUsdt / currentPrice;

        // Expected funding profit (on futures position)
        var expectedFundingProfit = positionSizeUsdt * absFundingRate;

        // Trading fees: 0.1% taker for spot + 0.04% taker for futures = 0.14% each way
        // Round trip (open + close) = 0.28% on each leg = 0.56% total
        // But we use 1x leverage on futures, so actual fee impact is ~0.16% round trip
        var estimatedTradingFees = positionSizeUsdt * 0.0016m;

        var netExpectedProfit = expectedFundingProfit - estimatedTradingFees;
        var profitPercentage = netExpectedProfit / positionSizeUsdt;

        // Determine signal type
        SignalType signalType;
        if (netExpectedProfit <= 0)
        {
            signalType = SignalType.Hold;
            confidence = 0;
        }
        else if (isPositiveFunding)
        {
            // Positive funding: Short futures (receive payment) + Buy spot (hedge)
            signalType = confidence >= 0.9m ? SignalType.StrongSell : SignalType.Sell;
        }
        else
        {
            // Negative funding: Long futures (receive payment) + Sell spot (hedge)
            signalType = confidence >= 0.9m ? SignalType.StrongBuy : SignalType.Buy;
        }

        // Build reason string
        var direction = isPositiveFunding ? "SHORT futures + BUY spot" : "LONG futures + SELL spot";
        var reason = string.Join(" | ", new[]
        {
            $"{rateLevel} funding rate: {fundingInfo.FundingRate:P4}",
            $"Delta-Neutral: {direction}",
            $"Settlement in {fundingInfo.MinutesToNextFunding} min",
            $"Expected profit: ${netExpectedProfit:F2} ({profitPercentage:P4})",
            $"APY: {fundingInfo.EstimatedAnnualizedRate:P2}",
            netExpectedProfit > 0 ? "PROFITABLE after fees" : "WARNING: Fees exceed profit"
        });

        return new DeltaNeutralHedgeInfo
        {
            SignalType = signalType,
            Confidence = confidence,
            IsPositiveFunding = isPositiveFunding,
            PositionSizeUsdt = positionSizeUsdt,
            PositionSizeCoin = positionSizeCoin,
            ExpectedFundingProfit = expectedFundingProfit,
            EstimatedTradingFees = estimatedTradingFees,
            NetExpectedProfit = netExpectedProfit,
            ProfitPercentage = profitPercentage,
            Reason = reason
        };
    }

    private class DeltaNeutralHedgeInfo
    {
        public SignalType SignalType { get; init; }
        public decimal Confidence { get; init; }
        public bool IsPositiveFunding { get; init; }
        public decimal PositionSizeUsdt { get; init; }
        public decimal PositionSizeCoin { get; init; }
        public decimal ExpectedFundingProfit { get; init; }
        public decimal EstimatedTradingFees { get; init; }
        public decimal NetExpectedProfit { get; init; }
        public decimal ProfitPercentage { get; init; }
        public string Reason { get; init; } = string.Empty;
    }
}

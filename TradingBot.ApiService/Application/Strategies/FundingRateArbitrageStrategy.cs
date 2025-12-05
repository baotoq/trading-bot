using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

/// <summary>
/// Funding Rate Arbitrage Strategy - Low-risk profit from funding fees
///
/// How it works:
/// 1. When funding rate is high positive: Longs pay shorts → Go SHORT to receive funding
/// 2. When funding rate is high negative: Shorts pay longs → Go LONG to receive funding
///
/// Risk Management:
/// - Uses minimal leverage (1x-2x) to avoid liquidation
/// - Only enters ~1 hour before funding settlement
/// - Exits after collecting funding fee
/// - Requires minimum funding rate threshold to cover trading fees
///
/// Funding Settlement Times (Binance): 00:00, 08:00, 16:00 UTC
/// </summary>
public class FundingRateArbitrageStrategy : IStrategy
{
    private readonly IBinanceService _binanceService;
    private readonly ILogger<FundingRateArbitrageStrategy> _logger;

    // Configuration constants
    private const decimal MinFundingRateThreshold = 0.0005m;  // 0.05% minimum to cover fees
    private const decimal HighFundingRateThreshold = 0.001m;  // 0.1% for high confidence
    private const decimal ExtremeRateThreshold = 0.003m;      // 0.3% for extreme rates
    private const int OptimalEntryMinutesBefore = 60;         // Enter 1 hour before settlement
    private const int MaxEntryMinutesBefore = 120;            // Don't enter more than 2 hours before
    private const int MinEntryMinutesBefore = 5;              // At least 5 minutes before

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
            // Step 1: Get current funding rate
            var fundingRate = await _binanceService.GetFundingRateAsync(symbol, cancellationToken);
            signal.Indicators["FundingRate"] = fundingRate;
            signal.Indicators["FundingRatePct"] = fundingRate * 100;

            _logger.LogInformation("Current funding rate for {Symbol}: {Rate:P4}", symbol, fundingRate);

            // Step 2: Check if funding rate meets minimum threshold
            var absFundingRate = Math.Abs(fundingRate);
            if (absFundingRate < MinFundingRateThreshold)
            {
                signal.Reason = $"Funding rate too low ({fundingRate:P4}). Minimum: {MinFundingRateThreshold:P4}";
                signal.Confidence = 0;
                return signal;
            }

            // Step 3: Check timing - are we within the optimal entry window?
            var timingAnalysis = AnalyzeFundingTiming();
            signal.Indicators["MinutesToSettlement"] = timingAnalysis.MinutesToSettlement;
            signal.Indicators["NextSettlementHour"] = timingAnalysis.NextSettlementHour;

            if (!timingAnalysis.IsOptimalEntry)
            {
                signal.Reason = timingAnalysis.TimingReason;
                signal.Confidence = 0.1m; // Low confidence - not optimal timing
                return signal;
            }

            // Step 4: Determine signal direction and strength
            var (signalType, confidence, reasons) = DetermineSignal(fundingRate, absFundingRate, timingAnalysis);
            signal.Type = signalType;
            signal.Confidence = confidence;

            // Step 5: Calculate position parameters (conservative)
            if (signalType != SignalType.Hold)
            {
                // Get current price for calculations
                var accountBalance = await _binanceService.GetFuturesAccountBalanceAsync(cancellationToken);

                // Use current mark price as entry (will be market order)
                signal.EntryPrice = null; // Market order - actual entry determined at execution

                // For funding arbitrage, we don't need traditional stop-loss
                // Risk is minimal since we're only holding for ~1 hour
                // But set a wide stop as safety net (5% from entry as placeholder)
                signal.StopLoss = null; // Will be calculated at execution time

                // No traditional take-profit - exit after funding settlement
                signal.TakeProfit1 = null;
                signal.TakeProfit2 = null;
                signal.TakeProfit3 = null;

                // Position sizing: Conservative, 10-20% of available balance
                var positionPercent = confidence >= 0.9m ? 0.20m : (confidence >= 0.75m ? 0.15m : 0.10m);
                signal.PositionSize = accountBalance.AvailableBalance * positionPercent;
                signal.Indicators["PositionPercent"] = positionPercent * 100;

                // Expected profit calculation
                var expectedFundingProfit = signal.PositionSize.Value * fundingRate;
                signal.Indicators["ExpectedFundingProfit"] = expectedFundingProfit;

                // Estimated trading fees (0.04% maker, 0.04% taker for round trip)
                var estimatedFees = signal.PositionSize.Value * 0.0008m; // 0.08% round trip
                signal.Indicators["EstimatedFees"] = estimatedFees;
                signal.Indicators["NetExpectedProfit"] = expectedFundingProfit - estimatedFees;

                signal.Reason = string.Join(" | ", reasons);
            }

            _logger.LogInformation(
                "Signal for {Symbol}: {Type} (Confidence: {Confidence:P0}) - {Reason}",
                symbol, signal.Type, signal.Confidence, signal.Reason);

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

    private (SignalType Type, decimal Confidence, List<string> Reasons) DetermineSignal(
        decimal fundingRate,
        decimal absFundingRate,
        FundingTimingAnalysis timing)
    {
        var reasons = new List<string>();
        SignalType signalType;
        decimal confidence;

        // Positive funding = Longs pay Shorts → SHORT to receive payment
        // Negative funding = Shorts pay Longs → LONG to receive payment
        bool goShort = fundingRate > 0;

        // Determine confidence based on funding rate magnitude
        if (absFundingRate >= ExtremeRateThreshold)
        {
            confidence = 0.95m;
            signalType = goShort ? SignalType.StrongSell : SignalType.StrongBuy;
            reasons.Add($"EXTREME funding rate: {fundingRate:P4}");
        }
        else if (absFundingRate >= HighFundingRateThreshold)
        {
            confidence = 0.85m;
            signalType = goShort ? SignalType.Sell : SignalType.Buy;
            reasons.Add($"HIGH funding rate: {fundingRate:P4}");
        }
        else
        {
            confidence = 0.70m;
            signalType = goShort ? SignalType.Sell : SignalType.Buy;
            reasons.Add($"Moderate funding rate: {fundingRate:P4}");
        }

        // Add direction explanation
        if (goShort)
        {
            reasons.Add("Longs paying shorts - SHORT to collect funding");
        }
        else
        {
            reasons.Add("Shorts paying longs - LONG to collect funding");
        }

        // Add timing information
        reasons.Add($"Settlement in {timing.MinutesToSettlement} minutes");

        // Boost confidence if timing is optimal
        if (timing.MinutesToSettlement <= 30)
        {
            confidence = Math.Min(confidence + 0.05m, 0.99m);
            reasons.Add("Optimal timing - settlement imminent");
        }

        return (signalType, confidence, reasons);
    }

    private FundingTimingAnalysis AnalyzeFundingTiming()
    {
        var now = DateTime.UtcNow;

        // Binance funding settlement times: 00:00, 08:00, 16:00 UTC
        var fundingHours = new[] { 0, 8, 16 };

        // Find the next funding time
        int nextSettlementHour = fundingHours
            .Select(h => h <= now.Hour ? h + 24 : h)
            .OrderBy(h => h)
            .First();

        if (nextSettlementHour >= 24)
        {
            nextSettlementHour -= 24;
        }

        // Calculate minutes until next settlement
        var nextSettlement = now.Date.AddHours(nextSettlementHour);
        if (nextSettlement <= now)
        {
            nextSettlement = nextSettlement.AddDays(1);
        }

        var minutesToSettlement = (int)(nextSettlement - now).TotalMinutes;

        // Determine if we're in optimal entry window
        bool isOptimalEntry = minutesToSettlement >= MinEntryMinutesBefore
                           && minutesToSettlement <= MaxEntryMinutesBefore;

        string timingReason;
        if (minutesToSettlement < MinEntryMinutesBefore)
        {
            timingReason = $"Too close to settlement ({minutesToSettlement} min). Wait for next cycle.";
        }
        else if (minutesToSettlement > MaxEntryMinutesBefore)
        {
            timingReason = $"Too early ({minutesToSettlement} min to settlement). Optimal entry: {OptimalEntryMinutesBefore} min before.";
        }
        else
        {
            timingReason = $"Optimal entry window ({minutesToSettlement} min to settlement)";
        }

        return new FundingTimingAnalysis
        {
            MinutesToSettlement = minutesToSettlement,
            NextSettlementHour = nextSettlementHour,
            IsOptimalEntry = isOptimalEntry,
            TimingReason = timingReason
        };
    }

    private class FundingTimingAnalysis
    {
        public int MinutesToSettlement { get; init; }
        public int NextSettlementHour { get; init; }
        public bool IsOptimalEntry { get; init; }
        public string TimingReason { get; init; } = string.Empty;
    }
}

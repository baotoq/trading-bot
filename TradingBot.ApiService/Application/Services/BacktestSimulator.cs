using TradingBot.ApiService.Application.Services.Backtest;
using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Pure static backtest simulation engine.
/// Replays a smart DCA strategy day-by-day against historical price data,
/// computing multipliers via MultiplierCalculator and tracking running totals.
/// </summary>
public static class BacktestSimulator
{
    /// <summary>
    /// Runs a backtest simulation for the given configuration and price data.
    /// </summary>
    /// <param name="config">Backtest configuration including multiplier tiers and parameters.</param>
    /// <param name="priceData">Historical price data array (each entry = one purchase day).</param>
    /// <returns>Complete backtest result with smart DCA, fixed DCA baselines, and comparison metrics.</returns>
    public static BacktestResult Run(BacktestConfig config, IReadOnlyList<DailyPriceData> priceData)
    {
        // Input validation
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(priceData);

        if (priceData.Count == 0)
        {
            throw new ArgumentException("Price data cannot be empty", nameof(priceData));
        }

        // Convert MultiplierTierConfig to MultiplierTier for MultiplierCalculator compatibility
        var multiplierTiers = config.Tiers
            .Select(t => new MultiplierTier { DropPercentage = t.DropPercentage, Multiplier = t.Multiplier })
            .ToList();

        // Track all three strategies' data during simulation
        var smartData = new List<DayData>();
        var sameBaseData = new List<DayData>();

        decimal smartCumulativeUsd = 0m;
        decimal smartCumulativeBtc = 0m;
        decimal sameBaseCumulativeUsd = 0m;
        decimal sameBaseCumulativeBtc = 0m;

        // First pass: Smart DCA + Same-Base Fixed DCA
        for (int i = 0; i < priceData.Count; i++)
        {
            var day = priceData[i];

            // Compute sliding window high (30-day)
            int highWindowStart = Math.Max(0, i - config.HighLookbackDays + 1);
            decimal high30Day = priceData
                .Skip(highWindowStart)
                .Take(i - highWindowStart + 1)
                .Max(d => d.High);

            // Compute sliding window MA200
            decimal ma200Day = 0m;
            if (i >= config.BearMarketMaPeriod - 1)
            {
                int maWindowStart = i - config.BearMarketMaPeriod + 1;
                ma200Day = priceData
                    .Skip(maWindowStart)
                    .Take(config.BearMarketMaPeriod)
                    .Average(d => d.Close);
            }

            // Calculate smart DCA multiplier
            var multiplierResult = MultiplierCalculator.Calculate(
                currentPrice: day.Close,
                baseAmount: config.BaseDailyAmount,
                high30Day: high30Day,
                ma200Day: ma200Day,
                tiers: multiplierTiers,
                bearBoostFactor: config.BearBoostFactor,
                maxCap: config.MaxMultiplierCap);

            // Smart DCA purchase
            decimal smartAmountUsd = multiplierResult.FinalAmount;
            decimal smartBtcBought = smartAmountUsd / day.Close;
            smartCumulativeUsd += smartAmountUsd;
            smartCumulativeBtc += smartBtcBought;
            decimal smartCostBasis = smartCumulativeBtc > 0 ? smartCumulativeUsd / smartCumulativeBtc : 0m;

            smartData.Add(new DayData(
                Date: day.Date,
                Price: day.Close,
                Multiplier: multiplierResult.Multiplier,
                Tier: multiplierResult.Tier,
                AmountUsd: smartAmountUsd,
                BtcBought: smartBtcBought,
                CumulativeUsd: smartCumulativeUsd,
                CumulativeBtc: smartCumulativeBtc,
                RunningCostBasis: smartCostBasis,
                High30Day: high30Day,
                Ma200Day: ma200Day));

            // Same-base fixed DCA purchase
            decimal sameBaseAmountUsd = config.BaseDailyAmount;
            decimal sameBaseBtcBought = sameBaseAmountUsd / day.Close;
            sameBaseCumulativeUsd += sameBaseAmountUsd;
            sameBaseCumulativeBtc += sameBaseBtcBought;
            decimal sameBaseCostBasis = sameBaseCumulativeBtc > 0 ? sameBaseCumulativeUsd / sameBaseCumulativeBtc : 0m;

            sameBaseData.Add(new DayData(
                Date: day.Date,
                Price: day.Close,
                Multiplier: 1.0m,
                Tier: "Base",
                AmountUsd: sameBaseAmountUsd,
                BtcBought: sameBaseBtcBought,
                CumulativeUsd: sameBaseCumulativeUsd,
                CumulativeBtc: sameBaseCumulativeBtc,
                RunningCostBasis: sameBaseCostBasis,
                High30Day: high30Day,
                Ma200Day: ma200Day));
        }

        // Second pass: Match-Total Fixed DCA
        decimal matchTotalDailyAmount = smartCumulativeUsd / priceData.Count;
        var matchTotalData = new List<DayData>();

        decimal matchTotalCumulativeUsd = 0m;
        decimal matchTotalCumulativeBtc = 0m;

        for (int i = 0; i < priceData.Count; i++)
        {
            var day = priceData[i];

            decimal matchTotalAmountUsd = matchTotalDailyAmount;
            decimal matchTotalBtcBought = matchTotalAmountUsd / day.Close;
            matchTotalCumulativeUsd += matchTotalAmountUsd;
            matchTotalCumulativeBtc += matchTotalBtcBought;
            decimal matchTotalCostBasis = matchTotalCumulativeBtc > 0 ? matchTotalCumulativeUsd / matchTotalCumulativeBtc : 0m;

            matchTotalData.Add(new DayData(
                Date: day.Date,
                Price: day.Close,
                Multiplier: 1.0m,
                Tier: "Base",
                AmountUsd: matchTotalAmountUsd,
                BtcBought: matchTotalBtcBought,
                CumulativeUsd: matchTotalCumulativeUsd,
                CumulativeBtc: matchTotalCumulativeBtc,
                RunningCostBasis: matchTotalCostBasis,
                High30Day: smartData[i].High30Day,
                Ma200Day: smartData[i].Ma200Day));
        }

        // Build purchase log
        var purchaseLog = new List<PurchaseLogEntry>();
        for (int i = 0; i < priceData.Count; i++)
        {
            var smart = smartData[i];
            var sameBase = sameBaseData[i];
            var matchTotal = matchTotalData[i];

            purchaseLog.Add(new PurchaseLogEntry(
                Date: smart.Date,
                Price: smart.Price,
                // Smart DCA
                SmartMultiplier: smart.Multiplier,
                SmartTier: smart.Tier,
                SmartAmountUsd: smart.AmountUsd,
                SmartBtcBought: smart.BtcBought,
                SmartCumulativeUsd: smart.CumulativeUsd,
                SmartCumulativeBtc: smart.CumulativeBtc,
                SmartRunningCostBasis: smart.RunningCostBasis,
                // Fixed DCA (same-base)
                FixedSameBaseAmountUsd: sameBase.AmountUsd,
                FixedSameBaseBtcBought: sameBase.BtcBought,
                FixedSameBaseCumulativeUsd: sameBase.CumulativeUsd,
                FixedSameBaseCumulativeBtc: sameBase.CumulativeBtc,
                FixedSameBaseRunningCostBasis: sameBase.RunningCostBasis,
                // Fixed DCA (match-total)
                FixedMatchTotalAmountUsd: matchTotal.AmountUsd,
                FixedMatchTotalBtcBought: matchTotal.BtcBought,
                FixedMatchTotalCumulativeUsd: matchTotal.CumulativeUsd,
                FixedMatchTotalCumulativeBtc: matchTotal.CumulativeBtc,
                FixedMatchTotalRunningCostBasis: matchTotal.RunningCostBasis,
                // Window values
                High30Day: smart.High30Day,
                Ma200Day: smart.Ma200Day));
        }

        // Calculate metrics for each strategy
        decimal finalPrice = priceData[^1].Close;

        // Calculate max drawdown for each strategy using the accumulated data
        var smartMaxDrawdown = CalculateMaxDrawdown(smartData, priceData);
        var sameBaseMaxDrawdown = CalculateMaxDrawdown(sameBaseData, priceData);
        var matchTotalMaxDrawdown = CalculateMaxDrawdown(matchTotalData, priceData);

        var smartMetrics = CalculateMetrics(
            smartCumulativeUsd,
            smartCumulativeBtc,
            finalPrice,
            smartMaxDrawdown);

        var sameBaseMetrics = CalculateMetrics(
            sameBaseCumulativeUsd,
            sameBaseCumulativeBtc,
            finalPrice,
            sameBaseMaxDrawdown);

        var matchTotalMetrics = CalculateMetrics(
            matchTotalCumulativeUsd,
            matchTotalCumulativeBtc,
            finalPrice,
            matchTotalMaxDrawdown);

        // Calculate comparison metrics
        var comparison = new ComparisonMetrics(
            CostBasisDeltaSameBase: smartMetrics.AvgCostBasis - sameBaseMetrics.AvgCostBasis,
            CostBasisDeltaMatchTotal: smartMetrics.AvgCostBasis - matchTotalMetrics.AvgCostBasis,
            ExtraBtcPercentSameBase: sameBaseMetrics.TotalBtc > 0
                ? (smartMetrics.TotalBtc - sameBaseMetrics.TotalBtc) / sameBaseMetrics.TotalBtc * 100m
                : 0m,
            ExtraBtcPercentMatchTotal: matchTotalMetrics.TotalBtc > 0
                ? (smartMetrics.TotalBtc - matchTotalMetrics.TotalBtc) / matchTotalMetrics.TotalBtc * 100m
                : 0m,
            EfficiencyRatio: sameBaseMetrics.ReturnPercent != 0
                ? smartMetrics.ReturnPercent / sameBaseMetrics.ReturnPercent
                : 0m);

        // Calculate tier breakdown
        var tierBreakdown = smartData
            .Where(d => d.Tier != "Base")
            .GroupBy(d => d.Tier)
            .Select(g => new TierBreakdownEntry(
                TierName: g.Key,
                TriggerCount: g.Count(),
                ExtraUsdSpent: g.Sum(d => d.AmountUsd - config.BaseDailyAmount),
                ExtraBtcAcquired: g.Sum(d => d.BtcBought - (config.BaseDailyAmount / d.Price))))
            .ToList();

        return new BacktestResult(
            SmartDca: smartMetrics,
            FixedDcaSameBase: sameBaseMetrics,
            FixedDcaMatchTotal: matchTotalMetrics,
            Comparison: comparison,
            TierBreakdown: tierBreakdown,
            PurchaseLog: purchaseLog);
    }

    private static DcaMetrics CalculateMetrics(
        decimal totalInvested,
        decimal totalBtc,
        decimal finalPrice,
        decimal maxDrawdown)
    {
        decimal avgCostBasis = totalBtc > 0 ? totalInvested / totalBtc : 0m;
        decimal portfolioValue = totalBtc * finalPrice;
        decimal returnPercent = totalInvested > 0
            ? (portfolioValue - totalInvested) / totalInvested * 100m
            : 0m;

        return new DcaMetrics(
            TotalInvested: totalInvested,
            TotalBtc: totalBtc,
            AvgCostBasis: avgCostBasis,
            PortfolioValue: portfolioValue,
            ReturnPercent: returnPercent,
            MaxDrawdown: maxDrawdown);
    }

    /// <summary>
    /// Calculates maximum drawdown as the worst unrealized loss from peak PnL relative to total invested.
    /// </summary>
    /// <param name="data">Day-by-day data with cumulative USD and BTC.</param>
    /// <param name="priceData">Price data for portfolio valuation.</param>
    /// <returns>Maximum drawdown as a positive percentage (0 if no drawdown occurred).</returns>
    private static decimal CalculateMaxDrawdown(IReadOnlyList<DayData> data, IReadOnlyList<DailyPriceData> priceData)
    {
        decimal maxDrawdown = 0m;
        decimal peakUnrealizedPnL = 0m;

        for (int i = 0; i < data.Count; i++)
        {
            var day = data[i];
            var portfolioValue = day.CumulativeBtc * priceData[i].Close;
            var unrealizedPnL = portfolioValue - day.CumulativeUsd;

            // Track peak unrealized PnL
            if (unrealizedPnL > peakUnrealizedPnL)
                peakUnrealizedPnL = unrealizedPnL;

            // Calculate drawdown from peak (only after we've had some profit)
            if (peakUnrealizedPnL > 0 && day.CumulativeUsd > 0)
            {
                var drawdown = (unrealizedPnL - peakUnrealizedPnL) / day.CumulativeUsd * 100m;
                if (drawdown < maxDrawdown)
                    maxDrawdown = drawdown;
            }
        }

        return Math.Abs(maxDrawdown); // Return as positive percentage
    }

    private record DayData(
        DateOnly Date,
        decimal Price,
        decimal Multiplier,
        string Tier,
        decimal AmountUsd,
        decimal BtcBought,
        decimal CumulativeUsd,
        decimal CumulativeBtc,
        decimal RunningCostBasis,
        decimal High30Day,
        decimal Ma200Day);
}

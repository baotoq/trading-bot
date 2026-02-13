namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Full nested backtest result structure containing metrics for all strategies,
/// comparison metrics, tier breakdown, and day-by-day purchase log.
/// </summary>
public record BacktestResult(
    DcaMetrics SmartDca,
    DcaMetrics FixedDcaSameBase,
    DcaMetrics FixedDcaMatchTotal,
    ComparisonMetrics Comparison,
    IReadOnlyList<TierBreakdownEntry> TierBreakdown,
    IReadOnlyList<PurchaseLogEntry> PurchaseLog);

/// <summary>
/// DCA strategy metrics - used for both smart and fixed DCA.
/// </summary>
public record DcaMetrics(
    decimal TotalInvested,
    decimal TotalBtc,
    decimal AvgCostBasis,
    decimal PortfolioValue,
    decimal ReturnPercent,
    decimal MaxDrawdown);

/// <summary>
/// Comparison metrics between smart DCA and fixed DCA baselines.
/// </summary>
public record ComparisonMetrics(
    decimal CostBasisDeltaSameBase,     // Smart avg cost - SameBase avg cost (negative = smart is cheaper)
    decimal CostBasisDeltaMatchTotal,   // Smart avg cost - MatchTotal avg cost
    decimal ExtraBtcPercentSameBase,    // (Smart BTC - SameBase BTC) / SameBase BTC * 100
    decimal ExtraBtcPercentMatchTotal,  // (Smart BTC - MatchTotal BTC) / MatchTotal BTC * 100
    decimal EfficiencyRatio);           // Smart return % / SameBase return %

/// <summary>
/// Per-tier breakdown showing impact of each multiplier tier.
/// </summary>
public record TierBreakdownEntry(
    string TierName,
    int TriggerCount,
    decimal ExtraUsdSpent,
    decimal ExtraBtcAcquired);

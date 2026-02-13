namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Single day's purchase log with all three strategies side-by-side.
/// Includes running totals and window values for full transparency.
/// </summary>
public record PurchaseLogEntry(
    DateOnly Date,
    decimal Price,
    // Smart DCA
    decimal SmartMultiplier,
    string SmartTier,
    decimal SmartAmountUsd,
    decimal SmartBtcBought,
    decimal SmartCumulativeUsd,
    decimal SmartCumulativeBtc,
    decimal SmartRunningCostBasis,
    // Fixed DCA (same-base)
    decimal FixedSameBaseAmountUsd,
    decimal FixedSameBaseBtcBought,
    decimal FixedSameBaseCumulativeUsd,
    decimal FixedSameBaseCumulativeBtc,
    decimal FixedSameBaseRunningCostBasis,
    // Fixed DCA (match-total) -- populated in Plan 02, default 0m for now
    decimal FixedMatchTotalAmountUsd,
    decimal FixedMatchTotalBtcBought,
    decimal FixedMatchTotalCumulativeUsd,
    decimal FixedMatchTotalCumulativeBtc,
    decimal FixedMatchTotalRunningCostBasis,
    // Window values for transparency
    decimal High30Day,
    decimal Ma200Day);

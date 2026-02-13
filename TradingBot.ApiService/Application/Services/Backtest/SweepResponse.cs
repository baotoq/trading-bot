namespace TradingBot.ApiService.Application.Services.Backtest;

/// <summary>
/// Response DTO for POST /api/backtest/sweep.
/// Contains all combinations with summary metrics and top 5 with full logs.
/// </summary>
public record SweepResponse(
    int TotalCombinations,
    int ExecutedCombinations,
    string RankedBy,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    List<SweepResultEntry> Results,
    List<SweepResultDetailEntry> TopResults,
    WalkForwardSummary? WalkForward = null);

/// <summary>
/// Single sweep result with summary metrics only (no purchase log).
/// </summary>
public record SweepResultEntry(
    int Rank,
    BacktestConfig Config,
    DcaMetrics SmartDca,
    DcaMetrics FixedDcaSameBase,
    ComparisonMetrics Comparison,
    WalkForwardEntry? WalkForward = null);

/// <summary>
/// Top N sweep result with full purchase logs and tier breakdown.
/// </summary>
public record SweepResultDetailEntry(
    int Rank,
    BacktestConfig Config,
    DcaMetrics SmartDca,
    DcaMetrics FixedDcaSameBase,
    ComparisonMetrics Comparison,
    IReadOnlyList<TierBreakdownEntry> TierBreakdown,
    IReadOnlyList<PurchaseLogEntry> PurchaseLog,
    WalkForwardEntry? WalkForward = null);

/// <summary>
/// Walk-forward validation metrics for a single configuration.
/// </summary>
public record WalkForwardEntry(
    decimal TrainReturnPercent,
    decimal TestReturnPercent,
    decimal ReturnDegradation,
    decimal TrainEfficiency,
    decimal TestEfficiency,
    decimal EfficiencyDegradation,
    bool OverfitWarning);

/// <summary>
/// Walk-forward validation summary for the entire sweep.
/// </summary>
public record WalkForwardSummary(
    decimal TrainRatio,
    DateOnly TrainEnd,
    DateOnly TestStart,
    int OverfitCount,
    int TotalValidated);

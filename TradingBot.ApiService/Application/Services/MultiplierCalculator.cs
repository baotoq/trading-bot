using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Pure static calculator for DCA multiplier logic.
/// Extracted from DcaExecutionService to enable reuse in backtest simulation engine.
/// </summary>
public static class MultiplierCalculator
{
    public static MultiplierResult Calculate(
        decimal currentPrice,
        decimal baseAmount,
        decimal high30Day,
        decimal ma200Day,
        IReadOnlyList<MultiplierTier> tiers,
        decimal bearBoostFactor,
        decimal maxCap)
    {
        // Stub implementation - returns wrong values to ensure RED phase
        return new MultiplierResult(
            Multiplier: 0m,
            Tier: "",
            IsBearMarket: false,
            BearBoostApplied: 0m,
            DropPercentage: 0m,
            High30Day: 0m,
            Ma200Day: 0m,
            FinalAmount: 0m);
    }
}

/// <summary>
/// Result of multiplier calculation containing all components and metadata.
/// </summary>
public record MultiplierResult(
    decimal Multiplier,
    string Tier,
    bool IsBearMarket,
    decimal BearBoostApplied,
    decimal DropPercentage,
    decimal High30Day,
    decimal Ma200Day,
    decimal FinalAmount);

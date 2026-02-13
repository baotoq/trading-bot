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
        // Calculate drop percentage from 30-day high
        decimal dropPercentage = 0m;
        if (high30Day > 0)
        {
            dropPercentage = (high30Day - currentPrice) / high30Day * 100m;
        }

        // Find matching tier (descending order, first match wins)
        decimal tierMultiplier = 1.0m;
        string tier = "Base";

        if (high30Day > 0 && tiers.Count > 0)
        {
            var matchedTier = tiers
                .OrderByDescending(t => t.DropPercentage)
                .FirstOrDefault(t => dropPercentage >= t.DropPercentage);

            if (matchedTier != null)
            {
                tierMultiplier = matchedTier.Multiplier;
                tier = $">= {matchedTier.DropPercentage}%";
            }
        }

        // Detect bear market
        bool isBearMarket = ma200Day > 0 && currentPrice < ma200Day;
        decimal bearBoostApplied = isBearMarket ? bearBoostFactor : 0m;

        // ADDITIVE bear boost (NOT multiplicative - this is the key change from old code)
        decimal uncappedMultiplier = tierMultiplier + bearBoostApplied;

        // Apply max cap
        decimal finalMultiplier = Math.Min(uncappedMultiplier, maxCap);

        // Calculate final amount
        decimal finalAmount = baseAmount * finalMultiplier;

        return new MultiplierResult(
            Multiplier: finalMultiplier,
            Tier: tier,
            IsBearMarket: isBearMarket,
            BearBoostApplied: bearBoostApplied,
            DropPercentage: dropPercentage,
            High30Day: high30Day,
            Ma200Day: ma200Day,
            FinalAmount: finalAmount);
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

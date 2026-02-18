using System.Globalization;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Pure static calculator for DCA multiplier logic.
/// Extracted from DcaExecutionService to enable reuse in backtest simulation engine.
/// </summary>
public static class MultiplierCalculator
{
    public static MultiplierResult Calculate(
        Price currentPrice,
        UsdAmount baseAmount,
        decimal high30Day,
        decimal ma200Day,
        IReadOnlyList<MultiplierTier> tiers,
        Multiplier bearBoostFactor,
        Multiplier maxCap)
    {
        // Calculate drop in 0-1 format (matching Percentage value object)
        decimal drop = high30Day > 0 ? (high30Day - currentPrice.Value) / high30Day : 0m;

        // Find matching tier (descending order, first match wins)
        decimal tierMultiplier = 1.0m;
        string tier = "Base";

        if (high30Day > 0 && tiers.Count > 0)
        {
            var matchedTier = tiers
                .OrderByDescending(t => t.DropPercentage.Value)
                .FirstOrDefault(t => drop >= t.DropPercentage.Value);

            if (matchedTier != null)
            {
                tierMultiplier = matchedTier.Multiplier.Value;
                tier = $">= {(matchedTier.DropPercentage.Value * 100).ToString("F1", CultureInfo.InvariantCulture)}%";
            }
        }

        // Detect bear market
        bool isBearMarket = ma200Day > 0 && currentPrice.Value < ma200Day;
        decimal bearBoostApplied = isBearMarket ? bearBoostFactor.Value : 0m;

        // ADDITIVE bear boost (NOT multiplicative - this is the key change from old code)
        // Use raw decimal arithmetic since bearBoostApplied can be 0 and Multiplier rejects 0
        decimal uncapped = tierMultiplier + bearBoostApplied;
        decimal final = Math.Min(uncapped, maxCap.Value);
        var finalMultiplier = Multiplier.From(final);

        // Calculate final amount
        var finalAmount = UsdAmount.From(baseAmount.Value * final);

        return new MultiplierResult(
            Multiplier: finalMultiplier,
            Tier: tier,
            IsBearMarket: isBearMarket,
            BearBoostApplied: bearBoostApplied,
            DropPercentage: Percentage.From(Math.Clamp(drop, 0, 1)),
            High30Day: high30Day,
            Ma200Day: ma200Day,
            FinalAmount: finalAmount);
    }
}

/// <summary>
/// Result of multiplier calculation containing all components and metadata.
/// </summary>
public record MultiplierResult(
    Multiplier Multiplier,
    string Tier,
    bool IsBearMarket,
    decimal BearBoostApplied,   // Stays decimal: additive amount, can be 0 (no value object for 0)
    Percentage DropPercentage,
    decimal High30Day,          // Stays decimal: 0 sentinel for "data unavailable"
    decimal Ma200Day,           // Stays decimal: 0 sentinel for "data unavailable"
    UsdAmount FinalAmount);

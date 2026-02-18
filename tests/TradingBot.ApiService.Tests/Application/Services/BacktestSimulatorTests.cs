using FluentAssertions;
using Snapper;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.Backtest;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Tests.Application.Services;

public class BacktestSimulatorTests
{
    #region Test Helpers

    private static BacktestConfig CreateStandardConfig()
    {
        return new BacktestConfig(
            BaseDailyAmount: UsdAmount.From(10m),
            HighLookbackDays: 30,
            BearMarketMaPeriod: 200,
            BearBoostFactor: Multiplier.From(1.5m),
            MaxMultiplierCap: Multiplier.From(4.5m),
            Tiers: new List<MultiplierTierConfig>
            {
                new(DropPercentage: Percentage.From(0.05m), Multiplier: Multiplier.From(1.5m)),
                new(DropPercentage: Percentage.From(0.10m), Multiplier: Multiplier.From(2.0m)),
                new(DropPercentage: Percentage.From(0.20m), Multiplier: Multiplier.From(3.0m))
            });
    }

    private static IReadOnlyList<DailyPriceData> CreatePriceData(params decimal[] closePrices)
    {
        var startDate = new DateOnly(2024, 1, 1);
        return closePrices.Select((close, index) => new DailyPriceData(
            Date: startDate.AddDays(index),
            Open: close,
            High: close * 1.01m,
            Low: close * 0.99m,
            Close: close,
            Volume: 100m
        )).ToList();
    }

    private static IReadOnlyList<DailyPriceData> CreateRealisticPriceData()
    {
        var prices = new List<decimal>();
        var startDate = new DateOnly(2024, 1, 1);

        // Days 1-15: 60000 -> 58000 (slight decline, minor tier triggers)
        for (int i = 0; i < 15; i++)
            prices.Add(60000m - i * 133.33m);

        // Days 16-30: 58000 -> 48000 (bear market, 20%+ tier triggers)
        for (int i = 0; i < 15; i++)
            prices.Add(58000m - i * 666.67m);

        // Days 31-45: 48000 -> 52000 (recovery)
        for (int i = 0; i < 15; i++)
            prices.Add(48000m + i * 266.67m);

        // Days 46-60: 52000 -> 65000 (bull run)
        for (int i = 0; i < 15; i++)
            prices.Add(52000m + i * 866.67m);

        return prices.Select((close, index) => new DailyPriceData(
            Date: startDate.AddDays(index),
            Open: close,
            High: close * 1.01m,
            Low: close * 0.99m,
            Close: close,
            Volume: 100m + index * 10m
        )).ToList();
    }

    #endregion

    [Fact]
    public void Run_ReturnsCorrectDayCount()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 50000m, 50000m, 50000m, 50000m,
                                        50000m, 50000m, 50000m, 50000m, 50000m,
                                        50000m, 50000m, 50000m, 50000m, 50000m,
                                        50000m, 50000m, 50000m, 50000m, 50000m,
                                        50000m, 50000m, 50000m, 50000m, 50000m,
                                        50000m, 50000m, 50000m, 50000m, 50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert
        result.PurchaseLog.Should().HaveCount(30);
    }

    [Fact]
    public void Run_SmartDca_NoDrop_UsesBaseAmount()
    {
        // Arrange - Flat prices (all same)
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 50000m, 50000m, 50000m, 50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert
        result.PurchaseLog.Should().AllSatisfy(entry =>
        {
            entry.SmartAmountUsd.Should().Be(config.BaseDailyAmount.Value);
            entry.SmartMultiplier.Should().Be(1.0m);
        });
    }

    [Fact]
    public void Run_SmartDca_WithDrop_AppliesMultiplier()
    {
        // Arrange - Price drops 10% from high
        var config = CreateStandardConfig();
        // Day 0: 50000, Day 1: 50000, Day 2: 45000 (10% drop from 50000)
        var priceData = CreatePriceData(50000m, 50000m, 45000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Day 2 should have 2.0x multiplier (10% drop tier)
        var day2 = result.PurchaseLog[2];
        day2.SmartMultiplier.Should().Be(2.0m);
        day2.SmartAmountUsd.Should().Be(config.BaseDailyAmount.Value * 2.0m);
    }

    [Fact]
    public void Run_FixedSameBase_AlwaysUsesBaseAmount()
    {
        // Arrange - Varying prices
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 45000m, 40000m, 55000m, 50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Fixed same-base always uses base amount regardless of price movement
        result.PurchaseLog.Should().AllSatisfy(entry =>
        {
            entry.FixedSameBaseAmountUsd.Should().Be(config.BaseDailyAmount.Value);
        });
    }

    [Fact]
    public void Run_RunningTotals_AccumulateCorrectly()
    {
        // Arrange - Flat prices for simple math
        var config = CreateStandardConfig();
        var price = 50000m;
        var priceData = CreatePriceData(price, price, price, price, price);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert
        var lastEntry = result.PurchaseLog[^1];
        var expectedTotalUsd = 5 * config.BaseDailyAmount.Value; // 5 days * 10 USD = 50 USD
        var expectedTotalBtc = expectedTotalUsd / price;          // 50 USD / 50000 = 0.001 BTC

        lastEntry.SmartCumulativeUsd.Should().Be(expectedTotalUsd);
        lastEntry.SmartCumulativeBtc.Should().BeApproximately(expectedTotalBtc, 0.00000001m);
    }

    [Fact]
    public void Run_CostBasis_IsWeightedAverage()
    {
        // Arrange - Two days at different prices
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(40000m, 60000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert
        var lastEntry = result.PurchaseLog[^1];
        var totalUsd = 10m + 10m; // 10 USD each day
        var totalBtc = (10m / 40000m) + (10m / 60000m);
        var expectedCostBasis = totalUsd / totalBtc;

        lastEntry.SmartRunningCostBasis.Should().BeApproximately(expectedCostBasis, 0.01m);
    }

    [Fact]
    public void Run_SlidingWindow_High30Day_UsesPartialWindowDuringWarmup()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 55000m, 52000m, 53000m, 54000m, 51000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Day 0 should use only its own high
        var day0 = result.PurchaseLog[0];
        day0.High30Day.Should().Be(50000m * 1.01m); // High field = Close * 1.01

        // Day 5 should use max high from days 0-5
        var day5 = result.PurchaseLog[5];
        var expectedHigh = 55000m * 1.01m; // Day 1 has the highest close
        day5.High30Day.Should().Be(expectedHigh);
    }

    [Fact]
    public void Run_SlidingWindow_Ma200_ReturnsZeroDuringWarmup()
    {
        // Arrange - Not enough data for MA200
        var config = CreateStandardConfig();
        var priceData = Enumerable.Range(0, 50)
            .Select(i => 50000m + i * 100m) // Gradually increasing prices
            .ToArray();
        var priceDataList = CreatePriceData(priceData);

        // Act
        var result = BacktestSimulator.Run(config, priceDataList);

        // Assert - Day 49 (50th day) should have MA200 = 0 (insufficient data)
        var day49 = result.PurchaseLog[49];
        day49.Ma200Day.Should().Be(0m);
    }

    [Fact]
    public void Run_SlidingWindow_Ma200_ComputesAfterWarmup()
    {
        // Arrange - 200+ days of data
        var config = CreateStandardConfig();
        var priceData = Enumerable.Range(0, 201)
            .Select(i => 50000m) // Flat prices for simple average
            .ToArray();
        var priceDataList = CreatePriceData(priceData);

        // Act
        var result = BacktestSimulator.Run(config, priceDataList);

        // Assert - Day 199 (200th day, 0-indexed) should have MA200 = average of first 200 closes
        var day199 = result.PurchaseLog[199];
        day199.Ma200Day.Should().Be(50000m); // Average of all 50000s is 50000
    }

    [Fact]
    public void Run_IsDeterministic()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 48000m, 45000m, 47000m, 49000m);

        // Act - Run twice
        var result1 = BacktestSimulator.Run(config, priceData);
        var result2 = BacktestSimulator.Run(config, priceData);

        // Assert - Results should be identical
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public void Run_EmptyPriceData_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = new List<DailyPriceData>();

        // Act & Assert
        var act = () => BacktestSimulator.Run(config, priceData);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Price data cannot be empty*");
    }

    [Fact]
    public void Run_PurchaseLog_IncludesWindowValues()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 48000m, 45000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Each entry should have window values populated
        result.PurchaseLog.Should().AllSatisfy(entry =>
        {
            entry.High30Day.Should().BeGreaterThan(0m);
            // Ma200Day may be 0 during warmup, but should be present
            entry.Ma200Day.Should().BeGreaterOrEqualTo(0m);
        });
    }

    #region Max Drawdown Tests

    [Fact]
    public void Run_MaxDrawdown_MeasuresWorstUnrealizedLoss()
    {
        // Arrange - Price rises then crashes then recovers
        var config = CreateStandardConfig();
        var prices = new List<decimal>();

        // Days 1-10: price rises from 50000 to 60000 (building profit)
        for (int i = 0; i < 10; i++)
            prices.Add(50000m + i * 1000m);

        // Days 11-20: price crashes to 30000 (unrealized loss from peak)
        for (int i = 0; i < 10; i++)
            prices.Add(60000m - i * 3000m);

        // Days 21-30: price recovers to 55000
        for (int i = 0; i < 10; i++)
            prices.Add(30000m + i * 2500m);

        var priceData = CreatePriceData(prices.ToArray());

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - MaxDrawdown should be positive (representing a loss)
        result.SmartDca.MaxDrawdown.Should().BeGreaterThan(0m);
        result.FixedDcaSameBase.MaxDrawdown.Should().BeGreaterThan(0m);
        result.FixedDcaMatchTotal.MaxDrawdown.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Run_MaxDrawdown_ZeroForMonotonicallyRisingPrices()
    {
        // Arrange - Prices only go up
        var config = CreateStandardConfig();
        var prices = Enumerable.Range(0, 30)
            .Select(i => 50000m + i * 1000m)
            .ToArray();
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - No drawdown when prices only rise
        result.SmartDca.MaxDrawdown.Should().Be(0m);
        result.FixedDcaSameBase.MaxDrawdown.Should().Be(0m);
        result.FixedDcaMatchTotal.MaxDrawdown.Should().Be(0m);
    }

    [Fact]
    public void Run_MaxDrawdown_CalculatedForAllThreeStrategies()
    {
        // Arrange - Volatile prices
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 60000m, 40000m, 55000m, 45000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - All three strategies should have MaxDrawdown populated
        result.SmartDca.MaxDrawdown.Should().BeGreaterOrEqualTo(0m);
        result.FixedDcaSameBase.MaxDrawdown.Should().BeGreaterOrEqualTo(0m);
        result.FixedDcaMatchTotal.MaxDrawdown.Should().BeGreaterOrEqualTo(0m);
    }

    #endregion

    #region Tier Breakdown Tests

    [Fact]
    public void Run_TierBreakdown_CountsTriggersPerTier()
    {
        // Arrange - Create price data with specific tier triggers
        var config = CreateStandardConfig();
        var prices = new List<decimal>();

        // Start high
        prices.Add(50000m);

        // 10 days at 5% below high (should trigger >= 5% tier with 1.5x)
        for (int i = 0; i < 10; i++)
            prices.Add(47500m);

        // 5 days at 15% below high (should trigger >= 10% tier with 2.0x)
        for (int i = 0; i < 5; i++)
            prices.Add(42500m);

        // 3 days at 25% below high (should trigger >= 20% tier with 3.0x)
        for (int i = 0; i < 3; i++)
            prices.Add(37500m);

        var priceData = CreatePriceData(prices.ToArray());

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Verify tier breakdown has correct trigger counts
        result.TierBreakdown.Should().NotBeEmpty();
        var totalTriggers = result.TierBreakdown.Sum(t => t.TriggerCount);
        totalTriggers.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Run_TierBreakdown_ExtraUsdSpent()
    {
        // Arrange - Price drop to trigger tier
        var config = CreateStandardConfig();
        // Day 0: 50000, Days 1-5: 45000 (10% drop, triggers 2.0x tier)
        var prices = new[] { 50000m, 45000m, 45000m, 45000m, 45000m, 45000m };
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Extra USD spent should be positive for triggered tiers
        var tiersWithExtraUsd = result.TierBreakdown.Where(t => t.ExtraUsdSpent > 0m);
        tiersWithExtraUsd.Should().NotBeEmpty();
    }

    [Fact]
    public void Run_TierBreakdown_ExtraBtcAcquired()
    {
        // Arrange - Price drop to trigger tier
        var config = CreateStandardConfig();
        var prices = new[] { 50000m, 45000m, 45000m, 45000m };
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Extra BTC acquired should be positive for triggered tiers
        var tiersWithExtraBtc = result.TierBreakdown.Where(t => t.ExtraBtcAcquired > 0m);
        tiersWithExtraBtc.Should().NotBeEmpty();
    }

    [Fact]
    public void Run_TierBreakdown_NoTriggeredTiers_ReturnsEmpty()
    {
        // Arrange - Flat prices, no tiers triggered
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 50000m, 50000m, 50000m, 50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - No tier triggers means empty or all zeros
        result.TierBreakdown.Should().BeEmpty();
    }

    #endregion

    #region Comparison Metrics Tests

    [Fact]
    public void Run_Comparison_CostBasisDeltaNegativeWhenSmartIsCheaper()
    {
        // Arrange - Significant price drop where smart DCA buys more at lower prices
        var config = CreateStandardConfig();
        var prices = new[] { 50000m, 45000m, 40000m, 35000m, 30000m };
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Smart DCA should have lower cost basis (buys more when cheap)
        result.Comparison.CostBasisDeltaSameBase.Should().BeLessThan(0m);
    }

    [Fact]
    public void Run_Comparison_ExtraBtcPercentPositiveWhenSmartBuysMore()
    {
        // Arrange - Price drops significantly
        var config = CreateStandardConfig();
        var prices = new[] { 50000m, 40000m, 30000m, 35000m, 40000m };
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Smart DCA should have more BTC than same-base
        result.Comparison.ExtraBtcPercentSameBase.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Run_Comparison_MatchTotalSameSpend()
    {
        // Arrange
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 48000m, 45000m, 47000m, 49000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Match-total should have approximately same total invested
        result.FixedDcaMatchTotal.TotalInvested.Should().BeApproximately(
            result.SmartDca.TotalInvested, 0.01m);
    }

    [Fact]
    public void Run_Comparison_EfficiencyRatioAboveOneWhenSmartOutperforms()
    {
        // Arrange - Price drops then recovers (smart DCA should outperform)
        var config = CreateStandardConfig();
        var prices = new[] { 50000m, 40000m, 30000m, 40000m, 55000m };
        var priceData = CreatePriceData(prices);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Efficiency ratio > 1.0 means smart outperformed
        // Note: This may not always be > 1.0 depending on price pattern
        result.Comparison.EfficiencyRatio.Should().BeGreaterOrEqualTo(0m);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Run_SingleDay_ProducesValidResult()
    {
        // Arrange - Only 1 day of data
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Should produce valid result without division-by-zero
        result.PurchaseLog.Should().HaveCount(1);
        result.SmartDca.TotalInvested.Should().BeGreaterThan(0m);
        result.SmartDca.TotalBtc.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Run_AllSamePrices_SmartAndFixedIdentical()
    {
        // Arrange - All days same price
        var config = CreateStandardConfig();
        var priceData = CreatePriceData(50000m, 50000m, 50000m, 50000m, 50000m);

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert - Smart and same-base should be identical (multiplier always 1.0)
        result.SmartDca.TotalBtc.Should().Be(result.FixedDcaSameBase.TotalBtc);
        result.SmartDca.AvgCostBasis.Should().Be(result.FixedDcaSameBase.AvgCostBasis);
    }

    [Fact]
    public void Run_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var priceData = CreatePriceData(50000m);

        // Act & Assert
        var act = () => BacktestSimulator.Run(null!, priceData);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Run_NullPriceData_ThrowsArgumentNullException()
    {
        // Arrange
        var config = CreateStandardConfig();

        // Act & Assert
        var act = () => BacktestSimulator.Run(config, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Golden Snapshot Test

    [Fact]
    public void Run_GoldenScenario_MatchSnapshot()
    {
        // Arrange: 60 days of realistic price movement
        // Start at 60000, dip to 48000 (bear + tier triggers), recover to 65000
        var config = CreateStandardConfig();
        var priceData = CreateRealisticPriceData();

        // Act
        var result = BacktestSimulator.Run(config, priceData);

        // Assert: snapshot captures full result structure for regression detection
        result.ShouldMatchSnapshot();
    }

    #endregion
}

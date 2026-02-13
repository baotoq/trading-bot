using FluentAssertions;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.Backtest;

namespace TradingBot.ApiService.Tests.Application.Services;

public class BacktestSimulatorTests
{
    #region Test Helpers

    private static BacktestConfig CreateStandardConfig()
    {
        return new BacktestConfig(
            BaseDailyAmount: 10m,
            HighLookbackDays: 30,
            BearMarketMaPeriod: 200,
            BearBoostFactor: 1.5m,
            MaxMultiplierCap: 4.5m,
            Tiers: new List<MultiplierTierConfig>
            {
                new(DropPercentage: 5m, Multiplier: 1.5m),
                new(DropPercentage: 10m, Multiplier: 2.0m),
                new(DropPercentage: 20m, Multiplier: 3.0m)
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
            entry.SmartAmountUsd.Should().Be(config.BaseDailyAmount);
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
        day2.SmartAmountUsd.Should().Be(config.BaseDailyAmount * 2.0m);
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
            entry.FixedSameBaseAmountUsd.Should().Be(config.BaseDailyAmount);
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
        var expectedTotalUsd = 5 * config.BaseDailyAmount; // 5 days * 10 USD = 50 USD
        var expectedTotalBtc = expectedTotalUsd / price;   // 50 USD / 50000 = 0.001 BTC

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
}

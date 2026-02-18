using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Models.Values;
using Snapper;

namespace TradingBot.ApiService.Tests.Application.Services;

public class MultiplierCalculatorTests
{
    // Default tiers from appsettings.json -- DropPercentage in 0-1 format
    private static readonly IReadOnlyList<MultiplierTier> DefaultTiers = new List<MultiplierTier>
    {
        new() { DropPercentage = Percentage.From(0.05m), Multiplier = Multiplier.From(1.5m) },
        new() { DropPercentage = Percentage.From(0.10m), Multiplier = Multiplier.From(2.0m) },
        new() { DropPercentage = Percentage.From(0.20m), Multiplier = Multiplier.From(3.0m) }
    }.AsReadOnly();

    private static readonly Multiplier DefaultBearBoost = Multiplier.From(1.5m);
    private static readonly Multiplier DefaultMaxCap = Multiplier.From(4.5m);
    private static readonly UsdAmount DefaultBaseAmount = UsdAmount.From(10.0m);

    #region Tier Boundary Tests

    [Theory]
    [InlineData(100000, "Base", 1.0, 0.00, 10.0)] // No drop
    [InlineData(95010, "Base", 1.0, 0.0499, 10.0)] // Just below 5%
    [InlineData(95000, ">= 5.0%", 1.5, 0.05, 15.0)] // Exactly 5%
    [InlineData(94990, ">= 5.0%", 1.5, 0.0501, 15.0)] // Just above 5%
    [InlineData(90010, ">= 5.0%", 1.5, 0.0999, 15.0)] // Just below 10%
    [InlineData(90000, ">= 10.0%", 2.0, 0.10, 20.0)] // Exactly 10%
    [InlineData(80010, ">= 10.0%", 2.0, 0.1999, 20.0)] // Just below 20%
    [InlineData(80000, ">= 20.0%", 3.0, 0.20, 30.0)] // Exactly 20%
    [InlineData(50000, ">= 20.0%", 3.0, 0.50, 30.0)] // Large drop
    public void Calculate_TierBoundaries_ReturnsCorrectMultiplier(
        decimal currentPrice,
        string expectedTier,
        decimal expectedMultiplier,
        decimal expectedDropPercentage,
        decimal expectedFinalAmount)
    {
        // Arrange
        const decimal high30Day = 100000m;
        const decimal ma200Day = 40000m; // Below all test prices, so no bear market

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(expectedMultiplier));
        result.Tier.Should().Be(expectedTier);
        result.DropPercentage.Value.Should().BeApproximately(expectedDropPercentage, 0.0001m);
        result.IsBearMarket.Should().BeFalse();
        result.BearBoostApplied.Should().Be(0);
        result.High30Day.Should().Be(high30Day);
        result.Ma200Day.Should().Be(ma200Day);
        result.FinalAmount.Should().Be(UsdAmount.From(expectedFinalAmount));
    }

    #endregion

    #region Bear Market + Tier Combination Tests

    [Theory]
    [InlineData(95000, ">= 5.0%", 1.5, false, 0, 0.05, 15.0)] // 5% drop, NOT bear (95000 > 90000)
    [InlineData(85000, ">= 10.0%", 3.5, true, 1.5, 0.15, 35.0)] // 15% drop, bear: 2.0 + 1.5 = 3.5
    [InlineData(75000, ">= 20.0%", 4.5, true, 1.5, 0.25, 45.0)] // 25% drop, bear: 3.0 + 1.5 = 4.5 (at cap)
    [InlineData(50000, ">= 20.0%", 4.5, true, 1.5, 0.50, 45.0)] // 50% drop, bear: capped at 4.5
    public void Calculate_BearMarketAndTierCombos_ReturnsAdditiveBoost(
        decimal currentPrice,
        string expectedTier,
        decimal expectedMultiplier,
        bool expectedIsBearMarket,
        decimal expectedBearBoost,
        decimal expectedDropPercentage,
        decimal expectedFinalAmount)
    {
        // Arrange
        const decimal high30Day = 100000m;
        const decimal ma200Day = 90000m; // Bear when price < 90000

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(expectedMultiplier));
        result.Tier.Should().Be(expectedTier);
        result.IsBearMarket.Should().Be(expectedIsBearMarket);
        result.BearBoostApplied.Should().Be(expectedBearBoost);
        result.DropPercentage.Value.Should().BeApproximately(expectedDropPercentage, 0.0001m);
        result.FinalAmount.Should().Be(UsdAmount.From(expectedFinalAmount));
    }

    #endregion

    #region Max Cap Tests

    [Fact]
    public void Calculate_MaxCapEnforcement_ClampsFinalMultiplier()
    {
        // Arrange - scenario where uncapped would be 4.5 but cap is 3.0
        const decimal currentPrice = 75000m; // 25% drop
        const decimal high30Day = 100000m;
        const decimal ma200Day = 80000m; // Bear market (75000 < 80000)
        var maxCap = Multiplier.From(3.0m); // Lower cap
        // Without cap: 3.0 (tier) + 1.5 (bear) = 4.5, with cap = 3.0

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            maxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(3.0m)); // Capped
        result.Tier.Should().Be(">= 20.0%");
        result.IsBearMarket.Should().BeTrue();
        result.BearBoostApplied.Should().Be(1.5m);
        result.FinalAmount.Should().Be(UsdAmount.From(30.0m)); // 10 * 3.0
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Calculate_Ma200DayZero_TreatsAsNonBearMarket()
    {
        // Arrange
        const decimal currentPrice = 90000m; // 10% drop - would be bear if ma200Day was valid
        const decimal high30Day = 100000m;
        const decimal ma200Day = 0m; // Invalid MA200

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.IsBearMarket.Should().BeFalse();
        result.BearBoostApplied.Should().Be(0);
        result.Multiplier.Should().Be(Multiplier.From(2.0m)); // Just tier multiplier (10% drop)
        result.Tier.Should().Be(">= 10.0%");
    }

    [Fact]
    public void Calculate_High30DayZero_ReturnsBaseMultiplier()
    {
        // Arrange
        const decimal currentPrice = 80000m;
        const decimal high30Day = 0m; // Invalid high
        const decimal ma200Day = 70000m; // Below current price, no bear market

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(1.0m));
        result.Tier.Should().Be("Base");
        result.DropPercentage.Value.Should().Be(0);
        result.FinalAmount.Should().Be(UsdAmount.From(10.0m));
    }

    [Fact]
    public void Calculate_EmptyTiersList_ReturnsBaseMultiplier()
    {
        // Arrange
        const decimal currentPrice = 80000m;
        const decimal high30Day = 100000m;
        const decimal ma200Day = 50000m; // Below current price, no bear market
        var emptyTiers = new List<MultiplierTier>().AsReadOnly();

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            emptyTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(1.0m));
        result.Tier.Should().Be("Base");
        result.DropPercentage.Value.Should().BeApproximately(0.20m, 0.0001m); // Drop exists but no tier matches
    }

    [Fact]
    public void Calculate_NegativeDrop_ReturnsBaseMultiplier()
    {
        // Arrange - current price above 30-day high (new high, no bear market)
        const decimal currentPrice = 110000m;
        const decimal high30Day = 100000m;
        const decimal ma200Day = 90000m; // Below current price, no bear market

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            DefaultBaseAmount,
            high30Day,
            ma200Day,
            DefaultTiers,
            DefaultBearBoost,
            DefaultMaxCap);

        // Assert
        result.Multiplier.Should().Be(Multiplier.From(1.0m));
        result.Tier.Should().Be("Base");
        // DropPercentage is clamped to 0 (negative drops become 0 via Math.Clamp)
        result.DropPercentage.Value.Should().Be(0m);
    }

    #endregion

    #region FinalAmount Calculation Tests

    [Theory]
    [InlineData(10.0, 1.5, 15.0)]
    [InlineData(10.0, 2.0, 20.0)]
    [InlineData(10.0, 3.5, 35.0)]
    [InlineData(25.0, 2.0, 50.0)]
    [InlineData(5.0, 4.5, 22.5)]
    public void Calculate_FinalAmount_EqualsBaseTimesMultiplier(
        decimal baseAmount,
        decimal tierMultiplier,
        decimal expectedFinalAmount)
    {
        // Arrange - set up scenario to produce specific multiplier
        const decimal currentPrice = 90000m; // 10% drop for 2.0x
        const decimal high30Day = 100000m;
        const decimal ma200Day = 50000m; // Below current price = no bear market

        // For this test we'll use simplified tier that matches our target
        var tiers = new List<MultiplierTier>
        {
            new() { DropPercentage = Percentage.From(0.10m), Multiplier = Multiplier.From(tierMultiplier) }
        }.AsReadOnly();

        // Act
        var result = MultiplierCalculator.Calculate(
            Price.From(currentPrice),
            UsdAmount.From(baseAmount),
            high30Day,
            ma200Day,
            tiers,
            Multiplier.From(1.5m), // Bear boost (won't apply since ma200Day < currentPrice)
            Multiplier.From(10.0m)); // High cap

        // Assert
        result.FinalAmount.Should().Be(UsdAmount.From(expectedFinalAmount));
        result.Multiplier.Should().Be(Multiplier.From(tierMultiplier));
    }

    #endregion

    #region Golden Snapshot Test

    [Fact]
    public void Calculate_GoldenScenarios_MatchSnapshot()
    {
        // Arrange - production-like scenarios from appsettings.json defaults
        var scenarios = new[]
        {
            new
            {
                Name = "Normal day - no drop",
                CurrentPrice = 100000m,
                High30Day = 100000m,
                Ma200Day = 90000m
            },
            new
            {
                Name = "5% dip - no bear",
                CurrentPrice = 95000m,
                High30Day = 100000m,
                Ma200Day = 90000m
            },
            new
            {
                Name = "10% dip - bear market",
                CurrentPrice = 85000m,
                High30Day = 100000m,
                Ma200Day = 90000m
            },
            new
            {
                Name = "20% dip - bear market - capped",
                CurrentPrice = 75000m,
                High30Day = 100000m,
                Ma200Day = 90000m
            },
            new
            {
                Name = "Extreme dip - bear market - heavily capped",
                CurrentPrice = 50000m,
                High30Day = 100000m,
                Ma200Day = 90000m
            }
        };

        var results = scenarios.Select(s => new
        {
            s.Name,
            Result = MultiplierCalculator.Calculate(
                Price.From(s.CurrentPrice),
                DefaultBaseAmount,
                s.High30Day,
                s.Ma200Day,
                DefaultTiers,
                DefaultBearBoost,
                DefaultMaxCap)
        }).ToList();

        // Assert - snapshot captures baseline for regression detection
        results.ShouldMatchSnapshot();
    }

    #endregion
}

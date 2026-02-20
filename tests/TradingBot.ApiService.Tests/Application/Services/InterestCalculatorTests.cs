using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Tests.Application.Services;

public class InterestCalculatorTests
{
    private static readonly DateOnly StartDate = new(2024, 1, 1);

    #region Simple Interest Tests

    [Theory]
    [InlineData(10_000_000, 0.065, 180, 10_320_547.945)] // 180 days: P * (1 + 0.065 * 180/365)
    [InlineData(10_000_000, 0.065, 365, 10_650_000.0)]    // Full year: P * (1 + 0.065)
    public void CalculateAccruedValue_SimpleInterest_ReturnsCorrectValue(
        decimal principal,
        decimal annualRate,
        int daysElapsed,
        decimal expectedAccrued)
    {
        // Arrange
        var asOfDate = StartDate.AddDays(daysElapsed);

        // Act
        var result = InterestCalculator.CalculateAccruedValue(
            principal, annualRate, StartDate, asOfDate, CompoundingFrequency.Simple);

        // Assert
        result.Should().BeApproximately(expectedAccrued, 1m);
    }

    #endregion

    #region Compound Interest Tests

    [Fact]
    public void CalculateAccruedValue_MonthlyCompound_365Days_ReturnsCorrectValue()
    {
        // P * (1 + 0.065/12)^(12*365/365) ≈ 10,669,719
        // Small precision variance from double cast in Math.Pow
        var asOfDate = StartDate.AddDays(365);

        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, asOfDate, CompoundingFrequency.Monthly);

        result.Should().BeApproximately(10_669_719m, 500m);
    }

    [Fact]
    public void CalculateAccruedValue_QuarterlyCompound_365Days_ReturnsCorrectValue()
    {
        // P * (1 + 0.065/4)^(4*365/365) ≈ 10,666,016
        // Variance from double cast in Math.Pow with decimal year fractions
        var asOfDate = StartDate.AddDays(365);

        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, asOfDate, CompoundingFrequency.Quarterly);

        result.Should().BeApproximately(10_666_016m, 500m);
    }

    [Fact]
    public void CalculateAccruedValue_SemiAnnualCompound_365Days_ReturnsCorrectValue()
    {
        // P * (1 + 0.065/2)^(2*365/365) ≈ 10,660,563
        var asOfDate = StartDate.AddDays(365);

        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, asOfDate, CompoundingFrequency.SemiAnnual);

        result.Should().BeApproximately(10_660_563m, 500m);
    }

    [Fact]
    public void CalculateAccruedValue_AnnualCompound_365Days_ReturnsCorrectValue()
    {
        // P * (1 + 0.065)^(365/365) = 10,650,000
        var asOfDate = StartDate.AddDays(365);

        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, asOfDate, CompoundingFrequency.Annual);

        result.Should().BeApproximately(10_650_000m, 500m);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void CalculateAccruedValue_AsOfDateEqualsStartDate_ReturnsPrincipal()
    {
        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, StartDate, CompoundingFrequency.Simple);

        result.Should().Be(10_000_000m);
    }

    [Fact]
    public void CalculateAccruedValue_AsOfDateBeforeStartDate_ReturnsPrincipal()
    {
        var asOfDate = StartDate.AddDays(-30);

        var result = InterestCalculator.CalculateAccruedValue(
            10_000_000m, 0.065m, StartDate, asOfDate, CompoundingFrequency.Monthly);

        result.Should().Be(10_000_000m);
    }

    #endregion
}

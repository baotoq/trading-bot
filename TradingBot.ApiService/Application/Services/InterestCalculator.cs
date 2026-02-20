using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Pure static calculator for fixed deposit accrued value.
/// Supports all 5 compounding frequencies per PORT-06.
/// Uses 365-day year convention (Vietnamese banking standard).
/// </summary>
public static class InterestCalculator
{
    /// <summary>
    /// Calculates accrued value of a fixed deposit as of a given date.
    /// </summary>
    /// <param name="principal">Initial deposit amount</param>
    /// <param name="annualRate">Annual interest rate as decimal (e.g., 0.065 = 6.5%)</param>
    /// <param name="startDate">Deposit start date</param>
    /// <param name="asOfDate">Date to calculate accrued value for</param>
    /// <param name="frequency">Compounding frequency</param>
    /// <returns>Accrued value (principal + interest earned)</returns>
    public static decimal CalculateAccruedValue(
        decimal principal,
        decimal annualRate,
        DateOnly startDate,
        DateOnly asOfDate,
        CompoundingFrequency frequency)
    {
        var daysElapsed = asOfDate.DayNumber - startDate.DayNumber;
        if (daysElapsed <= 0) return principal;

        var yearsElapsed = daysElapsed / 365.0m;

        return frequency switch
        {
            CompoundingFrequency.Simple =>
                principal * (1 + annualRate * yearsElapsed),

            CompoundingFrequency.Monthly =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 12), (double)(yearsElapsed * 12)),

            CompoundingFrequency.Quarterly =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 4), (double)(yearsElapsed * 4)),

            CompoundingFrequency.SemiAnnual =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 2), (double)(yearsElapsed * 2)),

            CompoundingFrequency.Annual =>
                principal * (decimal)Math.Pow((double)(1 + annualRate), (double)yearsElapsed),

            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
    }
}

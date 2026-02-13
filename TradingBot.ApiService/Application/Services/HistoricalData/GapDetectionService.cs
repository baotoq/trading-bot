using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services.HistoricalData.Models;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Application.Services.HistoricalData;

/// <summary>
/// Detects missing dates in historical price data using PostgreSQL generate_series.
/// </summary>
public class GapDetectionService(TradingBotDbContext db, ILogger<GapDetectionService> logger)
{
    /// <summary>
    /// Detects all missing dates in the specified range for the given symbol.
    /// Uses PostgreSQL generate_series for accurate gap detection.
    /// </summary>
    public async Task<List<DateOnly>> DetectGapsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string symbol = "BTC",
        CancellationToken ct = default)
    {
        // Use PostgreSQL generate_series to find missing dates
        var sql = $"""
            SELECT gs.date::date
            FROM generate_series({startDate}::date, {endDate}::date, '1 day'::interval) AS gs(date)
            LEFT JOIN "DailyPrices" dp ON gs.date::date = dp."Date" AND dp."Symbol" = {symbol}
            WHERE dp."Date" IS NULL
            ORDER BY gs.date
            """;

        var gaps = await db.Database
            .SqlQuery<DateOnly>(FormattableStringFactory.Create(sql))
            .ToListAsync(ct);

        logger.LogInformation(
            "Detected {GapCount} missing dates between {StartDate} and {EndDate} for {Symbol}",
            gaps.Count, startDate, endDate, symbol);

        return gaps;
    }

    /// <summary>
    /// Calculates data coverage statistics for the specified date range.
    /// </summary>
    public async Task<DataCoverageStats> GetCoverageStatsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string symbol = "BTC",
        CancellationToken ct = default)
    {
        var totalExpectedDays = (endDate.DayNumber - startDate.DayNumber) + 1;

        var storedDays = await db.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= endDate)
            .CountAsync(ct);

        var gapDates = await DetectGapsAsync(startDate, endDate, symbol, ct);

        var coveragePercent = totalExpectedDays > 0
            ? (decimal)storedDays / totalExpectedDays * 100
            : 0m;

        return new DataCoverageStats(
            startDate,
            endDate,
            totalExpectedDays,
            storedDays,
            gapDates.Count,
            gapDates,
            Math.Round(coveragePercent, 2)
        );
    }
}

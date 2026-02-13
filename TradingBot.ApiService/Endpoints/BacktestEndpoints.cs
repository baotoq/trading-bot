using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.Backtest;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Endpoints;

public static class BacktestEndpoints
{
    public static WebApplication MapBacktestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backtest");
        group.MapPost("/", RunBacktestAsync);
        return app;
    }

    private static async Task<IResult> RunBacktestAsync(
        BacktestRequest request,
        TradingBotDbContext db,
        IOptionsMonitor<DcaOptions> dcaOptions,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        const string symbol = "BTC";

        // Get production DcaOptions for defaults
        var prodOptions = dcaOptions.CurrentValue;

        // Query available data range
        var priceStats = await db.DailyPrices
            .Where(p => p.Symbol == symbol)
            .GroupBy(p => p.Symbol)
            .Select(g => new
            {
                MinDate = g.Min(p => p.Date),
                MaxDate = g.Max(p => p.Date),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        if (priceStats == null || priceStats.Count == 0)
        {
            logger.LogWarning("No historical data available for {Symbol}", symbol);
            return Results.BadRequest(new { error = "No historical data available. Run POST /api/backtest/data/ingest first." });
        }

        // Resolve date range
        var startDate = request.StartDate ?? priceStats.MaxDate.AddYears(-2);
        var endDate = request.EndDate ?? priceStats.MaxDate;

        // Clamp start date to available data
        if (startDate < priceStats.MinDate)
        {
            startDate = priceStats.MinDate;
        }

        // Validate date range
        if (startDate > priceStats.MaxDate || endDate < priceStats.MinDate)
        {
            logger.LogWarning("Date range [{StartDate}, {EndDate}] exceeds available data [{MinDate}, {MaxDate}]",
                startDate, endDate, priceStats.MinDate, priceStats.MaxDate);
            return Results.BadRequest(new
            {
                error = $"Date range exceeds available data [{priceStats.MinDate}, {priceStats.MaxDate}]"
            });
        }

        if (startDate > endDate)
        {
            logger.LogWarning("Invalid date range: StartDate {StartDate} is after EndDate {EndDate}",
                startDate, endDate);
            return Results.BadRequest(new { error = "StartDate must be before or equal to EndDate" });
        }

        // Fetch price data for the specified range
        var prices = await db.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= endDate)
            .OrderBy(p => p.Date)
            .Select(p => new DailyPriceData(
                p.Date,
                p.Open,
                p.High,
                p.Low,
                p.Close,
                p.Volume))
            .ToListAsync(ct);

        if (prices.Count == 0)
        {
            logger.LogWarning("No price data found for {Symbol} in range [{StartDate}, {EndDate}]",
                symbol, startDate, endDate);
            return Results.BadRequest(new { error = "No price data found for the specified date range" });
        }

        // Resolve config with defaults from production DcaOptions
        var baseDailyAmount = request.BaseDailyAmount ?? prodOptions.BaseDailyAmount;
        var highLookbackDays = request.HighLookbackDays ?? prodOptions.HighLookbackDays;
        var bearMarketMaPeriod = request.BearMarketMaPeriod ?? prodOptions.BearMarketMaPeriod;
        var bearBoostFactor = request.BearBoostFactor ?? prodOptions.BearBoostFactor;
        var maxMultiplierCap = request.MaxMultiplierCap ?? prodOptions.MaxMultiplierCap;

        // Convert tiers
        var tiers = request.Tiers != null
            ? request.Tiers.Select(t => new MultiplierTierConfig(t.DropPercentage, t.Multiplier)).ToList()
            : prodOptions.MultiplierTiers.Select(t => new MultiplierTierConfig(t.DropPercentage, t.Multiplier)).ToList();

        var config = new BacktestConfig(
            baseDailyAmount,
            highLookbackDays,
            bearMarketMaPeriod,
            bearBoostFactor,
            maxMultiplierCap,
            tiers);

        // Run simulation
        var result = BacktestSimulator.Run(config, prices);

        logger.LogInformation(
            "Backtest completed for {StartDate} to {EndDate}: {TotalDays} days, smart DCA return {ReturnPercent:F2}%",
            startDate, endDate, prices.Count, result.SmartDca.ReturnPercent);

        // Build response
        var response = new BacktestResponse(
            config,
            startDate,
            endDate,
            prices.Count,
            result);

        return Results.Ok(response);
    }
}

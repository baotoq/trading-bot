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
        group.MapPost("/sweep", RunSweepAsync);
        group.MapGet("/presets/{name}", GetPresetAsync);
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

    private static async Task<IResult> RunSweepAsync(
        SweepRequest request,
        TradingBotDbContext db,
        IOptionsMonitor<DcaOptions> dcaOptions,
        ParameterSweepService sweepService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        const string symbol = "BTC";

        // Resolve preset if specified
        var mergedRequest = request;
        if (!string.IsNullOrWhiteSpace(request.Preset))
        {
            try
            {
                var preset = SweepPresets.GetPreset(request.Preset);

                // Merge: preset values fill in null parameter lists; explicit request values override preset
                mergedRequest = new SweepRequest(
                    StartDate: request.StartDate ?? preset.StartDate,
                    EndDate: request.EndDate ?? preset.EndDate,
                    Preset: null, // Clear preset after resolution
                    BaseAmounts: request.BaseAmounts ?? preset.BaseAmounts,
                    HighLookbackDays: request.HighLookbackDays ?? preset.HighLookbackDays,
                    BearMarketMaPeriods: request.BearMarketMaPeriods ?? preset.BearMarketMaPeriods,
                    BearBoosts: request.BearBoosts ?? preset.BearBoosts,
                    MaxMultiplierCaps: request.MaxMultiplierCaps ?? preset.MaxMultiplierCaps,
                    TierSets: request.TierSets ?? preset.TierSets,
                    RankBy: request.RankBy,
                    MaxCombinations: request.MaxCombinations,
                    Validate: request.Validate);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid preset name: {Preset}", request.Preset);
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        // Get production DcaOptions for defaults
        var prodOptions = dcaOptions.CurrentValue;

        // Build defaults config from production options
        var defaultTiers = prodOptions.MultiplierTiers
            .Select(t => new MultiplierTierConfig(t.DropPercentage, t.Multiplier))
            .ToList();

        var defaults = new BacktestConfig(
            prodOptions.BaseDailyAmount,
            prodOptions.HighLookbackDays,
            prodOptions.BearMarketMaPeriod,
            prodOptions.BearBoostFactor,
            prodOptions.MaxMultiplierCap,
            defaultTiers);

        // Generate combinations
        var configs = sweepService.GenerateCombinations(mergedRequest, defaults);

        // Check safety cap
        if (configs.Count > mergedRequest.MaxCombinations)
        {
            logger.LogWarning("Parameter combination count {Count} exceeds maximum {Max}",
                configs.Count, mergedRequest.MaxCombinations);
            return Results.BadRequest(new
            {
                error = $"Parameter combination count ({configs.Count}) exceeds maximum ({mergedRequest.MaxCombinations}). Reduce parameter ranges or increase maxCombinations.",
                count = configs.Count,
                max = mergedRequest.MaxCombinations
            });
        }

        logger.LogInformation("Sweep requested with {Count} combinations, rankBy: {RankBy}, validate: {Validate}",
            configs.Count, mergedRequest.RankBy, mergedRequest.Validate);

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
        var startDate = mergedRequest.StartDate ?? priceStats.MaxDate.AddYears(-2);
        var endDate = mergedRequest.EndDate ?? priceStats.MaxDate;

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

        // Fetch price data
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

        // Execute sweep
        var (results, topResults) = await sweepService.ExecuteSweepAsync(configs, prices, mergedRequest.RankBy, ct);

        // Optional walk-forward validation
        WalkForwardSummary? walkForwardSummary = null;
        if (mergedRequest.Validate)
        {
            logger.LogInformation("Running walk-forward validation for {Count} configurations", configs.Count);

            // Build list of (config, result) from ranked results
            var configResultPairs = results.Select(r => (r.Config, BacktestResult: new BacktestResult(
                SmartDca: r.SmartDca,
                FixedDcaSameBase: r.FixedDcaSameBase,
                FixedDcaMatchTotal: new DcaMetrics(0, 0, 0, 0, 0, 0), // Not used for walk-forward
                Comparison: r.Comparison,
                TierBreakdown: [],
                PurchaseLog: []))).ToList();

            var (walkForwardEntries, summary) = WalkForwardValidator.ValidateAll(configResultPairs, prices);

            walkForwardSummary = summary;

            // Attach walk-forward entries to results
            for (int i = 0; i < results.Count && i < walkForwardEntries.Count; i++)
            {
                results[i] = results[i] with { WalkForward = walkForwardEntries[i] };
            }

            for (int i = 0; i < topResults.Count && i < walkForwardEntries.Count; i++)
            {
                topResults[i] = topResults[i] with { WalkForward = walkForwardEntries[i] };
            }

            logger.LogInformation("Walk-forward validation complete: {OverfitCount}/{TotalValidated} flagged as overfit",
                summary.OverfitCount, summary.TotalValidated);
        }

        // Build response
        var response = new SweepResponse(
            TotalCombinations: configs.Count,
            ExecutedCombinations: results.Count,
            RankedBy: mergedRequest.RankBy,
            StartDate: startDate,
            EndDate: endDate,
            TotalDays: prices.Count,
            Results: results,
            TopResults: topResults,
            WalkForward: walkForwardSummary);

        return Results.Ok(response);
    }

    private static IResult GetPresetAsync(string name, ILogger<Program> logger)
    {
        try
        {
            var preset = SweepPresets.GetPreset(name);
            return Results.Ok(preset);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid preset name: {Preset}", name);
            return Results.NotFound(new { error = ex.Message });
        }
    }
}

using System.ComponentModel.DataAnnotations;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Endpoints;

public static class ConfigurationEndpoints
{
    public static WebApplication MapConfigurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/config")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/", GetConfigAsync);
        group.MapGet("/defaults", GetDefaultsAsync);
        group.MapPut("/", UpdateConfigAsync);

        return app;
    }

    private static async Task<IResult> GetConfigAsync(
        IConfigurationService configService,
        CancellationToken ct)
    {
        var options = await configService.GetCurrentAsync(ct);
        var response = MapToConfigResponse(options);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetDefaultsAsync(
        IConfigurationService configService,
        CancellationToken ct)
    {
        var options = await configService.GetDefaultsAsync(ct);
        var response = MapToConfigResponse(options);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateConfigAsync(
        UpdateConfigRequest request,
        IConfigurationService configService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var options = new DcaOptions
            {
                BaseDailyAmount = request.BaseDailyAmount,
                DailyBuyHour = request.DailyBuyHour,
                DailyBuyMinute = request.DailyBuyMinute,
                HighLookbackDays = request.HighLookbackDays,
                DryRun = request.DryRun,
                BearMarketMaPeriod = request.BearMarketMaPeriod,
                BearBoostFactor = request.BearBoostFactor,
                MaxMultiplierCap = request.MaxMultiplierCap,
                MultiplierTiers = request.Tiers
                    .Select(t => new MultiplierTier
                    {
                        DropPercentage = t.DropPercentage,
                        Multiplier = t.Multiplier
                    })
                    .ToList()
            };

            await configService.UpdateAsync(options, ct);

            logger.LogInformation(
                "DCA configuration updated: BaseDailyAmount={BaseDailyAmount}, DailyBuyTime={Hour}:{Minute}, DryRun={DryRun}",
                options.BaseDailyAmount,
                options.DailyBuyHour,
                options.DailyBuyMinute,
                options.DryRun);

            return Results.Ok(new { message = "Configuration updated successfully" });
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Configuration update failed validation");
            return Results.BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private static ConfigResponse MapToConfigResponse(DcaOptions options)
    {
        return new ConfigResponse(
            BaseDailyAmount: options.BaseDailyAmount,
            DailyBuyHour: options.DailyBuyHour,
            DailyBuyMinute: options.DailyBuyMinute,
            HighLookbackDays: options.HighLookbackDays,
            DryRun: options.DryRun,
            BearMarketMaPeriod: options.BearMarketMaPeriod,
            BearBoostFactor: options.BearBoostFactor,
            MaxMultiplierCap: options.MaxMultiplierCap,
            Tiers: options.MultiplierTiers
                .Select(t => new MultiplierTierDto(t.DropPercentage, t.Multiplier))
                .ToList()
        );
    }
}

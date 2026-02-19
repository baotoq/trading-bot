using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Application.Services;

public interface IConfigurationService
{
    Task<DcaOptions> GetCurrentAsync(CancellationToken ct = default);
    Task<ErrorOr<Updated>> UpdateAsync(DcaOptions options, CancellationToken ct = default);
    Task<DcaOptions> GetDefaultsAsync(CancellationToken ct = default);
}

public class ConfigurationService(
    TradingBotDbContext db,
    IOptionsMonitor<DcaOptions> defaultOptions,
    IOptionsMonitorCache<DcaOptions> optionsCache,
    IValidateOptions<DcaOptions> validator,
    IConfiguration configuration) : IConfigurationService
{
    public async Task<DcaOptions> GetCurrentAsync(CancellationToken ct = default)
    {
        var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);

        if (entity == null)
        {
            // No DB override — return defaults from appsettings.json
            return defaultOptions.CurrentValue;
        }

        return MapToOptions(entity);
    }

    public async Task<ErrorOr<Updated>> UpdateAsync(DcaOptions options, CancellationToken ct = default)
    {
        // Validate input (defense-in-depth: catches config-binding errors before touching aggregate)
        var validationResult = validator.Validate(Options.DefaultName, options);
        if (validationResult.Failed)
        {
            return Error.Validation("ConfigValidationFailed",
                string.Join("; ", validationResult.Failures ?? []));
        }

        // Upsert configuration
        var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);

        if (entity == null)
        {
            // Create path: DcaConfiguration.Create() still throws per locked decision (validator already passed)
            entity = DcaConfiguration.Create(
                DcaConfigurationId.Singleton,
                options.BaseDailyAmount,
                options.DailyBuyHour,
                options.DailyBuyMinute,
                options.HighLookbackDays,
                options.DryRun,
                options.BearMarketMaPeriod,
                options.BearBoostFactor,
                options.MaxMultiplierCap,
                options.MultiplierTiers
                    .Select(t => new MultiplierTierData(t.DropPercentage.Value, t.Multiplier.Value))
                    .ToList());
            db.DcaConfigurations.Add(entity);
        }
        else
        {
            // Update path: call behavior methods and propagate ErrorOr errors
            entity.UpdateDailyAmount(options.BaseDailyAmount);

            var scheduleResult = entity.UpdateSchedule(options.DailyBuyHour, options.DailyBuyMinute);
            if (scheduleResult.IsError) return scheduleResult.Errors;

            var tiersResult = entity.UpdateTiers(options.MultiplierTiers
                .Select(t => new MultiplierTierData(t.DropPercentage.Value, t.Multiplier.Value))
                .ToList());
            if (tiersResult.IsError) return tiersResult.Errors;

            var bearResult = entity.UpdateBearMarket(options.BearMarketMaPeriod, options.BearBoostFactor);
            if (bearResult.IsError) return bearResult.Errors;

            var settingsResult = entity.UpdateSettings(options.HighLookbackDays, options.DryRun, options.MaxMultiplierCap);
            if (settingsResult.IsError) return settingsResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        // CRITICAL: Invalidate IOptionsMonitor cache so next read picks up new values
        optionsCache.TryRemove(Options.DefaultName);

        return Result.Updated;
    }

    public Task<DcaOptions> GetDefaultsAsync(CancellationToken ct = default)
    {
        // Return appsettings.json defaults (before any DB override)
        // We read directly from configuration to get the original values
        var defaults = new DcaOptions();
        configuration.GetSection("DcaOptions").Bind(defaults);
        return Task.FromResult(defaults);
    }

    private static DcaOptions MapToOptions(DcaConfiguration entity)
    {
        return new DcaOptions
        {
            BaseDailyAmount = entity.BaseDailyAmount,
            DailyBuyHour = entity.DailyBuyHour,
            DailyBuyMinute = entity.DailyBuyMinute,
            HighLookbackDays = entity.HighLookbackDays,
            DryRun = entity.DryRun,
            BearMarketMaPeriod = entity.BearMarketMaPeriod,
            BearBoostFactor = entity.BearBoostFactor,
            MaxMultiplierCap = entity.MaxMultiplierCap,
            MultiplierTiers = entity.MultiplierTiers
                .Select(t => new MultiplierTier
                {
                    DropPercentage = Percentage.From(t.DropPercentage),
                    Multiplier = Multiplier.From(t.Multiplier)
                })
                .ToList()
        };
    }
}

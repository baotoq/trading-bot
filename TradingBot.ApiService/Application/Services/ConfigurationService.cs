using System.ComponentModel.DataAnnotations;
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
    Task UpdateAsync(DcaOptions options, CancellationToken ct = default);
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

    public async Task UpdateAsync(DcaOptions options, CancellationToken ct = default)
    {
        // Validate input
        var validationResult = validator.Validate(Options.DefaultName, options);
        if (validationResult.Failed)
        {
            throw new ValidationException(string.Join("; ", validationResult.Failures ?? []));
        }

        // Upsert configuration
        var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);

        if (entity == null)
        {
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
            entity.UpdateDailyAmount(options.BaseDailyAmount);
            entity.UpdateSchedule(options.DailyBuyHour, options.DailyBuyMinute);
            entity.UpdateTiers(options.MultiplierTiers
                .Select(t => new MultiplierTierData(t.DropPercentage.Value, t.Multiplier.Value))
                .ToList());
            entity.UpdateBearMarket(options.BearMarketMaPeriod, options.BearBoostFactor);
            entity.UpdateSettings(options.HighLookbackDays, options.DryRun, options.MaxMultiplierCap);
        }

        await db.SaveChangesAsync(ct);

        // CRITICAL: Invalidate IOptionsMonitor cache so next read picks up new values
        optionsCache.TryRemove(Options.DefaultName);
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

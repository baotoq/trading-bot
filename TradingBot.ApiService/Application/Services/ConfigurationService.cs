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
            entity = new DcaConfiguration
            {
                Id = DcaConfigurationId.Singleton
            };
            db.DcaConfigurations.Add(entity);
        }

        MapFromOptions(entity, options);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

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

    private static void MapFromOptions(DcaConfiguration entity, DcaOptions options)
    {
        entity.BaseDailyAmount = options.BaseDailyAmount;
        entity.DailyBuyHour = options.DailyBuyHour;
        entity.DailyBuyMinute = options.DailyBuyMinute;
        entity.HighLookbackDays = options.HighLookbackDays;
        entity.DryRun = options.DryRun;
        entity.BearMarketMaPeriod = options.BearMarketMaPeriod;
        entity.BearBoostFactor = options.BearBoostFactor;
        entity.MaxMultiplierCap = options.MaxMultiplierCap;
        entity.MultiplierTiers = options.MultiplierTiers
            .Select(t => new MultiplierTierData(t.DropPercentage.Value, t.Multiplier.Value))
            .ToList();
    }
}

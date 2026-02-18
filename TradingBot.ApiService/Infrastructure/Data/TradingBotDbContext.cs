using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Infrastructure.Data;

public class TradingBotDbContext(DbContextOptions<TradingBotDbContext> options) : DbContext(options)
{
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<DcaConfiguration> DcaConfigurations => Set<DcaConfiguration>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Register Vogen EF Core value converters globally for all typed ID properties.
        // Vogen generates EfCoreValueConverter + EfCoreValueComparer inner classes per ID type.
        // Using ConfigureConventions ensures all properties of these types get converters automatically
        // across the entire model — no per-property registration needed in OnModelCreating.
        configurationBuilder.Properties<PurchaseId>()
            .HaveConversion<PurchaseId.EfCoreValueConverter, PurchaseId.EfCoreValueComparer>();
        configurationBuilder.Properties<IngestionJobId>()
            .HaveConversion<IngestionJobId.EfCoreValueConverter, IngestionJobId.EfCoreValueComparer>();
        configurationBuilder.Properties<DcaConfigurationId>()
            .HaveConversion<DcaConfigurationId.EfCoreValueConverter, DcaConfigurationId.EfCoreValueComparer>();

        // Phase 14 (value objects)
        configurationBuilder.Properties<Price>()
            .HaveConversion<Price.EfCoreValueConverter, Price.EfCoreValueComparer>();
        configurationBuilder.Properties<UsdAmount>()
            .HaveConversion<UsdAmount.EfCoreValueConverter, UsdAmount.EfCoreValueComparer>();
        configurationBuilder.Properties<Quantity>()
            .HaveConversion<Quantity.EfCoreValueConverter, Quantity.EfCoreValueComparer>();
        configurationBuilder.Properties<Multiplier>()
            .HaveConversion<Multiplier.EfCoreValueConverter, Multiplier.EfCoreValueComparer>();
        configurationBuilder.Properties<Percentage>()
            .HaveConversion<Percentage.EfCoreValueConverter, Percentage.EfCoreValueComparer>();
        configurationBuilder.Properties<Symbol>()
            .HaveConversion<Symbol.EfCoreValueConverter, Symbol.EfCoreValueComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Price)
                .HasPrecision(18, 8);

            entity.Property(e => e.Quantity)
                .HasPrecision(18, 8);

            entity.Property(e => e.Cost)
                .HasPrecision(18, 2);

            entity.Property(e => e.Multiplier)
                .HasPrecision(4, 2);

            entity.HasIndex(e => e.ExecutedAt);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasConversion<string>();

            entity.Property(e => e.OrderId)
                .HasMaxLength(100);

            entity.Property(e => e.FailureReason)
                .HasMaxLength(500);

            entity.Property(e => e.IsDryRun)
                .HasDefaultValue(false);

            // Multiplier metadata fields
            entity.Property(e => e.MultiplierTier)
                .HasMaxLength(50);

            entity.Property(e => e.DropPercentage)
                .HasPrecision(8, 4);

            entity.Property(e => e.High30Day)
                .HasPrecision(18, 8);

            entity.Property(e => e.Ma200Day)
                .HasPrecision(18, 8);

            // RawResponse stored as text (no max length)
        });

        modelBuilder.Entity<DailyPrice>(entity =>
        {
            entity.HasKey(e => new { e.Date, e.Symbol });

            entity.HasIndex(e => e.Date);

            entity.Property(e => e.Symbol)
                .HasMaxLength(20);

            entity.Property(e => e.Open)
                .HasPrecision(18, 8);

            entity.Property(e => e.High)
                .HasPrecision(18, 8);

            entity.Property(e => e.Low)
                .HasPrecision(18, 8);

            entity.Property(e => e.Close)
                .HasPrecision(18, 8);

            entity.Property(e => e.Volume)
                .HasPrecision(18, 8);
        });

        modelBuilder.Entity<IngestionJob>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasConversion<string>();

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<DcaConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BaseDailyAmount)
                .HasPrecision(18, 2);

            entity.Property(e => e.BearBoostFactor)
                .HasPrecision(4, 2);

            entity.Property(e => e.MaxMultiplierCap)
                .HasPrecision(4, 2);

            entity.Property(e => e.MultiplierTiers)
                .HasColumnType("jsonb");

            // Enforce single-row constraint
            entity.ToTable(t => t.HasCheckConstraint("CK_DcaConfiguration_SingleRow", "id = '00000000-0000-0000-0000-000000000001'::uuid"));
        });
    }
}

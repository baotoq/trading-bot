using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;
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
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeadLetterMessage> DeadLetterMessages => Set<DeadLetterMessage>();

    // Phase 26 (portfolio entities)
    public DbSet<PortfolioAsset> PortfolioAssets => Set<PortfolioAsset>();
    public DbSet<AssetTransaction> AssetTransactions => Set<AssetTransaction>();
    public DbSet<FixedDeposit> FixedDeposits => Set<FixedDeposit>();

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
        configurationBuilder.Properties<DeviceTokenId>()
            .HaveConversion<DeviceTokenId.EfCoreValueConverter, DeviceTokenId.EfCoreValueComparer>();

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

        // Phase 26 (portfolio typed IDs + value objects)
        configurationBuilder.Properties<PortfolioAssetId>()
            .HaveConversion<PortfolioAssetId.EfCoreValueConverter, PortfolioAssetId.EfCoreValueComparer>();
        configurationBuilder.Properties<AssetTransactionId>()
            .HaveConversion<AssetTransactionId.EfCoreValueConverter, AssetTransactionId.EfCoreValueComparer>();
        configurationBuilder.Properties<FixedDepositId>()
            .HaveConversion<FixedDepositId.EfCoreValueConverter, FixedDepositId.EfCoreValueComparer>();
        configurationBuilder.Properties<VndAmount>()
            .HaveConversion<VndAmount.EfCoreValueConverter, VndAmount.EfCoreValueComparer>();
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

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(10).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
        });

        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.Entity<DeadLetterMessage>(entity =>
        {
            entity.ToTable("DeadLetterMessages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventName);
            entity.HasIndex(e => e.FailedAt);
        });

        // Phase 26: Portfolio entities
        modelBuilder.Entity<PortfolioAsset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AssetType).HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.NativeCurrency).HasMaxLength(5).HasConversion<string>();
            entity.HasMany(e => e.Transactions)
                .WithOne()
                .HasForeignKey(t => t.PortfolioAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.PricePerUnit).HasPrecision(18, 8);
            entity.Property(e => e.Fee).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(5).HasConversion<string>();
            entity.Property(e => e.Type).HasMaxLength(10).HasConversion<string>();
            entity.Property(e => e.Source).HasMaxLength(10).HasConversion<string>();
            entity.Property(e => e.SourcePurchaseId).IsRequired(false);
            entity.HasIndex(e => e.SourcePurchaseId)
                .IsUnique()
                .HasFilter("\"SourcePurchaseId\" IS NOT NULL");
            entity.HasIndex(e => e.PortfolioAssetId);
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<FixedDeposit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BankName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Principal).HasPrecision(18, 0);
            entity.Property(e => e.AnnualInterestRate).HasPrecision(8, 6);
            entity.Property(e => e.CompoundingFrequency).HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.Status).HasMaxLength(10).HasConversion<string>();
            entity.HasIndex(e => e.Status);
        });
    }
}

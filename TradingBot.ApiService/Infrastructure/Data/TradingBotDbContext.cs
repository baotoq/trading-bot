using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Infrastructure.Data;

public class TradingBotDbContext(DbContextOptions<TradingBotDbContext> options) : DbContext(options)
{
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();

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
    }
}

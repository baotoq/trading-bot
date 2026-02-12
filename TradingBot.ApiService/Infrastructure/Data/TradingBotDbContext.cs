using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Infrastructure.Data;

public class TradingBotDbContext(DbContextOptions<TradingBotDbContext> options) : DbContext(options)
{
    public DbSet<Purchase> Purchases => Set<Purchase>();

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

            // RawResponse stored as text (no max length)
        });
    }
}

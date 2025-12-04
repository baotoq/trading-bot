using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure;

public class ApplicationDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<TradeLog> TradeLogs => Set<TradeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddCandleEntity();
        modelBuilder.AddOutboxMessageEntity();

        // Configure Position entity
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol)
                .HasConversion(new ValueConverter<Symbol, string>(
                    v => v.Value,
                    v => new Symbol(v)
                ))
                .HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.StopLoss).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit1).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit2).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit3).HasPrecision(18, 8);
            entity.Property(e => e.RiskAmount).HasPrecision(18, 8);
            entity.Property(e => e.RemainingQuantity).HasPrecision(18, 8);
            entity.Property(e => e.RealizedPnL).HasPrecision(18, 8);
            entity.Property(e => e.UnrealizedPnL).HasPrecision(18, 8);
            entity.Property(e => e.Strategy).HasMaxLength(100);
            entity.Property(e => e.SignalReason).HasMaxLength(500);
            entity.Property(e => e.ExitReason).HasMaxLength(500);

            entity.HasIndex(e => new { e.Symbol, e.Status });
            entity.HasIndex(e => e.EntryTime);
        });

        // Configure TradeLog entity
        modelBuilder.Entity<TradeLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol)
                .HasMaxLength(50)
                .HasConversion(new ValueConverter<Symbol, string>(
                    v => v.Value,
                    v => new Symbol(v)
                ))
                .IsRequired();
            entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
            entity.Property(e => e.ExitPrice).HasPrecision(18, 8);
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.StopLoss).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit1).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit2).HasPrecision(18, 8);
            entity.Property(e => e.TakeProfit3).HasPrecision(18, 8);
            entity.Property(e => e.RealizedPnL).HasPrecision(18, 8);
            entity.Property(e => e.RealizedPnLPercent).HasPrecision(18, 8);
            entity.Property(e => e.RiskRewardRatio).HasPrecision(18, 8);
            entity.Property(e => e.Fees).HasPrecision(18, 8);
            entity.Property(e => e.Slippage).HasPrecision(18, 8);
            entity.Property(e => e.AtrAtEntry).HasPrecision(18, 8);
            entity.Property(e => e.FundingRateAtEntry).HasPrecision(18, 8);
            entity.Property(e => e.VolumeAtEntry).HasPrecision(18, 8);
            entity.Property(e => e.RsiAtEntry).HasPrecision(18, 8);
            entity.Property(e => e.MacdAtEntry).HasPrecision(18, 8);
            entity.Property(e => e.Strategy).HasMaxLength(100);
            entity.Property(e => e.SignalReason).HasMaxLength(500);
            entity.Property(e => e.ExitReason).HasMaxLength(500);

            entity.Property(e => e.Indicators).HasColumnType("jsonb");

            entity.HasIndex(e => e.EntryTime);
            entity.HasIndex(e => e.ExitTime);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.IsWin);

            entity.HasOne(e => e.Position)
                .WithMany()
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
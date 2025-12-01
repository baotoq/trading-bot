using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel.DataAnnotations;
using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Domain;

public class Candle : BaseEntity
{
    public required string Symbol { get; set; }
    public required string Interval { get; set; }
    public required DateTimeOffset OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public DateTimeOffset CloseTime { get; set; }
}

public static class CandleModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public void AddCandleEntity(Action<EntityTypeBuilder<Candle>>? callback = null)
        {
            EntityTypeBuilder<Candle> outbox = modelBuilder.Entity<Candle>();

            outbox.HasKey(p => p.Id);
            outbox.HasIndex(p => new { p.Symbol, p.Interval, p.OpenTime }).IsUnique();

            callback?.Invoke(outbox);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel.DataAnnotations;
using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Domain;

public class Candle : AuditedEntity
{
    public required string Symbol { get; set; }
    public required CandleInterval Interval { get; set; }
    public required DateTimeOffset OpenTime { get; set; }
    public DateTimeOffset CloseTime { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal Volume { get; set; }
}

public static class CandleModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public void AddCandleEntity(Action<EntityTypeBuilder<Candle>>? callback = null)
        {
            EntityTypeBuilder<Candle> candle = modelBuilder.Entity<Candle>();

            candle.HasKey(p => new { p.Symbol, p.Interval, p.OpenTime });

            candle
                .Property(e => e.Symbol)
                .HasMaxLength(50);
            candle
                .Property(e => e.Interval)
                .HasMaxLength(100)
                .HasConversion(new ValueConverter<CandleInterval, string>(
                    v => v.Value,
                    v => new CandleInterval(v)
                ));

            callback?.Invoke(candle);
        }
    }
}
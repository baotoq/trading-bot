using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure;

public class ApplicationDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddCandleEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore;

public static class EfCoreOutboxMessageModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        public void AddOutboxMessageEntity(Action<EntityTypeBuilder<OutboxMessage>>? callback = null)
        {
            EntityTypeBuilder<OutboxMessage> outbox = modelBuilder.Entity<OutboxMessage>();

            outbox.ToTable("OutboxMessages");
            outbox.HasKey(p => p.Id);
            outbox.HasIndex(p => p.ProcessingStatus);

            callback?.Invoke(outbox);
        }
    }
}
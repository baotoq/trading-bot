using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct IngestionJobId
{
    public static IngestionJobId New() => From(Guid.CreateVersion7());
}

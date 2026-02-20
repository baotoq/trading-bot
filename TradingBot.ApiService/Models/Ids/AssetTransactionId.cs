using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct AssetTransactionId
{
    public static AssetTransactionId New() => From(Guid.CreateVersion7());
}

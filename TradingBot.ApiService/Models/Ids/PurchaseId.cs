using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct PurchaseId
{
    public static PurchaseId New() => From(Guid.CreateVersion7());
}

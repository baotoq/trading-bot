using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct DeviceTokenId
{
    public static DeviceTokenId New() => From(Guid.CreateVersion7());
}

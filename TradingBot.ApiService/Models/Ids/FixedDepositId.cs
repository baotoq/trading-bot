using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct FixedDepositId
{
    public static FixedDepositId New() => From(Guid.CreateVersion7());
}

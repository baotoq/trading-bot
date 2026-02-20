using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct PortfolioAssetId
{
    public static PortfolioAssetId New() => From(Guid.CreateVersion7());
}

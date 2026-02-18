using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct DcaConfigurationId
{
    public static DcaConfigurationId New() => From(Guid.CreateVersion7());

    public static readonly DcaConfigurationId Singleton =
        From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}

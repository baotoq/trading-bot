using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

public class DeviceToken : BaseEntity<DeviceTokenId>
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "ios";
}

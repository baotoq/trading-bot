namespace TradingBot.ApiService.Application.Services;

public interface ISignalGeneratorService
{
    Task GenerateSignalAsync(string symbol, CancellationToken cancellationToken = default);
    Task EnableSignalNotificationsAsync(string symbol, string strategy);
    Task DisableSignalNotificationsAsync(string symbol);
    bool IsNotificationEnabled(string symbol);
    IReadOnlyDictionary<string, string> GetEnabledNotifications();
}

using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ISignalGeneratorService
{
    Task GenerateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default);
    Task EnableSignalNotificationsAsync(Symbol symbol, string strategy);
    Task DisableSignalNotificationsAsync(Symbol symbol);
    bool IsNotificationEnabled(Symbol symbol);
    IReadOnlyDictionary<Symbol, string> GetEnabledNotifications();
}

using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ITelegramNotificationService
{
    Task SendSignalNotificationAsync(TradingSignal signal, CancellationToken cancellationToken = default);
    Task SendTradeExecutionNotificationAsync(string message, CancellationToken cancellationToken = default);
    Task SendErrorNotificationAsync(string error, CancellationToken cancellationToken = default);
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}

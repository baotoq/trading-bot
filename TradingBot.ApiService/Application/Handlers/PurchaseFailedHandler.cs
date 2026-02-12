using MediatR;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Telegram;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseFailedHandler(
    TelegramNotificationService telegramService,
    ILogger<PurchaseFailedHandler> logger) : INotificationHandler<PurchaseFailedEvent>
{
    public async Task Handle(PurchaseFailedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseFailedEvent with error type {ErrorType}", notification.ErrorType);

        var message = $"""
            *BTC Purchase Failed*

            *Error:* {notification.ErrorType}
            *Message:* `{notification.ErrorMessage}`
            *Retries:* {notification.RetryCount}/3

            _{notification.FailedAt:yyyy-MM-dd HH:mm:ss} UTC_
            """;

        try
        {
            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for failed purchase");
        }
    }
}

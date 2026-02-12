using MediatR;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Telegram;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseSkippedHandler(
    TelegramNotificationService telegramService,
    ILogger<PurchaseSkippedHandler> logger) : INotificationHandler<PurchaseSkippedEvent>
{
    public async Task Handle(PurchaseSkippedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseSkippedEvent with reason {Reason}", notification.Reason);

        var balanceLine = notification.CurrentBalance.HasValue
            ? $"*Balance:* `${notification.CurrentBalance.Value:F2}` USDC\n"
            : string.Empty;

        var requiredLine = notification.RequiredAmount.HasValue
            ? $"*Required:* `${notification.RequiredAmount.Value:F2}` USDC\n"
            : string.Empty;

        var message = $"""
            *BTC Purchase Skipped*

            *Reason:* {notification.Reason}
            {balanceLine}{requiredLine}
            _{notification.SkippedAt:yyyy-MM-dd HH:mm:ss} UTC_
            """;

        try
        {
            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for skipped purchase");
        }
    }
}

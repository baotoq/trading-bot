using MediatR;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Telegram;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseCompletedHandler(
    TelegramNotificationService telegramService,
    ILogger<PurchaseCompletedHandler> logger) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseCompletedEvent for purchase {PurchaseId}", notification.PurchaseId);

        var message = $"""
            *BTC Purchase Successful*

            *BTC Bought:* `{notification.BtcAmount:F8}` BTC
            *Price:* `${notification.Price:F2}`
            *USD Spent:* `${notification.UsdSpent:F2}`
            *BTC Balance:* `{notification.CurrentBtcBalance:F8}` BTC
            *USDC Remaining:* `${notification.RemainingUsdc:F2}`

            _{notification.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC_
            """;

        try
        {
            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for completed purchase");
        }
    }
}

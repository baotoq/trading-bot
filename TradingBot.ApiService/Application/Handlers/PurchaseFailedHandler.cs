using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Telegram;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseFailedHandler(
    TelegramNotificationService telegramService,
    TradingBotDbContext dbContext,
    ILogger<PurchaseFailedHandler> logger) : INotificationHandler<PurchaseFailedEvent>
{
    public async Task Handle(PurchaseFailedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseFailedEvent for purchase {PurchaseId}", notification.PurchaseId);

        try
        {
            var purchase = await dbContext.Purchases
                .FirstOrDefaultAsync(p => p.Id == notification.PurchaseId, cancellationToken);

            if (purchase is null)
            {
                logger.LogWarning("Purchase {PurchaseId} not found when handling PurchaseFailedEvent", notification.PurchaseId);
                return;
            }

            var errorMessage = purchase.FailureReason ?? "Unknown error";
            var failedAt = purchase.UpdatedAt ?? purchase.ExecutedAt;

            var retryMessage = "The bot will retry automatically on transient errors.";

            var message = $"""
                *BTC Purchase Failed*

                *Error:* OrderFailed
                *Details:* `{errorMessage}`
                *Retries:* 0/3

                {retryMessage}
                Check logs if this persists.

                _{failedAt:yyyy-MM-dd HH:mm:ss} UTC_
                """;

            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for failed purchase");
        }
    }
}

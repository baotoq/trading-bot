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

        // Build contextual detail based on skip reason
        var contextualDetail = BuildContextualDetail(notification.Reason);

        var balanceLine = notification.CurrentBalance.HasValue
            ? $"*Balance:* `${notification.CurrentBalance.Value:F2}` USDC\n"
            : string.Empty;

        var requiredLine = notification.RequiredAmount.HasValue
            ? $"*Required:* `${notification.RequiredAmount.Value:F2}` USDC\n"
            : string.Empty;

        var message = $"""
            *BTC Purchase Skipped*

            *Reason:* {notification.Reason}
            {contextualDetail}{balanceLine}{requiredLine}
            _{notification.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC_
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

    private string BuildContextualDetail(string reason)
    {
        if (reason.Contains("Already purchased today", StringComparison.OrdinalIgnoreCase))
        {
            return "Next buy scheduled tomorrow.\n\n";
        }

        if (reason.Contains("Insufficient balance", StringComparison.OrdinalIgnoreCase))
        {
            return "Please add USDC to your Hyperliquid account.\n\n";
        }

        if (reason.Contains("below minimum order value", StringComparison.OrdinalIgnoreCase))
        {
            return "The calculated buy amount was too small for a valid order.\n\n";
        }

        // Default: no additional context
        return "\n";
    }
}

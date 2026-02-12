using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Telegram;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseCompletedHandler(
    TelegramNotificationService telegramService,
    TradingBotDbContext dbContext,
    ILogger<PurchaseCompletedHandler> logger) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseCompletedEvent for purchase {PurchaseId}", notification.PurchaseId);

        try
        {
            // Query running totals from database (exclude dry-run purchases)
            var totals = await dbContext.Purchases
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .Where(p => !p.IsDryRun)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    TotalBtc = g.Sum(p => p.Quantity),
                    TotalUsd = g.Sum(p => p.Cost),
                    PurchaseCount = g.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            var totalBtc = totals?.TotalBtc ?? 0m;
            var totalUsd = totals?.TotalUsd ?? 0m;
            var avgCost = totalBtc > 0 ? totalUsd / totalBtc : 0m;

            // Build multiplier reasoning
            var reasoning = BuildMultiplierReasoning(notification);

            // Format message with rich content
            var titlePrefix = notification.IsDryRun ? "[SIMULATION] " : "";
            var simulationBanner = notification.IsDryRun ? "*⚠️ SIMULATION MODE - No real order placed*\n\n" : "";

            var message = $"""
                {simulationBanner}*{titlePrefix}BTC Purchase Successful*

                *Price:* `${notification.Price:N2}`
                *Bought:* `{notification.BtcAmount:F5}` BTC
                *Cost:* `${notification.UsdSpent:F2}`
                *Multiplier:* `{notification.Multiplier:F1}x`

                {reasoning}

                *Running Totals*
                Total BTC: `{totalBtc:F5}` BTC
                Total Spent: `${totalUsd:N2}`
                Avg Cost: `${avgCost:N2}`

                *Balances*
                BTC: `{notification.CurrentBtcBalance:F5}` BTC
                USDC: `${notification.RemainingUsdc:F2}`

                _{notification.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC_
                """;

            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for completed purchase");
        }
    }

    private string BuildMultiplierReasoning(PurchaseCompletedEvent e)
    {
        // Standard buy (no multiplier applied)
        if (e.Multiplier == 1.0m && (string.IsNullOrEmpty(e.MultiplierTier) || e.MultiplierTier == "None"))
        {
            return "Standard buy (no dip detected)";
        }

        // Multiplier calculation had errors or limited data
        if (e.MultiplierTier?.Contains("Error") == true || e.MultiplierTier?.Contains("N/A") == true)
        {
            return $"Multiplier calculation had limited data, using {e.Multiplier:F1}x";
        }

        // Build natural language explanation for multiplier
        var parts = new List<string>();

        // Dip component
        if (e.DropPercentage > 0 && e.High30Day > 0)
        {
            parts.Add($"BTC is {e.DropPercentage:F1}% below 30-day high (${e.High30Day:N0})");
        }

        // Bear market boost component
        if (e.Ma200Day > 0 && e.Price < e.Ma200Day)
        {
            parts.Add($"price below 200-day MA (${e.Ma200Day:N0}), bear boost active");
        }

        if (parts.Count > 0)
        {
            var explanation = string.Join(" and ", parts);
            return $"Buying {e.Multiplier:F1}x: {explanation}";
        }

        // Fallback if multiplier > 1 but no clear reason
        return $"Buying {e.Multiplier:F1}x (tier: {e.MultiplierTier ?? "unknown"})";
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Infrastructure.Firebase;
using TradingBot.ApiService.Infrastructure.Telegram;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Handlers;

public class PurchaseCompletedHandler(
    TelegramNotificationService telegramService,
    FcmNotificationService fcmService,
    TradingBotDbContext dbContext,
    HyperliquidClient hyperliquidClient,
    ILogger<PurchaseCompletedHandler> logger) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PurchaseCompletedEvent for purchase {PurchaseId}", notification.PurchaseId);

        try
        {
            var purchase = await dbContext.Purchases
                .FirstOrDefaultAsync(p => p.Id == notification.PurchaseId, cancellationToken);

            if (purchase is null)
            {
                logger.LogWarning("Purchase {PurchaseId} not found when handling PurchaseCompletedEvent", notification.PurchaseId);
                return;
            }

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

            // Fetch remaining USDC balance
            var remainingUsdc = await hyperliquidClient.GetBalancesAsync(cancellationToken);

            // TODO: Phase 4 will track actual BTC balance, for now use filled quantity
            var currentBtcBalance = purchase.Quantity;

            // Build multiplier reasoning
            var reasoning = BuildMultiplierReasoning(purchase);

            // Format message with rich content
            var titlePrefix = purchase.IsDryRun ? "[SIMULATION] " : "";
            var simulationBanner = purchase.IsDryRun ? "*⚠️ SIMULATION MODE - No real order placed*\n\n" : "";

            var message = $"""
                {simulationBanner}*{titlePrefix}BTC Purchase Successful*

                *Price:* `${purchase.Price.Value:N2}`
                *Bought:* `{purchase.Quantity.Value:F5}` BTC
                *Cost:* `${purchase.Cost.Value:F2}`
                *Multiplier:* `{purchase.Multiplier.Value:F1}x`

                {reasoning}

                *Running Totals*
                Total BTC: `{totalBtc:F5}` BTC
                Total Spent: `${totalUsd:N2}`
                Avg Cost: `${avgCost:N2}`

                *Balances*
                BTC: `{currentBtcBalance.Value:F5}` BTC
                USDC: `${remainingUsdc:F2}`

                _{purchase.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC_
                """;

            await telegramService.SendMessageAsync(message, cancellationToken);

            // Send FCM push notification
            try
            {
                var pushTitle = purchase.IsDryRun ? "[SIM] BTC Purchased" : "BTC Purchased";
                var pushBody = $"{purchase.Quantity.Value:F5} BTC at ${purchase.Price.Value:N2} ({purchase.Multiplier.Value:F1}x)";
                var data = new Dictionary<string, string>
                {
                    ["type"] = "purchase_completed",
                    ["route"] = "/history"
                };
                await fcmService.SendToAllDevicesAsync(pushTitle, pushBody, data, cancellationToken);
            }
            catch (Exception fcmEx)
            {
                logger.LogError(fcmEx, "Error sending FCM notification for completed purchase");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Telegram notification for completed purchase");
        }
    }

    private string BuildMultiplierReasoning(Purchase p)
    {
        // Standard buy (no multiplier applied)
        if (p.Multiplier.Value == 1.0m && (string.IsNullOrEmpty(p.MultiplierTier) || p.MultiplierTier == "None"))
        {
            return "Standard buy (no dip detected)";
        }

        // Multiplier calculation had errors or limited data
        if (p.MultiplierTier?.Contains("Error") == true || p.MultiplierTier?.Contains("N/A") == true)
        {
            return $"Multiplier calculation had limited data, using {p.Multiplier.Value:F1}x";
        }

        // Build natural language explanation for multiplier
        var parts = new List<string>();

        // Dip component (DropPercentage is in 0-1 format, multiply by 100 for display)
        if (p.DropPercentage.Value > 0 && p.High30Day > 0)
        {
            parts.Add($"BTC is {p.DropPercentage.Value * 100:F1}% below 30-day high (${p.High30Day:N0})");
        }

        // Bear market boost component
        if (p.Ma200Day > 0 && p.Price.Value < p.Ma200Day)
        {
            parts.Add($"price below 200-day MA (${p.Ma200Day:N0}), bear boost active");
        }

        if (parts.Count > 0)
        {
            var explanation = string.Join(" and ", parts);
            return $"Buying {p.Multiplier.Value:F1}x: {explanation}";
        }

        // Fallback if multiplier > 1 but no clear reason
        return $"Buying {p.Multiplier.Value:F1}x (tier: {p.MultiplierTier ?? "unknown"})";
    }
}

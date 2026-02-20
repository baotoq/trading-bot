using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Handlers;

public class PortfolioPurchaseCompletedEventHandler(
    TradingBotDbContext db,
    ILogger<PortfolioPurchaseCompletedEventHandler> logger) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var asset = await db.PortfolioAssets
                .AsTracking()
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.Ticker == "BTC" && a.AssetType == AssetType.Crypto, cancellationToken);

            if (asset is null)
            {
                logger.LogWarning(
                    "No BTC PortfolioAsset found — skipping auto-import for Purchase {PurchaseId}. User must create BTC asset first.",
                    notification.PurchaseId);
                return;
            }

            if (asset.Transactions.Any(t => t.SourcePurchaseId == notification.PurchaseId))
            {
                logger.LogDebug("Purchase {PurchaseId} already imported into portfolio asset {AssetId}",
                    notification.PurchaseId, asset.Id);
                return;
            }

            var purchase = await db.Purchases
                .FirstOrDefaultAsync(p => p.Id == notification.PurchaseId, cancellationToken);

            if (purchase is null)
            {
                logger.LogWarning("Purchase {PurchaseId} not found when handling portfolio auto-import",
                    notification.PurchaseId);
                return;
            }

            asset.AddTransaction(
                date: DateOnly.FromDateTime(purchase.ExecutedAt.DateTime),
                quantity: purchase.Quantity.Value,
                pricePerUnit: purchase.Price.Value,
                currency: Currency.USD,
                type: TransactionType.Buy,
                fee: null,
                source: TransactionSource.Bot,
                sourcePurchaseId: purchase.Id);

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Auto-imported Purchase {PurchaseId} into portfolio asset {AssetId}",
                notification.PurchaseId, asset.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error auto-importing Purchase {PurchaseId} into portfolio",
                notification.PurchaseId);
        }
    }
}

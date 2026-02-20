using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Services;

public class HistoricalPurchaseMigrator(
    TradingBotDbContext db,
    ILogger<HistoricalPurchaseMigrator> logger)
{
    public async Task<int> MigrateAsync(PortfolioAssetId btcAssetId, CancellationToken ct)
    {
        var asset = await db.PortfolioAssets
            .AsTracking()
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == btcAssetId, ct)
            ?? throw new InvalidOperationException($"PortfolioAsset {btcAssetId} not found");

        var importedPurchaseIds = asset.Transactions
            .Where(t => t.SourcePurchaseId != null)
            .Select(t => t.SourcePurchaseId!.Value)
            .ToHashSet();

        var purchases = await db.Purchases
            .Where(p => p.Status == PurchaseStatus.Filled)
            .OrderBy(p => p.ExecutedAt)
            .ToListAsync(ct);

        var newPurchases = purchases
            .Where(p => !importedPurchaseIds.Contains(p.Id))
            .ToList();

        foreach (var purchase in newPurchases)
        {
            asset.AddTransaction(
                date: DateOnly.FromDateTime(purchase.ExecutedAt.DateTime),
                quantity: purchase.Quantity.Value,
                pricePerUnit: purchase.Price.Value,
                currency: Currency.USD,
                type: TransactionType.Buy,
                fee: null,
                source: TransactionSource.Bot,
                sourcePurchaseId: purchase.Id);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Migrated {Count} historical purchases into portfolio asset {AssetId}",
            newPurchases.Count, btcAssetId);

        return newPurchases.Count;
    }
}

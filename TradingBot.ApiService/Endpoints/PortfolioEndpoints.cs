using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.PriceFeeds;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;
using TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Endpoints;

public static class PortfolioEndpoints
{
    private static readonly Dictionary<string, string> CoinGeckoIds = new()
    {
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum"
    };

    public static WebApplication MapPortfolioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portfolio")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/assets", GetAssetsAsync);
        group.MapPost("/assets", CreateAssetAsync);
        group.MapGet("/assets/{id}/transactions", GetTransactionsAsync);
        group.MapPost("/assets/{id}/transactions", CreateTransactionAsync);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        TradingBotDbContext db,
        ICryptoPriceProvider cryptoPriceProvider,
        IEtfPriceProvider etfPriceProvider,
        IExchangeRateProvider exchangeRateProvider,
        HistoricalPurchaseMigrator migrator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var assets = await db.PortfolioAssets
            .Include(a => a.Transactions)
            .ToListAsync(ct);

        // Trigger historical migration if BTC asset exists but has no bot-imported transactions
        var btcAsset = assets.FirstOrDefault(a => a.Ticker == "BTC" && a.AssetType == AssetType.Crypto);
        if (btcAsset != null && !btcAsset.Transactions.Any(t => t.Source == TransactionSource.Bot))
        {
            try
            {
                var migratedCount = await migrator.MigrateAsync(btcAsset.Id, ct);
                if (migratedCount > 0)
                {
                    // Reload to get migrated transactions
                    assets = await db.PortfolioAssets.Include(a => a.Transactions).ToListAsync(ct);
                    btcAsset = assets.First(a => a.Ticker == "BTC" && a.AssetType == AssetType.Crypto);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during historical purchase migration for BTC asset {AssetId}", btcAsset.Id);
            }
        }

        // Fetch exchange rate
        PriceFeedResult? exchangeRate = null;
        try
        {
            exchangeRate = await exchangeRateProvider.GetUsdToVndRateAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch USD/VND exchange rate for portfolio summary");
        }

        var rate = exchangeRate?.Price ?? 0m;

        // Load active fixed deposits
        var fixedDeposits = await db.FixedDeposits
            .Where(fd => fd.Status == FixedDepositStatus.Active)
            .ToListAsync(ct);

        var totalValueUsd = 0m;
        var totalValueVnd = 0m;
        var totalCostUsd = 0m;
        var totalCostVnd = 0m;
        var allocationsByType = new Dictionary<string, decimal>();
        var allocationsByTypeVnd = new Dictionary<string, decimal>();

        // Compute per-asset values
        foreach (var asset in assets)
        {
            var (currentPrice, _) = await GetCurrentPriceAsync(asset, cryptoPriceProvider, etfPriceProvider, logger, ct);
            var netQuantity = ComputeNetQuantity(asset);
            var totalCost = ComputeTotalCost(asset);
            var currentValue = netQuantity * currentPrice;

            decimal assetValueUsd;
            decimal assetValueVnd;
            decimal assetCostUsd;
            decimal assetCostVnd;

            if (asset.NativeCurrency == Currency.USD)
            {
                assetValueUsd = currentValue;
                assetValueVnd = rate > 0 ? currentValue * rate : 0m;
                assetCostUsd = totalCost;
                assetCostVnd = rate > 0 ? totalCost * rate : 0m;
            }
            else // VND
            {
                assetValueVnd = currentValue;
                assetValueUsd = rate > 0 ? currentValue / rate : 0m;
                assetCostVnd = totalCost;
                assetCostUsd = rate > 0 ? totalCost / rate : 0m;
            }

            totalValueUsd += assetValueUsd;
            totalValueVnd += assetValueVnd;
            totalCostUsd += assetCostUsd;
            totalCostVnd += assetCostVnd;

            var typeKey = asset.AssetType.ToString();
            allocationsByType[typeKey] = allocationsByType.GetValueOrDefault(typeKey) + assetValueUsd;
            allocationsByTypeVnd[typeKey] = allocationsByTypeVnd.GetValueOrDefault(typeKey) + assetValueVnd;
        }

        // Add fixed deposits (always VND)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fdTotalVnd = 0m;
        foreach (var fd in fixedDeposits)
        {
            var accruedValue = InterestCalculator.CalculateAccruedValue(
                fd.Principal.Value, fd.AnnualInterestRate, fd.StartDate, today, fd.CompoundingFrequency);
            fdTotalVnd += accruedValue;
        }

        if (fdTotalVnd > 0)
        {
            totalValueVnd += fdTotalVnd;
            var fdValueUsd = rate > 0 ? fdTotalVnd / rate : 0m;
            totalValueUsd += fdValueUsd;
            allocationsByType["FixedDeposit"] = allocationsByType.GetValueOrDefault("FixedDeposit") + fdValueUsd;
            allocationsByTypeVnd["FixedDeposit"] = allocationsByTypeVnd.GetValueOrDefault("FixedDeposit") + fdTotalVnd;
        }

        // Compute allocations
        var allocations = allocationsByType
            .Select(kvp => new AllocationDto(
                kvp.Key,
                Math.Round(kvp.Value, 2),
                Math.Round(allocationsByTypeVnd.GetValueOrDefault(kvp.Key), 0),
                totalValueUsd > 0 ? Math.Round(kvp.Value / totalValueUsd * 100, 2) : 0))
            .OrderByDescending(a => a.Percentage)
            .ToList();

        var unrealizedPnlUsd = totalValueUsd - totalCostUsd;
        var unrealizedPnlVnd = totalValueVnd - totalCostVnd;
        var unrealizedPnlPercent = totalCostUsd > 0
            ? Math.Round(unrealizedPnlUsd / totalCostUsd * 100, 2)
            : (decimal?)null;

        return Results.Ok(new PortfolioSummaryResponse(
            TotalValueUsd: Math.Round(totalValueUsd, 2),
            TotalValueVnd: Math.Round(totalValueVnd, 0),
            TotalCostUsd: Math.Round(totalCostUsd, 2),
            TotalCostVnd: Math.Round(totalCostVnd, 0),
            UnrealizedPnlUsd: Math.Round(unrealizedPnlUsd, 2),
            UnrealizedPnlVnd: Math.Round(unrealizedPnlVnd, 0),
            UnrealizedPnlPercent: unrealizedPnlPercent,
            Allocations: allocations,
            ExchangeRateUpdatedAt: exchangeRate?.FetchedAt));
    }

    private static async Task<IResult> GetAssetsAsync(
        TradingBotDbContext db,
        ICryptoPriceProvider cryptoPriceProvider,
        IEtfPriceProvider etfPriceProvider,
        IExchangeRateProvider exchangeRateProvider,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var assets = await db.PortfolioAssets
            .Include(a => a.Transactions)
            .ToListAsync(ct);

        PriceFeedResult? exchangeRate = null;
        try
        {
            exchangeRate = await exchangeRateProvider.GetUsdToVndRateAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch USD/VND exchange rate for asset breakdown");
        }

        var rate = exchangeRate?.Price ?? 0m;
        var result = new List<PortfolioAssetResponse>();

        foreach (var asset in assets)
        {
            var (currentPrice, priceFeed) = await GetCurrentPriceAsync(asset, cryptoPriceProvider, etfPriceProvider, logger, ct);
            var netQuantity = ComputeNetQuantity(asset);
            var avgCost = ComputeWeightedAverageCost(asset);
            var totalCost = ComputeTotalCost(asset);
            var currentValue = netQuantity * currentPrice;

            decimal valueUsd;
            decimal valueVnd;
            decimal pnlUsd;

            if (asset.NativeCurrency == Currency.USD)
            {
                valueUsd = currentValue;
                valueVnd = rate > 0 ? currentValue * rate : 0m;
                pnlUsd = currentValue - totalCost;
            }
            else // VND
            {
                valueVnd = currentValue;
                valueUsd = rate > 0 ? currentValue / rate : 0m;
                pnlUsd = rate > 0 ? (currentValue - totalCost) / rate : 0m;
            }

            var pnlPercent = totalCost > 0
                ? Math.Round((currentValue - totalCost) / totalCost * 100, 2)
                : (decimal?)null;

            result.Add(new PortfolioAssetResponse(
                Id: asset.Id.Value,
                Name: asset.Name,
                Ticker: asset.Ticker,
                AssetType: asset.AssetType.ToString(),
                NativeCurrency: asset.NativeCurrency.ToString(),
                Quantity: Math.Round(netQuantity, 8),
                AverageCost: Math.Round(avgCost, 8),
                CurrentPrice: Math.Round(currentPrice, 8),
                CurrentValueUsd: Math.Round(valueUsd, 2),
                CurrentValueVnd: Math.Round(valueVnd, 0),
                UnrealizedPnlUsd: Math.Round(pnlUsd, 2),
                UnrealizedPnlPercent: pnlPercent,
                PriceUpdatedAt: priceFeed?.FetchedAt,
                IsPriceStale: priceFeed?.IsStale ?? true));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTransactionsAsync(
        TradingBotDbContext db,
        Guid id,
        string? type,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken ct)
    {
        var assetId = PortfolioAssetId.From(id);
        var assetExists = await db.PortfolioAssets.AnyAsync(a => a.Id == assetId, ct);
        if (!assetExists)
            return Results.NotFound();

        var query = db.AssetTransactions.Where(t => t.PortfolioAssetId == assetId);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, ignoreCase: true, out var txType))
            query = query.Where(t => t.Type == txType);

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        var transactions = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TransactionResponse(
                t.Id.Value,
                t.Date,
                t.Quantity,
                t.PricePerUnit,
                t.Currency.ToString(),
                t.Type.ToString(),
                t.Fee,
                t.Source.ToString()))
            .ToListAsync(ct);

        return Results.Ok(transactions);
    }

    private static async Task<IResult> CreateTransactionAsync(
        TradingBotDbContext db,
        Guid id,
        CreateTransactionRequest request,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!Enum.TryParse<Currency>(request.Currency, ignoreCase: true, out var currency))
            return Results.BadRequest("Invalid currency");

        if (!Enum.TryParse<TransactionType>(request.Type, ignoreCase: true, out var type))
            return Results.BadRequest("Invalid transaction type");

        if (request.Date > DateOnly.FromDateTime(DateTime.UtcNow))
            return Results.BadRequest("Transaction date cannot be in the future");

        var asset = await db.PortfolioAssets
            .AsTracking()
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == PortfolioAssetId.From(id), ct);

        if (asset is null)
            return Results.NotFound();

        if (asset.AssetType == AssetType.ETF && request.Quantity != Math.Floor(request.Quantity))
            return Results.BadRequest("ETF quantities must be whole numbers");

        try
        {
            var tx = asset.AddTransaction(
                request.Date,
                request.Quantity,
                request.PricePerUnit,
                currency,
                type,
                request.Fee,
                TransactionSource.Manual);

            await db.SaveChangesAsync(ct);

            var response = new TransactionResponse(
                Id: tx.Id.Value,
                Date: tx.Date,
                Quantity: tx.Quantity,
                PricePerUnit: tx.PricePerUnit,
                Currency: tx.Currency.ToString(),
                Type: tx.Type.ToString(),
                Fee: tx.Fee,
                Source: tx.Source.ToString());

            return Results.Created($"/api/portfolio/assets/{id}/transactions/{tx.Id.Value}", response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> CreateAssetAsync(
        TradingBotDbContext db,
        CreateAssetRequest request,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Name is required");

        if (string.IsNullOrWhiteSpace(request.Ticker))
            return Results.BadRequest("Ticker is required");

        if (!Enum.TryParse<AssetType>(request.AssetType, ignoreCase: true, out var assetType))
            return Results.BadRequest("Invalid asset type. Valid values: Crypto, ETF");

        if (!Enum.TryParse<Currency>(request.NativeCurrency, ignoreCase: true, out var currency))
            return Results.BadRequest("Invalid currency. Valid values: USD, VND");

        var normalizedTicker = request.Ticker.Trim().ToUpper();
        var exists = await db.PortfolioAssets.AnyAsync(a => a.Ticker == normalizedTicker, ct);
        if (exists)
            return Results.Conflict($"Asset with ticker '{normalizedTicker}' already exists");

        var asset = PortfolioAsset.Create(
            request.Name.Trim(),
            normalizedTicker,
            assetType,
            currency);

        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created portfolio asset {Ticker} ({AssetType})", asset.Ticker, asset.AssetType);

        return Results.Created(
            $"/api/portfolio/assets/{asset.Id.Value}",
            new CreateAssetResponse(asset.Id.Value, asset.Name, asset.Ticker, asset.AssetType.ToString(), asset.NativeCurrency.ToString()));
    }

    private static async Task<(decimal Price, PriceFeedResult? Feed)> GetCurrentPriceAsync(
        PortfolioAsset asset,
        ICryptoPriceProvider cryptoPriceProvider,
        IEtfPriceProvider etfPriceProvider,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            if (asset.AssetType == AssetType.Crypto)
            {
                if (CoinGeckoIds.TryGetValue(asset.Ticker, out var coinGeckoId))
                {
                    var result = await cryptoPriceProvider.GetPriceAsync(coinGeckoId, ct);
                    return (result.Price, result);
                }

                logger.LogWarning("No CoinGecko ID mapping for crypto ticker {Ticker}", asset.Ticker);
                return (0m, null);
            }

            if (asset.AssetType == AssetType.ETF)
            {
                var result = await etfPriceProvider.GetPriceAsync(asset.Ticker, ct);
                return (result.Price, result);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch current price for {AssetType} {Ticker}",
                asset.AssetType, asset.Ticker);
        }

        return (0m, null);
    }

    private static decimal ComputeNetQuantity(PortfolioAsset asset)
    {
        var bought = asset.Transactions
            .Where(t => t.Type == TransactionType.Buy)
            .Sum(t => t.Quantity);
        var sold = asset.Transactions
            .Where(t => t.Type == TransactionType.Sell)
            .Sum(t => t.Quantity);
        return bought - sold;
    }

    private static decimal ComputeWeightedAverageCost(PortfolioAsset asset)
    {
        var buyTransactions = asset.Transactions
            .Where(t => t.Type == TransactionType.Buy)
            .ToList();

        var totalQuantity = buyTransactions.Sum(t => t.Quantity);
        if (totalQuantity == 0) return 0m;

        var totalCost = buyTransactions.Sum(t => t.PricePerUnit * t.Quantity);
        return totalCost / totalQuantity;
    }

    private static decimal ComputeTotalCost(PortfolioAsset asset)
    {
        var bought = asset.Transactions
            .Where(t => t.Type == TransactionType.Buy)
            .Sum(t => t.PricePerUnit * t.Quantity);
        var sold = asset.Transactions
            .Where(t => t.Type == TransactionType.Sell)
            .Sum(t => t.PricePerUnit * t.Quantity);
        return bought - sold;
    }
}

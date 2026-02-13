using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.BuildingBlocks.DistributedLocks;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Infrastructure.Hyperliquid.Models;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Core DCA execution service that orchestrates the complete buy flow:
/// distributed lock, idempotency check, balance verification, order placement,
/// persistence, and domain event publishing.
/// </summary>
public class DcaExecutionService(
    HyperliquidClient hyperliquidClient,
    TradingBotDbContext dbContext,
    IDistributedLock distributedLock,
    IPublisher publisher,
    IOptionsMonitor<DcaOptions> dcaOptions,
    IPriceDataService priceDataService,
    ILogger<DcaExecutionService> logger) : IDcaExecutionService
{
    private const decimal MinimumBalance = 1.0m;
    private const decimal MinimumOrderValue = 10.0m;
    private const int BtcDecimals = 5; // Standard BTC precision for size rounding

    public async Task ExecuteDailyPurchaseAsync(DateOnly purchaseDate, CancellationToken ct = default)
    {
        var options = dcaOptions.CurrentValue;

        if (options.DryRun)
            logger.LogInformation("[DRY RUN] Executing simulated purchase for {Date}", purchaseDate);

        // Step 1: Acquire distributed lock (prevents concurrent executions for same day)
        var lockKey = $"dca-purchase-{purchaseDate:yyyy-MM-dd}";
        await using var lockResponse = await distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(5), ct);

        if (!lockResponse.Success)
        {
            logger.LogWarning("Could not acquire lock for {Date}, another instance may be running", purchaseDate);
            return;
        }

        logger.LogInformation("Acquired lock for {Date}, starting purchase execution", purchaseDate);

        // Step 2: Idempotency check — skip entirely in dry-run mode
        var todayStart = purchaseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayEnd = purchaseDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        if (!options.DryRun)
        {
            var existingPurchase = await dbContext.Purchases
                .Where(p => p.ExecutedAt >= todayStart && p.ExecutedAt < todayEnd)
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .FirstOrDefaultAsync(ct);

            if (existingPurchase != null)
            {
                logger.LogInformation("Purchase already completed today {Date}: {PurchaseId}", purchaseDate, existingPurchase.Id);
                await publisher.Publish(new PurchaseSkippedEvent(
                    "Already purchased today",
                    null,
                    null,
                    DateTimeOffset.UtcNow), ct);
                return;
            }
        }
        else
        {
            logger.LogInformation("[DRY RUN] Bypassing idempotency check for {Date}", purchaseDate);
        }

        // Step 3: Check USDC balance
        var usdcBalance = await hyperliquidClient.GetBalancesAsync(ct);

        logger.LogInformation("USDC balance: {Balance}, target amount: {Target}", usdcBalance, options.BaseDailyAmount);

        if (usdcBalance < MinimumBalance)
        {
            logger.LogWarning("Balance {Balance} below minimum {Min}, skipping", usdcBalance, MinimumBalance);
            await publisher.Publish(new PurchaseSkippedEvent(
                "Insufficient balance",
                usdcBalance,
                options.BaseDailyAmount,
                DateTimeOffset.UtcNow), ct);
            return;
        }

        // Step 4: Get price and calculate smart multiplier
        var currentPrice = await hyperliquidClient.GetSpotPriceAsync("BTC/USDC", ct);
        var multiplierResult = await CalculateMultiplierAsync(currentPrice, options, ct);

        // Apply multiplier to base amount (capped by available balance)
        var multipliedAmount = options.BaseDailyAmount * multiplierResult.TotalMultiplier;
        var usdAmount = Math.Min(usdcBalance, multipliedAmount);

        logger.LogInformation("Multiplied target: {MultipliedAmount} USD (base: {Base} * {Multiplier}x), available: {Balance}, will buy: {UsdAmount}",
            multipliedAmount, options.BaseDailyAmount, multiplierResult.TotalMultiplier, usdcBalance, usdAmount);

        if (usdAmount < MinimumOrderValue)
        {
            logger.LogWarning("Amount {Amount} below minimum order {Min}", usdAmount, MinimumOrderValue);
            await publisher.Publish(new PurchaseSkippedEvent(
                $"Amount below minimum order value (${MinimumOrderValue})",
                usdcBalance,
                MinimumOrderValue,
                DateTimeOffset.UtcNow), ct);
            return;
        }

        // Step 5: Get metadata and calculate order size
        var meta = await hyperliquidClient.GetSpotMetadataAsync(ct);
        var btcAssetIndex = meta.Universe.FindIndex(u => u.Name == "BTC/USDC");

        if (btcAssetIndex == -1)
        {
            throw new InvalidOperationException("BTC/USDC not found in Hyperliquid spot metadata");
        }

        // Calculate BTC quantity and round DOWN to avoid exceeding balance
        var btcQuantity = usdAmount / currentPrice;
        var roundedQuantity = Math.Round(btcQuantity, BtcDecimals, MidpointRounding.ToZero);

        logger.LogInformation("Placing order: {UsdAmount} USD -> {BtcQty} BTC at {Price}",
            usdAmount, roundedQuantity, currentPrice);

        // Step 6: Create purchase record and place order
        var purchase = new Purchase
        {
            ExecutedAt = DateTimeOffset.UtcNow,
            Price = currentPrice,
            Quantity = 0, // Will be updated with actual fill
            Cost = 0, // Will be updated with actual cost
            Multiplier = multiplierResult.TotalMultiplier,
            MultiplierTier = multiplierResult.Tier,
            DropPercentage = multiplierResult.DropPercentage,
            High30Day = multiplierResult.High30Day,
            Ma200Day = multiplierResult.Ma200Day,
            IsDryRun = options.DryRun,
            Status = PurchaseStatus.Pending
        };

        try
        {
            if (options.DryRun)
            {
                logger.LogInformation("[DRY RUN] Simulating order: {Quantity} BTC at {Price}", roundedQuantity, currentPrice);
                purchase.Quantity = roundedQuantity;
                purchase.Price = currentPrice;
                purchase.Cost = roundedQuantity * currentPrice;
                purchase.Status = PurchaseStatus.Filled;
                purchase.OrderId = $"DRY-RUN-{Guid.NewGuid():N}";
                purchase.IsDryRun = true;
            }
            else
            {
                // Use 5% slippage tolerance for IOC (immediate-or-cancel) order
                var limitPrice = currentPrice * 1.05m;
                var orderResponse = await hyperliquidClient.PlaceSpotOrderAsync(
                    btcAssetIndex,
                    true, // isBuy
                    roundedQuantity,
                    limitPrice,
                    ct);

            // Parse fill info from response
            var status = orderResponse.Response?.Data?.Statuses.FirstOrDefault();
            var filled = status?.Filled;

            if (filled != null)
            {
                // Order filled (fully or partially)
                var filledQty = decimal.Parse(filled.TotalSz, CultureInfo.InvariantCulture);
                var avgPrice = decimal.Parse(filled.AvgPx, CultureInfo.InvariantCulture);

                purchase.Quantity = filledQty;
                purchase.Price = avgPrice;
                purchase.Cost = filledQty * avgPrice;
                purchase.OrderId = filled.Oid.ToString();

                // Consider filled if >= 95% of requested quantity
                purchase.Status = filledQty >= roundedQuantity * 0.95m
                    ? PurchaseStatus.Filled
                    : PurchaseStatus.PartiallyFilled;

                logger.LogInformation("Order filled: {Quantity} BTC at {Price}, status: {Status}",
                    filledQty, avgPrice, purchase.Status);
            }
            else if (status?.Resting != null)
            {
                // Order is resting (not filled) — unusual for IOC, treat as partial
                purchase.OrderId = status.Resting.Oid.ToString();
                purchase.Status = PurchaseStatus.PartiallyFilled;
                purchase.FailureReason = "Order resting instead of filling (IOC should not rest)";

                logger.LogWarning("Order resting unexpectedly: {OrderId}", purchase.OrderId);
            }
            else
            {
                // No fill or resting status
                purchase.Status = PurchaseStatus.Failed;
                purchase.FailureReason = "No fill or resting status in order response";

                logger.LogError("Order response missing fill/resting status");
            }

                purchase.RawResponse = JsonSerializer.Serialize(orderResponse);
            }
        }
        catch (HyperliquidApiException ex)
        {
            purchase.Status = PurchaseStatus.Failed;
            purchase.FailureReason = ex.Message;
            purchase.RawResponse = ex.Message;

            logger.LogError(ex, "Order placement failed: {Error}", ex.Message);
        }

        // Step 7: Persist purchase record
        dbContext.Purchases.Add(purchase);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Purchase persisted: {PurchaseId} with status {Status}", purchase.Id, purchase.Status);

        // Step 8: Publish domain event AFTER database commit
        if (purchase.Status == PurchaseStatus.Filled || purchase.Status == PurchaseStatus.PartiallyFilled)
        {
            // Get updated balance for notification
            var remainingUsdc = await hyperliquidClient.GetBalancesAsync(ct);

            // TODO: Phase 4 will track actual BTC balance, for now use filled quantity
            var currentBtcBalance = purchase.Quantity;

            await publisher.Publish(new PurchaseCompletedEvent(
                purchase.Id,
                purchase.Quantity,
                purchase.Price,
                purchase.Cost,
                remainingUsdc,
                currentBtcBalance,
                purchase.ExecutedAt,
                purchase.Multiplier,
                purchase.MultiplierTier,
                purchase.DropPercentage,
                purchase.High30Day,
                purchase.Ma200Day,
                purchase.IsDryRun), ct);

            logger.LogInformation("Published PurchaseCompletedEvent for {PurchaseId}", purchase.Id);
        }
        else
        {
            await publisher.Publish(new PurchaseFailedEvent(
                "OrderFailed",
                purchase.FailureReason ?? "Unknown error",
                0, // RetryCount — retries handled at scheduler level
                DateTimeOffset.UtcNow), ct);

            logger.LogWarning("Published PurchaseFailedEvent: {Reason}", purchase.FailureReason);
        }
    }

    private async Task<OldMultiplierResult> CalculateMultiplierAsync(
        decimal currentPrice, DcaOptions options, CancellationToken ct)
    {
        try
        {
            // Get price data for multiplier calculation
            var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
            var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

            // Calculate dip tier multiplier
            decimal dipMultiplier = 1.0m;
            decimal dropPercent = 0m;
            string tier = "None";

            if (high30Day > 0)
            {
                // Calculate drop percentage from 30-day high
                dropPercent = (high30Day - currentPrice) / high30Day * 100m;

                // Find matching tier (ordered descending, first match wins)
                var matchedTier = options.MultiplierTiers
                    .OrderByDescending(t => t.DropPercentage)
                    .FirstOrDefault(t => dropPercent >= t.DropPercentage);

                if (matchedTier != null)
                {
                    dipMultiplier = matchedTier.Multiplier;
                    tier = $">= {matchedTier.DropPercentage}%";
                }
            }
            else
            {
                logger.LogWarning("30-day high unavailable (returned 0), using 1.0x dip multiplier");
                tier = "N/A (no price data)";
            }

            // Calculate bear market boost
            decimal bearMultiplier = 1.0m;

            if (ma200Day > 0)
            {
                if (currentPrice < ma200Day)
                {
                    bearMultiplier = options.BearBoostFactor;
                    logger.LogInformation("Bear market detected: price {Price} < MA200 {Ma200}, applying {Boost}x boost",
                        currentPrice, ma200Day, bearMultiplier);
                }
            }
            else
            {
                logger.LogWarning("200-day SMA unavailable (returned 0), skipping bear boost");
            }

            // Stack multiplicatively and apply cap
            var totalMultiplier = dipMultiplier * bearMultiplier;
            totalMultiplier = Math.Min(totalMultiplier, options.MaxMultiplierCap);

            logger.LogInformation(
                "Multiplier: dip={DipMult}x (tier: {Tier}, drop: {Drop:F2}%) * bear={BearMult}x = {Total:F2}x (cap: {Cap}x)",
                dipMultiplier, tier, dropPercent, bearMultiplier, totalMultiplier, options.MaxMultiplierCap);

            return new OldMultiplierResult(
                TotalMultiplier: totalMultiplier,
                DipMultiplier: dipMultiplier,
                BearMultiplier: bearMultiplier,
                Tier: tier,
                DropPercentage: dropPercent,
                High30Day: high30Day,
                Ma200Day: ma200Day);
        }
        catch (Exception ex)
        {
            // Graceful degradation: fall back to 1.0x on any calculation failure
            logger.LogError(ex, "Multiplier calculation failed, falling back to 1.0x");

            return new OldMultiplierResult(
                TotalMultiplier: 1.0m,
                DipMultiplier: 1.0m,
                BearMultiplier: 1.0m,
                Tier: "Error (fallback)",
                DropPercentage: 0m,
                High30Day: 0m,
                Ma200Day: 0m);
        }
    }
}

/// <summary>
/// Result of multiplier calculation containing all components and metadata
/// (OLD - will be removed in refactor phase)
/// </summary>
internal record OldMultiplierResult(
    decimal TotalMultiplier,
    decimal DipMultiplier,
    decimal BearMultiplier,
    string Tier,
    decimal DropPercentage,
    decimal High30Day,
    decimal Ma200Day);

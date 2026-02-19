using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.BuildingBlocks.DistributedLocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Infrastructure.Hyperliquid.Models;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Values;

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
    IDomainEventPublisher domainEventPublisher,
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
                await domainEventPublisher.PublishDirectAsync(new PurchaseSkippedEvent(
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
            await domainEventPublisher.PublishDirectAsync(new PurchaseSkippedEvent(
                "Insufficient balance",
                usdcBalance,
                options.BaseDailyAmount.Value,
                DateTimeOffset.UtcNow), ct);
            return;
        }

        // Step 4: Get price and calculate smart multiplier
        var currentPriceDecimal = await hyperliquidClient.GetSpotPriceAsync("BTC/USDC", ct);
        var currentPrice = Price.From(currentPriceDecimal);
        var multiplierResult = await CalculateMultiplierAsync(currentPrice, options, ct);

        // Apply multiplier to base amount (capped by available balance)
        var multipliedAmount = options.BaseDailyAmount * multiplierResult.Multiplier;
        var usdAmount = UsdAmount.From(Math.Min(usdcBalance, multipliedAmount.Value));

        logger.LogInformation("Multiplied target: {MultipliedAmount} USD (base: {Base} * {Multiplier}x), available: {Balance}, will buy: {UsdAmount}",
            multipliedAmount, options.BaseDailyAmount, multiplierResult.Multiplier, usdcBalance, usdAmount);

        if (usdAmount.Value < MinimumOrderValue)
        {
            logger.LogWarning("Amount {Amount} below minimum order {Min}", usdAmount, MinimumOrderValue);
            await domainEventPublisher.PublishDirectAsync(new PurchaseSkippedEvent(
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
        var btcQuantity = usdAmount.Value / currentPriceDecimal;
        var roundedQuantity = Math.Round(btcQuantity, BtcDecimals, MidpointRounding.ToZero);

        logger.LogInformation("Placing order: {UsdAmount} USD -> {BtcQty} BTC at {Price}",
            usdAmount, roundedQuantity, currentPriceDecimal);

        // Step 6: Create purchase aggregate via factory method
        var purchase = Purchase.Create(
            price: currentPrice,
            cost: UsdAmount.From(usdAmount.Value),
            multiplier: multiplierResult.Multiplier,
            multiplierTier: multiplierResult.Tier,
            dropPercentage: multiplierResult.DropPercentage,
            high30Day: multiplierResult.High30Day,
            ma200Day: multiplierResult.Ma200Day,
            isDryRun: options.DryRun);

        try
        {
            if (options.DryRun)
            {
                logger.LogInformation("[DRY RUN] Simulating order: {Quantity} BTC at {Price}", roundedQuantity, currentPriceDecimal);
                purchase.RecordDryRunFill(
                    Quantity.From(roundedQuantity),
                    currentPrice,
                    UsdAmount.From(roundedQuantity * currentPriceDecimal));
            }
            else
            {
                // Use 5% slippage tolerance for IOC (immediate-or-cancel) order
                var limitPrice = currentPriceDecimal * 1.05m;
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

                    purchase.RecordFill(
                        Quantity.From(filledQty),
                        Price.From(avgPrice),
                        UsdAmount.From(filledQty * avgPrice),
                        filled.Oid.ToString(),
                        roundedQuantity);

                    logger.LogInformation("Order filled: {Quantity} BTC at {Price}, status: {Status}",
                        filledQty, avgPrice, purchase.Status);
                }
                else if (status?.Resting != null)
                {
                    // Order is resting (not filled) — unusual for IOC, treat as partial
                    purchase.RecordResting(status.Resting.Oid.ToString());

                    logger.LogWarning("Order resting unexpectedly: {OrderId}", purchase.OrderId);
                }
                else
                {
                    // No fill or resting status
                    purchase.RecordFailure("No fill or resting status in order response");

                    logger.LogError("Order response missing fill/resting status");
                }

                purchase.SetRawResponse(JsonSerializer.Serialize(orderResponse));
            }
        }
        catch (HyperliquidApiException ex)
        {
            purchase.RecordFailure(ex.Message, ex.Message);

            logger.LogError(ex, "Order placement failed: {Error}", ex.Message);
        }

        // Step 7: Persist purchase record (interceptor auto-dispatches domain events to outbox)
        dbContext.Purchases.Add(purchase);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Purchase persisted: {PurchaseId} with status {Status}", purchase.Id, purchase.Status);
    }

    private async Task<MultiplierResult> CalculateMultiplierAsync(
        Price currentPrice, DcaOptions options, CancellationToken ct)
    {
        try
        {
            // Get price data for multiplier calculation
            var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
            var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

            // Delegate to pure static calculator
            var result = MultiplierCalculator.Calculate(
                currentPrice,
                options.BaseDailyAmount,
                high30Day,
                ma200Day,
                options.MultiplierTiers,
                options.BearBoostFactor,
                options.MaxMultiplierCap);

            // Log the result
            if (result.IsBearMarket)
            {
                logger.LogInformation(
                    "Bear market detected: price {Price} < MA200 {Ma200}, applying +{Boost} boost",
                    currentPrice, ma200Day, result.BearBoostApplied);
            }

            logger.LogInformation(
                "Multiplier: tier={Tier} (drop: {Drop:F4}) + bear={BearBoost} = {Total:F2}x (cap: {Cap}x)",
                result.Tier, result.DropPercentage, result.BearBoostApplied, result.Multiplier, options.MaxMultiplierCap);

            return result;
        }
        catch (Exception ex)
        {
            // Graceful degradation: fall back to 1.0x on any calculation failure
            logger.LogError(ex, "Multiplier calculation failed, falling back to 1.0x");

            // Construct fallback result directly to avoid 0-sentinel path
            return new MultiplierResult(
                Multiplier: Multiplier.From(1.0m),
                Tier: "Base",
                IsBearMarket: false,
                BearBoostApplied: 0m,
                DropPercentage: Percentage.From(0m),
                High30Day: 0m,
                Ma200Day: 0m,
                FinalAmount: options.BaseDailyAmount);
        }
    }
}

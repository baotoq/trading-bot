# Domain Pitfalls: Hyperliquid BTC Smart DCA Bot

**Domain:** Recurring crypto DCA bot with exchange integration
**Researched:** 2026-02-12
**Confidence:** HIGH (based on exchange integration patterns and DCA automation best practices)

## Critical Pitfalls

Mistakes that cause fund loss, duplicate orders, or system failures.

### Pitfall 1: Duplicate Order Execution

**What goes wrong:** The same buy executes multiple times on the same day, spending 2x-5x intended amount.

**Why it happens:**
- Background service restarts mid-execution (crashes, deployments)
- No idempotency key or execution tracking
- Distributed lock not actually acquired (current code has `return new();` stub)
- Multiple instances running simultaneously
- Retry logic without deduplication

**Consequences:**
- Spending multiples of daily budget
- Violating risk management rules
- Potential margin calls if using leverage
- Loss of user trust

**Prevention:**
1. **Idempotency at database level:**
   ```csharp
   // Store purchase intent BEFORE executing
   var purchaseId = Guid.NewGuid(); // or date-based: "BTC-DCA-2026-02-12"
   await dbContext.PurchaseIntents.AddAsync(new PurchaseIntent
   {
       Id = purchaseId,
       Date = DateOnly.FromDateTime(DateTime.UtcNow),
       Status = PurchaseStatus.Pending
   });
   await dbContext.SaveChangesAsync();
   ```

2. **Check execution history before EVERY attempt:**
   ```csharp
   var today = DateOnly.FromDateTime(DateTime.UtcNow);
   var alreadyExecuted = await dbContext.Purchases
       .AnyAsync(p => p.Date == today && p.Status == PurchaseStatus.Completed);
   if (alreadyExecuted) return; // CRITICAL: Exit early
   ```

3. **Fix distributed lock (currently bypassed):**
   ```csharp
   // Current code in DistributedLock.cs just returns success without locking:
   // return new(); // ⚠️ THIS IS DANGEROUS

   // Must actually use Dapr lock:
   var lockResponse = await dapr.TryLockAsync(StoreName, $"dca-buy-{today}", "api", 60, cancellationToken);
   if (!lockResponse.Success) return; // Another instance is running
   ```

4. **Use unique client order ID on exchange:**
   ```csharp
   var clientOrderId = $"DCA-{today:yyyyMMdd}-{purchaseId}";
   // Include in Hyperliquid order request
   ```

5. **Atomic status updates:**
   ```csharp
   // Update status ONLY after exchange confirms
   purchaseIntent.Status = PurchaseStatus.Completed;
   purchaseIntent.ExchangeOrderId = response.OrderId;
   await dbContext.SaveChangesAsync();
   ```

**Detection:**
- Multiple database records for same day
- Telegram receives 2+ notifications for same date
- Account balance drops faster than expected
- Exchange shows multiple orders with similar timestamps

**Phase mapping:** Phase 1 (Core DCA Engine) must implement idempotency from day one.

---

### Pitfall 2: Silent Failure Mode

**What goes wrong:** Buy fails silently for days/weeks, user doesn't know, misses major dip opportunities.

**Why it happens:**
- Exception caught and logged but not alerted
- Telegram notification fails, no fallback
- Background service swallows errors (TimeBackgroundService has try/catch)
- API key expires, insufficient balance, exchange downtime
- Rate limit errors treated as transient

**Consequences:**
- Missing accumulation during best buying opportunities
- Strategy completely ineffective
- User discovers weeks later after dip passes
- Loss of confidence in automation

**Prevention:**
1. **Multi-channel alerting:**
   ```csharp
   try
   {
       await ExecutePurchase();
       await telegram.SendSuccessNotification();
   }
   catch (Exception ex)
   {
       // Log
       logger.LogCritical(ex, "CRITICAL: DCA purchase failed");

       // Telegram alert
       await telegram.SendCriticalAlert($"🚨 DCA FAILED: {ex.Message}");

       // Email fallback (if Telegram fails)
       await email.SendAlert("DCA Bot Failure", ex.ToString());

       // Increment failure counter in database
       await IncrementFailureCounter();

       // Re-throw to ensure visibility
       throw;
   }
   ```

2. **Health check endpoint:**
   ```csharp
   app.MapGet("/health/dca", async (DbContext db) =>
   {
       var lastSuccess = await db.Purchases
           .Where(p => p.Status == PurchaseStatus.Completed)
           .OrderByDescending(p => p.Date)
           .FirstOrDefaultAsync();

       var daysSinceSuccess = (DateTime.UtcNow - lastSuccess?.CreatedAt)?.TotalDays ?? 999;

       return daysSinceSuccess > 2
           ? Results.Unhealthy("No successful purchase in 2+ days")
           : Results.Healthy();
   });
   ```

3. **Daily success verification:**
   ```csharp
   // Separate background service that runs at end of day
   protected override async Task ProcessAsync(CancellationToken ct)
   {
       var today = DateOnly.FromDateTime(DateTime.UtcNow);
       var todayPurchase = await db.Purchases.FirstOrDefaultAsync(p => p.Date == today);

       if (todayPurchase == null || todayPurchase.Status != PurchaseStatus.Completed)
       {
           await telegram.SendCriticalAlert($"⚠️ No purchase recorded for {today}. Check logs.");
       }
   }
   ```

4. **Persistent failure tracking:**
   ```csharp
   if (consecutiveFailures >= 3)
   {
       await telegram.SendCriticalAlert(
           "🚨 CRITICAL: 3+ consecutive DCA failures. Manual intervention required."
       );
       // Optional: Disable automatic retries to prevent spam
   }
   ```

**Detection:**
- Logs show errors but no Telegram alerts received
- Health check endpoint returns unhealthy
- Manual inspection shows gaps in purchase history
- Account balance unchanged for multiple days

**Phase mapping:** Phase 2 (Purchase Execution) must implement comprehensive alerting. Phase 4 (Monitoring) adds health checks.

---

### Pitfall 3: Price Data Staleness

**What goes wrong:** 200-day MA calculation uses outdated candle data, bot misidentifies market regime.

**Why it happens:**
- Candle data not refreshed before calculation
- WebSocket connection drops, falls back to stale cache
- Time zone confusion (exchange UTC vs local time)
- Partial candle update (OHLC incomplete)
- Redis cache returns old data

**Consequences:**
- Buying with wrong multiplier (1x when should be 2.5x, or vice versa)
- Missing bear market boost during crashes
- Over-allocating during bull markets
- Incorrect 30-day high tracking

**Prevention:**
1. **Always fetch fresh candles before decision:**
   ```csharp
   // DON'T rely on cache for critical decisions
   var candles = await hyperliquidClient.GetCandlesAsync(
       symbol: "BTC",
       interval: "1d",
       limit: 200, // Need 200 for 200-day MA
       nocache: true // Force fresh data
   );

   // Verify freshness
   var latestCandle = candles.OrderByDescending(c => c.CloseTime).First();
   var age = DateTime.UtcNow - latestCandle.CloseTime;

   if (age > TimeSpan.FromHours(25)) // Allow 1-day + buffer
   {
       logger.LogWarning("Candle data is stale: {Age} hours old", age.TotalHours);
       await telegram.SendAlert($"⚠️ Stale price data: {age.TotalHours:F1}h old");
       // Consider skipping purchase or using fallback
   }
   ```

2. **Explicit UTC handling:**
   ```csharp
   // Already set in Program.cs, but reinforce:
   var utcNow = DateTime.UtcNow;
   var today = DateOnly.FromDateTime(utcNow);

   // When comparing with exchange timestamps:
   var candleTime = DateTimeOffset.FromUnixTimeMilliseconds(response.Timestamp).UtcDateTime;
   ```

3. **Validate candle completeness:**
   ```csharp
   if (candles.Count < 200)
   {
       logger.LogWarning("Insufficient candle data: {Count}/200", candles.Count);
       // Fallback: Use available data but adjust confidence
       // OR skip purchase if data too sparse
   }

   // Check for gaps
   var sortedCandles = candles.OrderBy(c => c.OpenTime).ToList();
   for (int i = 1; i < sortedCandles.Count; i++)
   {
       var gap = sortedCandles[i].OpenTime - sortedCandles[i-1].CloseTime;
       if (gap > TimeSpan.FromDays(1.5))
       {
           logger.LogWarning("Gap detected in candle data: {Gap} days", gap.TotalDays);
       }
   }
   ```

4. **WebSocket failover:**
   ```csharp
   // Primary: WebSocket subscription for real-time candles
   // Fallback: REST API if WebSocket disconnected

   if (!webSocketConnected || lastCandleAge > TimeSpan.FromMinutes(5))
   {
       logger.LogWarning("WebSocket stale, falling back to REST API");
       candles = await hyperliquidClient.GetCandlesRestAsync();
   }
   ```

5. **Cache invalidation strategy:**
   ```csharp
   // Redis cache should have short TTL for candle data
   var cacheKey = "btc-candles-1d";
   await cache.SetAsync(cacheKey, candles, new DistributedCacheEntryOptions
   {
       AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) // Short TTL
   });
   ```

**Detection:**
- MA values don't match TradingView or other sources
- Buy multiplier seems incorrect for current market
- Logs show old candle timestamps
- WebSocket reconnection events in logs

**Phase mapping:** Phase 2 (Purchase Execution) must validate data freshness. Phase 3 (Smart Multipliers) relies on accurate MA.

---

### Pitfall 4: Partial Fill Mishandling

**What goes wrong:** Order only partially fills, bot records full purchase, next day duplicates the order.

**Why it happens:**
- Spot markets have lower liquidity than perps
- Large orders on Hyperliquid may not fill completely
- Bot assumes order = executed
- Status check happens before fill completes
- Retry logic doesn't account for partial fills

**Consequences:**
- Database shows full purchase but only 50% executed
- Next day's order duplicates the unfilled portion
- Accumulation tracking is incorrect
- Portfolio allocation gets skewed

**Prevention:**
1. **Poll order status until fully filled:**
   ```csharp
   var order = await hyperliquid.PlaceOrderAsync(request);

   // Don't assume immediate fill
   var maxWaitTime = TimeSpan.FromMinutes(5);
   var deadline = DateTime.UtcNow + maxWaitTime;

   while (DateTime.UtcNow < deadline)
   {
       var status = await hyperliquid.GetOrderStatusAsync(order.OrderId);

       if (status.Status == "filled")
       {
           // Record ACTUAL filled amount, not requested
           purchase.Amount = status.ExecutedQty;
           purchase.AveragePrice = status.AveragePrice;
           purchase.Status = PurchaseStatus.Completed;
           break;
       }
       else if (status.Status == "partially_filled")
       {
           logger.LogWarning("Partial fill: {Executed}/{Requested}",
               status.ExecutedQty, status.OriginalQty);
           // Continue waiting
       }
       else if (status.Status == "canceled" || status.Status == "rejected")
       {
           logger.LogError("Order {OrderId} failed: {Status}", order.OrderId, status.Status);
           purchase.Status = PurchaseStatus.Failed;
           break;
       }

       await Task.Delay(TimeSpan.FromSeconds(10), ct);
   }

   if (DateTime.UtcNow >= deadline && status.Status != "filled")
   {
       // Timeout handling
       logger.LogWarning("Order timeout. Status: {Status}, Filled: {Executed}/{Requested}",
           status.Status, status.ExecutedQty, status.OriginalQty);

       // Decision: Accept partial fill or cancel and retry?
       if (status.ExecutedQty > 0)
       {
           // Accept partial
           purchase.Amount = status.ExecutedQty;
           purchase.Status = PurchaseStatus.PartiallyCompleted;
       }
       else
       {
           // Cancel and reschedule
           await hyperliquid.CancelOrderAsync(order.OrderId);
           purchase.Status = PurchaseStatus.Failed;
       }
   }
   ```

2. **Store partial fill details:**
   ```csharp
   public class Purchase
   {
       public decimal RequestedAmount { get; set; }
       public decimal ExecutedAmount { get; set; }
       public decimal AveragePrice { get; set; }
       public PurchaseStatus Status { get; set; } // Completed, PartiallyCompleted, Failed
       public string? ExchangeOrderId { get; set; }
       public int FillPercentage => (int)((ExecutedAmount / RequestedAmount) * 100);
   }
   ```

3. **Partial fill retry strategy:**
   ```csharp
   if (purchase.Status == PurchaseStatus.PartiallyCompleted && purchase.FillPercentage < 80)
   {
       var remainingAmount = purchase.RequestedAmount - purchase.ExecutedAmount;

       logger.LogInformation("Retrying remainder: {Remaining} BTC", remainingAmount);

       // Retry with remainder (same day, different order)
       var retryOrder = await hyperliquid.PlaceOrderAsync(new OrderRequest
       {
           Symbol = "BTC",
           Amount = remainingAmount,
           ClientOrderId = $"DCA-{today:yyyyMMdd}-retry-{Guid.NewGuid()}"
       });
   }
   ```

4. **Use market orders with caution:**
   ```csharp
   // Spot markets: Consider limit orders near market price
   var currentPrice = await hyperliquid.GetCurrentPriceAsync("BTC");
   var limitPrice = currentPrice * 1.001m; // 0.1% above market (buys at or below)

   var order = new OrderRequest
   {
       Type = "limit", // Better control than market
       Price = limitPrice,
       TimeInForce = "IOC" // Immediate-or-cancel to avoid lingering
   };
   ```

**Detection:**
- Database shows full purchase but exchange shows partial fill
- Telegram notification shows different amount than database
- Portfolio value doesn't match accumulated purchases
- Exchange transaction history shows smaller amounts

**Phase mapping:** Phase 2 (Purchase Execution) must handle partial fills correctly. Critical for spot markets.

---

### Pitfall 5: Rate Limit Cascade Failure

**What goes wrong:** Bot hits Hyperliquid rate limits, gets blocked, cannot execute purchases for hours/days.

**Why it happens:**
- Polling order status too frequently (every second)
- Fetching 200-day candles on every check (daily service runs every hour)
- No backoff strategy
- Multiple services hitting same endpoints
- Rate limit headers ignored

**Consequences:**
- IP or API key temporarily banned
- Missing scheduled purchases
- Cannot check order status, unsure if filled
- Manual intervention required

**Prevention:**
1. **Cache aggressively:**
   ```csharp
   // 200-day MA only needs daily candle refresh
   var cacheKey = $"btc-candles-1d-{DateTime.UtcNow:yyyyMMdd}";
   var candles = await cache.GetOrCreateAsync(cacheKey, async entry =>
   {
       entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
       return await hyperliquid.GetCandlesAsync("BTC", "1d", 200);
   });
   ```

2. **Respect rate limit headers:**
   ```csharp
   public class HyperliquidClient
   {
       private DateTime _rateLimitResetTime = DateTime.MinValue;
       private int _remainingRequests = int.MaxValue;

       private async Task<T> ExecuteRequestAsync<T>(Func<Task<HttpResponseMessage>> request)
       {
           // Check if we're in cooldown
           if (DateTime.UtcNow < _rateLimitResetTime)
           {
               var waitTime = _rateLimitResetTime - DateTime.UtcNow;
               logger.LogWarning("Rate limit active. Waiting {WaitTime}", waitTime);
               await Task.Delay(waitTime);
           }

           var response = await request();

           // Parse rate limit headers
           if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
           {
               _remainingRequests = int.Parse(remaining.First());
           }

           if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
           {
               _rateLimitResetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset.First())).UtcDateTime;
           }

           if (response.StatusCode == (HttpStatusCode)429) // Too Many Requests
           {
               var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
               logger.LogError("Rate limited. Retry after: {RetryAfter}", retryAfter);
               await telegram.SendAlert($"⚠️ Rate limited by Hyperliquid. Retry in {retryAfter.TotalMinutes:F0}m");
               throw new RateLimitException(retryAfter);
           }

           return await response.Content.ReadFromJsonAsync<T>();
       }
   }
   ```

3. **Exponential backoff for order status:**
   ```csharp
   var attempts = 0;
   var delays = new[] { 5, 10, 20, 30, 60 }; // seconds

   while (attempts < delays.Length)
   {
       var status = await hyperliquid.GetOrderStatusAsync(orderId);
       if (status.Status == "filled") break;

       var delay = TimeSpan.FromSeconds(delays[attempts]);
       logger.LogDebug("Order not filled. Retrying in {Delay}s", delay.TotalSeconds);
       await Task.Delay(delay);
       attempts++;
   }
   ```

4. **Batch requests where possible:**
   ```csharp
   // Instead of:
   // foreach (var symbol in symbols) { await GetPrice(symbol); }

   // Use batch API if available:
   var prices = await hyperliquid.GetPricesAsync(symbols); // Single request
   ```

5. **Monitor rate limit usage:**
   ```csharp
   logger.LogInformation("Rate limit status: {Remaining} requests remaining. Resets at {ResetTime}",
       _remainingRequests, _rateLimitResetTime);

   if (_remainingRequests < 10)
   {
       logger.LogWarning("Low rate limit: {Remaining} requests left", _remainingRequests);
   }
   ```

**Detection:**
- HTTP 429 errors in logs
- Sudden stop in purchases
- "X-RateLimit-Remaining: 0" in response headers
- Long delays between log entries

**Phase mapping:** Phase 1 (Hyperliquid Integration) must implement rate limiting from start. Critical for production.

---

## Moderate Pitfalls

Mistakes that cause delays, data inconsistencies, or suboptimal performance.

### Pitfall 6: Incorrect Drop-From-High Calculation

**What goes wrong:** 30-day high tracking uses wrong timeframe or calculation, multiplier tiers are incorrect.

**Why it happens:**
- Using 30 calendar days instead of 30 trading days
- Including current incomplete candle in calculation
- Not handling exchange downtime gaps
- Time zone issues (day boundaries)

**Prevention:**
1. **Use completed candles only:**
   ```csharp
   var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
   var completedCandles = await db.Candles
       .Where(c => c.Symbol == "BTC"
                && c.Interval == "1d"
                && c.CloseTime <= DateTime.UtcNow // Only closed candles
                && c.CloseTime >= thirtyDaysAgo)
       .OrderByDescending(c => c.CloseTime)
       .Take(30) // Exactly 30 candles
       .ToListAsync();

   var thirtyDayHigh = completedCandles.Max(c => c.High);
   ```

2. **Handle gaps explicitly:**
   ```csharp
   if (completedCandles.Count < 30)
   {
       logger.LogWarning("Insufficient candles for 30-day high: {Count}/30", completedCandles.Count);
       // Fallback: Use available data or skip multiplier
   }
   ```

3. **Validate drop percentage:**
   ```csharp
   var currentPrice = await hyperliquid.GetCurrentPriceAsync("BTC");
   var dropPercent = ((thirtyDayHigh - currentPrice) / thirtyDayHigh) * 100;

   logger.LogInformation("Current: ${Current}, 30d High: ${High}, Drop: {Drop:F2}%",
       currentPrice, thirtyDayHigh, dropPercent);

   if (dropPercent < 0)
   {
       logger.LogWarning("Price above 30-day high (new high). Drop: {Drop:F2}%", dropPercent);
       dropPercent = 0; // No dip, use base multiplier
   }
   ```

**Detection:**
- Multipliers don't match manual calculation
- TradingView shows different 30-day high
- Logs show unexpected drop percentages
- Buying with 1x when clearly in dip

**Phase mapping:** Phase 3 (Smart Multipliers) must validate drop calculations against multiple sources.

---

### Pitfall 7: Time Zone and Scheduling Confusion

**What goes wrong:** Purchase executes at wrong time of day or skips days due to time zone issues.

**Why it happens:**
- Background service uses local time instead of UTC
- Daylight saving time changes
- Scheduler configured with local time
- Database stores mixed time zones

**Prevention:**
1. **UTC everywhere:**
   ```csharp
   // Already set in Program.cs, reinforce:
   var utcNow = DateTime.UtcNow; // NEVER use DateTime.Now
   var today = DateOnly.FromDateTime(utcNow);
   ```

2. **Explicit scheduling:**
   ```csharp
   public class DcaPurchaseBackgroundService : TimeBackgroundService
   {
       protected override TimeSpan Interval => TimeSpan.FromHours(1); // Check every hour

       protected override async Task ProcessAsync(CancellationToken ct)
       {
           var utcNow = DateTime.UtcNow;
           var targetHour = 14; // 2 PM UTC = 10 AM Eastern

           if (utcNow.Hour != targetHour) return; // Only run at specific hour

           var today = DateOnly.FromDateTime(utcNow);
           var alreadyExecuted = await db.Purchases.AnyAsync(p => p.Date == today);
           if (alreadyExecuted) return;

           await ExecutePurchase(ct);
       }
   }
   ```

3. **Configuration validation:**
   ```csharp
   public class DcaConfiguration
   {
       public int PurchaseHourUtc { get; set; } = 14; // Store in UTC

       public void Validate()
       {
           if (PurchaseHourUtc < 0 || PurchaseHourUtc > 23)
               throw new InvalidOperationException("PurchaseHourUtc must be 0-23");
       }
   }
   ```

**Detection:**
- Purchases happen at unexpected times
- Logs show wrong hour execution
- Missing purchases on specific days
- Duplicate purchases due to DST change

**Phase mapping:** Phase 2 (Purchase Execution) must use UTC consistently. Phase 4 (Configuration) makes schedule configurable.

---

### Pitfall 8: Insufficient Balance Check

**What goes wrong:** Bot attempts purchase but insufficient USDT balance, order fails, no retry.

**Why it happens:**
- Balance check happens before calculation, not before order
- Concurrent operations (manual trades) drain balance
- Fees not accounted for in calculation
- Minimum order size not validated

**Prevention:**
1. **Check balance immediately before order:**
   ```csharp
   // Calculate required USDT
   var btcAmount = baseAmountUsd * multiplier / currentPrice;
   var estimatedFee = btcAmount * currentPrice * 0.001m; // 0.1% taker fee
   var requiredUsdt = (btcAmount * currentPrice) + estimatedFee;

   // Check balance
   var balance = await hyperliquid.GetBalanceAsync("USDT");

   if (balance.Available < requiredUsdt)
   {
       logger.LogError("Insufficient balance. Required: {Required}, Available: {Available}",
           requiredUsdt, balance.Available);

       await telegram.SendAlert(
           $"⚠️ DCA purchase failed: Insufficient USDT balance\n" +
           $"Required: ${requiredUsdt:F2}\n" +
           $"Available: ${balance.Available:F2}\n" +
           $"Deficit: ${requiredUsdt - balance.Available:F2}"
       );

       // Don't throw, mark as failed and continue
       purchase.Status = PurchaseStatus.InsufficientBalance;
       return;
   }
   ```

2. **Validate minimum order size:**
   ```csharp
   var minOrderSize = 0.0001m; // BTC, check Hyperliquid docs

   if (btcAmount < minOrderSize)
   {
       logger.LogWarning("Order too small: {Amount} BTC < {Min} BTC minimum",
           btcAmount, minOrderSize);

       // Option 1: Skip this purchase
       // Option 2: Accumulate and execute larger order next time
       purchase.Status = PurchaseStatus.BelowMinimum;
       return;
   }
   ```

3. **Reserve buffer:**
   ```csharp
   var bufferPercent = 1.02m; // 2% buffer for fees and price movement
   var requiredUsdt = btcAmount * currentPrice * bufferPercent;
   ```

**Detection:**
- "Insufficient balance" errors from exchange
- Failed purchases on days with manual trades
- Telegram alerts about balance issues

**Phase mapping:** Phase 2 (Purchase Execution) must validate balance and order size before submission.

---

### Pitfall 9: Outbox Message Accumulation

**What goes wrong:** Outbox table grows infinitely, database performance degrades, queries slow down.

**Why it happens:**
- No cleanup of processed messages
- Outbox processor doesn't mark messages as processed
- Retention policy not configured
- Every purchase generates multiple events

**Prevention:**
1. **Automatic cleanup:**
   ```csharp
   public class OutboxCleanupBackgroundService : TimeBackgroundService
   {
       protected override TimeSpan Interval => TimeSpan.FromHours(6);

       protected override async Task ProcessAsync(CancellationToken ct)
       {
           var cutoff = DateTime.UtcNow.AddDays(-7); // Keep 7 days

           var deleted = await db.OutboxMessages
               .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
               .ExecuteDeleteAsync(ct);

           logger.LogInformation("Cleaned up {Count} old outbox messages", deleted);
       }
   }
   ```

2. **Mark processed messages:**
   ```csharp
   // Ensure OutboxMessageProcessor actually sets ProcessedAt
   message.ProcessedAt = DateTime.UtcNow;
   await db.SaveChangesAsync();
   ```

3. **Add index:**
   ```csharp
   modelBuilder.Entity<OutboxMessage>()
       .HasIndex(m => new { m.ProcessedAt, m.CreatedAt });
   ```

**Detection:**
- OutboxMessages table grows indefinitely
- Slow queries on outbox table
- Database storage alerts

**Phase mapping:** Phase 5 (Operational Excellence) should add outbox cleanup. Not critical for MVP.

---

## Minor Pitfalls

Mistakes that cause annoyance but are easily fixable.

### Pitfall 10: Verbose Telegram Notifications

**What goes wrong:** Telegram sends too many messages (candle updates, health checks), user mutes bot.

**Why it happens:**
- Every domain event sends notification
- Debug logs sent to Telegram
- No message grouping or batching

**Prevention:**
1. **Notification filtering:**
   ```csharp
   public enum NotificationPriority { Critical, Important, Info, Debug }

   public class TelegramService
   {
       private NotificationPriority _minPriority = NotificationPriority.Important;

       public async Task SendAsync(string message, NotificationPriority priority)
       {
           if (priority < _minPriority) return; // Filter

           await telegramClient.SendMessageAsync(message);
       }
   }
   ```

2. **Daily summary instead of individual events:**
   ```csharp
   // Send ONE message per day with all details
   var summary =
       $"✅ BTC DCA Purchase Complete\n\n" +
       $"Date: {purchase.Date}\n" +
       $"Amount: {purchase.Amount:F8} BTC\n" +
       $"Price: ${purchase.AveragePrice:F2}\n" +
       $"Cost: ${purchase.Amount * purchase.AveragePrice:F2}\n" +
       $"Multiplier: {purchase.Multiplier}x\n" +
       $"Reason: {purchase.Reason}\n\n" +
       $"Total BTC: {totalBtc:F8}\n" +
       $"Total Invested: ${totalInvested:F2}\n" +
       $"Avg Cost: ${totalInvested / totalBtc:F2}";

   await telegram.SendAsync(summary, NotificationPriority.Important);
   ```

**Detection:**
- User feedback about too many messages
- Bot is muted

**Phase mapping:** Phase 2 (Purchase Execution) should implement notification filtering from start.

---

### Pitfall 11: Missing Configuration Validation

**What goes wrong:** User sets baseAmountUsd = -100 or multiplier tier = 0, bot crashes or behaves erratically.

**Why it happens:**
- No input validation on configuration
- Settings loaded but not validated
- Invalid JSON accepted

**Prevention:**
```csharp
public class DcaConfiguration
{
    public decimal BaseAmountUsd { get; set; }
    public int PurchaseHourUtc { get; set; }
    public MultiplierTiers Tiers { get; set; }

    public void Validate()
    {
        if (BaseAmountUsd <= 0)
            throw new InvalidOperationException("BaseAmountUsd must be positive");

        if (PurchaseHourUtc < 0 || PurchaseHourUtc > 23)
            throw new InvalidOperationException("PurchaseHourUtc must be 0-23");

        if (Tiers.Level1Multiplier < 1 || Tiers.Level2Multiplier < Tiers.Level1Multiplier)
            throw new InvalidOperationException("Multiplier tiers must be ascending");
    }
}

// Validate on startup
var config = builder.Configuration.GetSection("Dca").Get<DcaConfiguration>();
config.Validate();
```

**Detection:**
- Crashes on startup
- Unexpected purchase amounts
- Negative values in logs

**Phase mapping:** Phase 4 (Configuration Management) must validate all inputs.

---

### Pitfall 12: API Key Exposure in Logs

**What goes wrong:** Hyperliquid API keys logged in plaintext, visible in console or files.

**Why it happens:**
- Logging request headers
- Exception messages include full request
- Serilog logs configuration object

**Prevention:**
```csharp
// Redact sensitive headers
public class SensitiveDataFilter : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (logEvent.Properties.ContainsKey("ApiKey"))
        {
            logEvent.RemovePropertyIfPresent("ApiKey");
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("ApiKey", "***REDACTED***"));
        }
    }
}

// Configure Serilog
.Enrich.With<SensitiveDataFilter>()

// Never log API keys
logger.LogInformation("Calling Hyperliquid API"); // Good
logger.LogInformation("Calling Hyperliquid API with key {Key}", apiKey); // BAD
```

**Detection:**
- API keys visible in logs
- Security audit findings

**Phase mapping:** Phase 1 (Hyperliquid Integration) must never log secrets.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|----------------|------------|
| Phase 1: Hyperliquid Integration | Rate limiting not implemented | Add rate limit tracking from day one, use aggressive caching |
| Phase 1: Hyperliquid Integration | API keys logged | Implement log filtering before any API calls |
| Phase 2: Purchase Execution | Duplicate orders | Idempotency check BEFORE every execution, fix distributed lock stub |
| Phase 2: Purchase Execution | Silent failures | Multi-channel alerting (Telegram + email + health check) |
| Phase 2: Purchase Execution | Partial fills | Poll order status until fully filled, store actual executed amount |
| Phase 3: Smart Multipliers | Stale price data | Fetch fresh candles before calculation, validate timestamp |
| Phase 3: Smart Multipliers | Incorrect drop calculation | Use completed candles only, validate against TradingView |
| Phase 4: Configuration | Invalid settings | Validate all inputs on startup, reject negative/out-of-range values |
| Phase 4: Monitoring | No failure visibility | Daily success verification service, health check endpoint |
| Phase 5: Operational | Outbox accumulation | Implement cleanup service (7-day retention) |

---

## Hyperliquid-Specific Warnings

### API Reliability Concerns

**Known characteristics of newer exchanges:**
- Higher downtime frequency than established exchanges
- WebSocket disconnections more common
- API rate limits may change without notice
- Documentation may be incomplete or outdated

**Mitigation:**
1. **Assume API will fail:**
   ```csharp
   var maxRetries = 3;
   var retryPolicy = Policy
       .Handle<HttpRequestException>()
       .Or<TaskCanceledException>()
       .WaitAndRetryAsync(maxRetries, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

   await retryPolicy.ExecuteAsync(async () =>
   {
       return await hyperliquid.PlaceOrderAsync(request);
   });
   ```

2. **Fallback for critical operations:**
   ```csharp
   try
   {
       return await hyperliquid.GetPriceAsync("BTC");
   }
   catch (Exception ex)
   {
       logger.LogWarning(ex, "Hyperliquid price fetch failed, trying fallback");
       return await coinGeckoClient.GetPriceAsync("BTC"); // Backup price source
   }
   ```

3. **Circuit breaker pattern:**
   ```csharp
   var circuitBreaker = Policy
       .Handle<Exception>()
       .CircuitBreakerAsync(
           exceptionsAllowedBeforeBreaking: 5,
           durationOfBreak: TimeSpan.FromMinutes(2)
       );
   ```

### Authentication Edge Cases

**Concern:** Hyperliquid authentication may use non-standard signing methods.

**Mitigation:**
1. Research exact signing algorithm (likely HMAC-SHA256 but verify)
2. Test with small orders first
3. Store test results in integration tests
4. Monitor for authentication errors in production

---

## Research Confidence Assessment

| Category | Confidence | Source |
|----------|-----------|--------|
| DCA bot patterns | HIGH | Established patterns for recurring buy automation |
| Duplicate order prevention | HIGH | Standard idempotency practices |
| Exchange integration | HIGH | Common patterns across crypto exchanges |
| Hyperliquid-specific | LOW | Limited public information, newer exchange |
| Rate limiting | MEDIUM | General exchange patterns, need Hyperliquid docs |
| Order execution | HIGH | Standard spot market patterns |
| Time handling | HIGH | .NET UTC patterns, already configured correctly |
| Distributed locking | HIGH | Current code has stub that MUST be fixed |

---

## Critical Action Items for Phase 1

Before writing ANY Hyperliquid integration code:

1. **Fix distributed lock stub** in `DistributedLocks/DistributedLock.cs` (currently bypassed with `return new();`)
2. **Research Hyperliquid API docs** for:
   - Rate limits (requests per minute, IP vs API key)
   - Authentication method (HMAC? Ed25519?)
   - Order status polling recommendations
   - Minimum order sizes
   - Spot market availability for BTC
3. **Implement idempotency pattern** BEFORE first purchase logic
4. **Set up test environment** with small amounts ($10-50) before production
5. **Verify Hyperliquid supports spot BTC** (not just perps)

---

**Research completed:** 2026-02-12
**Overall confidence:** HIGH for DCA patterns, MEDIUM for Hyperliquid specifics (needs API documentation verification)
**Recommended next step:** Research Hyperliquid official API documentation before Phase 1 implementation

# Phase 02: Core DCA Engine - Research

**Researched:** 2026-02-12
**Domain:** DCA (Dollar-Cost Averaging) bot implementation with background services, Hyperliquid API integration, retry resilience, and Telegram notifications
**Confidence:** HIGH

## Summary

Research focused on the technical patterns and libraries needed to implement a production-ready DCA engine in .NET 8. The phase involves daily scheduled purchases, order execution with resilience, idempotent crash recovery, and comprehensive Telegram notifications.

**Standard approach**: .NET BackgroundService with PeriodicTimer for scheduling (already established in codebase via TimeBackgroundService), Polly for retry resilience with exponential backoff and jitter, PostgreSQL advisory locks for idempotency (already in place), MediatR domain events for notifications, and direct Hyperliquid API integration (already implemented).

The codebase has strong foundations: TimeBackgroundService abstract class provides battle-tested scheduling patterns, HyperliquidClient handles authentication and order placement, Purchase entity and DbContext are ready for persistence, and PostgreSQL distributed locks prevent duplicate executions.

**Primary recommendation:** Extend TimeBackgroundService for scheduled daily execution, wrap HyperliquidClient calls with Polly retry policies (3 retries with exponential backoff + jitter for transient failures), use MediatR notifications for purchase success/failure events, implement Telegram handlers as INotificationHandler<PurchaseCompleted>, and leverage existing PostgreSQL advisory locks for idempotency. Trust Hyperliquid's immediate IOC order response as fill confirmation per user decision.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 8 BackgroundService | Built-in | Long-running background tasks | Official Microsoft pattern for hosted services, battle-tested |
| PeriodicTimer | .NET 6+ | Async timer ticks | Recommended over Timer for async patterns, built-in |
| Polly | 8.x | Retry, circuit breaker, resilience | Industry standard for .NET resilience, Microsoft-recommended |
| MediatR | 12.x | In-process messaging (domain events) | De facto standard for CQRS and domain events in .NET |
| EF Core | 8.x | ORM and database persistence | Already in use, standard .NET ORM |
| Serilog | 3.x | Structured logging | Already configured in codebase |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Http.Polly | 8.x | HttpClient + Polly integration | For retry on HyperliquidClient HTTP calls |
| Telegram.Bot | 21.x | Telegram Bot API client | For sending formatted messages (if not already present) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | PostgreSQL provider for EF Core | Already in use for advisory locks |

### Already Implemented in Codebase
- TimeBackgroundService: Abstract base with PeriodicTimer pattern (BuildingBlocks/TimeBackgroundService.cs)
- HyperliquidClient: API client with order placement, balance checking, price fetching
- Purchase entity: Domain model with PurchaseStatus enum
- PostgreSQL advisory locks: Via Npgsql for distributed locking
- BaseEntity/AuditedEntity: DDD entity base classes

**Installation (new packages only):**
```bash
# Add Polly for retry resilience
dotnet add package Polly --version 8.4.2
dotnet add package Microsoft.Extensions.Http.Polly --version 8.0.8

# Add Telegram Bot SDK (if not already present)
dotnet add package Telegram.Bot --version 21.7.1

# Add MediatR (if not already present)
dotnet add package MediatR --version 12.4.0
```

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.ApiService/
├── BackgroundServices/
│   └── DcaSchedulerBackgroundService.cs    # Extends TimeBackgroundService
├── Application/
│   ├── Commands/
│   │   ├── ExecuteDailyPurchaseCommand.cs
│   │   └── ExecuteDailyPurchaseHandler.cs
│   └── Events/
│       ├── PurchaseCompletedEvent.cs
│       ├── PurchaseFailedEvent.cs
│       └── InsufficientBalanceEvent.cs
├── Application/Handlers/
│   └── TelegramNotificationHandler.cs       # INotificationHandler<Events>
├── Infrastructure/
│   ├── Hyperliquid/
│   │   └── ResilientHyperliquidClient.cs   # Wraps HyperliquidClient with Polly
│   └── Telegram/
│       └── TelegramMessageFormatter.cs
└── Models/
    └── Purchase.cs                          # Already exists
```

### Pattern 1: Daily Scheduled Task with TimeBackgroundService
**What:** Extend TimeBackgroundService to trigger daily purchase at configured UTC time
**When to use:** For scheduled daily execution with idempotent distributed locking

**Example:**
```csharp
// Source: Existing codebase pattern + .NET BackgroundService best practices
public class DcaSchedulerBackgroundService(
    ILogger<DcaSchedulerBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<DcaOptions> options
) : TimeBackgroundService(logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5); // Check every 5 min

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var targetTime = new TimeOnly(options.Value.DailyBuyHour, options.Value.DailyBuyMinute);
        var todayTarget = DateOnly.FromDateTime(now.Date).ToDateTime(targetTime, TimeSpan.Zero);

        // Only execute if we're past target time and haven't executed today
        if (now < todayTarget || now > todayTarget.AddMinutes(Interval.TotalMinutes))
        {
            return; // Skip: either too early or missed window
        }

        // Create scope for scoped services
        await using var scope = scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Execute purchase command
        var command = new ExecuteDailyPurchaseCommand(DateOnly.FromDateTime(now.Date));
        await mediator.Send(command, cancellationToken);
    }
}
```

**Key principles:**
- Check every N minutes (5-10 min) whether it's time to execute
- Use TimeOnly/DateOnly for time-of-day comparisons
- Execute within window after target time (e.g., 08:00-08:10 UTC)
- Missed window = skip until tomorrow
- Create async scope for scoped services (DbContext, HttpClient)

### Pattern 2: Resilient HTTP Client with Polly
**What:** Wrap HyperliquidClient calls with retry policy for transient failures
**When to use:** For all external API calls that may fail transiently

**Example:**
```csharp
// Source: Microsoft Learn + Polly documentation
// In ServiceCollectionExtensions.cs
services.AddHttpClient<HyperliquidClient>(client =>
{
    client.BaseAddress = new Uri("https://api.hyperliquid.xyz");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;

    // Only retry on transient errors
    options.Retry.ShouldHandle = new PredicateBuilder()
        .Handle<HttpRequestException>()
        .Handle<TimeoutException>()
        .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError);
});
```

**Retry behavior:**
- Attempt 1: immediate
- Attempt 2: ~1s delay (with jitter)
- Attempt 3: ~2s delay (with jitter)
- Attempt 4: ~4s delay (with jitter)
- Total retries: 3 (4 attempts including original)
- Jitter: ±20% randomness to prevent thundering herd

**Permanent errors (no retry):**
- 400 Bad Request (invalid parameters)
- 401/403 Unauthorized/Forbidden (auth failure)
- 404 Not Found

### Pattern 3: MediatR Domain Events for Notifications
**What:** Publish domain events after purchase completion, handle with Telegram notification
**When to use:** For decoupling purchase execution from notification logic

**Example:**
```csharp
// Source: .NET domain events pattern + MediatR best practices
// Domain event
public record PurchaseCompletedEvent(
    Guid PurchaseId,
    decimal BtcAmount,
    decimal Price,
    decimal UsdSpent,
    decimal RemainingUsdc,
    decimal CurrentBtcBalance
) : INotification;

// Handler publishes after saving to DB
public class ExecuteDailyPurchaseHandler(
    IMediator mediator,
    TradingBotDbContext dbContext
) : IRequestHandler<ExecuteDailyPurchaseCommand, Result>
{
    public async Task<Result> Handle(ExecuteDailyPurchaseCommand request, CancellationToken ct)
    {
        // ... execute purchase, save to DB ...
        await dbContext.SaveChangesAsync(ct);

        // Publish event AFTER transaction commits
        await mediator.Publish(new PurchaseCompletedEvent(
            purchase.Id,
            purchase.Quantity,
            purchase.Price,
            purchase.Cost,
            remainingBalance,
            currentBtcBalance
        ), ct);

        return Result.Success();
    }
}

// Telegram notification handler
public class TelegramNotificationHandler(
    ITelegramBotClient telegram,
    IOptions<TelegramOptions> options
) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken ct)
    {
        var message = FormatSuccessMessage(notification);
        await telegram.SendTextMessageAsync(
            chatId: options.Value.ChatId,
            text: message,
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct
        );
    }

    private string FormatSuccessMessage(PurchaseCompletedEvent evt)
    {
        // Escape special characters for MarkdownV2
        return $"""
            ✅ *Purchase Successful*

            *BTC Bought:* `{evt.BtcAmount:F8}` BTC
            *Price:* `${evt.Price:F2}`
            *USD Spent:* `${evt.UsdSpent:F2}`
            *Current BTC Balance:* `{evt.CurrentBtcBalance:F8}` BTC
            *Remaining USDC:* `${evt.RemainingUsdc:F2}`
            """;
    }
}
```

**Key principles:**
- Publish events AFTER database transaction commits
- One event type per outcome (success, failure, skip)
- Each handler is independent (fire-and-forget)
- Telegram errors don't fail the purchase

### Pattern 4: Idempotent Daily Purchase with Database Check
**What:** Query database for today's purchase before executing, ensure exactly-once semantics
**When to use:** For crash recovery and preventing duplicate purchases

**Example:**
```csharp
// Source: .NET idempotency patterns + distributed locking
public async Task<Result> Handle(ExecuteDailyPurchaseCommand request, CancellationToken ct)
{
    var today = request.PurchaseDate;

    // 1. Acquire distributed lock (already implemented via PostgreSQL advisory locks)
    await using var lockHandle = await distributedLock.AcquireAsync($"dca-purchase-{today:yyyy-MM-dd}", ct);

    if (lockHandle == null)
    {
        logger.LogWarning("Could not acquire lock for purchase on {Date}, skipping", today);
        return Result.Failure("Lock acquisition failed");
    }

    // 2. Check if today's purchase already completed
    var existingPurchase = await dbContext.Purchases
        .Where(p => p.ExecutedAt.Date == today.ToDateTime(TimeOnly.MinValue))
        .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
        .FirstOrDefaultAsync(ct);

    if (existingPurchase != null)
    {
        logger.LogInformation("Purchase already completed today: {PurchaseId}", existingPurchase.Id);
        return Result.Success(); // Already done, skip
    }

    // 3. Execute purchase
    // ... check balance, place order, persist result ...

    return Result.Success();
}
```

**Crash recovery approach (per user decision):**
On startup, query Hyperliquid for recent fills (last 24h) and check against database to detect already-executed orders:
```csharp
// In application startup
var recentFills = await hyperliquidClient.GetUserFillsAsync(
    startTime: DateTimeOffset.UtcNow.AddHours(-24),
    ct: cancellationToken
);

foreach (var fill in recentFills.Where(f => f.Coin == "BTC"))
{
    var existingPurchase = await dbContext.Purchases
        .FirstOrDefaultAsync(p => p.OrderId == fill.Oid.ToString(), ct);

    if (existingPurchase == null)
    {
        // Detected fill not in database - create purchase record
        logger.LogWarning("Detected orphaned fill {Oid}, creating purchase record", fill.Oid);
        // ... create Purchase entity from fill data ...
    }
}
```

### Anti-Patterns to Avoid
- **Don't poll order status**: User decided to trust immediate IOC order response as fill confirmation (no status polling needed)
- **Don't retry on permanent errors**: 400/401/403 errors mean bad request or auth failure, retrying won't help
- **Don't execute in Background Service directly**: Use MediatR command handlers for testability and separation of concerns
- **Don't use async void**: Background services already return Task, use proper async/await
- **Don't skip distributed lock**: Multiple instances could execute duplicate purchases without locking

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Retry with backoff | Custom retry loop with Thread.Sleep | Polly retry policy with exponential backoff | Handles jitter, max attempts, conditional retry, cancellation tokens, battle-tested |
| Daily scheduling | Custom timer logic with DateTime comparisons | PeriodicTimer + time window check | Async-friendly, cancellation support, less error-prone |
| Decimal precision rounding | Manual Math.Round calls | Use `decimal` type + `MidpointRounding.AwayFromZero` | Cryptocurrency requires exact decimal arithmetic, float/double introduce errors |
| Message formatting | String concatenation | String interpolation with raw string literals (`"""`) | Maintains formatting, easier to read multiline messages |
| Idempotency | Custom flag files or in-memory cache | PostgreSQL advisory locks + database query | Distributed, crash-safe, already implemented in codebase |
| Domain events | Manual event handler registry | MediatR INotification + INotificationHandler | Supports multiple handlers per event, DI integration, decoupled |
| HTTP client lifecycle | new HttpClient() per request | IHttpClientFactory with named/typed clients | Prevents socket exhaustion, built-in DI, Polly integration |
| Configuration hot-reload | Manual file watching | IOptionsMonitor<T> | Built-in change detection, thread-safe, already used in codebase |

**Key insight:** .NET ecosystem has mature solutions for all these cross-cutting concerns. Custom implementations add risk and maintenance burden without benefit. Polly alone handles retry complexity (jitter, circuit breakers, timeouts) that would take weeks to implement correctly.

## Common Pitfalls

### Pitfall 1: Telegram MarkdownV2 Character Escaping
**What goes wrong:** Telegram rejects messages with unescaped special characters in MarkdownV2 mode
**Why it happens:** Characters like `.`, `-`, `(`, `)`, `!`, `=`, `>`, `#`, `+`, `{`, `}`, `[`, `]` have special meaning in MarkdownV2
**How to avoid:** Use raw string literals for templates and escape special chars, OR use Markdown (v1) which is less strict
**Warning signs:** Telegram API returns 400 Bad Request with "can't parse entities" error

**Solution:**
```csharp
// Escape function for MarkdownV2
private string EscapeMarkdownV2(string text)
{
    var charsToEscape = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
    return charsToEscape.Aggregate(text, (current, c) => current.Replace(c.ToString(), $"\\{c}"));
}

// OR use Markdown v1 (less strict)
await telegram.SendTextMessageAsync(
    chatId: chatId,
    text: message,
    parseMode: ParseMode.Markdown, // v1, not v2
    cancellationToken: ct
);
```

### Pitfall 2: Decimal Rounding Errors with Cryptocurrency
**What goes wrong:** Order placement fails with "invalid size precision" or "invalid price precision" errors
**Why it happens:** Hyperliquid enforces specific decimal precision per asset (BTC requires 5 decimals for size, prices allow 8 decimals)
**How to avoid:** Always use `decimal` type (never `double`/`float`), round to asset's `szDecimals` before placing order
**Warning signs:** Hyperliquid API error: "Invalid price" or "Invalid size" or minimum order value errors

**Solution:**
```csharp
// Round to asset-specific precision (from spotMeta.universe[].szDecimals)
decimal RoundToSzDecimals(decimal value, int szDecimals)
{
    return Math.Round(value, szDecimals, MidpointRounding.AwayFromZero);
}

// Get metadata first, then round
var meta = await hyperliquidClient.GetSpotMetadataAsync(ct);
var btcAsset = meta.Universe.First(u => u.Name == "BTC/USDC");
var roundedSize = RoundToSzDecimals(btcQuantity, btcAsset.SzDecimals);

// HyperliquidClient already handles price formatting via FloatToWire (8 decimals)
```

**Important:** Hyperliquid's minimum order value is ~$10 USDC. Check `usdcAmount >= 10m` before placing order.

### Pitfall 3: BackgroundService Exception Handling Stops Host
**What goes wrong:** Unhandled exception in BackgroundService.ExecuteAsync stops the entire application in .NET 6+
**Why it happens:** .NET 6 changed default behavior to stop host on background service failure (previous versions swallowed exceptions)
**How to avoid:** Wrap ProcessAsync logic in try-catch within TimeBackgroundService (already done in codebase), OR set `HostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore`
**Warning signs:** Application exits unexpectedly after background service error

**Solution (already implemented in TimeBackgroundService.cs):**
```csharp
// Existing pattern in codebase - good!
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    try
    {
        await ProcessAsync(stoppingToken); // Derived class implements this
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "{BackgroundService} error while processing", GetType().Name);
        // Continue to next iteration - don't crash
    }
}
```

### Pitfall 4: Scoped Service Injection in Singleton BackgroundService
**What goes wrong:** Cannot inject scoped services (DbContext, HttpClient) into BackgroundService constructor
**Why it happens:** BackgroundService is registered as singleton, but DbContext/HttpClient are scoped per request
**How to avoid:** Inject IServiceScopeFactory, create async scope in ProcessAsync for each iteration
**Warning signs:** DI container throws InvalidOperationException: "Cannot resolve scoped service from singleton"

**Solution:**
```csharp
public class DcaSchedulerBackgroundService(
    ILogger<DcaSchedulerBackgroundService> logger,
    IServiceScopeFactory scopeFactory // Inject factory, not DbContext directly
) : TimeBackgroundService(logger)
{
    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        // Create scope for this iteration
        await using var scope = scopeFactory.CreateAsyncScope();

        // Resolve scoped services
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Use scoped services
        // ... scope disposes automatically at end of iteration
    }
}
```

### Pitfall 5: Rate Limiting on Hyperliquid API
**What goes wrong:** API returns 429 Too Many Requests or requests silently fail
**Why it happens:** Hyperliquid enforces 1200 weight/minute per IP, exchange requests have weight 1, info requests vary (2-60)
**How to avoid:** Use WebSocket for real-time data, batch operations when possible, respect rate limits in retry policy
**Warning signs:** 429 status code, or slower response times during high usage

**Current risk level:** LOW for daily DCA bot (1 balance check + 1 order/day = 3 weight/day << 1200/minute limit)

**If needed later:**
```csharp
// Add rate limit policy
services.AddHttpClient<HyperliquidClient>()
    .AddPolicyHandler(Policy.RateLimitAsync(
        numberOfExecutions: 1200,
        perTimeSpan: TimeSpan.FromMinutes(1)
    ));
```

### Pitfall 6: Trusting IOC Order Fill Without Validation
**What goes wrong:** Assume full fill when order response shows partial fill or error in nested response
**Why it happens:** Hyperliquid order response structure has nested `response.data.statuses[].error` field
**How to avoid:** Parse full response structure, check `status == "ok"` AND `statuses[].error == null` AND compare filled vs requested quantity
**Warning signs:** Database shows Filled status but Cost is lower than expected

**Solution (already implemented in HyperliquidClient.cs):**
```csharp
// Check for errors in nested response structure
if (response.Status == "err" || response.Response?.Data?.Statuses.Any(s => s.Error != null) == true)
{
    var errorMsg = response.Response?.Data?.Statuses.FirstOrDefault(s => s.Error != null)?.Error ?? "Unknown error";
    throw new HyperliquidApiException($"Order placement failed: {errorMsg}");
}

// Also check filled quantity vs requested
var filledQty = response.Response?.Data?.Statuses.FirstOrDefault()?.Filled?.TotalSz;
if (filledQty == null || decimal.Parse(filledQty) < requestedSize * 0.95m) // Allow 5% slippage
{
    logger.LogWarning("Partial fill: requested {Requested}, filled {Filled}", requestedSize, filledQty);
    // Mark as PartiallyFilled, not Filled
}
```

## Code Examples

Verified patterns from official sources and codebase:

### Daily Time Window Check
```csharp
// Source: .NET PeriodicTimer + DateOnly/TimeOnly best practices
private bool IsWithinExecutionWindow(DateTimeOffset now, TimeOnly targetTime, TimeSpan window)
{
    var todayTarget = DateOnly.FromDateTime(now.Date).ToDateTime(targetTime, TimeSpan.Zero);
    var windowStart = todayTarget;
    var windowEnd = todayTarget.Add(window);

    return now >= windowStart && now < windowEnd;
}

// Usage in ProcessAsync
var targetTime = new TimeOnly(options.Value.DailyBuyHour, options.Value.DailyBuyMinute);
if (!IsWithinExecutionWindow(DateTimeOffset.UtcNow, targetTime, TimeSpan.FromMinutes(10)))
{
    return; // Skip, not in execution window
}
```

### Polly Retry with Conditional Logic
```csharp
// Source: Polly documentation - https://www.pollydocs.org/strategies/retry.html
var retryPolicy = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,

        // Only retry on transient errors
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutException>()
            .HandleResult(r => (int)r.StatusCode >= 500), // 5xx only

        OnRetry = args =>
        {
            logger.LogWarning(
                "Retry attempt {Attempt} after {Delay}ms due to {Outcome}",
                args.AttemptNumber,
                args.RetryDelay.TotalMilliseconds,
                args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString()
            );
            return ValueTask.CompletedTask;
        }
    })
    .Build();
```

### MediatR Event Publishing Pattern
```csharp
// Source: MediatR best practices + .NET domain events
// In ExecuteDailyPurchaseHandler after successful purchase
var purchase = new Purchase
{
    ExecutedAt = DateTimeOffset.UtcNow,
    Price = currentPrice,
    Quantity = btcAmount,
    Cost = usdcAmount,
    Multiplier = 1.0m,
    Status = PurchaseStatus.Filled,
    OrderId = response.Response?.Data?.Statuses.First().Resting?.Oid.ToString()
};

dbContext.Purchases.Add(purchase);
await dbContext.SaveChangesAsync(ct); // Commit transaction FIRST

// Publish event AFTER transaction commits successfully
await mediator.Publish(new PurchaseCompletedEvent(
    PurchaseId: purchase.Id,
    BtcAmount: purchase.Quantity,
    Price: purchase.Price,
    UsdSpent: purchase.Cost,
    RemainingUsdc: await hyperliquidClient.GetBalancesAsync(ct),
    CurrentBtcBalance: await GetCurrentBtcBalance(ct)
), ct);
```

### Telegram Message Formatting
```csharp
// Source: Telegram Bot API - https://core.telegram.org/bots/api
public async Task SendPurchaseSuccessNotification(
    PurchaseCompletedEvent evt,
    CancellationToken ct)
{
    // Use Markdown v1 (less strict than v2)
    var message = $"""
        ✅ *Purchase Successful*

        *BTC Bought:* `{evt.BtcAmount:F8}` BTC
        *Price:* `${evt.Price:F2}`
        *USD Spent:* `${evt.UsdSpent:F2}`
        *Current BTC Balance:* `{evt.CurrentBtcBalance:F8}` BTC
        *Remaining USDC:* `${evt.RemainingUsdc:F2}`

        _Daily DCA executed at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_
        """;

    await telegramBot.SendTextMessageAsync(
        chatId: chatId,
        text: message,
        parseMode: ParseMode.Markdown, // v1, not MarkdownV2
        cancellationToken: ct
    );
}

// Failure notification
public async Task SendPurchaseFailureNotification(
    PurchaseFailedEvent evt,
    CancellationToken ct)
{
    var message = $"""
        ❌ *Purchase Failed*

        *Error:* {evt.ErrorType}
        *Message:* `{evt.ErrorMessage}`
        *Retry Count:* {evt.RetryCount}/3

        _Failed at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_
        """;

    await telegramBot.SendTextMessageAsync(
        chatId: chatId,
        text: message,
        parseMode: ParseMode.Markdown,
        cancellationToken: ct
    );
}
```

### Balance Check with Minimum Threshold
```csharp
// Source: Hyperliquid API docs + cryptocurrency exchange patterns
public async Task<Result<decimal>> CheckSufficientBalance(
    decimal requiredAmount,
    CancellationToken ct)
{
    const decimal MinimumBalance = 1.0m; // Skip if < $1 USDC
    const decimal MinimumOrderValue = 10.0m; // Hyperliquid minimum ~$10

    var balance = await hyperliquidClient.GetBalancesAsync(ct);

    if (balance < MinimumBalance)
    {
        logger.LogWarning("Balance {Balance} below minimum {Min}, skipping purchase", balance, MinimumBalance);
        await mediator.Publish(new InsufficientBalanceEvent(balance, requiredAmount), ct);
        return Result.Failure<decimal>("Insufficient balance");
    }

    // Buy what we can if balance < target
    var actualAmount = Math.Min(balance, requiredAmount);

    if (actualAmount < MinimumOrderValue)
    {
        logger.LogWarning("Actual amount {Amount} below minimum order {Min}", actualAmount, MinimumOrderValue);
        await mediator.Publish(new InsufficientBalanceEvent(balance, MinimumOrderValue), ct);
        return Result.Failure<decimal>("Amount below minimum order value");
    }

    return Result.Success(actualAmount);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Timer class for background tasks | PeriodicTimer (async) | .NET 6 (2021) | Better async/await support, proper cancellation |
| Polly v7 fluent API | Polly v8 resilience pipeline | Polly 8.0 (2023) | Unified API for retry/circuit breaker/timeout |
| MediatR Publish (sequential) | MediatR Publish (parallel) | MediatR 11+ (2023) | Handlers run in parallel by default, faster notifications |
| Manual retry loops | Polly + IHttpClientFactory | .NET Core 2.1+ | Standardized resilience, socket exhaustion prevention |
| Telegram Bot API v1 Markdown | MarkdownV2 (strict escaping) | Bot API 4.5 (2019) | More features but requires escaping special chars |
| BackgroundService swallows exceptions | Stops host on unhandled exception | .NET 6 (2021) | Fail-fast by default, requires explicit error handling |

**Deprecated/outdated:**
- System.Timers.Timer for background tasks: Use PeriodicTimer instead (async-friendly)
- Polly v7 syntax (Policy.Handle<>.WaitAndRetry()): Use ResiliencePipelineBuilder in v8
- IHostedService with manual Task.Run loops: Use BackgroundService base class
- Float/double for money: Always use decimal for financial calculations
- Parse_mode omitted in Telegram: Default plain text, must specify Markdown/MarkdownV2 explicitly

## Open Questions

Things that couldn't be fully resolved:

1. **Hyperliquid Spot Market Order Fill Confirmation**
   - What we know: IOC orders execute immediately, order response includes fill status, user decided to trust immediate response
   - What's unclear: Edge cases where IOC order partially fills due to low liquidity (unlikely for BTC/USDC)
   - Recommendation: Trust immediate response per user decision, but log full response for debugging. Mark as PartiallyFilled if filled quantity < requested. Re-evaluate if production data shows frequent partial fills.

2. **Telegram Rate Limits for Bot Messages**
   - What we know: Telegram enforces rate limits per bot (exact limits undocumented, estimated 20-30 msg/second)
   - What's unclear: Exact limit for 1-on-1 bot messages (vs group chats)
   - Recommendation: Not a concern for daily DCA (3-4 messages/day max). If adding more notifications later, implement message queuing.

3. **PostgreSQL Advisory Lock Timeout Strategy**
   - What we know: Codebase has distributed lock implementation, used for preventing duplicate execution
   - What's unclear: Should lock acquisition timeout after N seconds or wait indefinitely?
   - Recommendation: Set timeout (e.g., 30 seconds). If lock not acquired, log warning and skip execution (another instance is running). Configure via LockOptions if available.

4. **BTC Balance Tracking After Purchase**
   - What we know: Purchase records BTC quantity bought, user wants "current BTC balance" in notification
   - What's unclear: Should we query Hyperliquid for total BTC balance, or calculate from Purchase history?
   - Recommendation: Query Hyperliquid for actual balance (source of truth). Sum of purchases may not match if user trades manually or transfers BTC.

5. **Crash Recovery Window**
   - What we know: On startup, query Hyperliquid for recent fills (last 24h) to detect orphaned orders
   - What's unclear: What if bot was offline for >24 hours?
   - Recommendation: 24h window is sufficient for daily bot. If offline longer, manual reconciliation needed. Could extend to 7 days for safer recovery.

## Sources

### Primary (HIGH confidence)
- [Polly Retry Strategy Documentation](https://www.pollydocs.org/strategies/retry.html) - Official Polly v8 retry patterns
- [Microsoft Learn: Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-9.0) - Official BackgroundService guide
- [Microsoft Learn: HTTP call retries with Polly](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly) - Official retry implementation
- [Hyperliquid Exchange Endpoint Docs](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint) - Official order placement API
- [Hyperliquid Info Endpoint Docs](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint) - Official balance/status queries
- [Hyperliquid Spot Endpoint Docs](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint/spot) - Official spot trading specifics
- [Hyperliquid Order Types](https://hyperliquid.gitbook.io/hyperliquid-docs/trading/order-types) - IOC and market order behavior
- [Hyperliquid Rate Limits](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/rate-limits-and-user-limits) - API rate limiting details
- [Hyperliquid Tick and Lot Size](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/tick-and-lot-size) - Size precision requirements
- [Telegram Bot API](https://core.telegram.org/bots/api) - Official sendMessage and formatting docs
- Existing codebase: TimeBackgroundService.cs, HyperliquidClient.cs, Purchase.cs, CONVENTIONS.md

### Secondary (MEDIUM confidence)
- [Milan Jovanovic: Distributed Locking in .NET](https://www.milanjovanovic.tech/blog/distributed-locking-in-dotnet-coordinating-work-across-multiple-instances) - Verified with PostgreSQL advisory lock docs
- [Milan Jovanovic: Implementing the Outbox Pattern](https://www.milanjovanovic.tech/blog/implementing-the-outbox-pattern) - Domain events with EF Core transactions
- [Milan Jovanovic: Idempotent Consumer Pattern](https://www.milanjovanovic.tech/blog/the-idempotent-consumer-pattern-in-dotnet-and-why-you-need-it) - Database-based idempotency
- [How to Build HTTP Clients with Polly Retry in .NET (2026)](https://oneuptime.com/blog/post/2026-01-25-http-clients-polly-retry-dotnet/view) - Recent Polly 8 patterns
- [C# Retry with Polly for Failed Requests (2026)](https://www.zenrows.com/blog/c-sharp-polly-retry) - Exponential backoff examples
- [Handling Transient Failures in .NET 8 With Polly](https://www.c-sharpcorner.com/article/handling-transient-failures-in-net-8-with-polly/) - Transient error classification
- [Telegram Format NPM Package](https://github.com/EdJoPaTo/telegram-format) - MarkdownV2 escaping patterns
- [Kraken API: Decimal precision for API calculations](https://support.kraken.com/articles/201988998-decimal-precision-for-api-calculations) - Exchange decimal precision patterns

### Tertiary (LOW confidence)
- WebSearch results for DCA bot implementations - General patterns only, not .NET specific
- GitHub community discussions on MediatR error handling - No official guidance, community opinions
- Medium articles on Polly jitter - Verified with official docs

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries verified via official docs, already partially implemented in codebase
- Architecture: HIGH - Patterns verified via Microsoft Learn, Polly docs, existing codebase structure
- Hyperliquid API: HIGH - All endpoints verified via official Hyperliquid documentation
- Retry patterns: HIGH - Polly official documentation + Microsoft recommendations
- Background services: HIGH - Existing TimeBackgroundService implementation + Microsoft docs
- Telegram formatting: MEDIUM - Official API docs available but escaping details from community sources
- Pitfalls: HIGH - Most verified via official docs or existing codebase, some from community experience
- Idempotency: HIGH - PostgreSQL advisory locks already implemented, patterns verified

**Research date:** 2026-02-12
**Valid until:** 2026-03-12 (30 days - stable technologies, Hyperliquid API may evolve)
**Codebase analysis:** Existing TimeBackgroundService, HyperliquidClient, Purchase model, and PostgreSQL locks provide strong foundation for implementation

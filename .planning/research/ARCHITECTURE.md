# Architecture: Hyperliquid BTC Smart DCA Bot

**Domain:** Smart DCA Trading Bot for BTC Spot on Hyperliquid
**Researched:** 2026-02-12
**Confidence:** HIGH (based on existing BuildingBlocks analysis)

## Executive Summary

The smart DCA bot integrates with existing .NET 10.0 infrastructure using a **layered, event-driven architecture** with clear component boundaries. The bot leverages BuildingBlocks (TimeBackgroundService, domain events, outbox pattern, EF Core, distributed locks) to create a reliable, observable, and maintainable system.

**Key Architectural Decisions:**
1. **Scheduler-driven execution** via TimeBackgroundService for daily buy cycles
2. **Event-driven notifications** using MediatR domain events + outbox pattern
3. **Hyperliquid API client** as isolated infrastructure service
4. **Price data service** with 30-day cache for drop-from-high calculations
5. **Domain-driven design** with Purchase aggregate root and value objects

## Recommended Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                         │
├─────────────────────────────────────────────────────────────────┤
│  Program.cs                                                      │
│  - Registers DI services                                         │
│  - Configures background services                                │
│  - Sets up database migrations                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                           │
├─────────────────────────────────────────────────────────────────┤
│  DcaSchedulerBackgroundService : TimeBackgroundService          │
│  - Runs on configurable schedule (e.g., daily at 10:00 AM UTC)  │
│  - Acquires distributed lock for DCA execution                  │
│  - Orchestrates buy cycle workflow                              │
│                                                                  │
│  DcaExecutionService (Scoped)                                   │
│  - Fetches current price data                                   │
│  - Calculates multiplier (drop-from-high + 200 MA boost)        │
│  - Executes buy order via Hyperliquid                           │
│  - Publishes domain events                                      │
│  - Persists purchase record                                     │
│                                                                  │
│  PriceDataService (Scoped)                                      │
│  - Fetches current BTC price from Hyperliquid                   │
│  - Calculates 30-day high (cached/database)                     │
│  - Calculates 200-day MA (cached/database)                      │
│  - Returns PriceAnalysis value object                           │
│                                                                  │
│  MultiplierCalculator (Stateless)                               │
│  - Calculates drop-from-high percentage                         │
│  - Maps to tier multiplier (1x / 1.5x / 2x / 3x)                │
│  - Applies 200 MA bear market boost (1.5x additional)           │
│  - Returns MultiplierResult value object                        │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                       DOMAIN LAYER                              │
├─────────────────────────────────────────────────────────────────┤
│  Purchase (Aggregate Root : BaseEntity)                         │
│  - Id : Guid (UUIDv7)                                            │
│  - Timestamp : DateTimeOffset                                   │
│  - AmountUsd : decimal                                           │
│  - BtcPrice : decimal                                            │
│  - BtcQuantity : decimal                                         │
│  - Multiplier : decimal                                          │
│  - DropFromHighPct : decimal                                     │
│  - Below200Ma : bool                                             │
│  - HyperliquidOrderId : string                                   │
│                                                                  │
│  DcaConfiguration (Value Object)                                │
│  - BaseAmountUsd : decimal                                       │
│  - ScheduleCron : string (e.g., "0 10 * * *" for 10 AM daily)   │
│  - DropTiers : List<DropTier>                                    │
│  - BearMarketMultiplier : decimal (1.5x)                         │
│                                                                  │
│  DropTier (Value Object)                                        │
│  - MinDropPct : decimal                                          │
│  - MaxDropPct : decimal                                          │
│  - Multiplier : decimal                                          │
│                                                                  │
│  PriceAnalysis (Value Object)                                   │
│  - CurrentPrice : decimal                                        │
│  - ThirtyDayHigh : decimal                                       │
│  - TwoHundredDayMa : decimal                                     │
│  - DropFromHighPct : decimal                                     │
│  - IsBelowMa : bool                                              │
│                                                                  │
│  MultiplierResult (Value Object)                                │
│  - BaseMultiplier : decimal                                      │
│  - BearMarketBoost : decimal                                     │
│  - FinalMultiplier : decimal                                     │
│  - Tier : DropTier                                               │
│                                                                  │
│  Domain Events:                                                  │
│  - DcaCycleStartedDomainEvent                                    │
│  - BuyOrderExecutedDomainEvent                                   │
│  - BuyOrderFailedDomainEvent                                     │
│  - PriceDataRefreshedDomainEvent                                 │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER                          │
├─────────────────────────────────────────────────────────────────┤
│  HyperliquidApiClient (Scoped)                                  │
│  - GetCurrentPrice(symbol) : Task<decimal>                       │
│  - GetHistoricalCandles(symbol, days) : Task<List<Candle>>      │
│  - ExecuteSpotBuy(symbol, amountUsd) : Task<OrderResult>        │
│  - Handles authentication (API key/wallet signature)            │
│  - Uses HttpClient with resilience policies                     │
│                                                                  │
│  ApplicationDbContext : DbContext                               │
│  - DbSet<Purchase> Purchases                                     │
│  - DbSet<DailyPrice> DailyPrices (cache for 30-day/200-day)     │
│  - DbSet<OutboxMessage> OutboxMessages (from BuildingBlocks)    │
│  - Configures entity mappings                                    │
│                                                                  │
│  DailyPrice (Entity : BaseEntity)                               │
│  - Date : DateOnly                                               │
│  - Symbol : string                                               │
│  - High : decimal                                                │
│  - Low : decimal                                                 │
│  - Close : decimal                                               │
│  - Used for 30-day high and 200-day MA calculations             │
│                                                                  │
│  TelegramNotificationService (from existing)                    │
│  - SendBuyNotification(Purchase, PriceAnalysis)                 │
│  - SendErrorNotification(Exception)                             │
│  - Uses Telegram.Bot SDK                                         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      BUILDINGBLOCKS                             │
│                      (Existing Infrastructure)                   │
├─────────────────────────────────────────────────────────────────┤
│  TimeBackgroundService - Base class for scheduler                │
│  MediatR + IDomainEvent - Event publishing                       │
│  OutboxEventPublisher + OutboxMessageBackgroundService          │
│  EfCoreOutboxStore - Transactional outbox                       │
│  IDistributedLock - Prevent concurrent DCA executions           │
│  BaseEntity, AuditedEntity - Entity base classes                │
│  Serilog - Structured logging                                    │
│  PostgreSQL + EF Core - Persistence                              │
│  Redis - Distributed cache for price data                        │
└─────────────────────────────────────────────────────────────────┘
```

## Component Boundaries

| Component | Responsibility | Depends On | Consumed By |
|-----------|---------------|------------|-------------|
| **DcaSchedulerBackgroundService** | Trigger daily buy cycle at configured time | IServiceProvider, IDistributedLock | Hosted by Program.cs |
| **DcaExecutionService** | Orchestrate buy workflow (fetch price → calculate → buy → persist → publish) | PriceDataService, MultiplierCalculator, HyperliquidApiClient, ApplicationDbContext | DcaSchedulerBackgroundService |
| **PriceDataService** | Fetch and cache price data for analysis | HyperliquidApiClient, ApplicationDbContext, IDistributedCache | DcaExecutionService |
| **MultiplierCalculator** | Pure calculation logic for multipliers | DcaConfiguration, PriceAnalysis | DcaExecutionService |
| **HyperliquidApiClient** | HTTP integration with Hyperliquid API | HttpClient, IConfiguration (API keys) | PriceDataService, DcaExecutionService |
| **ApplicationDbContext** | EF Core persistence layer | PostgreSQL connection | All services needing database access |
| **TelegramNotificationService** | Send alerts to user's Telegram | Telegram.Bot, IConfiguration | Domain event handlers |
| **Domain Event Handlers** | React to DCA lifecycle events (send notifications, log metrics) | TelegramNotificationService, ILogger | MediatR pipeline |

## Data Flow: Daily Buy Cycle

```
1. TRIGGER (Daily at configured time)
   DcaSchedulerBackgroundService.ProcessAsync()
   ↓
   Acquire distributed lock: "dca-execution-lock" (TTL: 5 minutes)
   ↓

2. ORCHESTRATION
   DcaExecutionService.ExecuteBuyCycle()
   ↓
   Publish: DcaCycleStartedDomainEvent
   ↓

3. PRICE ANALYSIS
   PriceDataService.GetCurrentPriceAnalysis()
   ↓
   HyperliquidApiClient.GetCurrentPrice("BTC")
   ↓
   Load DailyPrices from database (last 200 days) OR
   Fetch missing days from Hyperliquid + cache in database
   ↓
   Calculate 30-day high, 200-day MA
   ↓
   Return PriceAnalysis value object
   ↓

4. MULTIPLIER CALCULATION
   MultiplierCalculator.Calculate(PriceAnalysis, DcaConfiguration)
   ↓
   Calculate drop from high: (high - current) / high * 100
   ↓
   Map to tier: 0-5% = 1x, 5-10% = 1.5x, 10-20% = 2x, >20% = 3x
   ↓
   Apply bear market boost: If current < 200 MA, multiply by 1.5x
   ↓
   Return MultiplierResult value object
   ↓

5. ORDER EXECUTION
   Calculate purchase amount: BaseAmountUsd * FinalMultiplier
   ↓
   HyperliquidApiClient.ExecuteSpotBuy("BTC", purchaseAmount)
   ↓
   Receive OrderResult (orderId, executedPrice, btcQuantity)
   ↓

6. PERSISTENCE
   Create Purchase entity:
   - AmountUsd, BtcPrice, BtcQuantity, Multiplier, DropFromHighPct
   - Below200Ma, HyperliquidOrderId, Timestamp
   ↓
   Save to ApplicationDbContext
   ↓
   Publish: BuyOrderExecutedDomainEvent (via outbox pattern)
   ↓
   Commit transaction (Purchase + OutboxMessage atomically)
   ↓

7. NOTIFICATION (Async via OutboxMessageBackgroundService)
   OutboxMessageBackgroundService processes OutboxMessage
   ↓
   Deserialize: BuyOrderExecutedDomainEvent
   ↓
   MediatR publishes to handlers
   ↓
   BuyOrderExecutedHandler sends Telegram notification:
   "✅ BTC Buy Executed
    Amount: $150 (1.5x multiplier)
    Price: $42,350
    Quantity: 0.00354 BTC
    Drop from 30d high: 8.2%
    Below 200 MA: Yes (bear market boost applied)
    Total BTC accumulated: 0.245 BTC"
   ↓

8. RELEASE LOCK
   Distributed lock disposed/released
```

## Integration with Existing BuildingBlocks

### 1. TimeBackgroundService Integration

**Pattern:** DcaSchedulerBackgroundService extends TimeBackgroundService

```csharp
public class DcaSchedulerBackgroundService : TimeBackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IDistributedLock _lock;
    private readonly DcaConfiguration _config;

    protected override TimeSpan Interval => CalculateNextInterval();

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        // Acquire lock to prevent concurrent executions
        await using var lockHandle = await _lock.AcquireLockAsync(
            "dca-execution-lock", TimeSpan.FromMinutes(5), ct);

        if (!lockHandle.Success) return; // Another instance is running

        // Execute buy cycle
        await using var scope = _services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<DcaExecutionService>();
        await executor.ExecuteBuyCycle(ct);
    }

    private TimeSpan CalculateNextInterval()
    {
        // Parse cron expression and return time until next execution
        // Example: If scheduled for 10:00 AM daily, return time until next 10 AM
    }
}
```

**Why this pattern:**
- Leverages existing infrastructure (logging, error handling, periodic timer)
- Inherits graceful shutdown and exception recovery
- Automatic retry on failure (next interval)

### 2. Domain Events + Outbox Pattern Integration

**Pattern:** Publish events transactionally with Purchase entity

```csharp
public class DcaExecutionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher; // OutboxEventPublisher

    public async Task ExecuteBuyCycle(CancellationToken ct)
    {
        // ... price analysis and order execution ...

        var purchase = new Purchase
        {
            AmountUsd = purchaseAmount,
            BtcPrice = orderResult.ExecutedPrice,
            BtcQuantity = orderResult.Quantity,
            Multiplier = multiplier.FinalMultiplier,
            HyperliquidOrderId = orderResult.OrderId
        };

        await _dbContext.Purchases.AddAsync(purchase, ct);

        // Publish event via outbox (stored in same transaction)
        await _eventPublisher.PublishAsync(
            new BuyOrderExecutedDomainEvent(purchase.Id), ct);

        // Atomic commit: Purchase + OutboxMessage
        await _dbContext.SaveChangesAsync(ct);
    }
}
```

**Why this pattern:**
- Guarantees event delivery (transactional outbox)
- Decouples execution from notification (async processing)
- Allows multiple handlers (Telegram, logging, metrics)

### 3. Distributed Lock Integration

**Pattern:** Use IDistributedLock to prevent concurrent DCA executions

```csharp
// In DcaSchedulerBackgroundService
await using var lockHandle = await _lock.AcquireLockAsync(
    "dca-execution-lock", TimeSpan.FromMinutes(5), ct);

if (!lockHandle.Success)
{
    _logger.LogWarning("DCA execution already in progress, skipping");
    return;
}
```

**Why this pattern:**
- Prevents double-buying if multiple instances are deployed
- Safe for horizontal scaling
- Redis-backed lock survives service restarts

### 4. EF Core + PostgreSQL Integration

**Pattern:** Extend ApplicationDbContext with new entities

```csharp
public class ApplicationDbContext : DbContext
{
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<DailyPrice> DailyPrices { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; } // From BuildingBlocks

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Configure outbox pattern
        builder.ApplyOutboxMessageConfiguration();

        // Configure Purchase entity
        builder.Entity<Purchase>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.AmountUsd).HasColumnType("decimal(18,2)");
            entity.Property(p => p.BtcPrice).HasColumnType("decimal(18,2)");
            entity.Property(p => p.BtcQuantity).HasColumnType("decimal(18,8)");
            entity.HasIndex(p => p.Timestamp);
        });

        // Configure DailyPrice entity
        builder.Entity<DailyPrice>(entity =>
        {
            entity.HasKey(p => new { p.Symbol, p.Date });
            entity.Property(p => p.Close).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.Date);
        });
    }
}
```

**Why this pattern:**
- Single database for all data (purchases, price cache, outbox)
- Atomic transactions across entities
- EF Core migrations track schema evolution

### 5. Redis Cache Integration

**Pattern:** Cache 30-day and 200-day price data to reduce API calls

```csharp
public class PriceDataService
{
    private readonly IDistributedCache _cache;
    private readonly HyperliquidApiClient _api;
    private readonly ApplicationDbContext _dbContext;

    public async Task<PriceAnalysis> GetCurrentPriceAnalysis(CancellationToken ct)
    {
        var currentPrice = await _api.GetCurrentPrice("BTC", ct);

        // Try cache for 30-day high
        var thirtyDayHigh = await _cache.GetStringAsync("btc-30d-high", ct);
        if (thirtyDayHigh == null)
        {
            // Cache miss: calculate from database or API
            thirtyDayHigh = await CalculateThirtyDayHigh(ct);
            await _cache.SetStringAsync("btc-30d-high", thirtyDayHigh,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, ct);
        }

        // Similar pattern for 200-day MA
        // ...

        return new PriceAnalysis(currentPrice, ...);
    }
}
```

**Why this pattern:**
- Reduces Hyperliquid API calls (rate limits, cost)
- Fast lookups for frequently accessed data
- Automatic expiration and refresh

### 6. Telegram Notification Integration

**Pattern:** Domain event handlers trigger notifications

```csharp
public class BuyOrderExecutedHandler : INotificationHandler<BuyOrderExecutedDomainEvent>
{
    private readonly TelegramNotificationService _telegram;
    private readonly ApplicationDbContext _dbContext;

    public async Task Handle(BuyOrderExecutedDomainEvent evt, CancellationToken ct)
    {
        var purchase = await _dbContext.Purchases.FindAsync(evt.PurchaseId, ct);

        var message = $"""
            ✅ BTC Buy Executed
            Amount: ${purchase.AmountUsd:F2} ({purchase.Multiplier:F1}x multiplier)
            Price: ${purchase.BtcPrice:F2}
            Quantity: {purchase.BtcQuantity:F8} BTC
            Drop from 30d high: {purchase.DropFromHighPct:F1}%
            Below 200 MA: {(purchase.Below200Ma ? "Yes (bear market boost)" : "No")}
            Order ID: {purchase.HyperliquidOrderId}
            """;

        await _telegram.SendMessageAsync(message, ct);
    }
}
```

**Why this pattern:**
- Decoupled from execution logic
- Can add multiple notification channels (email, webhook)
- Async processing via outbox (doesn't block buy execution)

## Suggested Build Order

The following order respects dependency chains and enables incremental validation.

### Phase 1: Foundation (Core Infrastructure)

**Build First:**
1. **Domain Models** (no dependencies)
   - Purchase entity
   - DailyPrice entity
   - Value objects: DcaConfiguration, DropTier, PriceAnalysis, MultiplierResult
   - Domain events: DcaCycleStartedDomainEvent, BuyOrderExecutedDomainEvent, etc.

2. **ApplicationDbContext** (depends on: domain models, BuildingBlocks)
   - Configure Purchase and DailyPrice entities
   - Include OutboxMessage configuration
   - Create initial EF Core migration

3. **Database Migration** (depends on: ApplicationDbContext)
   - Run migration to create schema
   - Verify tables created: Purchases, DailyPrices, OutboxMessages

**Validation:** Can persist Purchase entities and query database

### Phase 2: External Integration (Hyperliquid API)

**Build Next:**
4. **HyperliquidApiClient** (depends on: HttpClient, configuration)
   - Implement GetCurrentPrice()
   - Implement GetHistoricalCandles()
   - Implement ExecuteSpotBuy()
   - Add authentication (API key/wallet signature)
   - Add error handling and retry policies

5. **Integration Tests** (depends on: HyperliquidApiClient)
   - Test connection to Hyperliquid testnet
   - Verify price fetching
   - Test spot buy execution (small amount)

**Validation:** Can fetch real price data and execute test orders

### Phase 3: Business Logic (Price Analysis + Multipliers)

**Build Next:**
6. **MultiplierCalculator** (depends on: value objects, DcaConfiguration)
   - Implement drop-from-high calculation
   - Implement tier mapping
   - Implement bear market boost
   - Unit tests for all scenarios

7. **PriceDataService** (depends on: HyperliquidApiClient, ApplicationDbContext, Redis)
   - Implement current price fetching
   - Implement 30-day high calculation (with caching)
   - Implement 200-day MA calculation (with caching)
   - Return PriceAnalysis value object

**Validation:** Can calculate correct multipliers for test scenarios

### Phase 4: Execution Workflow (DCA Engine)

**Build Next:**
8. **DcaExecutionService** (depends on: all previous components)
   - Orchestrate full buy cycle
   - Integrate price analysis → multiplier calculation → order execution
   - Persist Purchase entity
   - Publish domain events via outbox

9. **Domain Event Handlers** (depends on: domain events, TelegramNotificationService)
   - BuyOrderExecutedHandler
   - BuyOrderFailedHandler
   - Log important metrics

**Validation:** Can execute manual buy cycle via API endpoint or test harness

### Phase 5: Scheduling (Background Service)

**Build Next:**
10. **DcaSchedulerBackgroundService** (depends on: DcaExecutionService, TimeBackgroundService)
    - Implement time-based scheduling (configurable cron)
    - Integrate distributed lock
    - Handle errors gracefully

11. **Configuration Management** (depends on: appsettings.json)
    - DcaConfiguration: base amount, schedule, tiers
    - Hyperliquid API credentials
    - Telegram credentials

**Validation:** Can run automated daily buys

### Phase 6: Observability (Logging + Notifications)

**Build Last:**
12. **Telegram Notifications** (depends on: domain event handlers)
    - Rich notifications with all purchase details
    - Error notifications

13. **Enhanced Logging** (depends on: Serilog)
    - Structured logging for each step
    - Performance metrics
    - Audit trail

**Validation:** Receive Telegram alerts for every buy

## Dependencies Between Components

```
Purchase, DailyPrice, Value Objects (no dependencies)
    ↓
ApplicationDbContext (depends on: entities, BuildingBlocks)
    ↓
HyperliquidApiClient (depends on: HttpClient)
    ↓
MultiplierCalculator (depends on: value objects)
    ↓
PriceDataService (depends on: HyperliquidApiClient, ApplicationDbContext, Redis)
    ↓
DcaExecutionService (depends on: PriceDataService, MultiplierCalculator, HyperliquidApiClient, ApplicationDbContext, OutboxEventPublisher)
    ↓
Domain Event Handlers (depends on: TelegramNotificationService, ApplicationDbContext)
    ↓
DcaSchedulerBackgroundService (depends on: DcaExecutionService, TimeBackgroundService, IDistributedLock)
```

## Architecture Patterns to Follow

### Pattern 1: Command-Query Separation

**What:** Separate read operations (price queries) from write operations (order execution)

**When:** All price data fetching vs. order placement

**Example:**
```csharp
// Query: Read-only, cacheable
public class PriceDataService
{
    public Task<PriceAnalysis> GetCurrentPriceAnalysis(); // Query
}

// Command: State-changing, transactional
public class DcaExecutionService
{
    public Task ExecuteBuyCycle(); // Command
}
```

### Pattern 2: Value Objects for Immutability

**What:** Use immutable value objects for calculations and analysis

**When:** Price analysis, multiplier results, configuration

**Example:**
```csharp
public record PriceAnalysis(
    decimal CurrentPrice,
    decimal ThirtyDayHigh,
    decimal TwoHundredDayMa,
    decimal DropFromHighPct,
    bool IsBelowMa
)
{
    // All properties are init-only, immutable
}
```

### Pattern 3: Repository Pattern (via EF Core DbContext)

**What:** Encapsulate data access behind DbContext

**When:** All database operations

**Example:**
```csharp
// DbContext IS the repository
public class ApplicationDbContext : DbContext
{
    public DbSet<Purchase> Purchases { get; set; }

    // Use LINQ directly in services:
    var totalBtc = await _dbContext.Purchases.SumAsync(p => p.BtcQuantity);
}
```

### Pattern 4: Transactional Outbox

**What:** Publish domain events atomically with entity changes

**When:** Every state change that requires notification

**Example:**
```csharp
// Single transaction for entity + event
await _dbContext.Purchases.AddAsync(purchase);
await _eventPublisher.PublishAsync(new BuyOrderExecutedDomainEvent(purchase.Id));
await _dbContext.SaveChangesAsync(); // Commits both
```

### Pattern 5: Distributed Lock for Idempotency

**What:** Prevent concurrent executions of the same operation

**When:** Daily DCA cycle execution

**Example:**
```csharp
await using var lockHandle = await _lock.AcquireLockAsync(
    "dca-execution-lock", TimeSpan.FromMinutes(5));

if (!lockHandle.Success) return; // Another instance already running
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Tight Coupling to Hyperliquid API

**What:** Directly calling Hyperliquid API from business logic

**Why bad:** Cannot swap exchanges, difficult to test, violates separation of concerns

**Instead:** Use IExchangeClient abstraction with Hyperliquid implementation

```csharp
// BAD
public class DcaExecutionService
{
    public async Task ExecuteBuyCycle()
    {
        var price = await HttpClient.GetAsync("https://api.hyperliquid.xyz/info");
        // Tightly coupled to Hyperliquid
    }
}

// GOOD
public interface IExchangeClient
{
    Task<decimal> GetCurrentPrice(string symbol);
    Task<OrderResult> ExecuteSpotBuy(string symbol, decimal amountUsd);
}

public class HyperliquidApiClient : IExchangeClient
{
    // Hyperliquid-specific implementation
}
```

### Anti-Pattern 2: Direct Telegram Calls from Execution Service

**What:** Calling TelegramNotificationService directly from DcaExecutionService

**Why bad:** Couples execution to notification, can't add other notification channels, blocks execution on Telegram API

**Instead:** Use domain events + async handlers

```csharp
// BAD
public async Task ExecuteBuyCycle()
{
    var purchase = await ExecuteBuy();
    await _telegram.SendMessage(purchase); // Coupled + blocking
}

// GOOD
public async Task ExecuteBuyCycle()
{
    var purchase = await ExecuteBuy();
    await _eventPublisher.PublishAsync(new BuyOrderExecutedDomainEvent(purchase.Id));
    // Notification happens asynchronously via event handler
}
```

### Anti-Pattern 3: Storing Calculated Values Without Source Data

**What:** Only storing final multiplier without drop percentage or 200 MA flag

**Why bad:** Cannot audit why multiplier was chosen, difficult to debug or analyze historical decisions

**Instead:** Store all inputs and outputs

```csharp
// BAD
public class Purchase
{
    public decimal Multiplier { get; set; } // 2.25x - but why?
}

// GOOD
public class Purchase
{
    public decimal Multiplier { get; set; } // 2.25x
    public decimal DropFromHighPct { get; set; } // 12.3%
    public bool Below200Ma { get; set; } // true
    // Can reconstruct why multiplier was 2.25x (1.5x tier * 1.5x bear boost)
}
```

### Anti-Pattern 4: Synchronous HTTP Calls in Background Service

**What:** Using synchronous HttpClient.Get() instead of async

**Why bad:** Blocks thread pool, reduces scalability, can cause deadlocks

**Instead:** Use async/await throughout

```csharp
// BAD
protected override void ProcessAsync(CancellationToken ct)
{
    var price = HttpClient.Get("...").Result; // Blocking!
}

// GOOD
protected override async Task ProcessAsync(CancellationToken ct)
{
    var price = await HttpClient.GetAsync("...", ct); // Async
}
```

### Anti-Pattern 5: Hardcoding Schedule in Code

**What:** Hardcoding "10:00 AM daily" in background service

**Why bad:** Requires redeployment to change schedule, cannot A/B test different times

**Instead:** Load from configuration

```csharp
// BAD
private readonly TimeSpan Interval = TimeSpan.FromDays(1);
private readonly TimeOnly ExecutionTime = new TimeOnly(10, 0);

// GOOD
private readonly DcaConfiguration _config; // Loaded from appsettings.json

protected override TimeSpan Interval => CalculateNextInterval(_config.ScheduleCron);
```

## Scalability Considerations

| Concern | At Single Instance | At Multiple Instances | At High Volume |
|---------|-------------------|----------------------|----------------|
| **Concurrent Execution** | No issue (single service) | Use distributed lock to ensure only one instance executes DCA at a time | Same distributed lock approach |
| **Database Connections** | Single connection pool (default 100) | Each instance has own pool, PostgreSQL handles concurrency | Increase max connections, use read replicas for price queries |
| **Hyperliquid API Rate Limits** | Unlikely to hit (1 buy/day + few price queries) | Same (lock ensures sequential execution) | Implement rate limiter, queue requests |
| **Outbox Processing** | Single background service polls every 5 seconds | Each instance has own outbox processor (all process same messages, idempotent) | Increase batch size (default 100), decrease interval |
| **Price Cache Freshness** | Redis cache with 1-hour TTL | Shared Redis cache across instances | Decrease TTL for more frequent updates |
| **Telegram Rate Limits** | Unlikely to hit (1 message/day) | Same | Batch notifications or implement message queue |

## Configuration Structure

```json
{
  "Dca": {
    "BaseAmountUsd": 100,
    "ScheduleCron": "0 10 * * *",
    "DropTiers": [
      { "MinDropPct": 0, "MaxDropPct": 5, "Multiplier": 1.0 },
      { "MinDropPct": 5, "MaxDropPct": 10, "Multiplier": 1.5 },
      { "MinDropPct": 10, "MaxDropPct": 20, "Multiplier": 2.0 },
      { "MinDropPct": 20, "MaxDropPct": 100, "Multiplier": 3.0 }
    ],
    "BearMarketMultiplier": 1.5
  },
  "Hyperliquid": {
    "ApiUrl": "https://api.hyperliquid.xyz",
    "ApiKey": "your-api-key",
    "WalletAddress": "0x...",
    "PrivateKey": "your-private-key"
  },
  "Telegram": {
    "BotToken": "your-bot-token",
    "ChatId": "your-chat-id"
  },
  "ConnectionStrings": {
    "Database": "Host=localhost;Database=tradingbotdb;Username=postgres;Password=***"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Error Handling Strategy

**Strategy:** Graceful degradation with retry and notification

**Patterns:**

1. **API Failures:** Retry with exponential backoff (Polly)
   - If Hyperliquid API is down, retry 3 times with 1s, 2s, 4s delays
   - If all retries fail, publish BuyOrderFailedDomainEvent → Telegram alert

2. **Price Data Unavailable:** Use cached data or skip cycle
   - If cannot fetch current price, use last known price from database (with staleness check)
   - If data is too stale (>1 hour), skip cycle and alert user

3. **Order Execution Failure:** Log and alert, do not retry automatically
   - Market orders can partially fill or reject (insufficient balance, etc.)
   - Do NOT retry automatically (risk double-buying)
   - Publish BuyOrderFailedDomainEvent with full error details

4. **Database Failures:** Fail fast, rely on next cycle
   - If cannot persist Purchase, rollback transaction
   - Do NOT execute buy if cannot persist (risk losing record)
   - Alert user via fallback mechanism (Serilog → console → monitoring)

5. **Lock Acquisition Failure:** Skip cycle gracefully
   - If cannot acquire distributed lock (another instance running), log and return
   - Next cycle will retry

## Monitoring and Observability

**Key Metrics to Track:**

1. **DCA Execution Metrics:**
   - `dca.cycle.started` (count)
   - `dca.cycle.completed` (count)
   - `dca.cycle.failed` (count)
   - `dca.cycle.duration` (histogram)
   - `dca.purchase.amount_usd` (gauge)
   - `dca.purchase.btc_quantity` (gauge)
   - `dca.purchase.multiplier` (gauge)

2. **Hyperliquid API Metrics:**
   - `hyperliquid.api.request.count` (count)
   - `hyperliquid.api.request.duration` (histogram)
   - `hyperliquid.api.error.count` (count)

3. **Outbox Processing Metrics:**
   - `outbox.messages.pending` (gauge)
   - `outbox.messages.published` (count)
   - `outbox.messages.failed` (count)
   - `outbox.processing.lag` (gauge - time since oldest message)

4. **Price Data Metrics:**
   - `price.cache.hit_rate` (gauge)
   - `price.current` (gauge)
   - `price.30d_high` (gauge)
   - `price.200d_ma` (gauge)

**Implementation:**
- Use OpenTelemetry metrics API
- Export to Prometheus or Application Insights
- Create dashboards for monitoring
- Set up alerts for critical failures

## Testing Strategy

**Unit Tests:**
- MultiplierCalculator (pure logic, no dependencies)
- Value objects (PriceAnalysis, MultiplierResult)
- Domain events

**Integration Tests:**
- HyperliquidApiClient (hit testnet API)
- ApplicationDbContext (in-memory SQLite or test PostgreSQL)
- PriceDataService (with mock API client)

**End-to-End Tests:**
- Full buy cycle with test Hyperliquid account
- Verify Purchase entity persisted
- Verify domain event published
- Verify Telegram notification sent

**Manual Testing:**
- Deploy to staging environment
- Run daily cycle for 1 week
- Monitor logs and notifications
- Verify calculations are correct

## Security Considerations

1. **API Key Management:**
   - Store Hyperliquid credentials in Azure Key Vault or AWS Secrets Manager
   - Never commit to git
   - Rotate keys periodically

2. **Database Security:**
   - Use connection string with minimal permissions (INSERT, SELECT on specific tables)
   - Enable SSL/TLS for PostgreSQL connection
   - Encrypt sensitive data at rest (if storing wallet private keys)

3. **Telegram Security:**
   - Restrict bot to specific chat ID (user's private chat)
   - Validate Telegram webhook signatures (if using webhooks in future)

4. **API Security:**
   - If exposing API endpoints, add authentication (JWT, API keys)
   - Currently CORS allows all origins (HIGH RISK for production)
   - Restrict to internal network or disable public access

## Next Steps After Architecture

1. **Review and Approve Architecture**
   - Validate component boundaries make sense
   - Confirm build order is correct
   - Identify any missing components

2. **Set Up Development Environment**
   - Configure Hyperliquid testnet account
   - Set up PostgreSQL and Redis locally
   - Configure Telegram bot for testing

3. **Begin Phase 1: Foundation**
   - Implement domain models
   - Create ApplicationDbContext
   - Run initial migration

4. **Research Hyperliquid API Specifics**
   - Authentication mechanism (API key vs. wallet signature)
   - Spot buy order format
   - Rate limits and best practices
   - WebSocket for real-time price (optional optimization)

---

**Architecture confidence: HIGH**

This architecture leverages existing BuildingBlocks infrastructure effectively, maintains clear boundaries, and provides a solid foundation for reliable DCA execution. The suggested build order respects dependencies and enables incremental validation at each phase.

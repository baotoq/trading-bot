# Architecture Research: DDD Tactical Patterns Integration

**Domain:** .NET 10.0 trading bot with EF Core + MediatR + Dapr outbox
**Researched:** 2026-02-14
**Confidence:** HIGH

## Executive Summary

This research focuses on integrating DDD tactical patterns (rich aggregates, value objects, strongly-typed IDs, domain event dispatch, Result pattern, Specification pattern) into an existing .NET 10.0 architecture with EF Core, MediatR, and Dapr-based outbox pattern.

The current architecture already has foundation pieces in place:
- **Entity hierarchy**: `AuditedEntity` → `BaseEntity` (with UUIDv7 IDs)
- **Event infrastructure**: `IDomainEvent` (MediatR-based), `IntegrationEvent` (Dapr pub-sub), outbox pattern for reliable delivery
- **Data access**: EF Core with DbContext, entity configurations, string-backed enums, JSONB columns

Key integration points:
1. **Aggregate roots** will replace current anemic entities, adding behavior and invariant enforcement
2. **Domain events** will be collected within aggregates and dispatched via EF Core `SaveChangesInterceptor`
3. **Value objects** will use EF Core owned entities and value converters
4. **Strongly-typed IDs** will leverage EF Core value converters (built-in since EF Core 7)
5. **Result pattern** will replace exceptions for expected domain failures
6. **Specification pattern** will encapsulate complex queries as reusable, composable objects

The integration is **evolutionary, not revolutionary** — existing patterns are enhanced, not replaced.

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │  Endpoints  │  │  Dashboard  │  │   Jobs      │          │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │
├─────────┴─────────────────┴─────────────────┴────────────────┤
│                   Application Layer                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Services   │  │   Handlers   │  │ Orchestrators│       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
├─────────┴──────────────────┴──────────────────┴──────────────┤
│                     Domain Layer (NEW)                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │  Aggregates  │  │    Value     │  │  Domain      │       │
│  │  (Entities)  │  │   Objects    │  │  Events      │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                  │                  │               │
│  ┌──────┴──────────────────┴──────────────────┴──────┐       │
│  │         Domain Services & Specifications          │       │
│  └────────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                 Infrastructure Layer                         │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │  EF Core   │  │   MediatR  │  │    Dapr    │             │
│  │  DbContext │  │  Dispatch  │  │   Outbox   │             │
│  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘             │
├────────┴────────────────┴────────────────┴────────────────────┤
│                    Persistence Layer                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                   │
│  │PostgreSQL│  │   Redis  │  │   Dapr   │                   │
│  └──────────┘  └──────────┘  └──────────┘                   │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Integration with Existing |
|-----------|----------------|---------------------------|
| **Aggregate Root** | Enforce business invariants, collect domain events, coordinate child entities | Replace anemic entities (Purchase, DcaConfiguration), inherit from BaseEntity |
| **Value Objects** | Immutable domain concepts with equality by value | Use EF Core owned entities or value converters, replace primitive obsession |
| **Strongly-Typed IDs** | Type-safe entity identifiers | Wrap Guid in record structs, use EF Core value converters |
| **Domain Events** | Internal aggregate state changes | Collected in aggregate, dispatched via SaveChangesInterceptor → MediatR |
| **Integration Events** | Cross-service communication | Keep existing IntegrationEvent/Dapr/outbox pattern unchanged |
| **Specifications** | Reusable query filters | IQueryable<T> extensions, composable via AND/OR/NOT |
| **Result<T>** | Explicit success/failure without exceptions | Application layer return types, replace throws for expected failures |
| **SaveChangesInterceptor** | Dispatch domain events on SaveChanges | New EF Core interceptor, hooks into existing DbContext |

## Integration with Existing Architecture

### Current State → Future State Mapping

| Current Component | DDD Enhancement | Change Type |
|-------------------|-----------------|-------------|
| `BaseEntity` | Add `List<IDomainEvent> DomainEvents` property | **MODIFY** — add domain event collection |
| `Purchase` entity | Convert to `PurchaseAggregate` with business methods | **MODIFY** — add behavior, keep inheritance |
| `DcaConfiguration` entity | Convert to `DcaConfigurationAggregate` with validation | **MODIFY** — add invariant enforcement |
| `TradingBotDbContext` | Add `SaveChangesInterceptor` for event dispatch | **MODIFY** — register interceptor in Program.cs |
| `IDomainEvent` interface | Keep unchanged | **UNCHANGED** — already correct abstraction |
| `IntegrationEvent` + outbox | Keep unchanged | **UNCHANGED** — handles cross-service events |
| Services dispatch events | Aggregates raise events internally | **MODIFY** — move event raising into aggregates |
| Exception-based errors | Return `Result<T>` from domain operations | **NEW** — add Result pattern library/implementation |
| LINQ queries in services | Use `Specification<T>` pattern | **NEW** — add specification infrastructure |
| Primitive properties | Wrap in value objects | **NEW** — Money, Percentage, Price, Quantity value objects |
| `Guid` IDs | Wrap in strongly-typed IDs | **NEW** — PurchaseId, ConfigurationId record structs |

### Data Flow: Current vs Enhanced

**CURRENT: Service-Driven Event Publishing**
```
DcaExecutionService.ExecuteDailyPurchaseAsync()
    ↓
Create Purchase entity (anemic)
    ↓
dbContext.Purchases.Add(purchase)
    ↓
await dbContext.SaveChangesAsync()
    ↓
await publisher.Publish(new PurchaseCompletedEvent(...)) ← SERVICE dispatches event
```

**ENHANCED: Aggregate-Driven Event Collection**
```
DcaExecutionService.ExecuteDailyPurchaseAsync()
    ↓
PurchaseAggregate.CreatePurchase(...) → returns Result<Purchase>
    ↓ (inside aggregate)
    AddDomainEvent(new PurchaseCompletedEvent(...)) ← AGGREGATE raises event
    ↓
dbContext.Purchases.Add(purchase)
    ↓
await dbContext.SaveChangesAsync()
    ↓ (SaveChangesInterceptor triggered)
    Dispatch all purchase.DomainEvents via MediatR ← AUTOMATIC dispatch
```

**Key Benefits:**
- Domain events tied to aggregate lifecycle (atomic with database commit)
- Service layer simplified (no manual event publishing)
- Events dispatched BEFORE SaveChanges completes (transactional consistency)
- Failed save = events never dispatched (no orphaned events)

## New Components Needed

### 1. Domain Event Infrastructure

**Location:** `TradingBot.ApiService/BuildingBlocks/Domain/`

**Files:**
```
BuildingBlocks/Domain/
├── IAggregate.cs                    # Marker interface for aggregates
├── AggregateRoot.cs                 # Base class with domain event collection
├── DomainEventDispatcher.cs         # Interceptor to dispatch events on SaveChanges
└── DomainEventsExtensions.cs        # DbContext configuration extensions
```

**Implementation:**
```csharp
// IAggregate.cs
public interface IAggregate
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

// AggregateRoot.cs
public abstract class AggregateRoot : BaseEntity, IAggregate
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent @event)
    {
        _domainEvents.Add(@event);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// DomainEventDispatcher.cs (SaveChangesInterceptor)
public class DomainEventDispatcher : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    public DomainEventDispatcher(IPublisher publisher) => _publisher = publisher;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return result;

        // Collect domain events from all tracked aggregates
        var aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Dispatch events BEFORE saving (transactional consistency)
        foreach (var @event in events)
        {
            await _publisher.Publish(@event, cancellationToken);
        }

        // Clear events after dispatch
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
```

**Registration in Program.cs:**
```csharp
builder.Services.AddDbContext<TradingBotDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<DomainEventDispatcher>());
});

builder.Services.AddScoped<DomainEventDispatcher>();
```

### 2. Value Objects

**Location:** `TradingBot.ApiService/Domain/ValueObjects/`

**Files:**
```
Domain/ValueObjects/
├── Money.cs                         # Money(decimal Amount, string Currency)
├── Percentage.cs                    # Percentage(decimal Value)
├── Price.cs                         # Price(decimal Value, string Symbol)
├── Quantity.cs                      # Quantity(decimal Value, int Precision)
└── ValueObjectExtensions.cs         # EF Core value converter registration
```

**Example Implementation:**
```csharp
// Money.cs
public record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero => new(0m, "USD");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} and {other.Currency}");

        return new Money(Amount + other.Amount, Currency);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator *(Money money, decimal multiplier) =>
        new Money(money.Amount * multiplier, money.Currency);
}

// EF Core configuration
public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        // Convert Money value object to single decimal column
        builder.Property(p => p.Cost)
            .HasConversion(
                v => v.Amount,                    // To database
                v => new Money(v, "USD"))         // From database
            .HasPrecision(18, 2);
    }
}
```

### 3. Strongly-Typed IDs

**Location:** `TradingBot.ApiService/Domain/Ids/`

**Files:**
```
Domain/Ids/
├── PurchaseId.cs                    # record struct PurchaseId(Guid Value)
├── ConfigurationId.cs               # record struct ConfigurationId(Guid Value)
├── IngestionJobId.cs                # record struct IngestionJobId(Guid Value)
└── StronglyTypedIdExtensions.cs     # EF Core value converter registration
```

**Example Implementation:**
```csharp
// PurchaseId.cs
public readonly record struct PurchaseId(Guid Value)
{
    public static PurchaseId New() => new(Guid.CreateVersion7());
    public static PurchaseId NewDeterministic(DateTimeOffset timestamp) =>
        new(Guid.CreateVersion7(timestamp));

    public override string ToString() => Value.ToString();
}

// EF Core configuration (automatic since EF Core 7)
public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.HasKey(p => p.Id);

        // EF Core 7+ automatically handles record struct conversions
        builder.Property(p => p.Id)
            .HasConversion<PurchaseId>();
    }
}
```

### 4. Result Pattern

**Location:** `TradingBot.ApiService/BuildingBlocks/Results/`

**Files:**
```
BuildingBlocks/Results/
├── Result.cs                        # Result (no value)
├── Result_T.cs                      # Result<TValue>
├── Error.cs                         # Error(string Code, string Message)
└── ResultExtensions.cs              # Match, Bind, Map methods
```

**Example Implementation:**
```csharp
// Result<T>.cs
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

// Error.cs
public record Error(string Code, string Message)
{
    public static Error None => new(string.Empty, string.Empty);
    public static Error NullValue => new("Error.NullValue", "Value cannot be null");

    // Domain-specific errors
    public static Error InsufficientBalance(decimal required, decimal available) =>
        new("Purchase.InsufficientBalance",
            $"Insufficient balance: required {required}, available {available}");
}

// Usage in aggregate
public Result<Purchase> CreatePurchase(
    decimal quantity, decimal price, decimal multiplier)
{
    if (quantity <= 0)
        return Result<Purchase>.Failure(
            new Error("Purchase.InvalidQuantity", "Quantity must be positive"));

    var purchase = new Purchase(quantity, price, multiplier);
    AddDomainEvent(new PurchaseCompletedEvent(purchase.Id, ...));

    return Result<Purchase>.Success(purchase);
}
```

### 5. Specification Pattern

**Location:** `TradingBot.ApiService/Domain/Specifications/`

**Files:**
```
Domain/Specifications/
├── ISpecification.cs                # Interface with ToExpression()
├── Specification.cs                 # Base class with combinators
├── CompositeSpecification.cs        # AND/OR/NOT implementations
└── SpecificationExtensions.cs       # IQueryable<T>.Where(spec) extension
```

**Example Implementation:**
```csharp
// ISpecification<T>.cs
public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();
    bool IsSatisfiedBy(T entity);
}

// Specification<T>.cs
public abstract class Specification<T> : ISpecification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity) => ToExpression().Compile()(entity);

    public Specification<T> And(Specification<T> other) =>
        new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other) =>
        new OrSpecification<T>(this, other);

    public Specification<T> Not() => new NotSpecification<T>(this);
}

// Example domain specification
public class FilledPurchasesSpecification : Specification<Purchase>
{
    public override Expression<Func<Purchase, bool>> ToExpression() =>
        p => p.Status == PurchaseStatus.Filled;
}

public class PurchasesInDateRangeSpecification : Specification<Purchase>
{
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public PurchasesInDateRangeSpecification(DateTimeOffset start, DateTimeOffset end)
    {
        _start = start;
        _end = end;
    }

    public override Expression<Func<Purchase, bool>> ToExpression() =>
        p => p.ExecutedAt >= _start && p.ExecutedAt < _end;
}

// Usage in service
var spec = new FilledPurchasesSpecification()
    .And(new PurchasesInDateRangeSpecification(startDate, endDate));

var purchases = await dbContext.Purchases
    .Where(spec.ToExpression())
    .ToListAsync();
```

## Architectural Patterns

### Pattern 1: Aggregate Root with Invariant Enforcement

**What:** Entity that enforces business rules and coordinates child entities through public methods (not property setters).

**When to use:** When entity has business logic beyond simple CRUD, multiple related entities must maintain consistency, or state transitions require validation.

**Trade-offs:**
- **Pros:** Encapsulation, invariant guarantees, testable business logic
- **Cons:** More boilerplate, DbContext can't track changes automatically (must use methods)

**Example:**
```csharp
public class Purchase : AggregateRoot
{
    // Private setters prevent external mutation
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public PurchaseStatus Status { get; private set; }

    // Factory method enforces creation rules
    public static Result<Purchase> Create(
        decimal quantity, decimal price, decimal multiplier)
    {
        if (quantity <= 0)
            return Result<Purchase>.Failure(
                new Error("Purchase.InvalidQuantity", "Quantity must be positive"));

        if (price <= 0)
            return Result<Purchase>.Failure(
                new Error("Purchase.InvalidPrice", "Price must be positive"));

        var purchase = new Purchase
        {
            Quantity = quantity,
            Price = price,
            Multiplier = multiplier,
            Status = PurchaseStatus.Pending
        };

        purchase.AddDomainEvent(new PurchaseCreatedEvent(purchase.Id));

        return Result<Purchase>.Success(purchase);
    }

    // Business method with invariant enforcement
    public Result MarkAsFilled(decimal filledQuantity, decimal avgPrice)
    {
        if (Status != PurchaseStatus.Pending)
            return Result.Failure(
                new Error("Purchase.InvalidStatus",
                    $"Cannot fill purchase in status {Status}"));

        Quantity = filledQuantity;
        Price = avgPrice;
        Status = filledQuantity >= Quantity * 0.95m
            ? PurchaseStatus.Filled
            : PurchaseStatus.PartiallyFilled;

        AddDomainEvent(new PurchaseFilledEvent(Id, Quantity, Price));

        return Result.Success();
    }
}
```

### Pattern 2: Value Object for Domain Concepts

**What:** Immutable object defined by its attributes (not identity), with domain-specific operations.

**When to use:** When concept has no identity (equality by value), is immutable, or encapsulates validation/operations (Money, Percentage, Email).

**Trade-offs:**
- **Pros:** Immutability, compile-time type safety, domain vocabulary
- **Cons:** Slightly more verbose, EF Core mapping required

**Example:**
```csharp
public record Multiplier
{
    public decimal Value { get; }

    private Multiplier(decimal value) => Value = value;

    public static Result<Multiplier> Create(decimal value)
    {
        if (value < 1.0m)
            return Result<Multiplier>.Failure(
                new Error("Multiplier.TooLow", "Multiplier must be >= 1.0"));

        if (value > 10.0m)
            return Result<Multiplier>.Failure(
                new Error("Multiplier.TooHigh", "Multiplier cannot exceed 10.0"));

        return Result<Multiplier>.Success(new Multiplier(value));
    }

    public Multiplier ApplyCap(decimal cap) =>
        new Multiplier(Math.Min(Value, cap));

    public static Multiplier operator *(Multiplier left, Multiplier right) =>
        new Multiplier(left.Value * right.Value);
}

// EF Core configuration
builder.Property(p => p.Multiplier)
    .HasConversion(
        v => v.Value,
        v => Multiplier.Create(v).Value)  // Safe because DB values already validated
    .HasPrecision(4, 2);
```

### Pattern 3: Domain Event Collection and Dispatch

**What:** Aggregates collect domain events during state changes, dispatcher publishes them before SaveChanges commits.

**When to use:** Always, for domain events that represent state changes within a single aggregate.

**Trade-offs:**
- **Pros:** Transactional consistency, automatic dispatch, no manual event publishing
- **Cons:** Events must be idempotent (handlers might see uncommitted state if SaveChanges fails)

**Example:**
```csharp
// In aggregate
public Result MarkAsFilled(decimal filledQuantity, decimal avgPrice)
{
    // ... validation ...

    Status = PurchaseStatus.Filled;
    AddDomainEvent(new PurchaseFilledEvent(Id, filledQuantity, avgPrice));

    return Result.Success();
}

// In service
var purchaseResult = Purchase.Create(quantity, price, multiplier);
if (!purchaseResult.IsSuccess)
    return purchaseResult.Error;

var purchase = purchaseResult.Value;
dbContext.Purchases.Add(purchase);

// Events dispatched automatically by SaveChangesInterceptor
await dbContext.SaveChangesAsync();

// Event handler (receives event BEFORE SaveChanges completes)
public class PurchaseFilledEventHandler : INotificationHandler<PurchaseFilledEvent>
{
    public async Task Handle(PurchaseFilledEvent @event, CancellationToken ct)
    {
        // Publish integration event to outbox for Telegram notification
        await _outboxPublisher.PublishAsync(
            new PurchaseCompletedIntegrationEvent(@event.PurchaseId, ...));
    }
}
```

### Pattern 4: Specification for Complex Queries

**What:** Encapsulate query logic as objects that can be composed, tested, and reused.

**When to use:** When query logic is reused across services, complex filters need to be composed, or business rules need to be unit tested.

**Trade-offs:**
- **Pros:** Reusability, composability, testability, domain vocabulary
- **Cons:** Additional abstraction, slightly more code

**Example:**
```csharp
// Reusable specifications
public class FilledPurchasesSpec : Specification<Purchase>
{
    public override Expression<Func<Purchase, bool>> ToExpression() =>
        p => p.Status == PurchaseStatus.Filled;
}

public class NonDryRunSpec : Specification<Purchase>
{
    public override Expression<Func<Purchase, bool>> ToExpression() =>
        p => !p.IsDryRun;
}

public class PurchasesAboveAmountSpec : Specification<Purchase>
{
    private readonly decimal _minAmount;

    public PurchasesAboveAmountSpec(decimal minAmount) => _minAmount = minAmount;

    public override Expression<Func<Purchase, bool>> ToExpression() =>
        p => p.Cost >= _minAmount;
}

// Composition in service
var spec = new FilledPurchasesSpec()
    .And(new NonDryRunSpec())
    .And(new PurchasesAboveAmountSpec(100m));

var purchases = await dbContext.Purchases
    .Where(spec.ToExpression())
    .OrderByDescending(p => p.ExecutedAt)
    .ToListAsync();
```

## Data Flow

### Domain Event Flow (Enhanced)

```
DcaExecutionService.ExecuteDailyPurchaseAsync()
    ↓
Purchase.Create(quantity, price, multiplier)
    ↓ (inside aggregate)
    [AddDomainEvent(new PurchaseCreatedEvent(...))]
    ↓
dbContext.Purchases.Add(purchase)
    ↓
purchase.MarkAsFilled(filledQty, avgPrice)
    ↓ (inside aggregate)
    [AddDomainEvent(new PurchaseFilledEvent(...))]
    ↓
await dbContext.SaveChangesAsync()
    ↓ (DomainEventDispatcher interceptor triggered)
    Collect all DomainEvents from tracked aggregates
    ↓
    Dispatch each event via MediatR (before commit)
        ↓
        PurchaseFilledEventHandler receives event
            ↓
            Publish integration event to outbox
    ↓
    Clear DomainEvents from aggregates
    ↓
    Commit transaction to database
    ↓ (OutboxMessageBackgroundService polls every 5 seconds)
    Fetch unprocessed outbox messages
    ↓
    Publish via DaprMessageBroker to pub-sub
```

**Key Timing:**
1. **Domain events dispatched BEFORE SaveChanges commits** (transactional consistency)
2. **Integration events added to outbox table in SAME transaction**
3. **Outbox processing happens AFTER transaction commits** (eventual consistency)

### Value Object Persistence Flow

```
Service creates aggregate with value objects
    ↓
var price = Price.Create(98_450.75m, "USD");
var quantity = Quantity.Create(0.05128m, 5);
    ↓
Purchase.Create(quantity, price, multiplier)
    ↓
dbContext.Purchases.Add(purchase)
    ↓
await dbContext.SaveChangesAsync()
    ↓ (EF Core value converters triggered)
    Convert Price → decimal (to database)
    Convert Quantity → decimal (to database)
    ↓
INSERT INTO Purchases (Id, Price, Quantity, ...)
    ↓ (on read)
    Convert decimal → Price (from database)
    Convert decimal → Quantity (from database)
```

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0-1k purchases | Current implementation sufficient. Single DbContext, in-process MediatR, outbox every 5 seconds. |
| 1k-10k purchases | Add database indexes on commonly queried specifications. Consider connection pooling tuning. Outbox processing might need concurrency control. |
| 10k-100k purchases | Add read replicas for queries. Specifications should use compiled queries. Consider event sourcing for Purchase aggregate (append-only). |
| 100k+ purchases | Vertical partitioning (separate DCA config from purchases). CQRS with separate read models. Replace outbox with distributed queue (Kafka/RabbitMQ). |

### Scaling Priorities

1. **First bottleneck:** Domain event dispatch overhead
   - **Fix:** Batch event publishing, use background queue instead of sync dispatch
   - **When:** >1000 purchases/day with complex event handlers

2. **Second bottleneck:** Specification query performance
   - **Fix:** Add indexes matching specification filters, use compiled queries
   - **When:** Query latency >100ms for dashboard endpoints

## Anti-Patterns

### Anti-Pattern 1: Aggregates Referencing Other Aggregates by Object

**What people do:** Load multiple aggregate roots and pass them by reference.
```csharp
public class Purchase : AggregateRoot
{
    public DcaConfiguration Configuration { get; set; }  // ❌ BAD
}
```

**Why it's wrong:** Breaks aggregate boundaries, creates implicit dependencies, makes transactions span multiple aggregates.

**Do this instead:** Reference by ID, use domain events for coordination.
```csharp
public class Purchase : AggregateRoot
{
    public ConfigurationId ConfigurationId { get; private set; }  // ✅ GOOD
}

// Coordination via domain events
public class DcaConfigurationUpdatedEvent : IDomainEvent
{
    public ConfigurationId ConfigurationId { get; }
}

public class DcaConfigurationUpdatedEventHandler : INotificationHandler<DcaConfigurationUpdatedEvent>
{
    public async Task Handle(DcaConfigurationUpdatedEvent @event, CancellationToken ct)
    {
        // Update affected purchases via separate transaction
        var purchases = await _dbContext.Purchases
            .Where(p => p.ConfigurationId == @event.ConfigurationId)
            .ToListAsync(ct);

        // ... apply updates ...
    }
}
```

### Anti-Pattern 2: Exposing Setters on Aggregate Properties

**What people do:** Public setters allow external code to bypass invariants.
```csharp
public class Purchase : AggregateRoot
{
    public PurchaseStatus Status { get; set; }  // ❌ BAD — anyone can change
}

// External code bypasses business rules
purchase.Status = PurchaseStatus.Filled;  // ❌ No validation!
```

**Why it's wrong:** Invariants can be violated, business logic scattered across services, domain events not raised.

**Do this instead:** Private setters, public methods encapsulate logic.
```csharp
public class Purchase : AggregateRoot
{
    public PurchaseStatus Status { get; private set; }  // ✅ GOOD

    public Result MarkAsFilled(decimal quantity, decimal price)
    {
        // Invariant: only pending purchases can be filled
        if (Status != PurchaseStatus.Pending)
            return Result.Failure(new Error("Purchase.InvalidStatus", "..."));

        Status = PurchaseStatus.Filled;
        Quantity = quantity;
        Price = price;

        AddDomainEvent(new PurchaseFilledEvent(Id, Quantity, Price));

        return Result.Success();
    }
}

// Service layer
var result = purchase.MarkAsFilled(filledQty, avgPrice);
if (!result.IsSuccess)
    _logger.LogWarning("Failed to mark purchase as filled: {Error}", result.Error);
```

### Anti-Pattern 3: Using Specifications for Everything

**What people do:** Create specifications for simple queries, even one-time uses.
```csharp
public class PurchaseByIdSpec : Specification<Purchase>
{
    public PurchaseByIdSpec(PurchaseId id) => _id = id;
    public override Expression<Func<Purchase, bool>> ToExpression() => p => p.Id == _id;
}

// Overkill for simple lookup
var purchase = await dbContext.Purchases
    .Where(new PurchaseByIdSpec(purchaseId).ToExpression())
    .FirstOrDefaultAsync();
```

**Why it's wrong:** Over-engineering, unnecessary abstraction, harder to read.

**Do this instead:** Use specifications for reusable, composable, or complex queries only.
```csharp
// Simple query — use LINQ directly ✅
var purchase = await dbContext.Purchases
    .FirstOrDefaultAsync(p => p.Id == purchaseId);

// Complex, reusable query — use specification ✅
var spec = new FilledPurchasesSpec()
    .And(new NonDryRunSpec())
    .And(new LastNDaysSpec(30));

var recentPurchases = await dbContext.Purchases
    .Where(spec.ToExpression())
    .ToListAsync();
```

### Anti-Pattern 4: Validating Value Objects in Aggregate Constructors

**What people do:** Pass primitive values to aggregate, validate inside constructor.
```csharp
public class Purchase : AggregateRoot
{
    public Purchase(decimal quantity, decimal price)  // ❌ BAD — primitives
    {
        if (quantity <= 0) throw new ArgumentException("...");  // ❌ Validation in wrong place
        if (price <= 0) throw new ArgumentException("...");

        Quantity = quantity;
        Price = price;
    }
}
```

**Why it's wrong:** Validation scattered, value object domain logic in aggregate, primitive obsession.

**Do this instead:** Validate in value object creation, aggregate receives valid objects.
```csharp
// Value objects validate themselves ✅
public record Quantity
{
    public decimal Value { get; }

    private Quantity(decimal value) => Value = value;

    public static Result<Quantity> Create(decimal value, int precision)
    {
        if (value <= 0)
            return Result<Quantity>.Failure(
                new Error("Quantity.Invalid", "Quantity must be positive"));

        var rounded = Math.Round(value, precision, MidpointRounding.ToZero);
        return Result<Quantity>.Success(new Quantity(rounded));
    }
}

// Aggregate receives validated value objects ✅
public class Purchase : AggregateRoot
{
    public Quantity Quantity { get; private set; }
    public Price Price { get; private set; }

    public static Result<Purchase> Create(Quantity quantity, Price price)
    {
        // No validation needed — value objects already valid
        var purchase = new Purchase
        {
            Quantity = quantity,
            Price = price
        };

        return Result<Purchase>.Success(purchase);
    }
}

// Service layer composes validation ✅
var quantityResult = Quantity.Create(0.05128m, 5);
var priceResult = Price.Create(98_450.75m, "USD");

if (!quantityResult.IsSuccess)
    return quantityResult.Error;
if (!priceResult.IsSuccess)
    return priceResult.Error;

var purchaseResult = Purchase.Create(quantityResult.Value, priceResult.Value);
```

## Build Order Considering Dependencies

### Phase 1: Foundation Components (No Dependencies)

**Order:** Build in parallel

1. **Result Pattern** (`BuildingBlocks/Results/`)
   - `Result.cs`, `Result_T.cs`, `Error.cs`, `ResultExtensions.cs`
   - **Why first:** No dependencies, used by everything else
   - **Testing:** Unit tests for Match, Bind, Map methods

2. **Value Objects** (`Domain/ValueObjects/`)
   - `Money.cs`, `Percentage.cs`, `Price.cs`, `Quantity.cs`
   - **Why first:** No dependencies except Result pattern
   - **Testing:** Unit tests for Create, validation, operations

3. **Strongly-Typed IDs** (`Domain/Ids/`)
   - `PurchaseId.cs`, `ConfigurationId.cs`, `IngestionJobId.cs`
   - **Why first:** No dependencies
   - **Testing:** Unit tests for New(), equality, conversions

### Phase 2: Domain Event Infrastructure (Depends on Phase 1)

**Order:** Sequential

4. **Aggregate Root Base Class** (`BuildingBlocks/Domain/`)
   - `IAggregate.cs`, `AggregateRoot.cs`
   - **Depends on:** `BaseEntity` (existing), `IDomainEvent` (existing)
   - **Changes:** Modify `BaseEntity` to add `List<IDomainEvent> DomainEvents`
   - **Testing:** Unit tests for AddDomainEvent, ClearDomainEvents

5. **Domain Event Dispatcher** (`BuildingBlocks/Domain/`)
   - `DomainEventDispatcher.cs` (SaveChangesInterceptor)
   - **Depends on:** `IAggregate`, `IPublisher` (MediatR)
   - **Testing:** Integration tests with in-memory DbContext

### Phase 3: Domain Model Refactoring (Depends on Phase 1 & 2)

**Order:** Sequential (one aggregate at a time)

6. **Purchase Aggregate** (`Models/Purchase.cs`)
   - Convert to aggregate root with private setters
   - Add factory methods: `Create()`, `MarkAsFilled()`, `MarkAsFailed()`
   - Replace primitives with value objects: `Money`, `Price`, `Quantity`
   - Replace `Guid Id` with `PurchaseId`
   - **Depends on:** `AggregateRoot`, `Result<T>`, value objects, strongly-typed IDs
   - **Testing:** Unit tests for business methods, invariant enforcement

7. **DcaConfiguration Aggregate** (`Models/DcaConfiguration.cs`)
   - Add validation methods: `UpdateBaseDailyAmount()`, `UpdateMultiplierTiers()`
   - Replace primitives with value objects: `Money`, `Percentage`
   - **Depends on:** `AggregateRoot`, `Result<T>`, value objects
   - **Testing:** Unit tests for configuration validation

### Phase 4: EF Core Configuration (Depends on Phase 3)

**Order:** Parallel

8. **Entity Configurations** (`Infrastructure/Data/Configurations/`)
   - `PurchaseConfiguration.cs` — value converters for Money, Price, Quantity, PurchaseId
   - `DcaConfigurationConfiguration.cs` — value converters for value objects
   - **Depends on:** Value objects, strongly-typed IDs
   - **Testing:** Integration tests with PostgreSQL (Testcontainers)

9. **DbContext Updates** (`Infrastructure/Data/TradingBotDbContext.cs`)
   - Register `DomainEventDispatcher` interceptor
   - Apply entity configurations
   - **Depends on:** `DomainEventDispatcher`, entity configurations
   - **Testing:** Integration tests for event dispatch on SaveChanges

### Phase 5: Specification Pattern (Depends on Domain Model)

**Order:** Sequential

10. **Specification Infrastructure** (`Domain/Specifications/`)
    - `ISpecification.cs`, `Specification.cs`, `CompositeSpecification.cs`
    - **Depends on:** Nothing (pure infrastructure)
    - **Testing:** Unit tests for AND/OR/NOT composition

11. **Domain Specifications** (`Domain/Specifications/Purchases/`)
    - `FilledPurchasesSpec.cs`, `NonDryRunSpec.cs`, `PurchasesInDateRangeSpec.cs`
    - **Depends on:** `Specification<T>`, `Purchase` aggregate
    - **Testing:** Unit tests with in-memory data

### Phase 6: Service Layer Updates (Depends on All)

**Order:** Sequential

12. **DcaExecutionService Refactoring** (`Application/Services/DcaExecutionService.cs`)
    - Use `Purchase.Create()` instead of `new Purchase()`
    - Use `purchase.MarkAsFilled()` instead of setting properties
    - Remove manual event publishing (handled by interceptor)
    - Return `Result<T>` instead of throwing exceptions
    - **Depends on:** Purchase aggregate, Result pattern, domain event dispatcher
    - **Testing:** Integration tests with real DbContext

13. **Specification Usage** (Various services)
    - Replace LINQ queries with specifications where appropriate
    - **Depends on:** Specification infrastructure, domain specifications
    - **Testing:** Update existing service tests

### Phase 7: Migration and Validation

**Order:** Sequential

14. **Database Migration**
    - Generate EF Core migration for value object column changes (if any)
    - Test migration on dev database
    - **Depends on:** Entity configurations
    - **Testing:** Migration up/down tests

15. **End-to-End Testing**
    - Verify domain events dispatched correctly
    - Verify integration events still reach outbox
    - Verify Telegram notifications still work
    - **Depends on:** Everything
    - **Testing:** E2E tests with Aspire orchestration

### Dependency Graph

```
Result Pattern ──────┐
Value Objects ───────┼──> Aggregate Root ──> Purchase Aggregate ──> Entity Configs ──> Service Updates
Strongly-Typed IDs ──┘                    └──> DcaConfig Aggregate ─┘

IDomainEvent (existing) ──> Domain Event Dispatcher ──> DbContext Updates ──> Service Updates

Specification Infrastructure ──> Domain Specifications ──> Service Updates
```

## Integration Points

### With Existing Components

| Existing Component | Integration Point | Notes |
|--------------------|-------------------|-------|
| `BaseEntity` | Modify to add `List<IDomainEvent> DomainEvents` | Breaking change — requires all entities to inherit new base |
| `TradingBotDbContext` | Add `DomainEventDispatcher` interceptor | Non-breaking — register in Program.cs |
| `IPublisher` (MediatR) | Used by `DomainEventDispatcher` | No changes needed |
| `IntegrationEvent` | Keep for cross-service events | Domain events stay in-process, integration events go to outbox |
| `OutboxEventPublisher` | Called from domain event handlers | Domain handler → integration event → outbox |
| `DcaExecutionService` | Refactor to use aggregate methods | Replace property setters with method calls |
| `PurchaseCompletedEvent` | Keep as domain event | Already implements `IDomainEvent` |

### With External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Hyperliquid API | No changes needed | Service layer still calls `HyperliquidClient` |
| Telegram Bot | No changes needed | Still receives integration events via Dapr pub-sub |
| Dashboard | No changes needed | Queries still use DbContext (optionally with specifications) |
| PostgreSQL | Value converters for value objects | Transparent to database schema (mostly) |
| Dapr pub-sub | No changes needed | Integration events unchanged |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Domain ↔ Application | Result<T> return values | Services call aggregate methods, handle Result |
| Application ↔ Infrastructure | DbContext, repositories | Services use DbContext, specifications abstract queries |
| Domain events ↔ Integration events | Event handlers translate | Domain handler creates integration event, publishes to outbox |
| Aggregates ↔ Aggregates | Domain events, IDs only | No direct references, coordinate via events |

## Sources

**DDD Tactical Patterns & EF Core:**
- [Implementing a microservice domain model with .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model)
- [Domain-Driven Design With Entity Framework Core 8 - The Honest Coder](https://thehonestcoder.com/ddd-ef-core-8/)
- [Implementing the infrastructure persistence layer with Entity Framework Core - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core)
- [Modeling Aggregates with DDD and Entity Framework | Kalele](https://kalele.io/modeling-aggregates-with-ddd-and-entity-framework/)

**Strongly-Typed IDs:**
- [Strongly-typed IDs in EF Core (Revisited) - Andrew Lock](https://andrewlock.net/strongly-typed-ids-in-ef-core-using-strongly-typed-entity-ids-to-avoid-primitive-obsession-part-4/)
- [Entity Framework Core 7: Strongly Typed Ids - David Masters](https://david-masters.medium.com/entity-framework-core-7-strongly-typed-ids-together-with-auto-increment-columns-fd9715e331f3)
- [C# 9 records as strongly-typed ids - Part 4: Entity Framework Core integration - Thomas Levesque](https://thomaslevesque.com/2020/12/23/csharp-9-records-as-strongly-typed-ids-part-4-entity-framework-core-integration/)
- [A Better Way to Handle Entity Identification in .NET with Strongly Typed IDs](https://antondevtips.com/blog/a-better-way-to-handle-entity-identification-in-dotnet-with-strongly-typed-ids)

**Domain Event Dispatch:**
- [Domain events: Design and implementation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Using Domain Events within a .NET Core Microservice - Cesar de la Torre](https://devblogs.microsoft.com/cesardelatorre/using-domain-events-within-a-net-core-microservice/)
- [Dispatch Domain Events w/ EF Core SaveChangesInterceptor - Mehmet Ozkaya](https://mehmetozkaya.medium.com/dispatch-domain-events-w-ef-core-savechangesinterceptor-leads-to-integration-events-7943811f97b1)
- [Simple Domain Events with EFCore and MediatR | cfrenzel](https://cfrenzel.com/domain-events-efcore-mediatr/)
- [Domain events: simple and reliable solution - Enterprise Craftsmanship](https://enterprisecraftsmanship.com/posts/domain-events-simple-reliable-solution/)

**Result Pattern:**
- [Either Monad in C# - Functional approach to error handling - Dimitris Papadimitriou](https://functionalprogramming.medium.com/either-is-a-common-type-in-functional-languages-94b86eea325c)
- [Flow Control with Either Monad in C#](https://danyl.hashnode.dev/mastering-flow-control-with-either-monad-in-c)
- [Stopping Using Exception Use Result Monad Instead](https://goatreview.com/stopping_using_exception_use_monad/)
- [Result monad - csharp-functional-docs](https://csharp-functional.readthedocs.io/en/latest/result-monad.html)

**Specification Pattern:**
- [Specification Pattern in EF Core: Flexible Data Access Without Repositories](https://antondevtips.com/blog/specification-pattern-in-ef-core-flexible-data-access-without-repositories)
- [Implementing Query Specification pattern in Entity Framework Core - Gunnar Peipman](https://gunnarpeipman.com/ef-core-query-specification/)
- [The Specification Pattern in DDD .NET Core - Charles](https://medium.com/@cizu64/the-query-specification-pattern-in-ddd-net-core-25f1ec580f32)
- [Specification pattern: C# implementation - Enterprise Craftsmanship](https://enterprisecraftsmanship.com/posts/specification-pattern-c-implementation/)

**Aggregate Invariants:**
- [Domain Model Validation - Kamil Grzybek](https://www.kamilgrzybek.com/blog/posts/domain-model-validation)
- [Aggregate Roots: Controlling Invariants in Domain Aggregates - Moments Log](https://www.momentslog.com/development/design-pattern/aggregate-roots-controlling-invariants-in-domain-aggregates)
- [Domain-Driven Design: Aggregates in Practice - Ankit Sharma](https://medium.com/@aforank/domain-driven-design-aggregates-in-practice-bcced7d21ae5)
- [DDD Aggregates - Best Practices and Implementation Strategies - Alina Bo](https://alinabo.com/ddd-aggregates)
- [Designing validations in the domain model layer - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-model-layer-validations)

**EF Core Value Objects:**
- [Creating and Configuring a Model - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/)
- [Mastering EF Core Configuration: Part 6 - Owned Value Types - Yahia Saafan](https://medium.com/@yayasaafan/mastering-ef-core-configuration-part-6-owned-value-types-0e536cb97a99)
- [Configuring Entities with Fluent API in EF Core - codewithmukesh](https://codewithmukesh.com/blog/fluent-api-entity-configuration-efcore/)
- [EF Core In depth - Tips and techniques for configuring EF Core - The Reformed Programmer](https://www.thereformedprogrammer.net/ef-core-in-depth-tips-and-techniques-for-configuring-ef-core/)

---

*Architecture research for: DDD Tactical Patterns Integration with .NET 10.0 + EF Core + MediatR + Dapr*
*Researched: 2026-02-14*

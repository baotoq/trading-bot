# Phase 17: Domain Event Dispatch - Research

**Researched:** 2026-02-19
**Domain:** EF Core SaveChangesInterceptor, domain event collection, outbox bridging, Dapr subscriber migration
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Event-to-outbox bridging:**
- ALL domain events auto-bridge to outbox messages (no selective filtering)
- Outbox messages reuse domain event type name directly (e.g., `PurchaseCompletedEvent`) — no separate integration event naming
- Outbox insert happens in the SAME transaction as SaveChanges — if save fails, no outbox message; if outbox insert fails, entire transaction rolls back
- Single dispatch path: domain events go through outbox/Dapr ONLY — existing MediatR in-process handlers migrate to Dapr subscriber endpoints
- No dual dispatch (no MediatR + outbox parallel paths)

**Handler failure behavior:**
- Failed outbox messages (after 3 retries) go to dead-letter table for manual inspection/replay
- SaveChanges transaction rolls back if outbox insert fails — no data saved without its events
- Dapr handles retry delivery to subscriber endpoints — handlers are simple (succeed or throw)
- Failed event visibility through logs only — no dashboard UI for event monitoring

**Non-aggregate events:**
- PurchaseSkippedEvent routes through outbox manually (service creates outbox message directly, not via interceptor)
- PurchaseSkippedEvent keeps IDomainEvent interface for consistent type hierarchy
- Build a general helper method (e.g., `eventPublisher.PublishDirectAsync(event)`) for non-aggregate events — anticipate more service-level events in the future
- Helper encapsulates outbox insertion for events that don't originate from aggregates

**Event granularity:**
- DcaConfigurationUpdatedEvent stays generic (single event for all config changes) — handlers inspect aggregate if they need details
- PurchaseCreatedEvent kept despite no current handler — documents the business moment and bridges to outbox for potential future subscribers
- Domain events include key data (not just identity) — e.g., PurchaseCompletedEvent carries price, quantity, cost so handlers avoid extra DB queries
- All domain events get a DateTimeOffset OccurredAt timestamp for event ordering, debugging, and audit trails

### Claude's Discretion

- SaveChangesInterceptor implementation approach (SavedChangesAsync timing)
- Dead-letter table schema and retention policy
- Dapr subscriber endpoint structure and routing
- Event serialization format for outbox payload
- How to enrich existing thin events (PurchaseId-only) with key data without breaking existing code

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DE-01 | Aggregates raise domain events when state changes (PurchaseExecuted, ConfigurationUpdated) | Already implemented via Phase 15/16: Purchase and DcaConfiguration call AddDomainEvent() in behavior methods. Events carry identity only today; phase enriches with key data fields. |
| DE-02 | Domain events dispatch after SaveChanges via SaveChangesInterceptor (consistency guarantee) | ISaveChangesInterceptor.SavingChangesAsync() is the correct hook — fires inside the EF Core transaction before commit, allowing outbox inserts in the same atomic batch. |
| DE-03 | Domain events automatically bridge to integration events via existing outbox pattern | Interceptor collects AggregateRoot domain events from ChangeTracker, serializes each to OutboxMessage with JSON payload, adds to DbContext.Set<OutboxMessage>() — included in the same SaveChanges batch. |
| DE-04 | Existing MediatR event handlers continue working with new dispatch mechanism (no handler rewrites needed) | Decision: handlers MIGRATE from INotificationHandler<T> to Dapr subscriber endpoints. The logic inside Handle() methods moves to the Dapr endpoint handler bodies. The Dapr subscriber infrastructure (PubSubRegistry, MapPubSub) already exists but is not yet wired. |
</phase_requirements>

---

## Summary

Phase 17 wires domain events collected by aggregates through the existing outbox infrastructure via a new `SaveChangesInterceptor`. The interceptor hooks into `SavingChangesAsync` — BEFORE the SQL transaction commits — scans the EF Core ChangeTracker for `AggregateRoot<TId>` entities, collects their accumulated domain events, and adds `OutboxMessage` records to the same DbContext. Because EF Core includes all tracked additions in the same `SaveChanges` operation, the domain changes and outbox inserts land in a single atomic database transaction.

The three existing MediatR handlers (`PurchaseCompletedHandler`, `PurchaseFailedHandler`, `PurchaseSkippedHandler`) migrate from `INotificationHandler<T>` to Dapr subscriber endpoints by converting their logic to handlers registered via the existing `PubSubRegistry.Subscribe<T>()` / `MapPubSub()` infrastructure. `PurchaseSkippedEvent` requires a separate `PublishDirectAsync()` helper because it originates from service logic (not from an aggregate), so the interceptor never sees it — the helper directly creates an `OutboxMessage` and adds it to the DbContext within the calling service's existing SaveChanges or as a standalone outbox insert with its own `SaveChangesAsync`.

The dead-letter requirement adds a `DeadLetterMessage` table (or flag on `OutboxMessage`) for messages that exhaust 3 retries. The existing `OutboxMessageProcessor` already sets `ProcessingStatus.Failed` — the dead-letter step is moving Failed messages to an explicit queryable table with `FailedAt`, `LastError`, and `EventName` fields, visible via logs as required.

**Primary recommendation:** Use `SavingChangesAsync` (not `SavedChangesAsync`) so outbox inserts are atomic with domain changes. Register the interceptor via `AddInterceptors()` on the DbContext registration in `Program.cs`. Wire `AddDaprPubSub()` and `AddOutboxPublishingWithEfCore<TradingBotDbContext>()` in Program.cs — both are defined but currently unregistered.

---

## Codebase State (Phase 16 Complete)

### What Already Exists

**Aggregates with domain events (Phase 15/16 complete):**
- `AggregateRoot<TId>` in `BuildingBlocks/AggregateRoot.cs` — has `_domainEvents`, `AddDomainEvent()`, `ClearDomainEvents()`, `DomainEvents` (IReadOnlyList)
- `Purchase` raises: `PurchaseCreatedEvent`, `PurchaseCompletedEvent`, `PurchaseFailedEvent` in behavior methods
- `DcaConfiguration` raises: `DcaConfigurationCreatedEvent`, `DcaConfigurationUpdatedEvent` in Create()/Update*() methods
- `IDomainEvent : INotification` — events implement MediatR's INotification

**Existing thin events (identity-only, need enrichment):**
```csharp
// Current: TradingBot.ApiService/Application/Events/
public record PurchaseCompletedEvent(PurchaseId PurchaseId) : IDomainEvent;
public record PurchaseCreatedEvent(PurchaseId PurchaseId) : IDomainEvent;
public record PurchaseFailedEvent(PurchaseId PurchaseId) : IDomainEvent;
public record DcaConfigurationUpdatedEvent(DcaConfigurationId ConfigId) : IDomainEvent;
public record DcaConfigurationCreatedEvent(DcaConfigurationId ConfigId) : IDomainEvent;
// PurchaseSkippedEvent: carries Reason, CurrentBalance, RequiredAmount, SkippedAt already
```

**Existing Dapr/Outbox infrastructure (defined, NOT yet wired in Program.cs):**
- `PubSubRegistry` + `WebApplicationExtensions.MapPubSub()` — auto-generates `/dapr/subscribe` and POST handler routes
- `ServiceCollectionExtensions.AddDaprPubSub()` — registers DaprClient, PubSubRegistry, DaprMessageBroker
- `OutboxMessageBackgroundService` — polls every 5s, publishes Pending messages via DaprMessageBroker
- `OutboxMessageProcessor` — already implements 3-retry logic, sets `ProcessingStatus.Failed` on exhaustion
- `EfCoreOutboxStore` — uses the DbContext directly (no separate connection)
- `IEventPublisher` / `OutboxEventPublisher` — exists for IntegrationEvent → outbox (currently unused)

**Manual dispatch pattern (to be replaced):**
```csharp
// DcaExecutionService.cs (Steps 8-9 — will be deleted)
var eventsToDispatch = purchase.DomainEvents.ToList();
foreach (var domainEvent in eventsToDispatch)
{
    await publisher.Publish(domainEvent, ct);  // MediatR IPublisher
}
purchase.ClearDomainEvents();
```

**MediatR handlers (to migrate):**
- `PurchaseCompletedHandler : INotificationHandler<PurchaseCompletedEvent>` — Telegram notification with DB queries + HyperliquidClient
- `PurchaseFailedHandler : INotificationHandler<PurchaseFailedEvent>` — Telegram notification with DB query
- `PurchaseSkippedHandler : INotificationHandler<PurchaseSkippedEvent>` — Telegram notification (no DB)
- MediatR is registered in `Telegram/ServiceCollectionExtensions.AddTelegram()` — scans assembly for handlers

---

## Architecture Patterns

### Pattern 1: SaveChangesInterceptor for Atomic Outbox Insertion

**What:** Override `SavingChangesAsync` on `SaveChangesInterceptor`. The interceptor runs INSIDE the current EF Core database transaction before the SQL is sent. Adding `OutboxMessage` entities via `context.Set<OutboxMessage>().Add(...)` includes them in the same `SaveChanges` batch — no second `SaveChangesAsync()` call needed, no recursion risk.

**Critical timing distinction:**
- `SavingChangesAsync` = runs BEFORE data is written to DB, INSIDE transaction → use this for outbox inserts
- `SavedChangesAsync` = runs AFTER successful commit → use this only for fire-and-forget side effects

**Why `SavingChangesAsync` for outbox:** The requirement is "if save fails, no outbox message." This is only guaranteed if the outbox insert is in the same transaction. `SavedChangesAsync` runs AFTER commit, so you could get domain data saved without outbox messages on failure.

**Standard pattern (Milan Jovanovic, verified against EF Core docs):**
```csharp
// BuildingBlocks/Pubsub/Outbox/EfCore/DomainEventOutboxInterceptor.cs
public sealed class DomainEventOutboxInterceptor(JsonSerializerOptions jsonOptions)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            InsertOutboxMessages(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void InsertOutboxMessages(DbContext context)
    {
        // Collect from all AggregateRoot<TId> instances in the ChangeTracker
        var outboxMessages = context
            .ChangeTracker
            .Entries<IAggregateRoot>()           // interface or base class detection
            .Select(entry => entry.Entity)
            .SelectMany(aggregate =>
            {
                var events = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();
                return events;
            })
            .Select(domainEvent => new OutboxMessage
            {
                EventName = domainEvent.GetType().Name,
                Payload   = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions),
                ProcessingStatus = ProcessingStatus.Pending
            })
            .ToList();

        context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}
```

**No recursion risk:** Interceptor's `InsertOutboxMessages()` only calls `context.Set<OutboxMessage>().AddRange(...)` (tracking, not saving). The added entities are included by EF Core in the current SaveChanges batch automatically. Calling `context.SaveChangesAsync()` INSIDE the interceptor would create recursion — do NOT do this.

**Registration (in Program.cs or DbContext config):**
```csharp
// Option A: Via AddNpgsqlDbContext / AddDbContext
builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb", configureDbContextOptions: options =>
    options.AddInterceptors(/* interceptor instance */));

// Option B: Override OnConfiguring in TradingBotDbContext (needs DI access)
// Not recommended if interceptor needs DI services (JsonSerializerOptions)
```

**For DI-resolved interceptors**, register as scoped/singleton and inject via `AddInterceptors`:
```csharp
// In Program.cs
builder.Services.AddSingleton<DomainEventOutboxInterceptor>();
builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb", configureDbContextOptions: options => { },
    configureSettings: null);
// Then configure interceptor separately via DbContextOptions
```

**Simpler approach for this codebase (no DI injection needed):**
`JsonSerializerOptions` is registered as `AddSingleton` already. The interceptor can take it via constructor injection if registered as `Scoped` service and added via `AddDbContextPool` options, OR use a static `JsonSerializerOptions` instance. Recommend making interceptor a scoped service resolved from DI.

### Pattern 2: IAggregateRoot Interface for ChangeTracker Detection

**What:** The ChangeTracker returns `EntityEntry` objects. To filter for only aggregate roots, the interceptor uses `Entries<T>()` with a type filter. Currently `AggregateRoot<TId>` is a base class but not interface-constrained. Adding `IAggregateRoot` interface makes ChangeTracker filtering clean.

```csharp
// Option A: marker interface (preferred)
public interface IAggregateRoot
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public abstract class AggregateRoot<TId> : BaseEntity<TId>, IAggregateRoot { ... }

// In interceptor:
context.ChangeTracker.Entries<IAggregateRoot>()  // clean, typed
```

```csharp
// Option B: cast to known base (less clean, requires reflection or pattern)
context.ChangeTracker.Entries()
    .Where(e => e.Entity is AggregateRoot<Guid> or AggregateRoot<PurchaseId> or ...)
// Fragile — must update for each new aggregate type
```

**Recommendation:** Add `IAggregateRoot` interface to `AggregateRoot<TId>` — minimal change, clean ChangeTracker filtering. This is the standard DDD pattern.

### Pattern 3: Event Enrichment (Thin → Rich Events)

**Decision:** Events include key data (not just identity). Current events are identity-only records. Strategy:

**Option A: Add fields to existing record (non-breaking for consumers that don't use the new fields):**
```csharp
// Before:
public record PurchaseCompletedEvent(PurchaseId PurchaseId) : IDomainEvent;

// After (additive, not breaking):
public record PurchaseCompletedEvent(
    PurchaseId PurchaseId,
    decimal Price,
    decimal Quantity,
    decimal Cost,
    DateTimeOffset OccurredAt
) : IDomainEvent;
```

**Impact on existing handlers:** `PurchaseCompletedHandler` currently queries the DB to get price/quantity/cost. After enrichment, those DB queries can be removed — the handler uses `notification.Price` directly.

**When to pass the data:** The behavior method that calls `AddDomainEvent()` has access to the correct values at the time of the state change. For example, `RecordFill()` knows `quantity`, `avgPrice`, and `actualCost` — pass them to `PurchaseCompletedEvent`:

```csharp
// Purchase.cs
public void RecordFill(Quantity quantity, Price avgPrice, UsdAmount actualCost, ...)
{
    // ... state mutation
    AddDomainEvent(new PurchaseCompletedEvent(
        Id, avgPrice.Value, quantity.Value, actualCost.Value, DateTimeOffset.UtcNow));
}
```

**OccurredAt timestamp:** All events should carry a `DateTimeOffset OccurredAt`. This is set at the time the domain event is created (inside the behavior method), not at outbox processing time.

### Pattern 4: Non-Aggregate Events via PublishDirectAsync Helper

**What:** `PurchaseSkippedEvent` originates in `DcaExecutionService` (before a Purchase aggregate exists or after a decision to skip). The interceptor cannot capture it because no aggregate raises it.

**Decision:** Build `IDomainEventPublisher` helper that directly creates and saves an OutboxMessage. Must be called within an active DbContext scope to participate in a transaction, OR as a standalone operation.

**Current PurchaseSkippedEvent dispatch pattern in DcaExecutionService:**
```csharp
// Current (MediatR in-process):
await publisher.Publish(new PurchaseSkippedEvent("Insufficient balance", ...), ct);

// After (outbox via helper):
await domainEventPublisher.PublishDirectAsync(new PurchaseSkippedEvent("Insufficient balance", ...), ct);
```

**Helper design:**
```csharp
// BuildingBlocks/Pubsub/Outbox/IDomainEventPublisher.cs
public interface IDomainEventPublisher
{
    Task PublishDirectAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

// BuildingBlocks/Pubsub/Outbox/DomainEventPublisher.cs
public class DomainEventPublisher(IOutboxStore outboxStore, JsonSerializerOptions jsonOptions)
    : IDomainEventPublisher
{
    public async Task PublishDirectAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var message = new OutboxMessage
        {
            EventName = domainEvent.GetType().Name,
            Payload   = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions),
            ProcessingStatus = ProcessingStatus.Pending
        };
        await outboxStore.AddAsync(message, ct);
        // IMPORTANT: Caller must call SaveChangesAsync separately to persist
        // This is by design: the helper just tracks the OutboxMessage on the DbContext
    }
}
```

**Caller pattern in DcaExecutionService:**
```csharp
// Skip scenarios already return early before dbContext.SaveChangesAsync(),
// so we need to save the outbox message explicitly
await domainEventPublisher.PublishDirectAsync(new PurchaseSkippedEvent(...), ct);
await dbContext.SaveChangesAsync(ct);  // saves just the OutboxMessage
return;
```

### Pattern 5: Dapr Subscriber Migration Pattern

**What:** MediatR handlers implement `INotificationHandler<T>`. The Dapr infrastructure uses `PubSubRegistry.Subscribe<TEvent>()` where `TEvent : IntegrationEvent`. The existing handlers use `IDomainEvent` types — these need to either implement `IntegrationEvent` or the subscriber uses a separate "message" type.

**Key insight:** The existing Dapr subscriber infrastructure (`WebApplicationExtensions.MapPubSub`) deserializes the Dapr event payload as an `IntegrationEvent` and dispatches via `mediator.Publish(message)`. The handlers becoming Dapr subscribers means:

1. The outbox processor serializes the event JSON to the `OutboxMessage.Payload`
2. Dapr delivers it to the subscriber endpoint as a JSON wrapper
3. The endpoint deserializes back to the event type
4. The endpoint calls the handler logic directly (no MediatR hop — or via MediatR if desired)

**Problem with current type hierarchy:** `IDomainEvent : INotification`. The Dapr subscriber currently requires `IntegrationEvent` (abstract base record). Domain events are records implementing `IDomainEvent`, not extending `IntegrationEvent`. There's a type mismatch to resolve.

**Resolution options:**

**Option A (recommended): Keep IDomainEvent separate, make Dapr subscriber work with raw JSON type name**
- The `PubSubRegistry.Add<TEvent>()` currently requires `where TEvent : IntegrationEvent`
- Relax the constraint to use the type name + JSON deserialization approach
- The outbox stores `EventName = domainEvent.GetType().Name` and `Payload = json`
- The subscriber endpoint deserializes based on the topic name matching the EventName
- The endpoint handler directly executes business logic (no MediatR)

This avoids inheriting from `IntegrationEvent` on domain events (keeps DDD semantics clean).

**Option B: Make domain events extend IntegrationEvent**
- Change `IDomainEvent` to extend `IntegrationEvent` instead of `INotification`
- Breaking change to all event records
- Muddies DDD semantics (domain events ≠ integration events)
- Not recommended

**Option C: Add IDomainEvent support to PubSubRegistry**
- Generalize `PubSubRegistry.Add<T>()` to accept any type, not just `IntegrationEvent`
- `MapPubSub` handles deserialization by `Type` reference, not by `IntegrationEvent` constraint
- Cleanest long-term — keeps domain events and integration events distinct

**Recommendation:** Option C — generalize `PubSubRegistry` to support `IDomainEvent` types. The existing `WebApplicationExtensions.MapPubSub` deserializes by `sub.EventType` (already a `Type` object), so the only constraint to relax is the generic `where TEvent : IntegrationEvent` on `Add<TEvent>()` and `Subscribe<TEvent>()`.

**After migration, subscriber registration in Program.cs:**
```csharp
var registry = services.AddDaprPubSub();
registry
    .Subscribe<PurchaseCompletedEvent>()
    .Subscribe<PurchaseFailedEvent>()
    .Subscribe<PurchaseSkippedEvent>();
```

**Handler endpoint pattern (inside MapPubSub or dedicated endpoint class):**
```csharp
// Application/Handlers/PurchaseCompletedHandler.cs (new Dapr-endpoint style)
// Handler logic stays largely identical — just called from Dapr endpoint, not MediatR
```

### Pattern 6: Dead-Letter Table

**Decision:** Failed outbox messages (after 3 retries) go to a dead-letter table.

**Current state:** `OutboxMessageProcessor` already sets `ProcessingStatus.Failed` on retry exhaustion. The message stays in `OutboxMessages` table marked `Failed` but is never retried again (the `GetUnprocessedAsync` query filters `ProcessingStatus == Pending`). No separate dead-letter table exists.

**Recommended schema for `DeadLetterMessages`:**
```sql
CREATE TABLE "DeadLetterMessages" (
    "Id"          UUID        PRIMARY KEY,
    "EventName"   TEXT        NOT NULL,
    "Payload"     TEXT        NOT NULL,
    "FailedAt"    TIMESTAMPTZ NOT NULL,
    "LastError"   TEXT,
    "RetryCount"  INT         NOT NULL,
    "CreatedAt"   TIMESTAMPTZ NOT NULL
);
```

**Implementation approach:** In `OutboxMessageProcessor`, when `RetryCount >= 3`, instead of just marking Failed in place, move the message to `DeadLetterMessages` and delete or mark it as archived in `OutboxMessages`. This keeps the `OutboxMessages` table clean and provides a dedicated queryable dead-letter store.

**Retention:** No automated expiry required (decision: logs only for visibility). Keep forever for manual replay capability.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SaveChanges interception | Custom DbContext override with domain event logic | `ISaveChangesInterceptor` (EF Core built-in) | Interceptors compose cleanly, don't require subclassing DbContext, work with DI |
| Outbox message persistence | New outbox storage mechanism | Existing `EfCoreOutboxStore` + `IOutboxStore` | Already handles Add/GetUnprocessed/MarkAs — interceptor just calls `context.Set<OutboxMessage>().AddRange()` directly |
| JSON serialization for payloads | Custom binary format | `System.Text.Json` with `JsonSerializer.Serialize(obj, obj.GetType(), options)` | Already used in `OutboxEventPublisher`; type-aware overload handles polymorphism |
| Dapr subscriber routing | Manual HTTP endpoint mapping | Existing `PubSubRegistry` + `MapPubSub()` | Infrastructure already built — just needs registration wired in Program.cs |
| Retry logic | Custom retry in handlers | Dapr's built-in delivery retry | Dapr retries delivery; handlers just throw on failure |

---

## Common Pitfalls

### Pitfall 1: Using SavedChangesAsync Instead of SavingChangesAsync

**What goes wrong:** Outbox inserts happen AFTER the transaction commits. If the application crashes between commit and outbox insert, domain data is saved but no outbox message exists — events are silently lost.

**Why it happens:** `SavedChangesAsync` sounds like "after saving" which maps intuitively to "post-success." But atomicity requires being IN the transaction.

**How to avoid:** Always use `SavingChangesAsync` for outbox inserts. The interceptor adds to `context.Set<OutboxMessage>()` — these additions are automatically included in the current `SaveChanges` batch by EF Core.

**Warning signs:** Test by killing the app mid-save; domain data should not appear without a corresponding outbox message.

### Pitfall 2: Calling SaveChangesAsync Inside the Interceptor

**What goes wrong:** Infinite recursion. `SavingChangesAsync` fires when `SaveChangesAsync` is called. If the interceptor calls `context.SaveChangesAsync()`, it fires the interceptor again.

**Why it happens:** Developers see outbox entities added but think they need to explicitly save them.

**How to avoid:** Only call `context.Set<OutboxMessage>().AddRange(...)` inside the interceptor. EF Core includes the newly tracked entities in the current SaveChanges automatically. Never call `SaveChanges` or `SaveChangesAsync` on the same context inside the interceptor.

### Pitfall 3: Serializing IDomainEvent Without Type Information

**What goes wrong:** `JsonSerializer.Serialize(domainEvent)` serializes the compile-time type (IDomainEvent interface), not the runtime type. Payload becomes `{}` or loses all fields.

**Why it happens:** C# generics erase runtime type in some paths; the base interface has no properties.

**How to avoid:** Always pass the runtime type explicitly:
```csharp
JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions)
```

**Warning signs:** Outbox payload is empty JSON or missing expected fields after deserialization.

### Pitfall 4: ClearDomainEvents Called Before Events Are Collected

**What goes wrong:** The interceptor sees zero domain events because `ClearDomainEvents()` was already called by the old manual dispatch code in `DcaExecutionService`.

**Why it happens:** The manual dispatch pattern in `DcaExecutionService` (Steps 8-9) calls `purchase.ClearDomainEvents()` after publishing. If this code isn't removed before the interceptor is activated, the interceptor finds an empty list.

**How to avoid:** Remove the manual event dispatch block from `DcaExecutionService` (Steps 8-9 in `ExecuteDailyPurchaseAsync`) as part of this phase. The interceptor becomes the sole mechanism.

**Warning signs:** No outbox messages created for Purchase events even though events are raised in behavior methods.

### Pitfall 5: PurchaseSkippedEvent Never Reaches the Interceptor

**What goes wrong:** `PurchaseSkippedEvent` is published in `DcaExecutionService` before `dbContext.SaveChangesAsync()` is called, but it's not raised by an aggregate — it's a service-level event. The interceptor only scans `AggregateRoot<TId>` entities in the ChangeTracker.

**Why it happens:** Not all domain events originate from aggregates. Skip scenarios don't create a Purchase entity.

**How to avoid:** Use the `IDomainEventPublisher.PublishDirectAsync()` helper for `PurchaseSkippedEvent`. The helper creates an OutboxMessage and adds it to the DbContext tracking. Call `SaveChangesAsync` explicitly after the helper call to persist.

### Pitfall 6: Dapr Subscriber Topic Name Mismatch

**What goes wrong:** The outbox processor publishes to topic `purchasecompletedevent` (lowercased type name) but the subscriber endpoint registers for a different topic.

**Why it happens:** `OutboxMessageProcessor` calls `messageBroker.PublishAsync(message.EventName.ToLower(), ...)`. The `PubSubRegistry` derives the topic from `typeof(TEvent).Name.ToLower()`. These must match.

**Current outbox processor (line 26):**
```csharp
await messageBroker.PublishAsync(message.EventName.ToLower(), message.Payload, cancellationToken);
```

The `EventName` in the outbox is set to `domainEvent.GetType().Name` (e.g., `PurchaseCompletedEvent`). After `.ToLower()` it becomes `purchasecompletedevent`. The `PubSubRegistry` derives the topic as `typeof(PurchaseCompletedEvent).Name.ToLower()` = `purchasecompletedevent`. These match automatically — no manual routing needed.

**Warning signs:** Dapr subscriber endpoint never receives messages; outbox messages stay in Pending state.

### Pitfall 7: Interceptor Registered as Transient with Singleton Dependencies

**What goes wrong:** If `DomainEventOutboxInterceptor` holds mutable state (e.g., a list of collected events), and it's singleton, state bleeds across concurrent SaveChanges calls.

**Why it happens:** Singleton lifecycle means one shared instance.

**How to avoid:** The interceptor must be STATELESS. All data (domain events) is read from `eventData.Context` per call. No instance fields. Register as singleton safely only if stateless. The `JsonSerializerOptions` dependency is itself singleton — safe to inject.

---

## Code Examples

### Interceptor Registration via Program.cs (Aspire pattern)

```csharp
// Source: EF Core docs / Milan Jovanovic pattern (verified)
// In Program.cs, after AddNpgsqlDbContext
builder.Services.AddSingleton<DomainEventOutboxInterceptor>();

// Aspire uses AddNpgsqlDbContext which wraps AddDbContext
// Override via IDbContextOptionsConfiguration or AddDbContextPool options
// Alternative: register interceptor in TradingBotDbContext's OnConfiguring via IServiceProvider

// Simplest working pattern for Aspire:
builder.Services.AddSingleton<DomainEventOutboxInterceptor>();
// Then modify TradingBotDbContext to accept and apply it:
// In Program.cs, after builder.AddNpgsqlDbContext:
builder.Services.AddDbContext<TradingBotDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>();
    options.AddInterceptors(interceptor);
});
// Note: Aspire's AddNpgsqlDbContext also configures connection strings;
// may need to call both or use DbContextOptionsBuilder extension
```

**IMPORTANT NOTE ON ASPIRE INTEGRATION (LOW confidence — verify):** `builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb")` in the current `Program.cs` uses Aspire's extension. Aspire may provide an `optionsBuilder` overload for configuration. Check `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` documentation to confirm how to pass `AddInterceptors` when using Aspire's registration method.

Alternative: Override `OnConfiguring` in `TradingBotDbContext` to call `optionsBuilder.AddInterceptors(...)` — but this requires the interceptor to be available without DI injection (i.e., passed via constructor or created inline).

### Collecting Domain Events from ChangeTracker

```csharp
// Source: Verified against EF Core docs (ChangeTracker.Entries<T>() with base class)
private void InsertOutboxMessages(DbContext context)
{
    var aggregates = context
        .ChangeTracker
        .Entries<IAggregateRoot>()
        .Select(entry => entry.Entity)
        .Where(aggregate => aggregate.DomainEvents.Count > 0)
        .ToList();

    if (aggregates.Count == 0) return;

    var outboxMessages = aggregates
        .SelectMany(aggregate =>
        {
            var events = aggregate.DomainEvents.ToList();
            aggregate.ClearDomainEvents();
            return events;
        })
        .Select(domainEvent => new OutboxMessage
        {
            EventName = domainEvent.GetType().Name,
            Payload   = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions),
            ProcessingStatus = ProcessingStatus.Pending
        })
        .ToList();

    context.Set<OutboxMessage>().AddRange(outboxMessages);
}
```

### Handler Migration Pattern

```csharp
// BEFORE (MediatR handler):
// Application/Handlers/PurchaseCompletedHandler.cs
public class PurchaseCompletedHandler(...) : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent notification, CancellationToken cancellationToken)
    {
        // logic using notification.PurchaseId
        var purchase = await dbContext.Purchases.FirstOrDefaultAsync(p => p.Id == notification.PurchaseId);
        // ... Telegram notification
    }
}

// AFTER (Dapr subscriber — registered via PubSubRegistry):
// The same handler class can stay in Application/Handlers/ but implement a different interface
// OR: Logic moved directly into MapPubSub endpoint delegate
// The PubSubRegistry/MapPubSub infrastructure dispatches via MediatR internally (mediator.Publish)
// So if PurchaseCompletedEvent stays INotification, the handler STILL works via MediatR inside MapPubSub

// KEY INSIGHT: MapPubSub (WebApplicationExtensions.cs line 88) already does:
//   await mediator.Publish(message);
// So after migration, handlers can KEEP INotificationHandler<T> interface
// The difference is: events arrive via Dapr (outbox → DaprMessageBroker → HTTP endpoint → MediatR)
// NOT via direct IPublisher.Publish() in DcaExecutionService

// Minimal change path: Keep handler classes as INotificationHandler<T>
// Just ensure PubSubRegistry.Subscribe<PurchaseCompletedEvent>() is called
// and AddDaprPubSub() + AddOutboxPublishingWithEfCore() are registered in Program.cs
```

### EF Core Interceptor Base Class

```csharp
// Source: EF Core docs — SaveChangesInterceptor is in Microsoft.EntityFrameworkCore.Diagnostics
// The base class provides no-op implementations of all interface methods
public sealed class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    // Only override what you need — base class provides all no-ops
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...) { ... }
    public override InterceptionResult<int> SavingChanges(...) { ... }  // sync overload
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual event dispatch in service layer | SaveChangesInterceptor automatic collection | EF Core 5.0 (2020) | Removes boilerplate from services, guarantees transactional atomicity |
| MediatR in-process handlers | Dapr pub-sub subscriber endpoints | Phase 17 | Cross-process retry, dead-letter, loose coupling |
| IntegrationEvent base class for all events | IDomainEvent separate from IntegrationEvent | DDD best practice | Cleaner domain semantics; domain events stay domain, integration events stay infrastructure |
| Identity-only events (PurchaseId only) | Rich events with key data fields + OccurredAt | Phase 17 | Handlers avoid extra DB queries; event history self-documenting |

---

## Open Questions

1. **Aspire AddNpgsqlDbContext interceptor injection**
   - What we know: `builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb")` is the Aspire registration
   - What's unclear: Whether Aspire's extension supports an `configureDbContextOptions` overload for `AddInterceptors()`
   - Recommendation: Check `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.0.1 docs or source. Fallback: add a `AddDbContextOptions<TradingBotDbContext>()` call after Aspire registration, or override `OnConfiguring` in `TradingBotDbContext` with DI-provided interceptor via `IDbContextOptionsExtension`.

2. **Dapr subscriber type constraint relaxation**
   - What we know: `PubSubRegistry.Subscribe<TEvent>()` currently constrains `where TEvent : IntegrationEvent`; domain events are `IDomainEvent` not `IntegrationEvent`
   - What's unclear: Whether to relax the constraint to `where TEvent : class` or add a second `SubscribeDomainEvent<TEvent>()` overload
   - Recommendation: Generalize to `where TEvent : class` with a `Type eventType` stored in `PubSubSubscription`. Deserialize via `domainEvent.GetType()` passed at registration time.

3. **PurchaseSkippedEvent SaveChangesAsync scope**
   - What we know: `DcaExecutionService` calls `publisher.Publish(PurchaseSkippedEvent)` and then `return` — no `dbContext.SaveChangesAsync()` follows for skip paths
   - What's unclear: If `PublishDirectAsync` tracks an `OutboxMessage` on the DbContext but the service never calls `SaveChangesAsync`, the message is never persisted
   - Recommendation: `PublishDirectAsync` must ALSO call `SaveChangesAsync` internally via a new scope, OR the service must be modified to call `dbContext.SaveChangesAsync()` after every `PublishDirectAsync`. The cleanest: `PublishDirectAsync` calls `await outboxStore.AddAsync(msg, ct); await dbContext.SaveChangesAsync(ct);` — making it a true standalone publish. Document this explicitly in the helper.

4. **Event enrichment migration — handler impact**
   - What we know: `PurchaseCompletedHandler` queries DB to get `purchase.Price`, `purchase.Quantity` etc. after receiving `PurchaseCompletedEvent(PurchaseId only)`. With enriched events, the handler has the data directly.
   - What's unclear: Whether to remove DB queries from handlers simultaneously or leave them using both approaches
   - Recommendation: Enrich events AND simplify handlers in the same phase. `PurchaseCompletedHandler` drops the `dbContext.Purchases.FirstOrDefaultAsync()` call and uses `notification.Price`, `notification.Quantity`, `notification.Cost` directly. `PurchaseFailedHandler` still needs the DB query for `FailureReason` since that field may not belong in the event.

---

## Sources

### Primary (HIGH confidence)
- `/dotnet/entityframework.docs` (Context7) — SaveChangesInterceptor, SavingChangesAsync, SavedChangesAsync, ChangeTracker.Entries, AddInterceptors
- EF Core docs at https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors — interceptor registration, event data access, timing
- Codebase inspection — full read of: AggregateRoot.cs, IDomainEvent.cs, OutboxEventPublisher.cs, OutboxMessage.cs, EfCoreOutboxStore.cs, OutboxMessageProcessor.cs, DaprMessageBroker.cs, WebApplicationExtensions.cs, ServiceCollectionExtensions.cs (Dapr + Outbox), PurchaseCompletedHandler.cs, PurchaseFailedHandler.cs, PurchaseSkippedHandler.cs, DcaExecutionService.cs, Purchase.cs, DcaConfiguration.cs, TradingBotDbContext.cs, Program.cs

### Secondary (MEDIUM confidence)
- Milan Jovanovic — https://www.milanjovanovic.tech/blog/how-to-use-ef-core-interceptors — InsertOutboxMessagesInterceptor pattern using SavingChangesAsync. Pattern verified against EF Core docs structure.
- GitHub dotnet/efcore issue #27725 — SavedChangesAsync timing confirmation (fires after commit, entity caching note)

### Tertiary (LOW confidence)
- General community patterns for Dapr subscriber type generalization — not verified against official Dapr .NET SDK docs

---

## Metadata

**Confidence breakdown:**
- Standard stack (EF Core interceptors): HIGH — EF Core docs + Context7 verified
- Architecture (SavingChangesAsync timing, same-transaction atomicity): HIGH — EF Core docs explicit on this
- Codebase analysis (what exists, what doesn't): HIGH — direct file inspection
- Dapr subscriber type generalization: MEDIUM — existing codebase shows the pattern, relaxing constraint is straightforward C#
- Aspire interceptor injection approach: LOW — Aspire's `AddNpgsqlDbContext` overloads not confirmed in docs

**Research date:** 2026-02-19
**Valid until:** 2026-03-19 (EF Core and Dapr APIs stable)

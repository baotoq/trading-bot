---
phase: 17-domain-event-dispatch
plan: 01
subsystem: database
tags: [ef-core, outbox, domain-events, interceptor, csharp]

# Dependency graph
requires:
  - phase: 15-rich-aggregate-roots
    provides: AggregateRoot<TId> base class with AddDomainEvent/ClearDomainEvents and domain event records
  - phase: 16-result-pattern
    provides: ErrorOr-wired service and endpoint layer

provides:
  - IAggregateRoot marker interface enabling ChangeTracker filtering
  - DomainEventOutboxInterceptor bridging domain events to outbox messages atomically
  - Enriched domain events with key data fields (Price, Quantity, Cost) and OccurredAt timestamps
  - OutboxMessage entity registered in TradingBotDbContext

affects:
  - 17-domain-event-dispatch
  - any future phase consuming domain events from outbox

# Tech tracking
tech-stack:
  added: []
  patterns:
    - SaveChangesInterceptor pattern for atomic domain event -> outbox bridging
    - Runtime-type JSON serialization via JsonSerializer.Serialize(event, event.GetType(), options)
    - Singleton interceptor captured before AddNpgsqlDbContext registration (Aspire configureDbContextOptions is Action<DbContextOptionsBuilder> without IServiceProvider)

key-files:
  created:
    - TradingBot.ApiService/BuildingBlocks/IAggregateRoot.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DomainEventOutboxInterceptor.cs
  modified:
    - TradingBot.ApiService/BuildingBlocks/AggregateRoot.cs
    - TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs
    - TradingBot.ApiService/Application/Events/PurchaseCreatedEvent.cs
    - TradingBot.ApiService/Application/Events/PurchaseFailedEvent.cs
    - TradingBot.ApiService/Application/Events/PurchaseSkippedEvent.cs
    - TradingBot.ApiService/Application/Events/DcaConfigurationCreatedEvent.cs
    - TradingBot.ApiService/Application/Events/DcaConfigurationUpdatedEvent.cs
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Models/DcaConfiguration.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
    - TradingBot.ApiService/Program.cs

key-decisions:
  - "Aspire's AddNpgsqlDbContext configureDbContextOptions lambda is Action<DbContextOptionsBuilder> (no IServiceProvider); interceptor created before registration and captured by closure"
  - "PurchaseSkippedEvent.SkippedAt renamed to OccurredAt for consistency with all other domain events"
  - "Runtime type serialization (JsonSerializer.Serialize(event, event.GetType(), options)) prevents empty JSON when serializing through IDomainEvent interface"

patterns-established:
  - "DomainEventOutboxInterceptor: SavingChangesAsync collects from ChangeTracker.Entries<IAggregateRoot>(), never calls SaveChangesAsync internally (recursion prevention)"
  - "Domain events carry decimal primitives (not value objects) for JSON round-trip safety"

requirements-completed:
  - DE-01
  - DE-02

# Metrics
duration: 49min
completed: 2026-02-19
---

# Phase 17 Plan 01: Domain Event Outbox Interceptor Summary

**SaveChangesInterceptor atomically bridges domain events to OutboxMessage records, with all events enriched with key data fields (Price, Quantity, Cost) and OccurredAt timestamps**

## Performance

- **Duration:** 49 min
- **Started:** 2026-02-19T15:09:15Z
- **Completed:** 2026-02-19T15:57:48Z
- **Tasks:** 2
- **Files modified:** 12

## Accomplishments

- IAggregateRoot interface enables clean ChangeTracker filtering for aggregate entities without coupling to base class
- DomainEventOutboxInterceptor overrides SavingChangesAsync (inside transaction) to atomically persist domain events as OutboxMessage records in the same DB transaction
- All 5 domain event types enriched with key data and OccurredAt timestamps; Purchase behavior methods pass raw `.Value` primitives for JSON safety

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IAggregateRoot interface and DomainEventOutboxInterceptor** - `6493c75` (feat)
2. **Task 2: Enrich domain events with key data and OccurredAt timestamp** - `f2e60e7` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/BuildingBlocks/IAggregateRoot.cs` - Marker interface with DomainEvents and ClearDomainEvents
- `TradingBot.ApiService/BuildingBlocks/AggregateRoot.cs` - Now implements IAggregateRoot
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DomainEventOutboxInterceptor.cs` - SaveChangesInterceptor that collects domain events and writes OutboxMessage records atomically
- `TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs` - Enriched: Price, Quantity, Cost, OccurredAt
- `TradingBot.ApiService/Application/Events/PurchaseCreatedEvent.cs` - Enriched: Price, Cost, Multiplier, OccurredAt
- `TradingBot.ApiService/Application/Events/PurchaseFailedEvent.cs` - Enriched: FailureReason, OccurredAt
- `TradingBot.ApiService/Application/Events/PurchaseSkippedEvent.cs` - SkippedAt renamed to OccurredAt
- `TradingBot.ApiService/Application/Events/DcaConfigurationCreatedEvent.cs` - Added OccurredAt
- `TradingBot.ApiService/Application/Events/DcaConfigurationUpdatedEvent.cs` - Added OccurredAt
- `TradingBot.ApiService/Models/Purchase.cs` - Behavior methods pass enriched data to domain events
- `TradingBot.ApiService/Models/DcaConfiguration.cs` - All domain event calls pass DateTimeOffset.UtcNow
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - Added OutboxMessages DbSet and AddOutboxMessageEntity config
- `TradingBot.ApiService/Program.cs` - Interceptor registered as singleton, wired into AddNpgsqlDbContext

## Decisions Made

- **Aspire AddNpgsqlDbContext lambda constraint:** Aspire's `configureDbContextOptions` is `Action<DbContextOptionsBuilder>` without `IServiceProvider` access. Solution: instantiate interceptor before registration and capture by closure in lambda. Singleton also registered in DI container for potential future resolution.
- **PurchaseSkippedEvent SkippedAt -> OccurredAt:** Renamed for consistency; `SkippedAt` was semantically identical to OccurredAt used by all other events.
- **Runtime type serialization:** `JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), jsonOptions)` is critical - serializing through `IDomainEvent` interface produces empty JSON because STJ only sees interface members.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed compile error: configureDbContextOptions lambda takes 2 parameters**
- **Found during:** Task 1 (Program.cs interceptor registration)
- **Issue:** Plan suggested `(sp, options)` lambda but Aspire's overload only accepts `Action<DbContextOptionsBuilder>` (single parameter)
- **Fix:** Create interceptor instance before `AddNpgsqlDbContext` call and capture it by closure in the single-parameter lambda; also register singleton in DI
- **Files modified:** TradingBot.ApiService/Program.cs
- **Verification:** `dotnet build` succeeded after fix
- **Committed in:** 6493c75 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 compile error / Rule 1 bug fix)
**Impact on plan:** Fix was necessary for compilation; functionally equivalent to plan's intent. No scope creep.

## Issues Encountered

The Aspire `AddNpgsqlDbContext` `configureDbContextOptions` parameter is `Action<DbContextOptionsBuilder>` (not `Action<IServiceProvider, DbContextOptionsBuilder>`). The plan's suggested code with 2-parameter lambda didn't compile. Fixed by capturing the interceptor instance as a closure.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DomainEventOutboxInterceptor is ready; domain events will be captured atomically on every SaveChanges call
- OutboxMessage entity is configured in the DbContext; Plan 02 will wire the outbox processor and publisher
- All domain events carry rich data - handlers can process events without extra DB queries

---
*Phase: 17-domain-event-dispatch*
*Completed: 2026-02-19*

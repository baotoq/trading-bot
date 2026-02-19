---
phase: 15-rich-aggregate-roots
plan: 01
subsystem: domain
tags: [ddd, aggregate-root, domain-events, purchase, csharp, dotnet]

# Dependency graph
requires:
  - phase: 14-value-objects
    provides: Value objects (Price, Quantity, UsdAmount, Multiplier, Percentage) used as Purchase constructor parameters
  - phase: 13-strongly-typed-ids
    provides: PurchaseId strongly-typed ID used in aggregate and events

provides:
  - AggregateRoot<TId> base class with domain event collection (AddDomainEvent, ClearDomainEvents, DomainEvents)
  - Purchase as rich aggregate root with private constructor, static Create() factory, and behavior methods
  - Identity-only domain events: PurchaseCreatedEvent, PurchaseCompletedEvent, PurchaseFailedEvent
  - Aggregate-based event dispatch pattern in DcaExecutionService

affects: [16-specifications, 17-domain-event-interceptor, 18-outbox]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - AggregateRoot<TId> base class with protected AddDomainEvent / public ClearDomainEvents
    - Static factory method (Purchase.Create()) as sole creation entry point
    - Private constructor + protected EF Core parameterless constructor
    - Behavior methods that raise domain events via AddDomainEvent (RecordDryRunFill, RecordFill, RecordResting, RecordFailure)
    - Identity-only domain events - handlers load aggregate from DB
    - Post-SaveChanges domain event dispatch loop from purchase.DomainEvents collection

key-files:
  created:
    - TradingBot.ApiService/BuildingBlocks/AggregateRoot.cs
    - TradingBot.ApiService/Application/Events/PurchaseCreatedEvent.cs
  modified:
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs
    - TradingBot.ApiService/Application/Events/PurchaseFailedEvent.cs
    - TradingBot.ApiService/Application/Handlers/PurchaseCompletedHandler.cs
    - TradingBot.ApiService/Application/Handlers/PurchaseFailedHandler.cs
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs
    - TradingBot.ApiService/Application/BackgroundJobs/DcaSchedulerBackgroundService.cs

key-decisions:
  - "AggregateRoot<TId> inherits BaseEntity<TId> (not AuditedEntity directly) to maintain existing ID + audit trail inheritance chain"
  - "Events are raised inside Purchase behavior methods (not constructed in service) - single pattern, no inline event construction in service layer"
  - "DcaSchedulerBackgroundService scheduler-level catch blocks log only (no PurchaseFailedEvent) - no Purchase aggregate exists at that level"
  - "PurchaseCreatedEvent raised in Create() factory so aggregate creation is always tracked"
  - "DomainEvents dispatched after SaveChanges (not before) - critical for correct event ordering"

patterns-established:
  - "Aggregate.Create() is the only public entry point for creating a Purchase - no direct constructor access"
  - "Behavior methods (RecordFill, RecordFailure, etc.) mutate state AND raise domain events atomically"
  - "Handlers load full aggregate from DB when they need rich data - events are identity-only"
  - "eventsToDispatch = purchase.DomainEvents.ToList() before ClearDomainEvents() to capture count for logging"

requirements-completed: [DM-01, DM-02, DM-04]

# Metrics
duration: 4min
completed: 2026-02-19
---

# Phase 15 Plan 01: Rich Aggregate Roots Summary

**AggregateRoot<TId> base class with Purchase refactored to DDD aggregate root: private constructor, static Create() factory, behavior methods (RecordFill/RecordFailure/RecordResting/RecordDryRunFill) that raise identity-only domain events via AddDomainEvent(), dispatched post-SaveChanges from DomainEvents collection**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-19T13:22:25Z
- **Completed:** 2026-02-19T13:26:47Z
- **Tasks:** 2
- **Files modified:** 9 (2 created, 7 modified)

## Accomplishments

- AggregateRoot<TId> base class provides domain event collection with protected AddDomainEvent and public ClearDomainEvents
- Purchase is now a proper DDD aggregate root: no public constructor, static Create() factory, private setters on all properties, behavior methods that encapsulate state changes and domain event raising
- DcaExecutionService dispatches domain events from purchase.DomainEvents after SaveChanges -- single-pattern approach, no inline event construction in service layer
- PurchaseCompletedHandler and PurchaseFailedHandler load Purchase from DB via identity-only events (clean separation of concerns)
- All 53 existing tests pass without regression

## Task Commits

Each task was committed atomically:

1. **Task 1: Create AggregateRoot base class and refactor Purchase to rich aggregate** - `48bcf6d` (feat)
2. **Task 2: Refactor events, handlers, and DcaExecutionService to use aggregate API** - `26106aa` (feat)

**Plan metadata:** (docs commit, recorded after SUMMARY)

## Files Created/Modified

- `TradingBot.ApiService/BuildingBlocks/AggregateRoot.cs` - New base class with domain event collection (AddDomainEvent protected, ClearDomainEvents public, DomainEvents IReadOnlyList)
- `TradingBot.ApiService/Models/Purchase.cs` - Rich aggregate root: private constructor, protected EF ctor, Create() factory, private setters, behavior methods
- `TradingBot.ApiService/Application/Events/PurchaseCreatedEvent.cs` - New identity-only event raised in Create()
- `TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs` - Simplified to identity-only (PurchaseId only)
- `TradingBot.ApiService/Application/Events/PurchaseFailedEvent.cs` - Simplified to identity-only (PurchaseId only)
- `TradingBot.ApiService/Application/Handlers/PurchaseCompletedHandler.cs` - Now loads Purchase from DB, fetches USDC balance via HyperliquidClient
- `TradingBot.ApiService/Application/Handlers/PurchaseFailedHandler.cs` - Now loads Purchase from DB for FailureReason and timestamp
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Uses Purchase.Create() and behavior methods; dispatches from DomainEvents after SaveChanges
- `TradingBot.ApiService/Application/BackgroundJobs/DcaSchedulerBackgroundService.cs` - Removed direct PurchaseFailedEvent construction (no Purchase aggregate at scheduler level)

## Decisions Made

- AggregateRoot<TId> inherits BaseEntity<TId> to maintain existing ID + audit trail chain without duplication
- DcaSchedulerBackgroundService catch blocks now log-only for infrastructure-level failures (no Purchase aggregate exists at that point, so PurchaseFailedEvent with PurchaseId cannot be constructed)
- PurchaseCreatedEvent is raised in Create() factory so all purchase lifecycle events flow through the aggregate
- DomainEvents snapshot to list before ClearDomainEvents() to preserve count for logging

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Updated DcaSchedulerBackgroundService to remove old-signature PurchaseFailedEvent construction**
- **Found during:** Task 1 (when PurchaseFailedEvent was changed to identity-only)
- **Issue:** DcaSchedulerBackgroundService.cs was calling `new PurchaseFailedEvent("PermanentError", ex.Message, retryCount, DateTimeOffset.UtcNow)` which no longer compiles after the event signature change
- **Fix:** Removed direct PurchaseFailedEvent publishing from scheduler catch blocks; scheduler-level infrastructure failures are logged only (no Purchase aggregate exists at scheduler level to provide a PurchaseId)
- **Files modified:** TradingBot.ApiService/Application/BackgroundJobs/DcaSchedulerBackgroundService.cs
- **Verification:** Build succeeds with 0 errors, all 53 tests pass
- **Committed in:** 26106aa (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - compile error from event signature change)
**Impact on plan:** Necessary consequence of moving to identity-only events; scheduler-level failures are still logged with full exception context.

## Issues Encountered

The plan expected Task 1 to leave compile errors ONLY in DcaExecutionService.cs. However, Purchase.cs itself needed to reference PurchaseCompletedEvent and PurchaseFailedEvent (for behavior method AddDomainEvent calls), which meant those events needed to be updated to identity-only before Purchase.cs could compile. The events were therefore updated as part of Task 1 rather than Task 2. All changes remain correct and the sequence of commits reflects the logical grouping.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- AggregateRoot pattern established; ready for Specification pattern (Phase 16)
- Purchase aggregate encapsulation complete; behavior methods provide clean API for all mutation
- Identity-only events + DB-loading handler pattern ready for Phase 17 (SaveChangesInterceptor auto-dispatch)
- No blockers or concerns

---
*Phase: 15-rich-aggregate-roots*
*Completed: 2026-02-19*

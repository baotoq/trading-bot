---
phase: 17-domain-event-dispatch
plan: 02
subsystem: database
tags: [dapr, outbox, domain-events, dead-letter, ef-core, csharp]

# Dependency graph
requires:
  - phase: 17-domain-event-dispatch
    plan: 01
    provides: DomainEventOutboxInterceptor, IAggregateRoot, OutboxMessage entity registered in DbContext

provides:
  - PubSubRegistry generalized to accept any class type (domain events and integration events)
  - MapPubSub deserializes by runtime type without IntegrationEvent cast
  - Dapr pub-sub and outbox infrastructure wired in Program.cs
  - IDomainEventPublisher for direct outbox publishing of non-aggregate events
  - DomainEventPublisher implementation (creates OutboxMessage + SaveChangesAsync)
  - DeadLetterMessage entity for capturing failed outbox messages after 3 retries
  - IOutboxStore.MoveToDeadLetterAsync for dead-letter promotion
  - EF migration AddDeadLetterMessage for DeadLetterMessages and OutboxMessages tables

affects:
  - 17-03-PLAN.md (domain event subscriptions registration)
  - any service that needs to publish non-aggregate domain events (PurchaseSkippedEvent)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - PubSubRegistry generalized constraint (where TEvent : class) supports both IntegrationEvent and IDomainEvent types
    - Dead-letter pattern: failed outbox messages with RetryCount >= 3 moved to DeadLetterMessages table
    - IDomainEventPublisher for non-aggregate event publishing (directly saves OutboxMessage, bypasses interceptor path)

key-files:
  created:
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/IDomainEventPublisher.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DomainEventPublisher.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DeadLetterMessage.cs
    - TradingBot.ApiService/Infrastructure/Data/Migrations/20260219160600_AddDeadLetterMessage.cs
  modified:
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/ServiceCollectionExtensions.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/WebApplicationExtensions.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessageProcessor.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/EfCore/EfCoreOutboxStore.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/Abstraction/IOutboxStore.cs
    - TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/ServiceCollectionExtensions.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
    - TradingBot.ApiService/Program.cs

key-decisions:
  - "MapPubSub uses object/null check instead of IntegrationEvent cast; mediator.Publish(object) dispatches at runtime to correct INotification handler"
  - "IDomainEventPublisher.PublishDirectAsync calls SaveChangesAsync immediately (standalone save); appropriate for non-aggregate events with no surrounding transaction"
  - "Dead-letter on RetryCount >= 3 check happens BEFORE processing attempt; OutboxMessageProcessor moves exhausted messages to DeadLetterMessages on next pickup cycle"

patterns-established:
  - "PubSubRegistry.Add<TEvent>() where TEvent : class: supports domain events (IDomainEvent) and integration events (IntegrationEvent) in same registry"
  - "Dead-letter pattern: remove from OutboxMessages + insert into DeadLetterMessages in single SaveChangesAsync call within MoveToDeadLetterAsync"

requirements-completed:
  - DE-03

# Metrics
duration: 4min
completed: 2026-02-19
---

# Phase 17 Plan 02: Wire Dapr Pub-Sub and Outbox Infrastructure Summary

**Dapr pub-sub + outbox fully wired in Program.cs with generalized PubSubRegistry, IDomainEventPublisher for non-aggregate events, and dead-letter table for retry-exhausted messages**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-19T16:03:18Z
- **Completed:** 2026-02-19T16:07:10Z
- **Tasks:** 2
- **Files modified:** 12

## Accomplishments

- PubSubRegistry.Add<TEvent>() constraint relaxed to `where TEvent : class` allowing domain events to be registered alongside integration events
- MapPubSub updated to deserialize by runtime Type and use MediatR's `Publish(object)` overload, eliminating IntegrationEvent dependency from the dispatch path
- AddDaprPubSub() and AddOutboxPublishingWithEfCore<TradingBotDbContext>() wired in Program.cs, activating the outbox background processor
- IDomainEventPublisher / DomainEventPublisher created for PurchaseSkippedEvent and other non-aggregate events that need direct outbox publishing without a surrounding aggregate SaveChanges
- DeadLetterMessages table captures failed messages after 3 retries; OutboxMessageProcessor calls MoveToDeadLetterAsync instead of MarkAsAsync(Failed)

## Task Commits

Each task was committed atomically:

1. **Task 1: Generalize PubSubRegistry and wire Dapr+outbox in Program.cs** - `fe37894` (feat)
2. **Task 2: Create IDomainEventPublisher, DeadLetterMessage entity, and dead-letter processor logic** - `40aad99` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/IDomainEventPublisher.cs` - Interface with PublishDirectAsync for non-aggregate domain events
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DomainEventPublisher.cs` - Creates OutboxMessage from IDomainEvent and calls SaveChangesAsync
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/DeadLetterMessage.cs` - AuditedEntity for failed outbox messages (EventName, Payload, FailedAt, LastError, RetryCount)
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/WebApplicationExtensions.cs` - PubSubRegistry.Add<TEvent>() relaxed to `where TEvent : class`; MapPubSub uses null check instead of IntegrationEvent cast
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/ServiceCollectionExtensions.cs` - Subscribe<TEvent>() constraint relaxed to `where TEvent : class`
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessageProcessor.cs` - Calls MoveToDeadLetterAsync when RetryCount >= 3 (was MarkAsAsync(Failed))
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/EfCore/EfCoreOutboxStore.cs` - Implements MoveToDeadLetterAsync
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/Abstraction/IOutboxStore.cs` - Added MoveToDeadLetterAsync method
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/ServiceCollectionExtensions.cs` - Registers IDomainEventPublisher as scoped
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - Added DeadLetterMessages DbSet; configures DeadLetterMessages table with EventName/FailedAt indexes
- `TradingBot.ApiService/Program.cs` - Wired AddDaprPubSub() and AddOutboxPublishingWithEfCore<TradingBotDbContext>()
- `TradingBot.ApiService/Infrastructure/Data/Migrations/20260219160600_AddDeadLetterMessage.cs` - EF migration for DeadLetterMessages and OutboxMessages tables

## Decisions Made

- **MapPubSub mediator.Publish(object):** MediatR's `IMediator.Publish(object)` does runtime dispatch to the correct `INotificationHandler<T>`. Using `object` and null check is cleaner than casting to `INotification` or `IntegrationEvent`, and works for all domain event types.
- **IDomainEventPublisher.PublishDirectAsync calls SaveChangesAsync immediately:** Non-aggregate events have no surrounding aggregate SaveChanges transaction. The publisher creates the OutboxMessage and saves immediately. The DomainEventOutboxInterceptor will see no aggregate domain events when this SaveChanges runs (no tracked aggregates with pending events).
- **Dead-letter check before processing:** RetryCount >= 3 is checked at the START of ProcessOutboxMessagesAsync. This means on the 4th pickup cycle the message is moved to dead-letter without another processing attempt. This is intentional and matches the plan specification.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Re-added missing using directive for Abstraction namespace in ServiceCollectionExtensions.cs**
- **Found during:** Task 1 (PubSubRegistry constraint relaxation)
- **Issue:** Removed `TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction` using with intent to eliminate IntegrationEvent reference, but the namespace also contains IEventPublisher and IMessageBroker needed for DI registration
- **Fix:** Added the using back (IEventPublisher, IMessageBroker are in the Abstraction namespace, not IntegrationEvent)
- **Files modified:** TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/ServiceCollectionExtensions.cs
- **Verification:** `dotnet build` succeeded after fix
- **Committed in:** fe37894 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 compile error / Rule 1 bug fix)
**Impact on plan:** Minor fix during development, no scope impact.

## Issues Encountered

Removed the `Abstraction` using from ServiceCollectionExtensions.cs while stripping out the `IntegrationEvent` constraint — but `IEventPublisher` and `IMessageBroker` live in the same Abstraction namespace. Build failure caught it immediately; fixed by restoring the using.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Dapr pub-sub and outbox infrastructure is fully wired and operational
- PubSubRegistry is generalized to accept domain event types
- IDomainEventPublisher is available for PurchaseSkippedEvent and other non-aggregate events
- Dead-letter table captures retry-exhausted messages for inspection
- Plan 03 can now register domain event subscriptions (PurchaseCompletedEvent, PurchaseSkippedEvent, etc.) via PubSubRegistry

## Self-Check: PASSED

All created files confirmed present:
- FOUND: IDomainEventPublisher.cs
- FOUND: DomainEventPublisher.cs
- FOUND: DeadLetterMessage.cs
- FOUND: Migration 20260219160600_AddDeadLetterMessage.cs
- FOUND: 17-02-SUMMARY.md

All task commits verified:
- FOUND: fe37894 (Task 1)
- FOUND: 40aad99 (Task 2)

---
*Phase: 17-domain-event-dispatch*
*Completed: 2026-02-19*

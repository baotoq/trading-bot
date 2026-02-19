---
phase: 17-domain-event-dispatch
plan: 03
subsystem: infra
tags: [dapr, pubsub, domain-events, outbox, mediatR, dca]

# Dependency graph
requires:
  - phase: 17-domain-event-dispatch
    provides: "Plan 01: DomainEventOutboxInterceptor wiring domain events to outbox on SaveChanges"
  - phase: 17-domain-event-dispatch
    provides: "Plan 02: IDomainEventPublisher, PubSubRegistry, MapPubSub, OutboxMessageProcessor with Dapr delivery"

provides:
  - All 6 domain events subscribed in PubSubRegistry for Dapr delivery
  - DcaExecutionService cleaned of manual MediatR event dispatch
  - PurchaseSkippedEvent published via IDomainEventPublisher.PublishDirectAsync (outbox path)
  - Single dispatch path: ALL domain events flow exclusively through outbox/Dapr

affects:
  - 18-integration-testing
  - any future domain event additions

# Tech tracking
tech-stack:
  added: []
  patterns:
    - PubSubRegistry.Subscribe<T>() fluent registration pattern for all domain event types
    - IDomainEventPublisher.PublishDirectAsync for non-aggregate (non-transactional) events
    - Single dispatch path: aggregate events via SaveChangesInterceptor outbox, non-aggregate via PublishDirectAsync outbox

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Program.cs
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs

key-decisions:
  - "All 6 domain events subscribed in PubSubRegistry using fluent .Subscribe<T>() chaining on returned registry value"
  - "DcaExecutionService manual dispatch block (Steps 8-9) removed -- interceptor from Plan 01 handles this automatically during SaveChangesAsync"
  - "PurchaseSkippedEvent now uses IDomainEventPublisher.PublishDirectAsync (same outbox path as aggregate events via Dapr)"
  - "IPublisher (MediatR) fully removed from DcaExecutionService; all event dispatch via single outbox pipeline"

patterns-established:
  - "Single dispatch path: ALL domain events flow through outbox -> DaprMessageBroker -> MapPubSub -> mediator.Publish -> INotificationHandler<T>"
  - "PubSubRegistry fluent subscription registration captures AddDaprPubSub() return value, chains Subscribe<T>() calls"

requirements-completed:
  - DE-04

# Metrics
duration: 2min
completed: 2026-02-19
---

# Phase 17 Plan 03: Domain Event Subscriptions and Single Dispatch Path Summary

**Eliminated manual MediatR dispatch from DcaExecutionService by wiring all 6 domain events through PubSubRegistry and routing PurchaseSkippedEvent via IDomainEventPublisher outbox path**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-19T16:10:37Z
- **Completed:** 2026-02-19T16:12:27Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Registered all 6 domain event types (PurchaseCreated, PurchaseCompleted, PurchaseFailed, PurchaseSkipped, DcaConfigurationCreated, DcaConfigurationUpdated) in PubSubRegistry for Dapr delivery
- Removed manual Steps 8-9 event dispatch block from DcaExecutionService (interceptor from Plan 01 handles this automatically)
- Replaced all 3 `publisher.Publish(PurchaseSkippedEvent)` calls with `domainEventPublisher.PublishDirectAsync()`, fully removing MediatR IPublisher dependency
- Achieved single dispatch path: ALL domain events now flow exclusively through outbox/Dapr pipeline

## Task Commits

Each task was committed atomically:

1. **Task 1: Subscribe domain events in PubSubRegistry, remove manual dispatch** - `107f3d5` (feat)
2. **Task 2: Replace IPublisher with IDomainEventPublisher in DcaExecutionService** - `196f9c5` (feat)

**Plan metadata:** (docs commit to follow)

## Files Created/Modified

- `TradingBot.ApiService/Program.cs` - Added using for Application.Events, captured AddDaprPubSub() return value, chained 6 Subscribe<T>() calls
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Removed MediatR using/IPublisher, replaced with IDomainEventPublisher, removed manual dispatch block, updated Step 7 comment

## Decisions Made

- All 6 domain events subscribed using the fluent PubSubRegistry pattern (captures return value of AddDaprPubSub() and chains Subscribe<T>() calls)
- Manual dispatch block (Steps 8-9) was safe to remove because the DomainEventOutboxInterceptor (Plan 01) automatically handles domain events accumulated by the Purchase aggregate during SaveChangesAsync
- PurchaseSkippedEvent uses PublishDirectAsync (not aggregate SaveChanges path) because it's fired before a Purchase aggregate exists -- the outbox message is created and saved immediately

## Deviations from Plan

None - plan executed exactly as written. PurchaseSkippedHandler already used `notification.OccurredAt` (renamed in Plan 01) so no updates were needed there.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 17 (Domain Event Dispatch) is now fully complete across all 3 plans
- Single dispatch path established: ALL domain events flow through outbox -> DaprMessageBroker -> MapPubSub -> MediatR mediator.Publish -> INotificationHandler<T>
- Existing handlers (PurchaseCompletedHandler, PurchaseFailedHandler, PurchaseSkippedHandler) continue working unchanged via MapPubSub mediator dispatch
- Ready for Phase 18 (integration testing or next planned phase)

---
*Phase: 17-domain-event-dispatch*
*Completed: 2026-02-19*

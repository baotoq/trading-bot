# Phase 17: Domain Event Dispatch - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Aggregates raise domain events when state changes, and those events reliably dispatch after persistence via SaveChangesInterceptor. Domain events automatically bridge to integration events via the existing outbox pattern, enabling loose coupling between aggregates. Existing MediatR handlers migrate to consume from Dapr (outbox-only dispatch path).

</domain>

<decisions>
## Implementation Decisions

### Event-to-outbox bridging
- ALL domain events auto-bridge to outbox messages (no selective filtering)
- Outbox messages reuse domain event type name directly (e.g., `PurchaseCompletedEvent`) — no separate integration event naming
- Outbox insert happens in the SAME transaction as SaveChanges — if save fails, no outbox message; if outbox insert fails, entire transaction rolls back
- Single dispatch path: domain events go through outbox/Dapr ONLY — existing MediatR in-process handlers migrate to Dapr subscriber endpoints
- No dual dispatch (no MediatR + outbox parallel paths)

### Handler failure behavior
- Failed outbox messages (after 3 retries) go to dead-letter table for manual inspection/replay
- SaveChanges transaction rolls back if outbox insert fails — no data saved without its events
- Dapr handles retry delivery to subscriber endpoints — handlers are simple (succeed or throw)
- Failed event visibility through logs only — no dashboard UI for event monitoring

### Non-aggregate events
- PurchaseSkippedEvent routes through outbox manually (service creates outbox message directly, not via interceptor)
- PurchaseSkippedEvent keeps IDomainEvent interface for consistent type hierarchy
- Build a general helper method (e.g., `eventPublisher.PublishDirectAsync(event)`) for non-aggregate events — anticipate more service-level events in the future
- Helper encapsulates outbox insertion for events that don't originate from aggregates

### Event granularity
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

</decisions>

<specifics>
## Specific Ideas

- Current manual dispatch pattern in DcaExecutionService (iterate DomainEvents, publish via MediatR, clear) should be fully replaced by interceptor
- Existing PurchaseCompletedHandler (Telegram notification), PurchaseFailedHandler, PurchaseSkippedHandler all migrate from `INotificationHandler<T>` to Dapr subscription endpoints
- OutboxEventPublisher already handles IntegrationEvent → outbox; domain event bridging should integrate with this existing infrastructure

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 17-domain-event-dispatch*
*Context gathered: 2026-02-19*

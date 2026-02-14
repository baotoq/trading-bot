# Requirements: BTC Smart DCA Bot — v2.0 DDD Foundation

**Defined:** 2026-02-14
**Core Value:** Upgrade building blocks and domain model to proper DDD tactical patterns — rich aggregates, value objects, strongly-typed IDs, domain event dispatch, and clean event bridging.

## v1 Requirements

Requirements for v2.0 milestone. Each maps to roadmap phases.

### Type Safety

- [ ] **TS-01**: All entity IDs use strongly-typed wrappers (PurchaseId, DailyPriceId, IngestionJobId, DcaConfigurationId) instead of raw Guid
- [ ] **TS-02**: Domain primitives use value objects with validation (Price, Quantity, Multiplier, UsdAmount, Symbol)
- [ ] **TS-03**: Value objects persist via auto-generated EF Core converters registered in ConfigureConventions
- [ ] **TS-04**: Value objects serialize/deserialize correctly in all API endpoints (JSON round-trip)

### Domain Model

- [ ] **DM-01**: Base entity hierarchy includes AggregateRoot base class with domain event collection
- [ ] **DM-02**: Purchase aggregate enforces invariants (price > 0, quantity > 0, valid symbol) via factory method
- [ ] **DM-03**: DcaConfiguration aggregate enforces invariants (tiers ascending, daily amount > 0, valid schedule) via encapsulated behavior methods
- [ ] **DM-04**: Entities use private setters — state changes only through domain methods

### Domain Events

- [ ] **DE-01**: Aggregates raise domain events when state changes (PurchaseExecuted, ConfigurationUpdated)
- [ ] **DE-02**: Domain events dispatch after SaveChanges via SaveChangesInterceptor (consistency guarantee)
- [ ] **DE-03**: Domain events automatically bridge to integration events via existing outbox pattern
- [ ] **DE-04**: Existing MediatR event handlers continue working with new dispatch mechanism

### Error Handling

- [ ] **EH-01**: Domain operations return ErrorOr<T> instead of throwing exceptions for expected failures
- [ ] **EH-02**: Minimal API endpoints map ErrorOr results to appropriate HTTP status codes
- [ ] **EH-03**: Application services use Result pattern for orchestration (no try/catch for domain logic)

### Query Patterns

- [ ] **QP-01**: Complex queries encapsulated in Specification classes (reusable, testable)
- [ ] **QP-02**: Specifications translate to server-side SQL (no client-side evaluation)
- [ ] **QP-03**: Dashboard queries (purchases, daily prices) use specifications for filtering/pagination

## Future Requirements

Deferred to future milestone. Tracked but not in current roadmap.

### Advanced Domain Patterns

- **ADP-01**: Smart enums with behavior for workflow states (OrderStatus, JobStatus)
- **ADP-02**: CQRS read models separate from aggregates for optimized queries
- **ADP-03**: Domain event versioning for schema evolution

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Generic Repository<T> | EF Core DbContext is already unit of work — adds no value |
| Event Sourcing | Massive complexity, not needed for DCA bot state-based persistence |
| Separate domain/persistence models | Over-engineering for this domain size |
| Domain events via message bus (Dapr) | Domain events are in-process (MediatR); integration events use Dapr |
| Reflection-based value object libraries | Vogen source generation has zero runtime overhead |
| Cross-aggregate transactions | Violates DDD boundaries — use domain events for eventual consistency |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TS-01 | — | Pending |
| TS-02 | — | Pending |
| TS-03 | — | Pending |
| TS-04 | — | Pending |
| DM-01 | — | Pending |
| DM-02 | — | Pending |
| DM-03 | — | Pending |
| DM-04 | — | Pending |
| DE-01 | — | Pending |
| DE-02 | — | Pending |
| DE-03 | — | Pending |
| DE-04 | — | Pending |
| EH-01 | — | Pending |
| EH-02 | — | Pending |
| EH-03 | — | Pending |
| QP-01 | — | Pending |
| QP-02 | — | Pending |
| QP-03 | — | Pending |

**Coverage:**
- v1 requirements: 18 total
- Mapped to phases: 0
- Unmapped: 18 ⚠️ (awaiting roadmap creation)

---
*Requirements defined: 2026-02-14*
*Last updated: 2026-02-14 after initial definition*

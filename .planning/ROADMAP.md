# Roadmap: BTC Smart DCA Bot

## Milestones

- **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- **v1.1 Backtesting Engine** -- Phases 5-8 (shipped 2026-02-13) -- [archive](milestones/v1.1-ROADMAP.md)
- **v1.2 Web Dashboard** -- Phases 9-12 (shipped 2026-02-14) -- [archive](milestones/v1.2-ROADMAP.md)
- **v2.0 DDD Foundation** -- Phases 13-18 (in progress)

## Phases

<details>
<summary>v1.0 Daily BTC Smart DCA (Phases 1-4) -- SHIPPED 2026-02-12</summary>

- [x] Phase 1: Foundation & Hyperliquid Client (3/3 plans) -- completed 2026-02-12
- [x] Phase 2: Core DCA Engine (3/3 plans) -- completed 2026-02-12
- [x] Phase 3: Smart Multipliers (3/3 plans) -- completed 2026-02-12
- [x] Phase 4: Enhanced Notifications & Observability (3/3 plans) -- completed 2026-02-12

</details>

<details>
<summary>v1.1 Backtesting Engine (Phases 5-8) -- SHIPPED 2026-02-13</summary>

- [x] Phase 5: MultiplierCalculator Extraction (1/1 plan) -- completed 2026-02-13
- [x] Phase 6: Backtest Simulation Engine (2/2 plans) -- completed 2026-02-13
- [x] Phase 7: Historical Data Pipeline (2/2 plans) -- completed 2026-02-13
- [x] Phase 8: API Endpoints & Parameter Sweep (2/2 plans) -- completed 2026-02-13

</details>

<details>
<summary>v1.2 Web Dashboard (Phases 9-12) -- SHIPPED 2026-02-14</summary>

- [x] Phase 9: Infrastructure & Aspire Integration (2/2 plans) -- completed 2026-02-13
- [x] Phase 9.1: Migrate Dashboard to Fresh Nuxt Setup (1/1 plan) -- completed 2026-02-13
- [x] Phase 10: Dashboard Core (3/3 plans) -- completed 2026-02-13
- [x] Phase 11: Backtest Visualization (4/4 plans) -- completed 2026-02-14
- [x] Phase 12: Configuration Management (2/2 plans) -- completed 2026-02-14

</details>

### v2.0 DDD Foundation (In Progress)

**Milestone Goal:** Upgrade building blocks and domain model to proper DDD tactical patterns -- rich aggregates, value objects, strongly-typed IDs, domain event dispatch, and clean event bridging.

- [ ] **Phase 13: Strongly-Typed IDs** - Replace raw Guid IDs with source-generated typed wrappers
- [ ] **Phase 14: Value Objects** - Domain primitives with encapsulated validation
- [ ] **Phase 15: Rich Aggregate Roots** - Base entity hierarchy, factory methods, and invariant enforcement
- [ ] **Phase 16: Result Pattern** - Explicit error handling replacing exceptions in domain operations
- [ ] **Phase 17: Domain Event Dispatch** - Aggregate-raised events dispatched after SaveChanges
- [ ] **Phase 18: Specification Pattern** - Reusable, testable query composition

## Phase Details

### Phase 13: Strongly-Typed IDs
**Goal**: Entity IDs are type-safe -- impossible to pass a PurchaseId where a DailyPriceId is expected
**Depends on**: Nothing (first phase of v2.0)
**Requirements**: TS-01
**Success Criteria** (what must be TRUE):
  1. All entities use strongly-typed ID wrappers (PurchaseId, DailyPriceId, IngestionJobId, DcaConfigurationId) instead of raw Guid
  2. EF Core persists and loads entities with typed IDs without schema changes (Guid columns unchanged in database)
  3. All API endpoints serialize/deserialize typed IDs as plain GUIDs in JSON responses (dashboard unaffected)
  4. All existing tests pass with typed IDs (no behavioral regression)
**Plans**: TBD

### Phase 14: Value Objects
**Goal**: Domain primitives enforce their own validity -- invalid prices, quantities, or amounts cannot exist at runtime
**Depends on**: Phase 13 (Vogen infrastructure already installed)
**Requirements**: TS-02, TS-03, TS-04
**Success Criteria** (what must be TRUE):
  1. Core domain primitives (Price, Quantity, Multiplier, UsdAmount, Symbol) are value objects with built-in validation (e.g., Price rejects negative values)
  2. Value objects persist via EF Core converters registered in ConfigureConventions (no manual mapping per property)
  3. All API endpoints serialize/deserialize value objects correctly in JSON (round-trip: send value, receive same value)
  4. Existing tests pass with value objects replacing raw decimal/string fields
**Plans**: TBD

### Phase 15: Rich Aggregate Roots
**Goal**: Aggregates own their state changes and enforce business rules -- no external code can put an aggregate into an invalid state
**Depends on**: Phase 14 (aggregates use value objects for properties)
**Requirements**: DM-01, DM-02, DM-03, DM-04
**Success Criteria** (what must be TRUE):
  1. Base entity hierarchy includes AggregateRoot base class with domain event collection (AddDomainEvent, ClearDomainEvents)
  2. Purchase aggregate enforces invariants (price > 0, quantity > 0, valid symbol) via static factory method -- constructor is private
  3. DcaConfiguration aggregate enforces invariants (tiers ascending, daily amount > 0, valid schedule) via encapsulated behavior methods -- no public setters
  4. Entities use private setters throughout -- all state changes go through domain methods
  5. Application services create aggregates through factory methods and mutate through behavior methods (no direct property assignment)
**Plans**: TBD

### Phase 16: Result Pattern
**Goal**: Domain operations communicate failures through return values, not exceptions -- callers handle errors explicitly
**Depends on**: Phase 15 (domain methods exist to return results from)
**Requirements**: EH-01, EH-02, EH-03
**Success Criteria** (what must be TRUE):
  1. Domain operations (factory methods, behavior methods) return ErrorOr<T> for expected failures instead of throwing exceptions
  2. Minimal API endpoints map ErrorOr results to appropriate HTTP status codes (400 for validation, 404 for not found, 409 for conflict)
  3. Application services use Result pattern for orchestration -- no try/catch wrapping domain logic
**Plans**: TBD

### Phase 17: Domain Event Dispatch
**Goal**: Aggregates raise domain events when state changes, and those events reliably dispatch after persistence -- enabling loose coupling between aggregates
**Depends on**: Phase 15 (AggregateRoot base class with event collection), Phase 16 (events raised on successful operations)
**Requirements**: DE-01, DE-02, DE-03, DE-04
**Success Criteria** (what must be TRUE):
  1. Aggregates raise domain events when state changes (PurchaseExecuted when purchase created, ConfigurationUpdated when config modified)
  2. Domain events dispatch after SaveChanges via SaveChangesInterceptor -- if SaveChanges fails, no events dispatch
  3. Domain events automatically bridge to integration events via existing outbox pattern (domain event triggers outbox message creation)
  4. Existing MediatR event handlers continue working with new dispatch mechanism (no handler rewrites needed)
**Plans**: TBD

### Phase 18: Specification Pattern
**Goal**: Complex queries are encapsulated in reusable, testable specification classes -- query logic lives in the domain, not scattered across services
**Depends on**: Phase 14 (specifications use value objects in filter criteria)
**Requirements**: QP-01, QP-02, QP-03
**Success Criteria** (what must be TRUE):
  1. Complex queries encapsulated in Specification classes using Ardalis.Specification (e.g., PurchasesByDateRangeSpec, DailyPricesByPeriodSpec)
  2. Specifications translate to server-side SQL -- no client-side evaluation (verified via EF Core query logging)
  3. Dashboard queries (purchases with filtering/pagination, daily prices by date range) use specifications instead of inline LINQ
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 13 -> 14 -> 15 -> 16 -> 17 -> 18

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation & Hyperliquid Client | v1.0 | 3/3 | Complete | 2026-02-12 |
| 2. Core DCA Engine | v1.0 | 3/3 | Complete | 2026-02-12 |
| 3. Smart Multipliers | v1.0 | 3/3 | Complete | 2026-02-12 |
| 4. Enhanced Notifications & Observability | v1.0 | 3/3 | Complete | 2026-02-12 |
| 5. MultiplierCalculator Extraction | v1.1 | 1/1 | Complete | 2026-02-13 |
| 6. Backtest Simulation Engine | v1.1 | 2/2 | Complete | 2026-02-13 |
| 7. Historical Data Pipeline | v1.1 | 2/2 | Complete | 2026-02-13 |
| 8. API Endpoints & Parameter Sweep | v1.1 | 2/2 | Complete | 2026-02-13 |
| 9. Infrastructure & Aspire Integration | v1.2 | 2/2 | Complete | 2026-02-13 |
| 9.1 Migrate Dashboard to Fresh Nuxt Setup | v1.2 | 1/1 | Complete | 2026-02-13 |
| 10. Dashboard Core | v1.2 | 3/3 | Complete | 2026-02-13 |
| 11. Backtest Visualization | v1.2 | 4/4 | Complete | 2026-02-14 |
| 12. Configuration Management | v1.2 | 2/2 | Complete | 2026-02-14 |
| 13. Strongly-Typed IDs | v2.0 | 0/? | Not started | - |
| 14. Value Objects | v2.0 | 0/? | Not started | - |
| 15. Rich Aggregate Roots | v2.0 | 0/? | Not started | - |
| 16. Result Pattern | v2.0 | 0/? | Not started | - |
| 17. Domain Event Dispatch | v2.0 | 0/? | Not started | - |
| 18. Specification Pattern | v2.0 | 0/? | Not started | - |

---
*Roadmap updated: 2026-02-14 after v2.0 DDD Foundation roadmap creation*

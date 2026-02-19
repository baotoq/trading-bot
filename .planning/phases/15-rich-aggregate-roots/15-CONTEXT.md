# Phase 15: Rich Aggregate Roots - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Refactor entities into proper DDD aggregates with private constructors, static factory methods, behavior methods, and invariant enforcement. Aggregates own their state changes -- no external code can put an aggregate into an invalid state. Depends on Phase 14 value objects being in place.

</domain>

<decisions>
## Implementation Decisions

### Aggregate Boundaries
- **Claude's Discretion:** Evaluate which entities are aggregate roots vs child entities vs data carriers based on which have real business invariants to enforce
- **Claude's Discretion:** Whether DcaConfiguration owns MultiplierTiers as child entities or keeps them as JSON column -- evaluate trade-offs based on current schema
- OutboxMessage stays as infrastructure record -- no DDD ceremony, it's plumbing not domain
- **Claude's Discretion:** Whether dashboard config updates go through aggregate methods or bypass them -- evaluate based on where invariant enforcement matters most

### Encapsulation Depth
- EF Core parameterless constructors: **protected** (not private) -- allows potential inheritance
- Private setters on **aggregate roots only** -- simpler entities like DailyPrice keep public setters if they're data carriers
- Generic `AggregateRoot<TId> : BaseEntity<TId>` -- carries both ID typing and domain event collection in one hierarchy
- Vogen value objects left as-is -- already immutable by design, no need for private setters on top

### Behavior Method Design
- Factory methods take **value objects** directly (e.g., `Purchase.Create(Symbol, Price, Quantity, UsdAmount)`) -- caller constructs VOs, factory validates relationships
- Factory methods return **entity directly** (not ErrorOr<T>) -- throws on invalid input; Result pattern comes in Phase 16
- **Fine-grained behavior methods** on DcaConfiguration: `UpdateDailyAmount()`, `UpdateSchedule()`, `UpdateTiers()`, `UpdateBearMarket()` etc. -- each enforces its own invariants
- Aggregate enforces **tier ordering** in `UpdateTiers()` -- ascending drop percentages, no overlaps, sane multiplier ranges (this is a domain invariant, not a UI concern)

### Domain Event Collection
- Both **creation and mutation** raise domain events -- PurchaseCreated, ConfigurationUpdated, TiersUpdated, etc.
- Events carry **identity only** (aggregate ID) -- handlers load the aggregate if they need details
- **Refactor existing events now** to use AggregateRoot.AddDomainEvent() -- clean break, single pattern going forward
- Simple `List<IDomainEvent>` with `AddDomainEvent()` and `ClearDomainEvents()` -- no event versioning or metadata

</decisions>

<specifics>
## Specific Ideas

No specific requirements -- open to standard approaches.

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope.

</deferred>

---

*Phase: 15-rich-aggregate-roots*
*Context gathered: 2026-02-19*

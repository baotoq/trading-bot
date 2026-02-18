# Phase 13: Strongly-Typed IDs - Context

**Gathered:** 2026-02-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace raw Guid IDs with source-generated typed wrappers (Vogen 8.0.4) across all entities (PurchaseId, DailyPriceId, IngestionJobId, DcaConfigurationId). Type safety prevents mixing up ID types at compile time. Database schema unchanged (Guid columns persist). Dashboard unaffected (JSON serializes as plain GUID strings).

</domain>

<decisions>
## Implementation Decisions

### Foreign key scope
- All FK properties use the target entity's typed ID (e.g., `Purchase.DcaConfigurationId` becomes type `DcaConfigurationId`)
- Only EF Core-mapped entity properties get typed IDs — infrastructure tables (outbox messages, etc.) keep raw Guid
- Preserve UUIDv7 generation for all typed IDs (time-ordered, index-friendly)
- Service and handler method signatures use typed IDs throughout the call chain (e.g., `GetPurchaseById(PurchaseId id)`)

### API surface typing
- Endpoint route parameters bind directly to typed IDs (e.g., `/{id:PurchaseId}`) — requires custom model binder or Vogen integration
- Query parameters also bind to typed IDs — consistent typing across all parameter sources
- JSON serialization is transparent: typed IDs serialize/deserialize as plain GUID strings (dashboard sees no change)
- Dashboard TypeScript types use branded types to mirror backend safety (e.g., `type PurchaseId = string & { __brand: 'PurchaseId' }`)

### Conversion ergonomics
- Implicit conversion both directions: Guid → TypedId and TypedId → Guid — minimal ceremony, IDs feel like Guids with extra safety
- Each typed ID has a `.New()` factory method that generates UUIDv7 internally — clean, discoverable creation pattern
- Request/response bodies deserialize plain GUID strings directly into typed IDs

### Rollout strategy
- Two-plan approach: Plan 1 = Vogen setup + all ID type definitions + generic `BaseEntity<TId>`. Plan 2 = Apply typed IDs to all entities + update all callers
- Each plan leaves the codebase fully compiling — no broken intermediate state
- EF Core migration created if converter registration requires schema changes (expected: no migration needed, just value converters)
- `BaseEntity` refactored to generic `BaseEntity<TId>` — prepares for Phase 15 `AggregateRoot<TId>` hierarchy

### Claude's Discretion
- Value semantics configuration (equality operators, IComparable, GetHashCode) — configure Vogen appropriately
- EF Core converter registration approach (ConfigureConventions vs per-property)
- Exact Vogen attribute configuration and global settings
- Test helper patterns for creating entities with typed IDs

</decisions>

<specifics>
## Specific Ideas

- Typed IDs should feel invisible in day-to-day use — implicit conversions mean existing code patterns mostly stay the same
- `PurchaseId.New()` is the standard way to create new IDs (replaces direct UUIDv7 calls)
- Dashboard branded types: lightweight type safety without runtime overhead (compile-time only via TypeScript)
- Generic `BaseEntity<TId>` sets up the entity hierarchy for the entire v2.0 DDD Foundation milestone

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 13-strongly-typed-ids*
*Context gathered: 2026-02-18*

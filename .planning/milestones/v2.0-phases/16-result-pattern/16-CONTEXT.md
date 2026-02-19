# Phase 16: Result Pattern - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Domain operations (behavior methods on aggregates) communicate failures through ErrorOr<T> return values instead of exceptions. Minimal API endpoints map ErrorOr results to appropriate HTTP status codes. Application services use Result pattern for orchestration. Factory methods and value object constructors are NOT changed — they already enforce invariants via construction.

</domain>

<decisions>
## Implementation Decisions

### Error classification
- Infrastructure failures (DB down, Hyperliquid API timeout, Redis unavailable) stay as thrown exceptions — caught at middleware/background service level
- Value object constructors keep throwing on invalid input — invalid values "can't exist" at runtime
- Factory methods (e.g., Purchase.Create()) stay as-is — value object parameters already guarantee validity
- Only aggregate behavior methods that can fail based on state return ErrorOr<T>
- DcaOptionsValidator stays as defense-in-depth alongside domain ErrorOr validation

### Error granularity
- Fine-grained errors per failure reason (e.g., InvalidTierConfiguration, ScheduleOverlap) — not one generic error per aggregate
- Error types live next to their aggregate (e.g., DcaConfigurationErrors.cs alongside DcaConfiguration.cs)
- Errors carry code + human-readable message only — no rich metadata objects (e.g., Error.Validation("TiersNotAscending", "Tiers must be in ascending order"))
- Short error codes without aggregate prefix (e.g., "TiersNotAscending" not "DcaConfiguration.TiersNotAscending")

### API error responses
- RFC 7807 Problem Details format using .NET's built-in TypedResults.Problem() support
- Dashboard NOT updated in this phase — API-side changes only
- Background services (DcaScheduler, IngestionJob) check .IsError, log error details, AND raise domain events on failure
- Shared extension method (e.g., ToHttpResult()) on ErrorOr<T> to map error types to HTTP status codes consistently across all endpoints

### Migration scope
- All aggregates (Purchase + DcaConfiguration) get ErrorOr in one pass — not phased rollout
- Existing tests updated to assert on ErrorOr results (result.IsError, result.FirstError.Code) — not new tests alongside

### Claude's Discretion
- Whether application services return ErrorOr to callers or handle errors internally — Claude determines the right boundary based on service patterns
- ErrorOr built-in error categories (Error.Validation, Error.NotFound, Error.Conflict) vs custom error types — Claude picks what best fits the codebase
- Exact plan splitting (how many plans, what goes in each)

</decisions>

<specifics>
## Specific Ideas

- ErrorOr 2.0.1 already in project dependencies (noted in STATE.md decisions)
- Background services should raise domain events on ErrorOr failures (e.g., PurchaseFailedEvent) — enables downstream reaction
- Shared ToHttpResult() extension keeps endpoint mapping DRY and consistent

</specifics>

<deferred>
## Deferred Ideas

- Dashboard error handling for Problem Details format — future phase or enhancement
- Retry/circuit breaker patterns for infrastructure failures — separate concern

</deferred>

---

*Phase: 16-result-pattern*
*Context gathered: 2026-02-19*

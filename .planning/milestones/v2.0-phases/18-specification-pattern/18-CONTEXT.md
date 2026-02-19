# Phase 18: Specification Pattern - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Encapsulate complex queries into reusable, testable specification classes using Ardalis.Specification. Query logic moves from inline LINQ in services/endpoints into composable spec classes. Only complex queries are targeted -- simple lookups and aggregate queries stay as inline LINQ.

</domain>

<decisions>
## Implementation Decisions

### Query coverage
- Complex queries only (~7 queries): cursor-paginated purchase history, price chart queries, data range aggregates with dynamic filters
- Simple lookups (FindAsync, single-condition FirstOrDefault) stay as inline LINQ
- Moderate queries (2-3 conditions) stay as inline LINQ even if duplicated -- deduplication is a separate concern
- Aggregate queries (GroupBy + Sum/Count) stay as inline LINQ -- poor fit for spec pattern
- Raw SQL queries (gap detection with generate_series) -- Claude's discretion on whether to wrap in spec
- Purchase history endpoint (5 dynamic filters, cursor pagination) is the primary target and first spec to implement

### Spec granularity
- Composable specs: small building-block specs that combine at call sites
- One filter per spec: DateRangeSpec, TierFilterSpec, CursorSpec, StatusFilterSpec, etc. -- maximum composability
- Specs handle filtering and sorting only -- no .Select() projection; callers handle DTO projection on the returned IQueryable
- Spec classes live in `Application/Specifications/` (application layer, not domain)

### Repository layer
- No repository abstraction -- keep DbContext injection as-is
- Add DbSet extension methods (e.g., `.WithSpecification(spec)`) that return IQueryable<T> using Ardalis SpecificationEvaluator
- Reads only: specs for query side; writes stay on DbContext directly (Add, SaveChanges unchanged)
- Extension method returns IQueryable<T> so callers can chain .Select() projection and .ToListAsync()
- Tests: integration tests with TestContainers against real PostgreSQL to verify SQL translation

### Pagination & sorting
- Pagination stays at endpoint level -- specs don't encapsulate cursor/Take logic
- Default sort orders baked into specs (e.g., purchases always descending by CreatedAt)
- Callers apply cursor comparison and Take to the spec-returned IQueryable

### Claude's Discretion
- Whether to wrap raw SQL gap detection in a spec or leave as-is
- Pagination hasMore indicator pattern (current pageSize+1 or alternative)
- Exact composable spec naming conventions
- Which specific complex queries beyond purchase history get spec treatment in which order

</decisions>

<specifics>
## Specific Ideas

- User wants integration tests with TestContainers for spec verification (not in-memory unit tests)
- Purchase history endpoint is the showcase query -- implement first to validate the pattern
- Composable pattern should feel natural: combine small specs at call site, not monolithic per-use-case specs

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 18-specification-pattern*
*Context gathered: 2026-02-19*

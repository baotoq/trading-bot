---
phase: 18-specification-pattern
plan: 01
subsystem: database
tags: [ardalis-specification, entity-framework-core, query-specification, purchase, daily-price]

# Dependency graph
requires:
  - phase: 17-domain-event-dispatch
    provides: Purchase and DailyPrice aggregate models with EF Core context
provides:
  - Ardalis.Specification 9.3.1 installed with EntityFrameworkCore evaluator
  - WithSpecification<T> extension on IQueryable<T> for chained spec composition
  - 6 composable Purchase specification classes
  - 1 composable DailyPrice specification class
affects: [18-02-PLAN, dashboard-endpoints, query-composition]

# Tech tracking
tech-stack:
  added:
    - Ardalis.Specification 9.3.1
    - Ardalis.Specification.EntityFrameworkCore 9.3.1
  patterns:
    - Composable specification pattern: each spec applies exactly one filter or sort concern
    - WithSpecification extension wraps SpecificationEvaluator.Default.GetQuery() for IQueryable<T> chaining
    - Ordering specs (PurchasesOrderedByDateSpec, DailyPriceByDateRangeSpec) own AsNoTracking
    - PurchaseFilledStatusSpec has no OrderBy to avoid ordering conflicts when composed

key-files:
  created:
    - TradingBot.ApiService/Application/Specifications/SpecificationExtensions.cs
    - TradingBot.ApiService/Application/Specifications/Purchases/PurchaseFilledStatusSpec.cs
    - TradingBot.ApiService/Application/Specifications/Purchases/PurchaseDateRangeSpec.cs
    - TradingBot.ApiService/Application/Specifications/Purchases/PurchaseTierFilterSpec.cs
    - TradingBot.ApiService/Application/Specifications/Purchases/PurchaseCursorSpec.cs
    - TradingBot.ApiService/Application/Specifications/Purchases/PurchasesOrderedByDateSpec.cs
    - TradingBot.ApiService/Application/Specifications/DailyPrices/DailyPriceByDateRangeSpec.cs
  modified:
    - TradingBot.ApiService/TradingBot.ApiService.csproj

key-decisions:
  - "PurchaseFilledStatusSpec has only Where, no OrderBy -- avoids ordering conflicts when composed with PurchasesOrderedByDateSpec or PurchaseCursorSpec"
  - "PurchaseCursorSpec includes its own OrderByDescending because cursor comparison inherently assumes descending order"
  - "AsNoTracking lives in ordering specs (PurchasesOrderedByDateSpec, DailyPriceByDateRangeSpec) as the always-applied base for read queries"
  - "WithSpecification uses SpecificationEvaluator.Default.GetQuery() on IQueryable<T> (not DbSet<T>) to enable chaining multiple specs"

patterns-established:
  - "One-concern-per-spec: filter specs have Where only, ordering specs have OrderBy + AsNoTracking"
  - "No .Select() or .Take() inside specs -- those stay at call sites for flexibility"
  - "PurchaseTierFilterSpec handles Base special case (null or 'Base') matching existing DashboardEndpoints behavior"

requirements-completed: [QP-01]

# Metrics
duration: 2min
completed: 2026-02-19
---

# Phase 18 Plan 01: Specification Pattern Summary

**Ardalis.Specification 9.3.1 installed with 7 composable spec classes (6 Purchase + 1 DailyPrice) and WithSpecification IQueryable extension for chained composition**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-19T16:50:21Z
- **Completed:** 2026-02-19T16:52:09Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Installed Ardalis.Specification 9.3.1 and Ardalis.Specification.EntityFrameworkCore 9.3.1
- Created WithSpecification<T> extension on IQueryable<T> using SpecificationEvaluator for multi-spec chaining
- Created 6 Purchase specs: PurchaseFilledStatusSpec, PurchaseDateRangeSpec, PurchaseTierFilterSpec, PurchaseCursorSpec, PurchasesOrderedByDateSpec
- Created 1 DailyPrice spec: DailyPriceByDateRangeSpec with combined symbol + date filter, ascending sort, and AsNoTracking
- All 53 existing tests continue to pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Install Ardalis.Specification packages and create WithSpecification extension** - `1d532df` (chore)
2. **Task 2: Create all composable specification classes** - `ad3b913` (feat)

**Plan metadata:** `a7d613b` (docs: complete plan)

## Files Created/Modified
- `TradingBot.ApiService/TradingBot.ApiService.csproj` - Added Ardalis.Specification 9.3.1 and Ardalis.Specification.EntityFrameworkCore 9.3.1 package references
- `TradingBot.ApiService/Application/Specifications/SpecificationExtensions.cs` - WithSpecification<T> extension on IQueryable<T>
- `TradingBot.ApiService/Application/Specifications/Purchases/PurchaseFilledStatusSpec.cs` - Filters non-dry-run filled/partially-filled purchases (Where only, no OrderBy)
- `TradingBot.ApiService/Application/Specifications/Purchases/PurchaseDateRangeSpec.cs` - DateOnly-to-DateTime UTC range filter
- `TradingBot.ApiService/Application/Specifications/Purchases/PurchaseTierFilterSpec.cs` - Tier filter with Base null/string special case
- `TradingBot.ApiService/Application/Specifications/Purchases/PurchaseCursorSpec.cs` - Cursor pagination with built-in OrderByDescending
- `TradingBot.ApiService/Application/Specifications/Purchases/PurchasesOrderedByDateSpec.cs` - Default descending sort with AsNoTracking
- `TradingBot.ApiService/Application/Specifications/DailyPrices/DailyPriceByDateRangeSpec.cs` - Symbol + date range with ascending sort and AsNoTracking

## Decisions Made
- PurchaseFilledStatusSpec has no OrderBy to avoid ordering conflicts when composed with PurchasesOrderedByDateSpec or PurchaseCursorSpec
- PurchaseCursorSpec owns its OrderByDescending because cursor-based pagination inherently assumes descending time ordering
- AsNoTracking lives in ordering specs as the "always-applied base" pattern -- callers don't need to think about tracking
- WithSpecification wraps SpecificationEvaluator.Default.GetQuery() on IQueryable<T> (not DbSet<T>) to enable chaining: `query.WithSpecification(spec1).WithSpecification(spec2)`

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 7 spec building blocks ready for Plan 02 call-site composition
- WithSpecification extension available for chaining in endpoints and services
- Existing DashboardEndpoints inline LINQ can be replaced with spec-based queries in Plan 02

---
*Phase: 18-specification-pattern*
*Completed: 2026-02-19*

## Self-Check: PASSED

All 8 expected files found. Both task commits verified (1d532df, ad3b913).

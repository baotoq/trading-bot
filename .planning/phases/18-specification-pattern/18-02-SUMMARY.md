---
phase: 18-specification-pattern
plan: 02
subsystem: database
tags: [ardalis-specification, entity-framework-core, query-specification, dashboard-endpoints, background-services]

# Dependency graph
requires:
  - phase: 18-01
    provides: 7 composable spec classes and WithSpecification<T> extension for chaining

provides:
  - All 5 dashboard endpoints using composable WithSpecification call-site composition
  - WeeklySummaryService weekly purchases query using specs
  - MissedPurchaseVerificationService today-purchase query using specs
  - Pagination (.Take/hasMore) and .Select() projections remain at call sites
  - Aggregate queries (GroupBy+Sum) remain as inline LINQ

affects: [dashboard-queries, background-job-queries]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Call-site composition: WithSpecification chained on IQueryable<T> in endpoints and services
    - Cursor-conditional ordering: PurchaseCursorSpec (has OrderByDescending) replaces PurchasesOrderedByDateSpec when cursor present
    - Same-day range filter: PurchaseDateRangeSpec(today, today) for single-day queries

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - TradingBot.ApiService/Application/BackgroundJobs/WeeklySummaryService.cs
    - TradingBot.ApiService/Application/BackgroundJobs/MissedPurchaseVerificationService.cs

key-decisions:
  - "GetPurchaseHistoryAsync uses PurchaseCursorSpec (with OrderByDescending) OR PurchasesOrderedByDateSpec (no cursor) -- mutually exclusive to avoid duplicate ordering"
  - "GetPriceChartAsync purchase markers use .Where() chained after spec for DateOnly comparison (different pattern from PurchaseDateRangeSpec's DateTimeOffset)"
  - "MissedPurchaseVerificationService failed-purchase diagnostic query stays as inline LINQ (PurchaseStatus.Failed, not filled status, simple 3-condition query)"
  - "WeeklySummaryService lifetime totals query stays as inline LINQ (GroupBy+Sum aggregate)"

patterns-established:
  - "Cursor-conditional spec selection: if cursor present use PurchaseCursorSpec, else use PurchasesOrderedByDateSpec -- never apply both"
  - "Same-day range: PurchaseDateRangeSpec(today, today) for 'today only' queries"
  - "Plain .Where() chained after spec for one-off comparisons with different column/type patterns"

requirements-completed: [QP-03]

# Metrics
duration: 3min
completed: 2026-02-19
---

# Phase 18 Plan 02: Specification Pattern Summary

**All 5 dashboard endpoints and 2 background services refactored to use composable WithSpecification call-site chaining, with pagination/projection/aggregates remaining at call sites**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-19T16:54:29Z
- **Completed:** 2026-02-19T16:57:40Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Refactored all 5 dashboard endpoints (portfolio, purchases, status, chart, config) to use WithSpecification instead of inline .Where chains
- GetPurchaseHistoryAsync dynamically chains up to 4 specs: PurchaseFilledStatusSpec, PurchaseDateRangeSpec (optional), PurchaseTierFilterSpec (optional), PurchaseCursorSpec or PurchasesOrderedByDateSpec
- WeeklySummaryService and MissedPurchaseVerificationService use PurchaseFilledStatusSpec+PurchaseDateRangeSpec for purchase queries
- Aggregate (GroupBy+Sum) and diagnostic (Failed status) queries correctly remain as inline LINQ per locked decisions
- All 53 existing tests continue to pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor DashboardEndpoints to use composable specs** - `7062d5a` (refactor)
2. **Task 2: Refactor background services to use composable specs** - `ac531e0` (refactor)

**Plan metadata:** `[to be filled]` (docs: complete plan)

## Files Created/Modified
- `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` - All dashboard endpoints refactored; added 3 using directives for spec namespaces; inline .Where/.AsNoTracking replaced with WithSpecification chaining
- `TradingBot.ApiService/Application/BackgroundJobs/WeeklySummaryService.cs` - Weekly purchases query uses PurchaseFilledStatusSpec+PurchaseDateRangeSpec; removed unused weekStartDateTime variable; GroupBy+Sum stays inline
- `TradingBot.ApiService/Application/BackgroundJobs/MissedPurchaseVerificationService.cs` - Today-purchase query uses PurchaseFilledStatusSpec+PurchaseDateRangeSpec(today, today); failed-purchase diagnostic stays inline

## Decisions Made
- For GetPurchaseHistoryAsync, PurchaseCursorSpec and PurchasesOrderedByDateSpec are mutually exclusive (cursor owns OrderByDescending, non-cursor path gets PurchasesOrderedByDateSpec)
- GetPriceChartAsync purchase markers keep a plain `.Where()` after PurchaseFilledStatusSpec for the DateOnly comparison pattern -- different from PurchaseDateRangeSpec's DateTimeOffset, avoids a one-off spec
- MissedPurchaseVerificationService uses PurchaseDateRangeSpec(today, today) for single-day range -- spec's inclusive upper bound (TimeOnly.MaxValue) correctly captures all of today's purchases

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

Pre-existing uncommitted test fixture `PostgresFixture.cs` had `PostgreSqlBuilder(PostgreSqlImage.PostgreSql17)` which referenced a non-existent API, blocking the build. The file was already fixed to `PostgreSqlBuilder("postgres:16")` by the time the build was run (out-of-scope pre-existing issue, not caused by this plan's changes).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 18 specification pattern complete: infrastructure (Plan 01) and call-site composition (Plan 02) both done
- All query logic for Purchase and DailyPrice entities now uses composable specs throughout the application
- QP-01 and QP-03 requirements fulfilled

---
*Phase: 18-specification-pattern*
*Completed: 2026-02-19*

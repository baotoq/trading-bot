---
phase: 13-strongly-typed-ids
plan: 02
subsystem: domain
tags: [vogen, efcore, strongly-typed-ids, entities, typescript, branded-types]

# Dependency graph
requires:
  - phase: 13-01
    provides: Vogen 8.0.4, PurchaseId/IngestionJobId/DcaConfigurationId structs, generic BaseEntity<TId>, EF Core converters in ConfigureConventions

provides:
  - Purchase entity extending BaseEntity<PurchaseId> with typed Id
  - IngestionJob entity extending BaseEntity<IngestionJobId> with typed Id
  - DcaConfiguration entity extending BaseEntity<DcaConfigurationId> with typed Id
  - Non-generic BaseEntity alias removed (migration complete)
  - PurchaseCompletedEvent uses PurchaseId in signature
  - DcaExecutionService creates Purchase with Id = PurchaseId.New()
  - ConfigurationService uses DcaConfigurationId.Singleton (no raw Guid.Parse)
  - DataIngestionService.RunIngestionAsync accepts IngestionJobId
  - IngestionJobQueue uses Channel<IngestionJobId>
  - DataEndpoints.GetJobStatusAsync binds IngestionJobId from route
  - Dashboard dashboard.ts has PurchaseId branded type and PurchaseDto.id uses it
  - ROADMAP.md and REQUIREMENTS.md corrected to remove DailyPriceId

affects:
  - Phase 14 (Value Objects) - builds on typed entity foundation

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Entity migration pattern: BaseEntity<TypedId> with Id = TypedId.New() at creation sites"
    - "DTO boundary pattern: Guid in DTOs + implicit Vogen cast at LINQ projection"
    - "Endpoint binding pattern: typed ID parameter (IngestionJobId jobId) with {jobId:guid} route constraint"
    - "TypeScript branded type: string & { readonly __brand: 'TypeName' } for type-safe IDs"
    - "FindAsync replacement: FirstOrDefaultAsync with j => j.Id == typedId for value-converted keys"

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Models/IngestionJob.cs
    - TradingBot.ApiService/Models/DcaConfiguration.cs
    - TradingBot.ApiService/BuildingBlocks/BaseEntity.cs
    - TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs
    - TradingBot.ApiService/Application/Services/ConfigurationService.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionService.cs
    - TradingBot.ApiService/Application/Services/HistoricalData/IngestionJobQueue.cs
    - TradingBot.ApiService/Endpoints/DataEndpoints.cs
    - TradingBot.Dashboard/app/types/dashboard.ts
    - .planning/ROADMAP.md
    - .planning/REQUIREMENTS.md

key-decisions:
  - "DashboardDtos.cs: PurchaseDto.Id stays as Guid (API surface stability) -- implicit Vogen cast from PurchaseId handles LINQ Select projection"
  - "FindAsync replaced with FirstOrDefaultAsync for type-safe LINQ on value-converted keys"
  - "IngestionJobId.New() explicitly set at all IngestionJob creation sites (DataEndpoints.cs)"
  - "PurchaseId.New() explicitly set at all Purchase creation sites (DcaExecutionService.cs)"

requirements-completed:
  - TS-01

# Metrics
duration: 4min
completed: 2026-02-18
---

# Phase 13 Plan 02: Apply Typed IDs to All Entities and Callers Summary

**All three entities (Purchase, IngestionJob, DcaConfiguration) migrated to typed IDs, all callers updated, non-generic BaseEntity alias removed, dashboard branded types added, planning docs corrected**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-18T07:34:30Z
- **Completed:** 2026-02-18T07:37:48Z
- **Tasks:** 2
- **Files modified:** 13

## Accomplishments

- Non-generic `BaseEntity` alias removed from `BaseEntity.cs` -- migration from Plan 01 fully complete
- `Purchase` now extends `BaseEntity<PurchaseId>`, `IngestionJob` extends `BaseEntity<IngestionJobId>`, `DcaConfiguration` extends `BaseEntity<DcaConfigurationId>`
- `PurchaseCompletedEvent` record parameter changed from `Guid PurchaseId` to `PurchaseId PurchaseId`
- `DcaExecutionService` adds `Id = PurchaseId.New()` at Purchase creation site
- `ConfigurationService` uses `DcaConfigurationId.Singleton` instead of raw `Guid.Parse("...")`
- `DataIngestionService.RunIngestionAsync` signature changed from `Guid jobId` to `IngestionJobId jobId`; `FindAsync` replaced with `FirstOrDefaultAsync` for reliable type-safe LINQ
- `IngestionJobQueue` changed from `Channel<Guid>` to `Channel<IngestionJobId>` throughout
- `DataEndpoints.GetJobStatusAsync` binds `IngestionJobId jobId` from route parameter; `FindAsync` replaced with `FirstOrDefaultAsync`
- `DataEndpoints.IngestAsync` adds `Id = IngestionJobId.New()` at job creation site
- Dashboard `PurchaseId` branded TypeScript type added; `PurchaseDto.id` uses `PurchaseId` type
- `ROADMAP.md` and `REQUIREMENTS.md` corrected to remove erroneous `DailyPriceId` (DailyPrice has composite key, no Guid PK)
- Full solution builds with zero errors; all 53 tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Migrate entities to typed IDs, remove BaseEntity alias, and update all callers** - `0580337` (feat)
2. **Task 2: Amend ROADMAP.md and REQUIREMENTS.md to remove erroneous DailyPriceId reference** - `2c0a748` (chore)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/Models/Purchase.cs` - Changed `BaseEntity` to `BaseEntity<PurchaseId>`
- `TradingBot.ApiService/Models/IngestionJob.cs` - Changed `AuditedEntity` to `BaseEntity<IngestionJobId>`, removed explicit `Guid Id` property
- `TradingBot.ApiService/Models/DcaConfiguration.cs` - Changed `AuditedEntity` to `BaseEntity<DcaConfigurationId>`, removed explicit `Guid Id` property
- `TradingBot.ApiService/BuildingBlocks/BaseEntity.cs` - Removed non-generic `BaseEntity : BaseEntity<Guid>` alias
- `TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs` - `Guid PurchaseId` changed to `PurchaseId PurchaseId`
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Added `Id = PurchaseId.New()` to Purchase initializer
- `TradingBot.ApiService/Application/Services/ConfigurationService.cs` - `Guid.Parse(...)` replaced with `DcaConfigurationId.Singleton`
- `TradingBot.ApiService/Application/Services/HistoricalData/DataIngestionService.cs` - Method signature uses `IngestionJobId`; `FindAsync` replaced with `FirstOrDefaultAsync`
- `TradingBot.ApiService/Application/Services/HistoricalData/IngestionJobQueue.cs` - `Channel<Guid>` replaced with `Channel<IngestionJobId>`
- `TradingBot.ApiService/Endpoints/DataEndpoints.cs` - `GetJobStatusAsync` uses `IngestionJobId`; job creation adds `Id = IngestionJobId.New()`
- `TradingBot.Dashboard/app/types/dashboard.ts` - `PurchaseId` branded type added; `PurchaseDto.id` uses `PurchaseId`
- `.planning/ROADMAP.md` - Phase 13 goal and success criteria corrected
- `.planning/REQUIREMENTS.md` - TS-01 text corrected

## Decisions Made

- **DTO boundary: PurchaseDto.Id stays as Guid.** API DTOs (`DashboardDtos.cs`, `IngestResponse.cs`, `JobStatusResponse.cs`, `DataStatusResponse.cs`) keep `Guid` for API surface stability. Vogen's implicit cast from `TypedId -> Guid` handles the boundary in LINQ Select projections without explicit casts.
- **FindAsync replaced with FirstOrDefaultAsync.** EF Core's `FindAsync` uses the key type directly and can have issues with value-converted keys. `FirstOrDefaultAsync(j => j.Id == typedId)` leverages the registered value converter for correct LINQ translation.
- **Creation sites must be explicit.** The old non-generic `BaseEntity` constructor auto-set `Id` via `Guid.CreateVersion7(CreatedAt)`. With `BaseEntity<TId>`, `Id = default!` requires explicit `Id = TypedId.New()` at each creation site. Applied to `DcaExecutionService.cs` (Purchase) and `DataEndpoints.cs` (IngestionJob). DcaConfiguration uses `DcaConfigurationId.Singleton` instead.

## Deviations from Plan

None - plan executed exactly as written.

The `DashboardEndpoints.cs` note in the plan about potentially needing `(Guid)p.Id` explicit cast in LINQ projections was verified: Vogen's implicit cast operator handles the `PurchaseId -> Guid` conversion transparently in EF Core LINQ Select projections. No explicit cast was needed.

## Issues Encountered

None beyond the minor note above about LINQ projections working with implicit casts (expected behavior, no code change needed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All entity IDs are strongly typed throughout the entire call chain
- EF Core persists typed IDs transparently via globally registered converters
- API surface unchanged (JSON wire format unchanged -- plain GUIDs)
- Dashboard TypeScript has branded PurchaseId type for frontend type safety
- Phase 14 (Value Objects) can now layer value object types (Price, Quantity, etc.) on top of the strongly-typed ID foundation

## Self-Check: PASSED

All modified files exist on disk. Both task commits verified in git log (0580337, 2c0a748). Build: 0 errors, 53 tests pass.

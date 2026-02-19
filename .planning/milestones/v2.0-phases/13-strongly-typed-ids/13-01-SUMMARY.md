---
phase: 13-strongly-typed-ids
plan: 01
subsystem: database
tags: [vogen, efcore, strongly-typed-ids, guid, uuidv7, source-generator]

# Dependency graph
requires:
  - phase: 12-web-dashboard
    provides: existing entity model (Purchase, IngestionJob, DcaConfiguration, DailyPrice) and EF Core DbContext

provides:
  - Vogen 8.0.4 installed with assembly-level VogenDefaults (Guid underlying, EfCore + STJ converters, implicit casting both directions)
  - PurchaseId, IngestionJobId, DcaConfigurationId as readonly partial structs with UUIDv7 New() factories
  - DcaConfigurationId.Singleton = 00000000-0000-0000-0000-000000000001
  - Generic BaseEntity<TId> with backward-compatible BaseEntity alias for Plan 01 compatibility
  - EF Core value converters registered globally in ConfigureConventions for all three typed IDs

affects:
  - 13-02 (applies typed IDs to entities and callers)

# Tech tracking
tech-stack:
  added:
    - Vogen 8.0.4 (source-generated strongly-typed value objects)
  patterns:
    - VogenDefaults assembly attribute for global ID configuration
    - readonly partial struct pattern for typed IDs
    - UUIDv7 via Guid.CreateVersion7() in hand-written New() factory
    - ConfigureConventions with per-type HaveConversion for global EF Core converter registration
    - Backward-compatible BaseEntity alias to maintain compilation during multi-plan rollout

key-files:
  created:
    - TradingBot.ApiService/Models/Ids/VogenGlobalConfig.cs
    - TradingBot.ApiService/Models/Ids/PurchaseId.cs
    - TradingBot.ApiService/Models/Ids/IngestionJobId.cs
    - TradingBot.ApiService/Models/Ids/DcaConfigurationId.cs
  modified:
    - TradingBot.ApiService/TradingBot.ApiService.csproj
    - TradingBot.ApiService/BuildingBlocks/BaseEntity.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs

key-decisions:
  - "VogenDefaults uses toPrimitiveCasting/fromPrimitiveCasting (not castOperator) in Vogen 8.0.4 API"
  - "Vogen assembly-attribute approach does not generate RegisterAllInEfCoreConverters extension method; use per-type Properties<T>().HaveConversion<> in ConfigureConventions instead"
  - "DailyPriceId not created: DailyPrice uses composite key (Date, Symbol) with no Guid column - schema unchanged constraint prevents adding surrogate PK"

patterns-established:
  - "ID types: [ValueObject<Guid>] readonly partial struct with static New() using Guid.CreateVersion7()"
  - "EF Core registration: ConfigureConventions + Properties<TypedId>().HaveConversion<TypedId.EfCoreValueConverter, TypedId.EfCoreValueComparer>()"
  - "Backward-compat alias: public abstract class BaseEntity : BaseEntity<Guid> for safe multi-plan rollout"

requirements-completed:
  - TS-01

# Metrics
duration: 6min
completed: 2026-02-18
---

# Phase 13 Plan 01: Strongly-Typed ID Infrastructure Summary

**Vogen 8.0.4 installed with PurchaseId, IngestionJobId, DcaConfigurationId source-generated structs, generic BaseEntity<TId>, and global EF Core converter registration in ConfigureConventions**

## Performance

- **Duration:** 6 min
- **Started:** 2026-02-18T07:25:54Z
- **Completed:** 2026-02-18T07:31:22Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Vogen 8.0.4 installed; source generator produces typed ID structs at build time
- Three typed ID structs defined (PurchaseId, IngestionJobId, DcaConfigurationId) with UUIDv7 New() factories and implicit casting
- DcaConfigurationId.Singleton = From(Guid.Parse("00000000-0000-0000-0000-000000000001")) preserving singleton pattern
- BaseEntity refactored to generic BaseEntity<TId> with backward-compatible non-generic alias
- EF Core converters registered globally via ConfigureConventions using per-type HaveConversion
- Full solution builds and all 53 tests pass with no regression

## Task Commits

Each task was committed atomically:

1. **Task 1: Install Vogen and define typed ID structs with global config** - `3cdb914` (feat)
2. **Task 2: Refactor BaseEntity to generic and register EF Core converters** - `3795875` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/TradingBot.ApiService.csproj` - Added Vogen 8.0.4 package reference
- `TradingBot.ApiService/Models/Ids/VogenGlobalConfig.cs` - Assembly-level VogenDefaults (Guid underlying, EfCore + STJ conversions, implicit casting)
- `TradingBot.ApiService/Models/Ids/PurchaseId.cs` - Strongly-typed Purchase ID with UUIDv7 New()
- `TradingBot.ApiService/Models/Ids/IngestionJobId.cs` - Strongly-typed IngestionJob ID with UUIDv7 New()
- `TradingBot.ApiService/Models/Ids/DcaConfigurationId.cs` - Strongly-typed DcaConfiguration ID with New() and Singleton
- `TradingBot.ApiService/BuildingBlocks/BaseEntity.cs` - Generic BaseEntity<TId> + backward-compatible BaseEntity alias
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - ConfigureConventions with global EF Core converter registration

## Decisions Made

- **VogenDefaults API change in 8.0.4:** The `castOperator` parameter used in research examples doesn't exist in Vogen 8.0.4. The correct parameters are `toPrimitiveCasting` and `fromPrimitiveCasting` (separate directional control). Fixed inline.
- **RegisterAllInEfCoreConverters not generated for assembly-attribute approach:** Vogen only generates this extension method when using a marker class pattern. With assembly-level `[VogenDefaults]`, each typed ID's converters must be explicitly registered per-type using `configurationBuilder.Properties<T>().HaveConversion<T.EfCoreValueConverter, T.EfCoreValueComparer>()`. This is still global (applies to all properties of that type) and avoids per-property registration in OnModelCreating.
- **DailyPriceId excluded:** DailyPrice uses a composite primary key (DateOnly Date, string Symbol) with no Guid column. A Vogen typed ID would require either a new surrogate Guid column (schema change, violates plan constraint) or a complex composite key wrapper type (not Vogen-generated). Documented as scope exception per plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed VogenDefaults castOperator parameter name**
- **Found during:** Task 1 (Install Vogen and define typed ID structs)
- **Issue:** Research doc and plan used `castOperator: CastOperator.Implicit` but Vogen 8.0.4's `VogenDefaultsAttribute` constructor uses separate `toPrimitiveCasting` and `fromPrimitiveCasting` parameters. Build error CS1739 ("best overload does not have a parameter named 'castOperator'").
- **Fix:** Changed `castOperator: CastOperator.Implicit` to `toPrimitiveCasting: CastOperator.Implicit, fromPrimitiveCasting: CastOperator.Implicit`
- **Files modified:** TradingBot.ApiService/Models/Ids/VogenGlobalConfig.cs
- **Verification:** Build succeeded after fix
- **Committed in:** 3cdb914 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed RegisterAllInEfCoreConverters not generated for assembly-attribute approach**
- **Found during:** Task 2 (Refactor BaseEntity to generic and register EF Core converters)
- **Issue:** Plan's `configurationBuilder.RegisterAllInEfCoreConverters()` call failed — CS1061 ("ModelConfigurationBuilder does not contain a definition for RegisterAllInEfCoreConverters"). Vogen only generates this extension method when using an `[EfCoreConverter<T>]` marker class, not with the assembly-attribute approach.
- **Fix:** Replaced with explicit per-type registration using Vogen's generated inner classes: `configurationBuilder.Properties<PurchaseId>().HaveConversion<PurchaseId.EfCoreValueConverter, PurchaseId.EfCoreValueComparer>()` for each ID type. This achieves the same global registration goal: all properties of each typed ID type automatically get converters without any per-property calls in OnModelCreating.
- **Files modified:** TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
- **Verification:** Build succeeded; all 53 tests pass
- **Committed in:** 3795875 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs — API parameter name mismatch and missing source-generated method)
**Impact on plan:** Both fixes were minor API discrepancies between research documentation and actual Vogen 8.0.4 implementation. The functional outcome is identical to the plan's intent. No scope creep.

## Issues Encountered

None beyond the two auto-fixed deviations documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All typed ID structs are defined and source-generating correctly
- EF Core converters registered globally — ready to switch entity properties to typed IDs in Plan 02
- BaseEntity<TId> generic base is in place — entities can switch to `BaseEntity<PurchaseId>` etc.
- Backward-compatible BaseEntity alias ensures Plan 01 leaves the codebase fully compiling; Plan 02 removes the alias when all entities have switched

## Self-Check: PASSED

All created files exist on disk. Both task commits verified in git log (3cdb914, 3795875).

---
*Phase: 13-strongly-typed-ids*
*Completed: 2026-02-18*

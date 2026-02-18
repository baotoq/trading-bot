---
phase: 14-value-objects
plan: 01
subsystem: domain
tags: [vogen, value-objects, ef-core, typescript, domain-primitives]

# Dependency graph
requires:
  - phase: 13-strongly-typed-ids
    provides: Vogen 8.0.4 installed, VogenGlobalConfig.cs assembly defaults, EF Core ConfigureConventions pattern
provides:
  - Price/UsdAmount/Quantity/Multiplier/Percentage/Symbol value object types in Models/Values/
  - EF Core converters for all 6 value object types registered in ConfigureConventions
  - Purchase/DcaConfiguration/DailyPrice entities typed with value objects
  - PurchaseCompletedEvent typed value object fields
  - Dashboard DTOs typed with value object types
  - Dashboard TypeScript branded types for all 6 value objects
affects:
  - 14-value-objects plan 02 (service signatures, DcaOptions config binding)
  - All services that create/read Purchase, DailyPrice, DcaConfiguration entities

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "[ValueObject<decimal>] per-type overrides global Guid VogenDefaults"
    - "Hand-written <, >, <=, >= comparison operators (Vogen does NOT generate these)"
    - "Cross-type arithmetic operators: UsdAmount/Price=Quantity, UsdAmount*Multiplier=UsdAmount, Quantity*Price=UsdAmount"
    - "Models/Values/ directory mirrors Models/Ids/ pattern from Phase 13"
    - "ConfigureConventions registers value object EF Core converters (continuation of Phase 13)"
    - "Sentinel pattern: High30Day/Ma200Day/RemainingUsdc stay decimal where 0 means 'data unavailable'"
    - "MultiplierTierData stays raw decimal inside jsonb column (avoid EF Core jsonb STJ serialization complexity)"

key-files:
  created:
    - TradingBot.ApiService/Models/Values/Price.cs
    - TradingBot.ApiService/Models/Values/UsdAmount.cs
    - TradingBot.ApiService/Models/Values/Quantity.cs
    - TradingBot.ApiService/Models/Values/Multiplier.cs
    - TradingBot.ApiService/Models/Values/Percentage.cs
    - TradingBot.ApiService/Models/Values/Symbol.cs
  modified:
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Models/DcaConfiguration.cs
    - TradingBot.ApiService/Models/DailyPrice.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
    - TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs
    - TradingBot.ApiService/Endpoints/DashboardDtos.cs
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - TradingBot.Dashboard/app/types/dashboard.ts

key-decisions:
  - "Symbol EfCoreValueConverter registered (not skipped) so Symbol can be used in DailyPrice composite PK key"
  - "Multiplier upper bound: 20x sanity cap (not 10x); actual operational cap remains MaxMultiplierCap in config"
  - "High30Day, Ma200Day, RemainingUsdc stay decimal: zero sentinel for data unavailable cannot be represented in value objects"
  - "MultiplierTierData (inside jsonb) keeps raw decimal to avoid EF Core jsonb/STJ serialization complexity"
  - "TypeScript uses BtcMultiplier and TradingSymbol names to avoid collision with built-in TypeScript types"

patterns-established:
  - "Value object validation uses Vogen Validation.Ok / Validation.Invalid pattern"
  - "Comparison operators hand-written in each numeric partial struct (4 one-liners each)"
  - "Cross-type operators placed on the left operand type by domain convention"
  - "Sentinel fields documented with inline comments explaining why they stay decimal"

requirements-completed: [TS-02, TS-03, TS-04]

# Metrics
duration: 5min
completed: 2026-02-18
---

# Phase 14 Plan 01: Value Object Definitions and Entity Application Summary

**6 Vogen decimal/string value objects (Price, UsdAmount, Quantity, Multiplier, Percentage, Symbol) defined with cross-type arithmetic, applied to Purchase/DcaConfiguration/DailyPrice entities, registered in EF Core ConfigureConventions, and typed in DTOs and TypeScript**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-02-18T08:52:03Z
- **Completed:** 2026-02-18T08:57:00Z
- **Tasks:** 2
- **Files modified:** 14

## Accomplishments
- All 6 value object types defined with validation, hand-written comparison operators, and cross-type arithmetic in `Models/Values/`
- Entity fields typed: `Purchase.Price` (Price), `Purchase.Cost` (UsdAmount), `Purchase.Multiplier` (Multiplier), `DailyPrice.Symbol` (Symbol), etc.
- EF Core converters registered for all 6 types in `ConfigureConventions` (continuation of Phase 13 pattern)
- `DashboardEndpoints` uses `Symbol.Btc` constant instead of string literal `"BTC"`
- Dashboard TypeScript has 6 branded types (Price, UsdAmount, Quantity, BtcMultiplier, Percentage, TradingSymbol)
- All 53 existing tests pass with zero changes (implicit casts maintain backward compatibility)

## Task Commits

Each task was committed atomically:

1. **Task 1: Define value object types with validation, comparison, and cross-type arithmetic** - `fa7798a` (feat)
2. **Task 2: Apply value objects to entities, register EF Core converters, update events and DTOs** - `bcac1a3` (feat)

## Files Created/Modified
- `TradingBot.ApiService/Models/Values/Price.cs` - Strictly positive decimal; comparison operators
- `TradingBot.ApiService/Models/Values/UsdAmount.cs` - Strictly positive decimal; accumulation, DCA boost, division to Quantity
- `TradingBot.ApiService/Models/Values/Quantity.cs` - Non-negative decimal; Price multiplication, accumulation
- `TradingBot.ApiService/Models/Values/Multiplier.cs` - Positive decimal capped at 20x; additive composition
- `TradingBot.ApiService/Models/Values/Percentage.cs` - 0-1 format decimal; comparison operators
- `TradingBot.ApiService/Models/Values/Symbol.cs` - Non-empty string max 20 chars; Btc/BtcUsdc constants
- `TradingBot.ApiService/Models/Purchase.cs` - Price/Quantity/UsdAmount/Multiplier/Percentage fields; High30Day/Ma200Day stay decimal
- `TradingBot.ApiService/Models/DcaConfiguration.cs` - UsdAmount/Multiplier fields; MultiplierTierData keeps raw decimal
- `TradingBot.ApiService/Models/DailyPrice.cs` - Symbol/Price fields; Symbol defaults to Symbol.Btc
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - 6 value object EF Core converters in ConfigureConventions
- `TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs` - Quantity/Price/UsdAmount/Multiplier/Percentage typed
- `TradingBot.ApiService/Endpoints/DashboardDtos.cs` - All DTO fields use value object types
- `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` - Symbol.Btc constant; using Values namespace
- `TradingBot.Dashboard/app/types/dashboard.ts` - 6 branded types; PortfolioResponse/PurchaseDto/LiveStatusResponse/PriceChartResponse/etc. updated

## Decisions Made
- Symbol EF Core converter registered (not skipped) so `DailyPrice.Symbol` composite PK key works correctly with value converter
- Multiplier sanity cap set to 20x (not 10x as in research example); operational cap remains `MaxMultiplierCap` in config
- `High30Day`, `Ma200Day`, `RemainingUsdc` kept as `decimal`: these fields use `0` as a sentinel value meaning "data unavailable", which is rejected by Price/UsdAmount validators
- `MultiplierTierData` inside `DcaConfiguration.MultiplierTiers` (jsonb) keeps raw `decimal` fields to avoid EF Core jsonb/STJ serialization complexity (research Pitfall 3)
- TypeScript branded type names use `BtcMultiplier` and `TradingSymbol` to avoid collision with built-in TypeScript `Multiplier` and `Symbol`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed PriceDataService.Average() on List<Price>**
- **Found during:** Task 2 (applying value objects to DailyPrice entity)
- **Issue:** `PriceDataService.cs` line 213: `closePrices.Average()` fails because `closePrices` is now `List<Price>` (no LINQ Average for non-decimal collection)
- **Fix:** Changed to `closePrices.Average(p => p.Value)` to extract underlying decimal
- **Files modified:** `TradingBot.ApiService/Application/Services/PriceDataService.cs`
- **Verification:** Build succeeds, all tests pass
- **Committed in:** bcac1a3 (Task 2 commit)

**2. [Rule 3 - Blocking] Fixed WeeklySummaryService ternary type ambiguity**
- **Found during:** Task 2 (applying value objects to Purchase entity)
- **Issue:** Lines 137-138 in `WeeklySummaryService.cs`: `purchases.Min(p => p.Price) : 0m` — ternary cannot determine type when both `Price` and `decimal` implicitly convert to each other (CS0172)
- **Fix:** Changed to `purchases.Min(p => p.Price.Value)` and `.Max(p => p.Price.Value)` to return explicit `decimal`
- **Files modified:** `TradingBot.ApiService/Application/BackgroundJobs/WeeklySummaryService.cs`
- **Verification:** Build succeeds, all tests pass
- **Committed in:** bcac1a3 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 3 - blocking build errors)
**Impact on plan:** Both fixes necessary for build to succeed. Minimal change — extracts `.Value` for decimal arithmetic, no semantic change.

## Issues Encountered
None beyond the two auto-fixed blocking build errors above.

## User Setup Required
None - no external service configuration required. No EF Core migration needed (column types unchanged; value converters transparent to DB schema).

## Next Phase Readiness
- All 6 value objects available for use in service layer (Plan 02 target)
- EF Core converters active; entities will load/persist correctly without migration
- `DcaOptions` still uses raw `decimal` — Plan 02 will apply value objects to config and services
- Tests pass; implicit casts allow existing services to continue compiling until Plan 02 updates signatures

## Self-Check: PASSED

All created files verified present. All task commits (fa7798a, bcac1a3) verified in git log.

---
*Phase: 14-value-objects*
*Completed: 2026-02-18*

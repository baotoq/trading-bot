---
phase: 03-smart-multipliers
plan: 03
subsystem: dca-engine
tags: [smart-multipliers, dca, price-data, multiplier-tiers, bear-market-detection]

# Dependency graph
requires:
  - phase: 03-01
    provides: DailyPrice entity, migrations for price history storage
  - phase: 03-02
    provides: PriceDataService with 30-day high and 200-day SMA calculations
provides:
  - Smart multiplier calculation integrated into DCA execution pipeline
  - Multiplier stacking (dip tier * bear boost) with configurable cap
  - Purchase metadata audit trail (multiplier, tier, drop %, 30-day high, MA200)
  - MaxMultiplierCap configuration option with validation
  - Graceful degradation on price data unavailability
affects: [phase-04-notifications]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Multiplicative stacking of multipliers from independent signals
    - 0-sentinel pattern for unavailable price data with component-level fallback
    - Graceful degradation with try-catch fallback to 1.0x

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs
    - TradingBot.ApiService/Configuration/DcaOptions.cs
    - TradingBot.ApiService/Program.cs
    - TradingBot.ApiService/appsettings.json

key-decisions:
  - "MaxMultiplierCap default 4.5x equals natural max (3x * 1.5x), effectively uncapped but configurable"
  - "Multiplied amount calculated before balance cap: min(balance, base * multiplier)"
  - "Component-level fallback: 0 price data = 1.0x for that component only"
  - "Exception-level fallback: calculation throws = 1.0x total with warning"
  - "MultiplierResult record captures all calculation metadata for audit trail"

patterns-established:
  - "Multiplier calculation isolated in private method returning structured result"
  - "Purchase metadata fully populated on creation, not backfilled"
  - "Graceful degradation never prevents DCA purchase, only affects multiplier"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 3 Plan 3: DCA Multiplier Calculator Integration Summary

**Smart multipliers now adjust buy amounts based on 30-day high dip tiers and 200-day MA bear boost, stacking multiplicatively with configurable cap**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-12T15:26:23Z
- **Completed:** 2026-02-12T15:29:21Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- DCA execution calculates smart multipliers from price data on every purchase
- Dip tier multiplier selected based on >= comparison (10% drop = 2x tier)
- Bear boost (1.5x) applied when price < 200-day MA
- Multipliers stack multiplicatively, capped at MaxMultiplierCap (default 4.5x)
- Buy amount = min(available_balance, base_daily_amount * total_multiplier)
- Purchase entity populated with full multiplier audit trail metadata
- Graceful degradation: 0 price data = 1.0x component, exception = 1.0x total
- All Phase 3 services registered in DI container

## Task Commits

Each task was committed atomically:

1. **Task 1: Add MaxMultiplierCap to DcaOptions and update appsettings.json** - `9211ac9` (feat)
2. **Task 2: Integrate multiplier calculation into DcaExecutionService and register Phase 3 services** - `72cab0e` (feat)

## Files Created/Modified
- `TradingBot.ApiService/Configuration/DcaOptions.cs` - Added MaxMultiplierCap property with validation (>0, >=1.0)
- `TradingBot.ApiService/appsettings.json` - Configured MaxMultiplierCap: 4.5
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Integrated multiplier calculation, updated purchase flow, added CalculateMultiplierAsync method and MultiplierResult record
- `TradingBot.ApiService/Program.cs` - Registered IPriceDataService and PriceDataRefreshService

## Decisions Made

1. **MaxMultiplierCap default 4.5x:** Natural max from tier structure (3x * 1.5x = 4.5x), so default effectively uncapped but allows safety limits
2. **Multiplied amount before balance cap:** Calculate `multipliedAmount = base * multiplier` first, then `usdAmount = min(balance, multipliedAmount)` - this ensures partial buys don't lose multiplier intent
3. **Component-level fallback:** If 30-day high returns 0, dip multiplier = 1.0x but bear boost still calculated; if MA200 returns 0, bear multiplier = 1.0x but dip tier still calculated
4. **Exception-level fallback:** If CalculateMultiplierAsync throws exception, entire multiplier = 1.0x with error log - never let multiplier calculation failure prevent DCA purchase
5. **MultiplierResult record:** Clean separation of calculation result from business logic, contains all metadata needed for Purchase entity

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Phase 3 (Smart Multipliers) complete.** All components functional:
- DailyPrice entity and migrations (Plan 01)
- PriceDataService with 30-day high and 200-day SMA calculations (Plan 02)
- DCA execution with smart multipliers (Plan 03)

**Ready for Phase 4 (Enhanced Notifications & Observability):**
- Purchase metadata includes full multiplier reasoning for rich notifications
- All multiplier components accessible for dashboard/monitoring
- Graceful degradation ensures DCA never breaks even if notifications want multiplier details

**Integration testing recommended:**
- Bootstrap historical data on first run
- Verify multiplier calculation with various price scenarios
- Confirm purchase metadata populated correctly
- Test graceful degradation paths (0 price data, exception handling)

---
*Phase: 03-smart-multipliers*
*Completed: 2026-02-12*

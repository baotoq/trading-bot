---
phase: 04-enhanced-notifications-observability
plan: 01
subsystem: core-dca
tags: [dry-run, domain-events, multiplier-metadata, ef-core-migrations]

# Dependency graph
requires:
  - phase: 03-smart-multipliers
    provides: Multiplier calculation with tier/drop/price metadata
provides:
  - IsDryRun flag on Purchase entity with EF Core migration
  - Enhanced PurchaseCompletedEvent with multiplier metadata (6 new fields)
  - Dry-run mode in DcaExecutionService (skips order placement, simulates fills)
  - Idempotency bypass in dry-run mode for repeated testing
affects: [04-02-rich-notifications, 04-03-running-totals, future-observability]

# Tech tracking
tech-stack:
  added: []
  patterns: [dry-run simulation, event enrichment with metadata, conditional idempotency]

key-files:
  created:
    - TradingBot.ApiService/Infrastructure/Data/Migrations/20260212160215_AddIsDryRunToPurchase.cs
  modified:
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs

key-decisions:
  - "Dry-run mode generates DRY-RUN-{guid} order IDs for traceability"
  - "Idempotency check bypassed entirely in dry-run mode to allow repeated testing"
  - "IsDryRun defaults to false for backward compatibility with existing purchases"
  - "Event enrichment uses all 6 multiplier metadata fields for rich notifications"

patterns-established:
  - "Dry-run pattern: conditional order placement with simulated fills and clear logging"
  - "Event metadata enrichment: domain events carry calculation results for downstream handlers"
  - "Conditional idempotency: wrapped in guard clauses based on execution mode"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 4 Plan 01: Dry-Run Mode & Event Enrichment Summary

**Dry-run simulation with IsDryRun flag, enriched PurchaseCompletedEvent carrying multiplier metadata, and idempotency bypass for safe testing**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-12T16:00:00Z
- **Completed:** 2026-02-12T16:03:22Z
- **Tasks:** 2
- **Files modified:** 6 (4 code files, 1 migration, 1 model snapshot)

## Accomplishments
- Purchase entity has IsDryRun boolean flag with EF Core migration (default false)
- PurchaseCompletedEvent enriched with multiplier metadata: Multiplier, MultiplierTier, DropPercentage, High30Day, Ma200Day, IsDryRun
- DcaExecutionService implements dry-run mode: skips order placement, simulates fill with status=Filled and DRY-RUN-{guid} order ID
- Idempotency check bypassed entirely in dry-run mode for repeated testing without interference
- [DRY RUN] log prefixes throughout execution for clear visibility

## Task Commits

Each task was committed atomically:

1. **Task 1: Add IsDryRun to Purchase entity and enhance PurchaseCompletedEvent** - `2b80414` (feat)
2. **Task 2: Wire dry-run mode and enriched events into DcaExecutionService** - `2ebdfbe` (feat)

## Files Created/Modified
- `TradingBot.ApiService/Models/Purchase.cs` - Added IsDryRun boolean property
- `TradingBot.ApiService/Application/Events/PurchaseCompletedEvent.cs` - Added 6 multiplier metadata fields (Multiplier, MultiplierTier, DropPercentage, High30Day, Ma200Day, IsDryRun)
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - Configured IsDryRun with default value false
- `TradingBot.ApiService/Infrastructure/Data/Migrations/20260212160215_AddIsDryRunToPurchase.cs` - EF Core migration for IsDryRun column (boolean, nullable: false, defaultValue: false)
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Dry-run conditional logic, idempotency bypass, enriched event publishing

## Decisions Made

1. **DRY-RUN-{guid} order ID format** - Provides traceability and uniqueness for simulated purchases without colliding with real Hyperliquid order IDs

2. **Idempotency bypass in dry-run mode** - Wrapped existing idempotency check in `if (!options.DryRun)` guard to allow repeated dry-run executions on the same day without interference. Real-mode behavior unchanged.

3. **IsDryRun default false** - Ensures backward compatibility: existing purchase rows without IsDryRun are treated as real purchases (non-dry-run)

4. **All multiplier metadata in event** - PurchaseCompletedEvent carries Multiplier, MultiplierTier, DropPercentage, High30Day, Ma200Day fields so notification handlers (Plan 02) can construct rich messages like "3.0x multiplier (>= 15% drop from $98,500 high, bear boost active at $42,000 below 200-day SMA $45,000)"

5. **[DRY RUN] log prefix convention** - Established clear logging pattern for dry-run visibility throughout execution flow

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - implementation proceeded smoothly. Build and tests passed on first attempt after both tasks completed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for Plan 02 (Rich Notifications):**
- PurchaseCompletedEvent carries all multiplier metadata needed for rich notification formatting
- IsDryRun flag enables clear SIMULATION banner in notifications
- Dry-run mode allows safe testing of notification formatting without real orders

**Ready for Plan 03 (Running Totals):**
- IsDryRun flag enables accurate queries: `WHERE NOT IsDryRun` for real spending totals
- Existing purchases treated as non-dry-run (default false) for backward compatibility

**No blockers or concerns** - foundation complete for enhanced observability.

---
*Phase: 04-enhanced-notifications-observability*
*Completed: 2026-02-12*

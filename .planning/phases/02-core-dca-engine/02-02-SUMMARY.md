---
phase: 02-core-dca-engine
plan: 02
subsystem: api
tags: [dca, hyperliquid, distributed-locks, postgres, mediatr, domain-events]

# Dependency graph
requires:
  - phase: 01-foundation-hyperliquid
    provides: HyperliquidClient for balance checks and order placement
  - phase: 02-01
    provides: Purchase entity, PurchaseStatus enum, domain events (PurchaseCompleted, PurchaseFailed, PurchaseSkipped)
provides:
  - IDcaExecutionService interface for DCA orchestration
  - DcaExecutionService implementing complete buy flow with distributed locking
  - Idempotency checks preventing duplicate daily purchases
  - Balance verification and order size calculation
  - IOC order placement with partial fill handling
  - Purchase persistence with full metadata
  - Domain event publishing after database commit
affects: [02-03, 03-smart-multipliers]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Distributed lock pattern for idempotency (PostgreSQL advisory locks)"
    - "Domain event publishing AFTER database commit"
    - "IOC orders with 5% slippage tolerance for immediate fill"
    - "Decimal rounding DOWN to avoid exceeding balance"
    - "Partial fill detection (< 95% of requested quantity)"

key-files:
  created:
    - TradingBot.ApiService/Application/Services/IDcaExecutionService.cs
    - TradingBot.ApiService/Application/Services/DcaExecutionService.cs
  modified: []

key-decisions:
  - "IOC orders with 5% slippage tolerance ensure immediate fill while protecting against extreme price movements"
  - "Partial fills (< 95% of requested) recorded as PartiallyFilled status"
  - "5-decimal BTC precision for size rounding (standard for spot trading)"
  - "Round quantity DOWN (ToZero) to avoid exceeding available balance"
  - "Distributed lock TTL of 5 minutes provides enough time for full flow"
  - "Idempotency based on date range + successful status (Filled/PartiallyFilled)"
  - "Minimum balance $1, minimum order $10 per Hyperliquid requirements"
  - "Domain events published AFTER SaveChangesAsync (transactional integrity)"
  - "HyperliquidApiException caught for order failures, other exceptions bubble for retry"
  - "Fixed 1x multiplier for Phase 2 (smart multipliers deferred to Phase 3)"

patterns-established:
  - "Lock acquisition with await using pattern for automatic disposal"
  - "CultureInfo.InvariantCulture for all decimal.Parse operations"
  - "Structured logging with named placeholders (no string interpolation)"
  - "Primary constructor with 6 dependencies following codebase convention"
  - "Event-driven outcomes (void return, communicate via events)"

# Metrics
duration: 2min
completed: 2026-02-12
---

# Phase 2 Plan 02: DCA Execution Engine Summary

**Core DCA service with distributed locking, idempotency checks, balance verification, IOC order placement, partial fill handling, and transactional event publishing**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-12T19:41:24Z
- **Completed:** 2026-02-12T19:43:09Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Complete DCA execution pipeline: lock → idempotency → balance → price → order → persist → event
- Distributed lock prevents concurrent daily purchases using PostgreSQL advisory locks
- Idempotency check ensures no duplicate purchases for same day
- Balance validation with $1 minimum balance and $10 minimum order
- IOC order placement with 5% slippage tolerance for immediate fill
- Partial fill detection and appropriate status assignment
- Purchase persistence with full metadata (price, quantity, cost, orderId, rawResponse, failureReason)
- Domain event publishing AFTER database commit for transactional integrity

## Task Commits

Each task was committed atomically:

1. **Task 1: IDcaExecutionService interface** - `68b3f0a` (feat)
2. **Task 2: DcaExecutionService implementation** - `0a55ca3` (feat)

## Files Created/Modified

- `TradingBot.ApiService/Application/Services/IDcaExecutionService.cs` - Interface defining ExecuteDailyPurchaseAsync method for orchestrating daily purchases
- `TradingBot.ApiService/Application/Services/DcaExecutionService.cs` - Core DCA service implementing 7-step buy flow with distributed locking, idempotency, balance checks, order placement, persistence, and event publishing

## Decisions Made

**IOC order strategy:**
- 5% slippage tolerance (currentPrice × 1.05) ensures immediate fill while protecting against extreme price movements
- Partial fills (< 95% of requested quantity) recorded as PartiallyFilled status
- Resting orders (unexpected for IOC) treated as PartiallyFilled with warning logged

**Precision and rounding:**
- 5-decimal BTC precision for size rounding (standard for spot trading)
- Round quantity DOWN (MidpointRounding.ToZero) to avoid exceeding available balance
- All decimal parsing uses CultureInfo.InvariantCulture per codebase convention

**Idempotency and locking:**
- Distributed lock key format: `dca-purchase-{yyyy-MM-dd}` with 5-minute TTL
- Idempotency query filters by date range (todayStart to todayEnd) AND successful status (Filled or PartiallyFilled)
- Lock acquired before any database operations to prevent race conditions

**Event publishing:**
- Domain events published AFTER SaveChangesAsync to ensure transactional integrity
- Event types: PurchaseCompletedEvent (Filled/PartiallyFilled), PurchaseFailedEvent (Failed), PurchaseSkippedEvent (insufficient balance, duplicate, below minimum)
- Events drive notifications (Telegram) and future analytics without tight coupling

**Error handling:**
- HyperliquidApiException caught for order placement failures (recorded in Purchase entity)
- Other exceptions (network, database) bubble up for scheduler-level retry logic
- Structured logging with named placeholders throughout for observability

**Phase 2 scope:**
- Fixed 1x multiplier for all purchases (smart multipliers deferred to Phase 3)
- BTC balance approximated as filled quantity (actual balance tracking in Phase 4)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - implementation proceeded smoothly with all dependencies in place.

## User Setup Required

None - no external service configuration required for this plan.

## Next Phase Readiness

**Ready for Plan 02-03 (Daily Scheduler):**
- IDcaExecutionService interface ready for dependency injection
- DcaExecutionService implements complete buy flow and can be invoked by scheduler
- Domain events published for all outcomes (success, failure, skip)
- Distributed locking ensures safe concurrent scheduler runs

**Blockers:** None

**Considerations:**
- Scheduler (Plan 02-03) will need to handle retry logic for transient failures (network, exchange downtime)
- Smart multipliers (Phase 3) will require modifying the `Multiplier` calculation logic in DcaExecutionService
- Actual BTC balance tracking (Phase 4) will replace the current approximation in PurchaseCompletedEvent

---
*Phase: 02-core-dca-engine*
*Plan: 02*
*Completed: 2026-02-12*

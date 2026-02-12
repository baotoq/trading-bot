---
phase: 04-enhanced-notifications-observability
plan: 02
subsystem: notifications
tags: [telegram, notification-handlers, mediatr, ef-core, natural-language, multiplier-reasoning]

# Dependency graph
requires:
  - phase: 04-enhanced-notifications-observability
    plan: 01
    provides: Enriched PurchaseCompletedEvent with multiplier metadata (6 fields)
  - phase: 03-smart-multipliers
    provides: Multiplier calculation with tier/drop/price metadata
provides:
  - Rich buy notification with multiplier reasoning in natural language
  - Running totals (total BTC, total USD, avg cost) from database queries
  - SIMULATION banner for dry-run purchases
  - Enhanced skip notifications with contextual reasoning
  - Enhanced failure notifications distinguishing retry states
affects: [04-03-weekly-summary, future-notifications]

# Tech tracking
tech-stack:
  added: []
  patterns: [natural-language reasoning, database aggregation in handlers, contextual notification formatting]

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Application/Handlers/PurchaseCompletedHandler.cs
    - TradingBot.ApiService/Application/Handlers/PurchaseFailedHandler.cs
    - TradingBot.ApiService/Application/Handlers/PurchaseSkippedHandler.cs

key-decisions:
  - "Running totals query excludes dry-run purchases (WHERE !IsDryRun)"
  - "Multiplier reasoning built in natural language (e.g., 'BTC is 15% below 30-day high and below 200-day MA')"
  - "SIMULATION banner for dry-run: warning emoji + clear text at top of message"
  - "Skip notifications provide actionable context based on reason"
  - "Failed notifications distinguish retriable vs exhausted retries"

patterns-established:
  - "Natural language reasoning pattern: transform multiplier metadata into readable explanations"
  - "Database aggregation in notification handlers: handlers can query for enriched context"
  - "Contextual notification formatting: reason-based conditional messaging"

# Metrics
duration: 2min
completed: 2026-02-12
---

# Phase 4 Plan 02: Rich Notifications with Multiplier Reasoning Summary

**Rich Telegram notifications with natural language multiplier reasoning, running totals from database, and contextual messaging for all purchase outcomes**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-12T16:06:44Z
- **Completed:** 2026-02-12T16:08:26Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Buy notifications include natural language multiplier reasoning (e.g., "Buying 3.0x: BTC is 15.0% below 30-day high ($98,000) and price below 200-day MA ($45,000), bear boost active")
- Buy notifications display running totals: total BTC accumulated, total USD spent, average cost basis (queried from database, excluding dry-run purchases)
- Dry-run notifications clearly marked with SIMULATION banner (warning emoji + "SIMULATION MODE - No real order placed" text)
- Skip notifications provide contextual reasoning with actionable guidance (e.g., "Please add USDC to your Hyperliquid account" for insufficient balance)
- Failed notifications distinguish between retriable failures and exhausted retries with different messaging

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite PurchaseCompletedHandler with rich formatting and running totals** - `b5033d6` (feat)
2. **Task 2: Enhance PurchaseFailedHandler and PurchaseSkippedHandler** - `291fc49` (feat)

## Files Created/Modified
- `TradingBot.ApiService/Application/Handlers/PurchaseCompletedHandler.cs` - Enhanced with TradingBotDbContext for running totals query, BuildMultiplierReasoning helper for natural language explanation, SIMULATION banner for dry-run, rich Telegram formatting
- `TradingBot.ApiService/Application/Handlers/PurchaseFailedHandler.cs` - Enhanced with conditional retry messaging (exhausted vs in-progress), clearer sections
- `TradingBot.ApiService/Application/Handlers/PurchaseSkippedHandler.cs` - Enhanced with BuildContextualDetail helper for reason-based messaging, actionable guidance

## Decisions Made

1. **Running totals query excludes dry-run purchases** - WHERE !IsDryRun filter ensures accurate totals for real spending, dry-run simulations don't pollute real performance metrics

2. **Natural language multiplier reasoning** - Transform multiplier metadata into readable explanations like "BTC is 15.0% below 30-day high ($98,000)" instead of raw numbers, makes notifications educational and transparent

3. **SIMULATION banner format** - Warning emoji + bold "SIMULATION MODE - No real order placed" at top of message, followed by [SIMULATION] prefix in title. Clear distinction from real purchases.

4. **Contextual skip reasoning** - Reason-based conditional messaging: "Next buy scheduled tomorrow" for already-purchased-today, "Please add USDC" for insufficient balance, "Amount too small for valid order" for minimum order value

5. **Failed notification retry states** - Conditional messaging: "The bot will retry automatically" for RetryCount < 3, "All retries exhausted. Manual intervention may be needed." for RetryCount >= 3

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - implementation proceeded smoothly. Build and tests passed on first attempt after both tasks completed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for Plan 03 (Weekly Summary & Running Totals):**
- Running totals query pattern established in PurchaseCompletedHandler (can be reused for weekly summary)
- IsDryRun filtering ensures accurate totals
- All notification handlers producing rich, readable messages
- Database aggregation pattern proven in handler context

**No blockers or concerns** - rich notifications complete, ready for summary reports and health monitoring.

---
*Phase: 04-enhanced-notifications-observability*
*Completed: 2026-02-12*

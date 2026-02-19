---
phase: 04-enhanced-notifications-observability
plan: 03
subsystem: observability
tags: [background-services, health-checks, telegram, weekly-summary, missed-purchase-detection]

# Dependency graph
requires:
  - phase: 04-01-dry-run-event-enrichment
    provides: IsDryRun flag on Purchase entity for filtering real purchases
  - phase: 02-core-dca-engine
    provides: Purchase entity and TradingBotDbContext for queries
  - phase: 01-foundation
    provides: TelegramNotificationService and HyperliquidClient
provides:
  - WeeklySummaryService: Sunday evening DCA report with weekly buys, totals, P&L
  - MissedPurchaseVerificationService: Daily check for silent failures with Telegram alerts
  - DcaHealthCheck: Health endpoint reporting last purchase and operational status
  - All services registered in Program.cs DI container
affects: [future-observability, future-reporting, phase-05-future]

# Tech tracking
tech-stack:
  added: []
  patterns: [weekly reporting, missed purchase detection, health checks, error-safe background services]

key-files:
  created:
    - TradingBot.ApiService/Application/BackgroundJobs/WeeklySummaryService.cs
    - TradingBot.ApiService/Application/BackgroundJobs/MissedPurchaseVerificationService.cs
    - TradingBot.ApiService/Application/Health/DcaHealthCheck.cs
  modified:
    - TradingBot.ApiService/Program.cs

key-decisions:
  - "Weekly summary sent Sunday 20:00-21:00 UTC with this week's buys, running totals, avg cost vs current price, unrealized P&L"
  - "Missed purchase verification runs ~40 min after execution window (target + 10 min window + 30 min grace)"
  - "Health check returns Degraded if no purchase in 36+ hours, accessible at /health endpoint"
  - "Both background services use de-duplication guards (_lastSummarySent, _lastAlertSent) to prevent duplicate messages"
  - "All services exclude IsDryRun purchases from queries for accurate real purchase totals"
  - "Missed purchase alert includes diagnostic reasoning (failed order or no scheduler trigger)"

patterns-established:
  - "Weekly reporting pattern: TimeBackgroundService with hourly check, Sunday 20:00-21:00 UTC window, de-duplication via DateOnly field"
  - "Missed purchase detection: TimeBackgroundService with 30-min check, verification window ~40 min after target, diagnostic reasoning in alerts"
  - "Health check pattern: IServiceScopeFactory for scoped DbContext resolution, returns Degraded vs Healthy vs Unhealthy with data dictionary"
  - "Error-safe background services: try-catch around all operations, log errors but never crash"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 4 Plan 03: Weekly Summary and Missed Purchase Verification Summary

**Sunday evening DCA reports with P&L calculation, daily missed purchase detection with diagnostic alerts, and DCA health check integration**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-12T16:07:32Z
- **Completed:** 2026-02-12T16:10:39Z
- **Tasks:** 2
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments

- WeeklySummaryService sends comprehensive DCA report Sunday 20:00-21:00 UTC with this week's buys (date/amount/multiplier), week totals (avg/best/worst prices), lifetime totals (total BTC/USD/avg cost), unrealized P&L calculation using current BTC price
- MissedPurchaseVerificationService detects silent purchase failures ~40 min after execution window, sends Telegram alert with diagnostic reasoning (failed order or no scheduler trigger)
- DcaHealthCheck reports last purchase timestamp/status/BTC amount, returns Degraded if no purchase in 36+ hours or no purchases recorded yet
- All services registered in Program.cs DI container and wired up for operation
- All services use IServiceScopeFactory pattern for scoped service resolution (DbContext, Telegram, Hyperliquid)
- Both background services have de-duplication guards to prevent duplicate messages on the same day

## Task Commits

Each task was committed atomically:

1. **Task 1: Create WeeklySummaryService and MissedPurchaseVerificationService** - `e37d7a1` (feat)
2. **Task 2: Add DCA health check and register all new services in Program.cs** - `d2b5c6c` (feat)

## Files Created/Modified

- `TradingBot.ApiService/Application/BackgroundJobs/WeeklySummaryService.cs` - Weekly DCA summary report sent Sunday evening 20:00-21:00 UTC with weekly buys, totals, P&L
- `TradingBot.ApiService/Application/BackgroundJobs/MissedPurchaseVerificationService.cs` - Daily missed purchase check with Telegram alert and diagnostic reasoning
- `TradingBot.ApiService/Application/Health/DcaHealthCheck.cs` - Health check for DCA service operation with last purchase reporting
- `TradingBot.ApiService/Program.cs` - DI registration for WeeklySummaryService, MissedPurchaseVerificationService, and DcaHealthCheck

## Decisions Made

1. **Weekly summary timing: Sunday 20:00-21:00 UTC** - End-of-week timing (Sunday evening) provides natural reporting cadence, hourly check ensures window is hit without tight polling

2. **Missed purchase verification window: target + 40 minutes** - Allows 10-minute execution window + 30-minute grace period, avoids false alarms while catching silent failures quickly

3. **Health check threshold: 36 hours** - More than daily purchase cadence to allow for weekend or schedule changes, but catches multi-day silent failures

4. **De-duplication via DateOnly fields** - Simple in-memory guard prevents duplicate messages on same day without database overhead or external coordination

5. **Diagnostic reasoning in missed purchase alerts** - Queries for failed purchases and includes specific diagnosis (order failed vs scheduler didn't trigger) for faster troubleshooting

6. **IServiceScopeFactory for all services** - Singleton background services need scoped service resolution for DbContext, Telegram, Hyperliquid

7. **Exclude IsDryRun purchases from all queries** - Ensures accurate real purchase totals in weekly summary and missed purchase detection

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - implementation proceeded smoothly. Build and tests passed on first attempt after both tasks completed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Phase 4 complete (3/3 plans):**
- Weekly summary provides comprehensive DCA performance view
- Missed purchase detection catches silent failures with diagnostics
- Health check gives operational confidence via /health endpoint
- All notification and observability features from Phase 4 delivered

**Ready for Phase 5 or future enhancements:**
- Observability foundation complete for monitoring DCA operations
- Weekly reporting pattern established for additional periodic reports
- Health check pattern established for additional service health checks
- Background service patterns established for future scheduled tasks

**No blockers or concerns** - Phase 4 complete, all observability and notification features operational.

---
*Phase: 04-enhanced-notifications-observability*
*Completed: 2026-02-12*

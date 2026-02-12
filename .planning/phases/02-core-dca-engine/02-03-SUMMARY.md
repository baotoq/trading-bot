---
phase: 02-core-dca-engine
plan: 03
subsystem: background-jobs
tags: [background-service, scheduler, cron, retry-logic, distributed-systems, dependency-injection]

# Dependency graph
requires:
  - phase: 02-02
    provides: DcaExecutionService for executing daily purchases
  - phase: 02-01
    provides: Telegram notifications with MediatR handlers
  - phase: 01-01
    provides: PostgreSQL distributed locks and EF Core context
  - phase: 01-02
    provides: Hyperliquid API client with EIP-712 signing
provides:
  - DcaSchedulerBackgroundService - daily scheduler with time window enforcement
  - Retry logic with exponential backoff + jitter for transient failures
  - Fail-fast for permanent errors (4xx) with Telegram notifications
  - Complete Phase 2 DI wiring in Program.cs
affects: [phase-03, testing, observability]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TimeBackgroundService extension pattern for background jobs"
    - "IServiceScopeFactory for scoped service resolution in singleton hosted services"
    - "IOptionsMonitor for hot-reload configuration support"
    - "Exponential backoff with jitter for retry logic"
    - "Exception filter pattern for 4xx vs 5xx error handling"

key-files:
  created:
    - TradingBot.ApiService/Application/BackgroundJobs/DcaSchedulerBackgroundService.cs
  modified:
    - TradingBot.ApiService/Program.cs

key-decisions:
  - "5-minute check interval with 10-minute execution window"
  - "No catch-up buys if bot starts after window"
  - "3 retries with exponential backoff (2s/4s/8s) + jitter (0-500ms)"
  - "4xx errors fail immediately without retry"
  - "IServiceScopeFactory for scoped service resolution"

patterns-established:
  - "TimeBackgroundService extension: PeriodicTimer-based background jobs with consistent error handling"
  - "Retry pattern: Exponential backoff with jitter using exception filters to distinguish permanent vs transient errors"
  - "Scoped service resolution: IServiceScopeFactory in singleton BackgroundService for scoped dependencies (DbContext, etc.)"

# Metrics
duration: 1.5min
completed: 2026-02-12
---

# Phase 02 Plan 03: Daily Scheduler Summary

**Daily DCA scheduler with 10-minute execution window, 3x retry with exponential backoff + jitter, and complete Phase 2 DI wiring**

## Performance

- **Duration:** 1.5 min
- **Started:** 2026-02-12T12:19:52Z
- **Completed:** 2026-02-12T12:21:20Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- DcaSchedulerBackgroundService extends TimeBackgroundService with 5-minute interval
- Time window enforcement: only executes within 10 minutes of configured daily time
- Retry logic: 3 attempts with exponential backoff (2^n seconds) + random jitter (0-500ms)
- Fail-fast for permanent errors (4xx status codes) with Telegram notifications
- Complete DI registration in Program.cs for all Phase 2 services

## Task Commits

Each task was committed atomically:

1. **Task 1: DcaSchedulerBackgroundService with time window and retry** - `bcddf6a` (feat)
2. **Task 2: Wire all Phase 2 services into Program.cs** - `4def977` (feat)

_Note: TDD tasks may have multiple commits (test → feat → refactor)_

## Files Created/Modified
- `TradingBot.ApiService/Application/BackgroundJobs/DcaSchedulerBackgroundService.cs` - Daily scheduler that triggers DCA purchases at configured time with retry logic
- `TradingBot.ApiService/Program.cs` - Added DI registrations for IDcaExecutionService and DcaSchedulerBackgroundService

## Decisions Made

1. **5-minute check interval with 10-minute execution window**
   - Rationale: Balances responsiveness (5min) with avoiding excessive checks while providing 10-min window for retries

2. **No catch-up buys if bot starts after window**
   - Rationale: Prevents unintended execution if bot restarts late in day. User can manually trigger if needed.

3. **3 retries with exponential backoff + jitter**
   - Rationale: 2^n seconds (2s/4s/8s) gives transient issues time to resolve. Jitter (0-500ms) prevents thundering herd.

4. **4xx errors fail immediately without retry**
   - Rationale: Client errors (bad request, insufficient balance, etc.) won't resolve with retry. Saves API quota and notifies user faster.

5. **IServiceScopeFactory instead of direct scoped service injection**
   - Rationale: BackgroundService is singleton but needs scoped services (DbContext). ServiceScopeFactory creates per-iteration scopes.

6. **IOptionsMonitor instead of IOptions**
   - Rationale: Supports hot-reload of buy time configuration without restarting bot.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed as planned with zero build errors.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Phase 2 (Core DCA Engine) is now complete.**

All Phase 2 components are implemented and wired together:
- ✅ Purchase domain model with event-driven architecture (Plan 02-01)
- ✅ DCA execution service with lock acquisition, idempotency, balance checking, order placement (Plan 02-02)
- ✅ Daily scheduler with time window enforcement and retry logic (Plan 02-03)
- ✅ Telegram notifications for all purchase events
- ✅ Complete DI registration in Program.cs

**Ready for Phase 3: Smart Multipliers**

Phase 3 can now implement:
- Price history tracking (200-day MA calculation)
- Drop-from-high multiplier tiers
- Bear market boost logic
- Multiplier calculation service

**No blockers.** The execution pipeline is fully functional with fixed 1x multiplier. Phase 3 will enhance the multiplier calculation while reusing the entire DCA execution flow.

---
*Phase: 02-core-dca-engine*
*Completed: 2026-02-12*

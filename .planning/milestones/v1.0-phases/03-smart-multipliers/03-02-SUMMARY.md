---
phase: 03-smart-multipliers
plan: 02
subsystem: application
tags: [price-data, background-service, candles, sma, moving-average, hyperliquid]

# Dependency graph
requires:
  - phase: 03-01
    provides: DailyPrice entity, HyperliquidClient.GetCandlesAsync method
  - phase: 02-core-dca
    provides: IServiceScopeFactory pattern for background services
provides:
  - IPriceDataService interface for price data operations
  - PriceDataService with bootstrap, refresh, 30-day high, and 200-day SMA calculations
  - PriceDataRefreshService background job for daily candle updates at 00:05 UTC
  - 200-day historical data bootstrap on app startup
affects: [03-03-integration, smart-multiplier-calculator]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Background service with custom UTC time scheduling (not fixed intervals)"
    - "Graceful bootstrap on startup with error isolation from app initialization"
    - "Return 0 as sentinel value for unavailable data (not exceptions or nulls)"
    - "Stale data policy: use last known values even if >24h old"

key-files:
  created:
    - TradingBot.ApiService/Application/Services/IPriceDataService.cs
    - TradingBot.ApiService/Application/Services/PriceDataService.cs
    - TradingBot.ApiService/Application/BackgroundJobs/PriceDataRefreshService.cs
  modified: []

key-decisions:
  - "Return 0 as sentinel value for insufficient/missing data (callers must check before using in calculations)"
  - "Use daily close prices for 30-day high calculation (not intraday highs)"
  - "Stale data policy: use last known values even if >24h old (CONTEXT.md override of FR-7)"
  - "Bootstrap runs once on startup, daily refresh at 00:05 UTC thereafter"
  - "10% tolerance for missing data in 200-day SMA (allow 180 days minimum)"
  - "Error handling: log but never crash background service to maintain DCA stability"

patterns-established:
  - "Sentinel value pattern: 0 means 'unavailable' for price data (unambiguous since BTC never 0)"
  - "Bootstrap-then-refresh pattern: comprehensive historical fetch on first run, incremental updates daily"
  - "Time-based scheduling in background service: calculate next run time dynamically for precise UTC timing"

# Metrics
duration: 2.5min
completed: 2026-02-12
---

# Phase 3 Plan 2: Price Data Service & Background Refresh

**PriceDataService with 200-day bootstrap, 30-day high calculation, 200-day SMA calculation, and daily background refresh at 00:05 UTC**

## Performance

- **Duration:** 2.5 min
- **Started:** 2026-02-12T15:20:22Z
- **Completed:** 2026-02-12T15:22:50Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Created PriceDataService handling bootstrap (200 days), daily refresh, 30-day high, and 200-day SMA calculations
- Implemented PriceDataRefreshService background job with startup bootstrap and daily 00:05 UTC refresh loop
- Return value contract: 0 signals "data unavailable" with caller responsibility to check before calculations
- Stale data policy: use last known values even if >24h old (never reject data or fall back to 1x due to staleness)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IPriceDataService interface and PriceDataService implementation** - `910a637` (feat)
2. **Task 2: Create PriceDataRefreshService background job** - `4265d00` (feat)

## Files Created/Modified

### Created
- `TradingBot.ApiService/Application/Services/IPriceDataService.cs` - Service interface with 4 methods: bootstrap, refresh, 30-day high, 200-day SMA
- `TradingBot.ApiService/Application/Services/PriceDataService.cs` - Implementation fetching from Hyperliquid, storing in DB, calculating from close prices
- `TradingBot.ApiService/Application/BackgroundJobs/PriceDataRefreshService.cs` - Daily refresh service with startup bootstrap and 00:05 UTC refresh loop

## Decisions Made

**1. Return 0 as sentinel value for insufficient/missing data**
- Rationale: BTC price never 0, so unambiguous signal. Simpler than nullable decimals or exceptions.
- Caller responsibility: Must check for 0 before using in calculations (e.g., avoid division-by-zero in dip percentage formula)
- Alternative: Nullable decimals would require null checks throughout; exceptions would need try/catch everywhere

**2. Use daily close prices for 30-day high calculation (not intraday highs)**
- Rationale: Per CONTEXT.md decision - avoids flash spike distortion from intraday wicks
- Trade-off: Slightly less sensitive to rapid drops, but more stable/fair multiplier triggers

**3. Stale data policy: use last known values even if >24h old**
- Rationale: CONTEXT.md override of FR-7 - stale data is always better than no data (fall back to 1x)
- Background refresh handles keeping data current; temporary staleness is acceptable
- Alternative: Rejecting stale data would cause multiplier calculation to fail on transient refresh issues

**4. Bootstrap runs once on startup, daily refresh at 00:05 UTC thereafter**
- Rationale: 200-day historical data needed before first DCA execution. 00:05 gives exchange time to finalize daily candle.
- Error isolation: Bootstrap failure logs but doesn't block app startup (DCA can work with existing data)

**5. 10% tolerance for missing data in 200-day SMA**
- Rationale: Allow 180+ days for SMA calculation (accommodate exchange gaps, weekends if any)
- Trade-off: Slightly less precise SMA with <200 days, but more resilient to data gaps

**6. Error handling: log but never crash background service**
- Rationale: DCA scheduler stability is paramount. Transient price refresh failures shouldn't affect core DCA execution.
- Recovery: 1-hour wait before retry on persistent errors (avoid tight loop)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed without issues. Build succeeded with no compilation errors.

## Next Phase Readiness

**Ready for Plan 03 (DCA Multiplier Calculator Integration):**
- IPriceDataService contract defined and implemented
- Historical data bootstrap ensures data availability before first DCA execution
- 30-day high and 200-day SMA methods ready for multiplier logic consumption
- Background refresh maintains up-to-date data without manual intervention
- Error handling ensures price data failures don't crash DCA scheduler

**Blockers:** None

**Considerations for next plan:**
- DCA execution service needs to inject IPriceDataService
- Multiplier calculation logic should check for 0 return values before using in formulas
- Consider exposing price data endpoints for manual inspection/verification (future nice-to-have)

---
*Phase: 03-smart-multipliers*
*Completed: 2026-02-12*

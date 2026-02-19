---
phase: 11-backtest-visualization
plan: 01
subsystem: dashboard-backtest-api
tags: [api, proxy, typescript, composable]
dependency_graph:
  requires:
    - Phase 9.1 (Nuxt 4 dashboard setup)
    - Phase 10 (Dashboard core with server API pattern)
  provides:
    - Backend config endpoint (GET /api/dashboard/config)
    - Nuxt server proxy routes for backtest API
    - TypeScript types for all backtest DTOs
    - useBacktest composable for reactive state
  affects:
    - All future backtest UI components will consume this composable
tech_stack:
  added:
    - DcaConfigResponse DTO (backend)
    - Nuxt server proxy pattern for backtest endpoints
    - TypeScript type coverage for backtest domain
  patterns:
    - Server-to-server auth (Nuxt -> .NET with API key)
    - Simulated progress bar for async operations
    - Reactive state management with Vue refs
key_files:
  created:
    - TradingBot.Dashboard/server/api/dashboard/config.get.ts
    - TradingBot.Dashboard/server/api/backtest/run.post.ts
    - TradingBot.Dashboard/server/api/backtest/sweep.post.ts
    - TradingBot.Dashboard/app/types/backtest.ts
    - TradingBot.Dashboard/app/composables/useBacktest.ts
  modified:
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - TradingBot.ApiService/Endpoints/DashboardDtos.cs
decisions:
  - Simulated progress bars (0-90% in 2s, jump to 100% on complete) for better UX during backtest execution
  - Separate TierSet type for sweep request to allow multiple tier configuration sets
  - All date fields as strings in TypeScript (ISO date format from backend)
metrics:
  duration: 2 minutes
  completed_at: 2026-02-14
---

# Phase 11 Plan 01: Backtest API Layer Summary

**One-liner:** Backend config endpoint, Nuxt server proxy routes, TypeScript types, and reactive composable for backtest functionality

## Tasks Completed

| Task | Description                                      | Commit  | Files Modified                                                                                         |
| ---- | ------------------------------------------------ | ------- | ------------------------------------------------------------------------------------------------------ |
| 1    | Add backend config endpoint and Nuxt server proxy | 2821446 | DashboardEndpoints.cs, DashboardDtos.cs, config.get.ts, run.post.ts, sweep.post.ts                   |
| 2    | Create TypeScript types and useBacktest composable | 55a102f | backtest.ts, useBacktest.ts                                                                            |

## What Was Built

### Backend Config Endpoint

**Added to DashboardEndpoints.cs:**
- `GET /api/dashboard/config` endpoint that returns current DCA configuration from `IOptionsMonitor<DcaOptions>`
- Maps `MultiplierTiers` to `MultiplierTierDto` list for frontend consumption
- Uses existing `ApiKeyEndpointFilter` for authentication

**DTOs in DashboardDtos.cs:**
- `DcaConfigResponse` - Contains all DCA parameters (base amount, lookback days, bear market settings, tiers)
- `MultiplierTierDto` - Drop percentage and multiplier pairs

### Nuxt Server Proxy Routes

**Created 3 proxy routes following existing pattern:**
1. `server/api/dashboard/config.get.ts` - GET proxy to `/api/dashboard/config`
2. `server/api/backtest/run.post.ts` - POST proxy to `/api/backtest`
3. `server/api/backtest/sweep.post.ts` - POST proxy to `/api/backtest/sweep`

**All routes:**
- Use `useRuntimeConfig(event)` for API endpoint and key
- Include `x-api-key` header for backend auth
- Wrap in try/catch with `createError` for proper error propagation
- Follow Nuxt 4 server API conventions

### TypeScript Type Coverage

**Created `app/types/backtest.ts` with 20 interfaces:**

**Request/Config types:**
- `DcaConfigResponse`, `MultiplierTierDto`
- `BacktestRequest`, `MultiplierTierInput`
- `SweepRequest`, `TierSet`
- `BacktestConfig`, `MultiplierTierConfig`

**Response/Result types:**
- `BacktestResponse`, `BacktestResult`
- `SweepResponse`, `SweepResultEntry`, `SweepResultDetailEntry`
- `DcaMetrics`, `ComparisonMetrics`
- `TierBreakdownEntry`, `PurchaseLogEntry`
- `WalkForwardEntry`, `WalkForwardSummary`

All interfaces use camelCase properties matching System.Text.Json serialization defaults.

### useBacktest Composable

**Created `app/composables/useBacktest.ts` with:**

**Reactive state:**
- `config` - DCA configuration from backend
- `backtestResult` - Single backtest response
- `sweepResult` - Parameter sweep response
- `isRunning` - Execution status flag
- `progress` - Progress percentage (0-100)
- `error` - Error message string

**Methods:**
- `fetchConfig()` - GET `/api/dashboard/config`
- `loadConfig()` - Wrapper for fetchConfig (called on init)
- `runBacktest(request)` - POST `/api/backtest/run` with simulated progress
- `runSweep(request)` - POST `/api/backtest/sweep` with simulated progress

**Progress simulation:**
- Increments from 0 to 90% in ~2 seconds (200ms intervals)
- Jumps to 100% when request completes
- Provides visual feedback during potentially long-running backtest operations

**Error handling:**
- Catches all $fetch errors
- Sets error ref with message
- Clears isRunning flag
- Rethrows for caller handling if needed

## Deviations from Plan

None - plan executed exactly as written.

## Integration Points

**Backend → Nuxt Server:**
- .NET endpoints require `x-api-key` header (from user-secrets)
- Nuxt server routes use `Dashboard:ApiKey` from runtime config
- Proxy pattern isolates API key from browser

**Nuxt Server → Client:**
- Browser calls `/api/dashboard/config`, `/api/backtest/run`, `/api/backtest/sweep`
- No API key needed in browser (handled by Nuxt server)
- Error responses use standard Nuxt `createError` format

**Composable → UI:**
- Reactive state updates automatically when methods called
- Progress bar reflects simulated progress for UX
- Error state available for toast/banner notifications
- Results stored in refs for immediate UI binding

## Testing Notes

**Backend build:** Verified with `dotnet build TradingBot.ApiService.csproj` - compiles successfully

**TypeScript types:** All interfaces match backend C# DTOs exactly (verified by inspection)

**Runtime verification deferred:** Full integration testing will happen in next plan when UI components consume these APIs

## Next Steps

Plan 02 will create the backtest UI components that consume:
- `fetchConfig()` to pre-fill form with current DCA settings
- `runBacktest()` to execute single backtest and display results
- `runSweep()` to run parameter sweep and show ranked configurations

The composable is ready for immediate use in Vue components with proper reactive updates and error handling.

## Self-Check: PASSED

**Files created verification:**
```
FOUND: TradingBot.Dashboard/server/api/dashboard/config.get.ts
FOUND: TradingBot.Dashboard/server/api/backtest/run.post.ts
FOUND: TradingBot.Dashboard/server/api/backtest/sweep.post.ts
FOUND: TradingBot.Dashboard/app/types/backtest.ts
FOUND: TradingBot.Dashboard/app/composables/useBacktest.ts
```

**Files modified verification:**
```
FOUND: TradingBot.ApiService/Endpoints/DashboardEndpoints.cs (config endpoint added)
FOUND: TradingBot.ApiService/Endpoints/DashboardDtos.cs (DTOs added)
```

**Commits verification:**
```
FOUND: 2821446 (Task 1: backend config endpoint + proxy routes)
FOUND: 55a102f (Task 2: TypeScript types + composable)
```

All artifacts delivered as specified.

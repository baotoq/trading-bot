---
phase: 10-dashboard-core
plan: 02
subsystem: dashboard-frontend
tags:
  - nuxt
  - composables
  - typescript
  - api-proxy
  - data-layer
dependency_graph:
  requires:
    - phase-09.1-plan-01 (Nuxt 4 foundation, package.json, nuxt.config.ts)
  provides:
    - TypeScript types matching backend DTOs
    - useDashboard composable with 10-second polling
    - usePurchaseHistory composable with infinite scroll
    - useCountdownTimer composable with 1-second ticks
    - Four server proxy routes for dashboard endpoints
  affects:
    - TradingBot.Dashboard frontend data layer
tech_stack:
  added:
    - chart.js (charting library)
    - vue-chartjs (Vue 3 wrapper for chart.js)
    - chartjs-plugin-annotation (chart annotations)
    - "@vueuse/core" (composable utilities)
    - date-fns (date formatting)
  patterns:
    - Composables pattern for reactive data fetching
    - Server proxy pattern for API authentication
    - Polling pattern with useIntervalFn
    - Cursor-based pagination with loading guards
key_files:
  created:
    - TradingBot.Dashboard/app/types/dashboard.ts (TypeScript interfaces)
    - TradingBot.Dashboard/app/composables/useDashboard.ts (portfolio/status polling)
    - TradingBot.Dashboard/app/composables/usePurchaseHistory.ts (infinite scroll pagination)
    - TradingBot.Dashboard/app/composables/useCountdownTimer.ts (countdown timer)
    - TradingBot.Dashboard/server/api/dashboard/portfolio.get.ts (portfolio proxy)
    - TradingBot.Dashboard/server/api/dashboard/purchases.get.ts (purchases proxy)
    - TradingBot.Dashboard/server/api/dashboard/status.get.ts (status proxy)
    - TradingBot.Dashboard/server/api/dashboard/chart.get.ts (chart proxy)
  modified:
    - TradingBot.Dashboard/package.json (chart dependencies)
    - TradingBot.Dashboard/package-lock.json (lockfile)
  deleted:
    - TradingBot.Dashboard/server/api/portfolio.get.ts (Phase 9 placeholder)
decisions:
  - decision: "Use server proxy pattern instead of client-side direct API calls"
    rationale: "Keep API key secure on server, avoid CORS issues, centralize error handling"
  - decision: "Use @vueuse/core for polling and timers instead of setInterval"
    rationale: "Automatic cleanup on unmount, composable-friendly API, better testability"
  - decision: "Cursor-based pagination with loading guard"
    rationale: "Prevent duplicate API calls, support infinite scroll UX, efficient for large datasets"
  - decision: "10-second polling interval for dashboard data"
    rationale: "Balance between freshness and server load, matches real-time monitoring needs"
metrics:
  duration: "124 seconds"
  completed_at: "2026-02-13"
  task_count: 2
  file_count: 11
  test_count: 0
---

# Phase 10 Plan 02: Frontend Data Layer Summary

**One-liner:** TypeScript types, polling composables (useDashboard, usePurchaseHistory, useCountdownTimer), and four server proxy routes for dashboard API endpoints.

## What Was Built

Created the complete frontend data layer for the Nuxt dashboard:

1. **TypeScript Types** - 8 interfaces matching backend DTOs exactly (PortfolioResponse, PurchaseHistoryResponse, LiveStatusResponse, PriceChartResponse, etc.)
2. **useDashboard Composable** - Fetches portfolio and status data with 10-second polling using useIntervalFn
3. **usePurchaseHistory Composable** - Cursor-based infinite scroll with loading guard and date filtering
4. **useCountdownTimer Composable** - Live countdown to next buy with 1-second updates (formats as "Xh Ym Zs")
5. **Server Proxy Routes** - Four Nuxt server routes forward dashboard API calls to .NET backend with x-api-key authentication
6. **npm Dependencies** - Installed chart.js, vue-chartjs, chartjs-plugin-annotation, @vueuse/core, date-fns

All composables encapsulate data fetching, polling, pagination, and timers — keeping Vue components pure presentation logic.

## Technical Implementation

**TypeScript Types:**
- Defined 8 interfaces matching .NET System.Text.Json camelCase serialization
- ChartTimeframe union type for chart component
- Proper nullable types matching backend nullability

**useDashboard Composable:**
- Uses `useFetch` with `{ lazy: true, server: false }` for client-side only fetching
- `useIntervalFn` for 10-second polling with immediate execution
- Automatic cleanup via `onUnmounted` to pause interval
- Returns both data and loading/error states for each endpoint

**usePurchaseHistory Composable:**
- Loading guard prevents duplicate API calls (`if (loading.value || !hasMore.value) return`)
- Cursor-based pagination appends items to array for infinite scroll
- `resetAndLoad` function for filter changes (startDate/endDate)
- `$fetch` instead of `useFetch` for programmatic control

**useCountdownTimer Composable:**
- Accepts `Ref<string | null>` for reactive target time
- Updates every 1 second via `useIntervalFn`
- Formats remaining time as "Xh Ym Zs" (e.g., "4h 23m 15s")
- Handles edge cases: null targetTime ("N/A"), past time ("Now")

**Server Proxy Routes:**
- Pattern: `${config.public.apiEndpoint}/api/dashboard/{endpoint}`
- Forward query params with `getQuery(event)`
- Add `x-api-key` header from server-side runtimeConfig
- Error handling with createError and 502 Bad Gateway fallback

## Deviations from Plan

None - plan executed exactly as written. All npm packages installed, all types defined, all composables created, all server routes implemented, old placeholder deleted.

## Commits

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Install chart dependencies and create TypeScript types | 68a0a48 | 3 created/modified |
| 2 | Create composables and server proxy routes | b60d3cd | 7 created, 1 deleted |

## Self-Check: PASSED

**Created files verified:**
```
FOUND: TradingBot.Dashboard/app/types/dashboard.ts
FOUND: TradingBot.Dashboard/app/composables/useDashboard.ts
FOUND: TradingBot.Dashboard/app/composables/usePurchaseHistory.ts
FOUND: TradingBot.Dashboard/app/composables/useCountdownTimer.ts
FOUND: TradingBot.Dashboard/server/api/dashboard/portfolio.get.ts
FOUND: TradingBot.Dashboard/server/api/dashboard/purchases.get.ts
FOUND: TradingBot.Dashboard/server/api/dashboard/status.get.ts
FOUND: TradingBot.Dashboard/server/api/dashboard/chart.get.ts
```

**Deleted files verified:**
```
DELETED: TradingBot.Dashboard/server/api/portfolio.get.ts
```

**Commits verified:**
```
FOUND: 68a0a48 (feat(10-02): install chart dependencies and create TypeScript types)
FOUND: b60d3cd (feat(10-02): create composables and server proxy routes)
```

**TypeScript compilation verified:**
```
PASSED: npx nuxi prepare succeeds without errors
```

## Next Steps

Proceed to **Phase 10 Plan 03**: Build UI components using these composables and types.

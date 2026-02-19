---
phase: 19-dashboard-nullable-price-fix
plan: 01
subsystem: api, ui
tags: [vogen, nullable, price, dashboard, vue, typescript, csharp, dtos]

# Dependency graph
requires:
  - phase: 14-value-objects
    provides: Price and UsdAmount value objects with strict positive validation that rejects zero
  - phase: 18-specification-pattern
    provides: WithSpecification extension used in dashboard endpoints
provides:
  - Null-safe portfolio and chart endpoints that return 200 when DB is empty or Hyperliquid unreachable
  - Nullable Price fields in C# DTOs (PortfolioResponse and PriceChartResponse)
  - Matching nullable TypeScript types in dashboard
  - Vue components that display '--' for unavailable price fields
affects: [dashboard, api, endpoints, value-objects]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Null-safe aggregate: explicit decimal cast before Sum() to avoid Vogen implicit cast, then guard totalBtc > 0 before constructing value object"
    - "DTO decimal for aggregates: response DTOs use plain decimal (not UsdAmount) when zero is a valid aggregate output (empty set)"

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Endpoints/DashboardDtos.cs
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - TradingBot.Dashboard/app/types/dashboard.ts
    - TradingBot.Dashboard/app/components/dashboard/PortfolioStats.vue
    - TradingBot.Dashboard/app/components/dashboard/PriceChart.vue

key-decisions:
  - "TotalCost is decimal (not UsdAmount) in PortfolioResponse DTO because UsdAmount rejects zero via value > 0 validation, but zero is a valid aggregate output when no purchases exist"
  - "AverageCostBasis and CurrentPrice are Price? (nullable) in DTOs -- null when no purchases or Hyperliquid unreachable respectively"
  - "PnL fields are decimal? (null) when CurrentPrice is unavailable -- explicit null guard, no .Value.Value outside null-check"
  - "Explicit (decimal) casts before Sum() in endpoints avoid Vogen implicit cast accumulation that would call UsdAmount.From(0m) on empty set"
  - "Current Price card refactored to use DashboardStatCard (same as other 4 cards) for consistency -- removes bespoke UCard+skeleton"

patterns-established:
  - "Null-safe aggregate pattern: (decimal) cast + guard before Price.From() prevents VogenInvalidValueException on empty DB"

requirements-completed: [TS-04]

# Metrics
duration: 4min
completed: 2026-02-19
---

# Phase 19 Plan 01: Dashboard Nullable Price Fix Summary

**Null-safe portfolio/chart endpoints using Price? DTOs prevent VogenInvalidValueException when DB is empty or Hyperliquid is unreachable, with dashboard showing '--' for unavailable values**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-02-19T17:46:58Z
- **Completed:** 2026-02-19T17:50:17Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- PortfolioResponse and PriceChartResponse DTOs now use `Price?` (nullable) for AverageCostBasis and CurrentPrice, and `decimal` (not UsdAmount) for TotalCost
- GetPortfolioAsync and GetPriceChartAsync use explicit `(decimal)` casts before Sum() and null guards before Price.From(), eliminating VogenInvalidValueException on empty DB
- TypeScript interfaces updated to match backend nullability (`Price | null`, `number | null`); Vue components display '--' for null fields and omit average cost chart line when null
- All 62 tests pass with no regression

## Task Commits

1. **Task 1: Nullable Price DTOs and endpoint logic** - `8a3462a` (fix)
2. **Task 2: TypeScript types and Vue components** - `07ea42b` (fix)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/Endpoints/DashboardDtos.cs` - TotalCost as decimal, AverageCostBasis/CurrentPrice as Price?, UnrealizedPnl/UnrealizedPnlPercent as decimal?
- `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` - Explicit decimal casts, null-safe Price construction, null PnL when currentPrice unavailable
- `TradingBot.Dashboard/app/types/dashboard.ts` - PortfolioResponse totalCost as number, nullable Price and number fields; PriceChartResponse averageCostBasis as Price | null
- `TradingBot.Dashboard/app/components/dashboard/PortfolioStats.vue` - '--' for null averageCostBasis/currentPrice/PnL; Current Price refactored to DashboardStatCard
- `TradingBot.Dashboard/app/components/dashboard/PriceChart.vue` - avgLine annotation omitted when averageCostBasis is null

## Decisions Made

- `TotalCost` is `decimal` in the DTO (not `UsdAmount`) because `UsdAmount.Validate` requires `value > 0` but `Sum()` on empty list returns `0m`, which would trigger `VogenInvalidValueException` on implicit cast.
- `AverageCostBasis` and `CurrentPrice` are `Price?` (null not zero) — `Price.From(0)` would throw, so we guard with `totalBtc > 0` and catch exceptions respectively.
- PnL computed only inside `if (currentPrice.HasValue)` block, never accessing `.Value.Value` outside null-check.
- Current Price card refactored to `DashboardStatCard` for layout consistency.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

Pre-existing TypeScript errors in unrelated files (backtest components, app.vue useFetch null vs undefined typing) were observed during `nuxt typecheck`. These are out of scope and were not modified.

## Next Phase Readiness

- Dashboard endpoints now handle empty DB and unreachable Hyperliquid gracefully (200 with null fields instead of 500)
- INT-01 and FLOW-01 gap items from v2.0 milestone audit are closed
- Phase 19 complete; milestone v2.0 closure work can proceed

---
*Phase: 19-dashboard-nullable-price-fix*
*Completed: 2026-02-19*

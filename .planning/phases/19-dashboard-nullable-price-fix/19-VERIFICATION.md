---
phase: 19-dashboard-nullable-price-fix
verified: 2026-02-20T00:00:00Z
status: passed
score: 8/8 must-haves verified
re_verification: false
---

# Phase 19: Dashboard Nullable Price Fix Verification Report

**Phase Goal:** Dashboard endpoints handle empty DB and unreachable Hyperliquid gracefully -- no 500 errors from value object validation
**Verified:** 2026-02-20
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Portfolio endpoint returns 200 with null prices (not 500) when DB is empty | VERIFIED | `DashboardEndpoints.cs:38-40`: explicit `(decimal)` casts prevent Vogen accumulation; `averageCostBasis = totalBtc > 0 ? Price.From(...) : null` guards zero |
| 2 | Portfolio endpoint returns 200 with null CurrentPrice when Hyperliquid is unreachable | VERIFIED | `DashboardEndpoints.cs:42-50`: `Price? currentPrice = null` with try/catch; exception caught and logged, null propagated |
| 3 | TotalCost is decimal in DTO (not UsdAmount) so zero from empty Sum does not throw | VERIFIED | `DashboardDtos.cs:8`: `decimal TotalCost` with comment "TotalCost is decimal (not UsdAmount) because zero is valid when no purchases exist"; endpoint passes `TotalCost: totalCost` (raw decimal) |
| 4 | PnL fields are null (not computed from zero) when CurrentPrice is unavailable | VERIFIED | `DashboardEndpoints.cs:52-58`: `decimal? unrealizedPnl = null; decimal? unrealizedPnlPercent = null;` initialized null, only set inside `if (currentPrice.HasValue)` block |
| 5 | Chart endpoint returns 200 with null AverageCostBasis when no purchases exist | VERIFIED | `DashboardEndpoints.cs:236-238`: `totalBtc > 0` guard before `Price.From()`; `Price? averageCostBasis` in `PriceChartResponse` construction |
| 6 | Dashboard shows '--' for unavailable price fields instead of crashing or showing $0 | VERIFIED | `PortfolioStats.vue:58`: `if (props.portfolio.averageCostBasis === null) return '--'`; line 64: `if (props.portfolio.currentPrice === null) return '--'`; line 70: null check for PnL fields |
| 7 | Chart omits average cost line when AverageCostBasis is null | VERIFIED | `PriceChart.vue:87`: `if (chartData.value.averageCostBasis !== null)` wraps the `annotations.avgLine` assignment; null means no annotation key added |
| 8 | All existing tests pass without behavioral regression | VERIFIED | `dotnet test` result: 62 passed, 0 failed, 0 skipped |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `TradingBot.ApiService/Endpoints/DashboardDtos.cs` | Nullable Price, decimal TotalCost, nullable decimal PnL fields | VERIFIED | `Price? AverageCostBasis` (line 9), `Price? CurrentPrice` (line 10), `decimal? UnrealizedPnl` (line 11), `decimal? UnrealizedPnlPercent` (line 12), `decimal TotalCost` (line 8), `Price? AverageCostBasis` in `PriceChartResponse` (line 51) |
| `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` | Null-safe endpoint logic for empty DB and failed price fetch | VERIFIED | Contains `Price? currentPrice = null` (line 42), explicit `(decimal)` casts at lines 38-39 and 236-237, guard `totalBtc > 0` at lines 40 and 238 |
| `TradingBot.Dashboard/app/types/dashboard.ts` | Nullable TypeScript types matching backend DTOs | VERIFIED | `averageCostBasis: Price \| null` (line 16 and 58), `currentPrice: Price \| null` (line 17), `unrealizedPnl: number \| null` (line 18), `unrealizedPnlPercent: number \| null` (line 19), `totalCost: number` (line 15) |
| `TradingBot.Dashboard/app/components/dashboard/PortfolioStats.vue` | Dash display for null price fields | VERIFIED | `=== null` check with `'--'` return at lines 58, 64, 70; `pnlColorClass` returns `''` when PnL is null (line 81) |
| `TradingBot.Dashboard/app/components/dashboard/PriceChart.vue` | Conditional average cost line omission | VERIFIED | `if (chartData.value.averageCostBasis !== null)` at line 87 wraps `annotations.avgLine` block |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `DashboardEndpoints.cs` | `DashboardDtos.cs` | `PortfolioResponse` and `PriceChartResponse` construction with nullable fields | WIRED | `Price?` pattern confirmed at lines 40, 42-50, 238; DTO record types with `Price?` fields used directly in construction at lines 63-73 and 240-244 |
| `dashboard.ts` | `DashboardDtos.cs` | TypeScript types mirror C# DTO nullability | WIRED | `Price \| null` pattern at lines 16-17 in `PortfolioResponse`; `Price \| null` at line 58 in `PriceChartResponse`; `totalCost: number` at line 15 matches `decimal TotalCost` in C# |
| `PortfolioStats.vue` | `dashboard.ts` | Null checks before formatting price values | WIRED | `=== null` checks at lines 58, 64, 70 guard formatting; `'--'` returned as fallback; TypeScript types imported at line 37 of PortfolioStats.vue |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| TS-04 | 19-01-PLAN.md | Value objects serialize/deserialize correctly in all API endpoints (JSON round-trip) | SATISFIED | Phase 19 closes the INT-01 and FLOW-01 runtime bugs identified in the v2.0 milestone audit. Dashboard endpoints previously crashed with VogenInvalidValueException when Price.From(0) was attempted on empty DB/unreachable Hyperliquid. Now correctly serialize null values via `Price?` nullable fields. The milestone audit (`v2.0-MILESTONE-AUDIT.md`) confirms TS-04 `affected_requirements` for both INT-01 and FLOW-01, and Phase 19 closes those gaps. |

**Notes on TS-04 attribution:** REQUIREMENTS.md traceability table shows TS-04 under "Phase 14: Value Objects" because the original value object implementation happened there. Phase 19 is a gap closure that completes TS-04 correctness in edge cases (empty DB, unreachable Hyperliquid). The requirement is satisfied -- the v2.0 audit confirms the prior partial implementation and Phase 19 provides the completion.

**Orphaned requirements check:** No additional requirements from REQUIREMENTS.md are mapped to Phase 19 beyond TS-04.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None | - | - | - | No TODO/FIXME, no placeholder returns, no stub implementations found in any of the 5 modified files |

### Human Verification Required

#### 1. Live Dashboard Rendering with Null Values

**Test:** Start the application with an empty database (no purchases, no DailyPrice records). Open the dashboard.
**Expected:** All portfolio stat cards display '--' for Avg Cost Basis, Current Price, and Unrealized P&L. Total BTC shows 0. Total Cost shows $0.00. Chart shows empty frame with message "No price data available."
**Why human:** Cannot run the full Nuxt + API stack programmatically in this verification context; visual rendering of '--' vs crash cannot be confirmed via file inspection alone.

#### 2. Live Dashboard Rendering with Hyperliquid Unreachable

**Test:** Configure an invalid Hyperliquid endpoint or block network access. Load the dashboard portfolio view.
**Expected:** Current Price card shows '--'. Unrealized P&L shows '--'. Other fields (Total BTC, Total Cost, Avg Cost Basis if purchases exist) display normally. No 500 error.
**Why human:** Requires network-level fault injection to verify the exception catch path in `GetPortfolioAsync` handles real network failures gracefully.

### Gaps Summary

No gaps found. All 8 observable truths are verified against actual code, all 5 artifacts exist with substantive implementations, all 3 key links are confirmed wired, and the test suite passes with 62/62 tests.

The phase delivered exactly what it set out to: nullable Price fields in C# DTOs, null-safe endpoint construction logic, matching TypeScript types, and Vue components that display '--' for unavailable values with the chart omitting the average cost reference line when null.

---

_Verified: 2026-02-20_
_Verifier: Claude (gsd-verifier)_

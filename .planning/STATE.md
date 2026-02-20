# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** Phase 32 Tech Debt Cleanup
**Updated:** 2026-02-21

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA
**Current focus:** Phase 32 tech debt cleanup — price feed unit tests and exchange rate graceful degradation

## Current Position

Phase: 32 of 32+ (Tech Debt Cleanup) — IN PROGRESS
Plan: 1 of N executed (32-01 complete)
Status: Phase 32 in progress — 32-01 complete (price feed unit tests + exchange rate fix)
Last activity: 2026-02-21 — 32-01 complete (14 unit tests for price feed providers, exchange rate fallback fix)

Progress: [##########] 32-01 complete (14 tests, exchange rate graceful degradation fixed)

## Performance Metrics

**Velocity:**
- Total plans completed: 67 (across v1.0-v4.0 + Phase 31)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (11 plans)
- v4.0: 1 day (10 plans)
- Phase 30: 1 day (1 plan)
- Phase 31: 1 day (1 plan)
- Phase 32: in progress (1 plan so far)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 | Complete |
| v4.0 | 26-29 | 10 | Complete |
| Phase 30 (critical-bug-fixes) | 30 | 1 | Complete |
| Phase 31 (milestone-verification) | 31 | 1 | Complete |
| Phase 32 (tech-debt-cleanup) | 32 | 1+ | In Progress |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**v4.0 architecture decisions (from research):**
- Separate aggregate roots for PortfolioAsset and FixedDeposit (not TPH — avoids nullable columns)
- VndAmount value object with HasPrecision(18, 0); integer share quantities for ETF
- store cost_native + cost_usd + exchange_rate_at_transaction at write time (P&L computed in native currency only)
- DCA domain has zero knowledge of portfolio domain — connected via PurchaseCompletedEvent only
- API always returns both valueUsd and valueVnd; currency toggle is pure Flutter display logic
- VN ETF prices: best-effort only, 48h Redis TTL, staleness indicator always shown

**Phase 26 decisions:**
- VndAmount allows zero (non-negative) for zero-fee scenarios
- AssetTransaction.Create is internal to enforce PortfolioAsset aggregate boundary
- InterestCalculator uses 365-day year convention (Vietnamese banking standard)
- Compound interest test tolerance: 500 VND on 10M deposits (double-precision variance from Math.Pow)
- VND Principal: numeric(18,0); Crypto Quantity: numeric(18,8); Fee: numeric(18,2); Rate: numeric(8,6)

**Phase 27 decisions:**
- VNDirect dchart-api used instead of finfo-api (finfo times out externally, dchart verified live)
- PriceFeedEntry uses positional record with long FetchedAtUnixSeconds (avoids MessagePack DateTimeOffset resolver issues)
- VNDirect ETF provider uses stale-while-revalidate (returns stale immediately, fire-and-forget refresh)
- OpenErApi exchange rate uses wait-for-fetch (accuracy matters more for currency conversion)
- CoinGecko API key added as DefaultRequestHeader on HttpClient at DI registration time
- Shared resilience config: 2 retries, 1s exponential backoff, 15s total/8s attempt timeout
- VNDirect close prices multiplied by 1000 to convert from thousands-of-VND to actual VND

**Phase 28 decisions:**
- SourcePurchaseId uses optional parameter (default null) rather than separate overload to keep API surface minimal
- CoinGecko ID mapping via static dictionary (BTC=bitcoin, ETH=ethereum) — extensible for future coins
- P&L uses weighted average cost from buy transactions only (sells reduce position but don't change avg cost basis)
- Fixed deposit accrued/projected values computed per request (no caching — computation is cheap)
- Price feed failures use 0 for that asset, don't fail portfolio summary endpoint
- Historical migration triggers automatically when BTC asset has no bot-imported transactions on first summary call

**Phase 29 decisions:**
- SharedPreferences pre-loaded in main() before runApp() to keep CurrencyPreference synchronous (not AsyncValue)
- AllocationDonutChart uses StatefulWidget (not HookConsumerWidget) to isolate touch state rebuilds
- Transaction history fetches per-asset in parallel and merges client-side (no cross-asset backend endpoint)
- Fixed deposit detail reads from existing portfolioPageDataProvider (no extra API call)
- Unified add entry form uses SegmentedButton<FormMode> for Buy/Sell vs Fixed Deposit mode switching
- Bot badge: Container with bitcoinOrange.withAlpha(40) background and border
- DropdownButtonFormField uses initialValue (not deprecated value) for Flutter 3.33+ compatibility

**v3.0 Flutter conventions carried forward:**
- Dark-only theme, NavigationBar (Material 3) + CupertinoIcons, StatefulShellRoute
- Manual fromJson for DTO models (no json_serializable), intl as explicit dependency
- SliverAppBar with floating+snap, fl_chart with two-LineChartBarData approach
- Explicit isLoadingMore boolean (not copyWithPrevious) for pagination state
- [Phase 30-critical-bug-fixes]: allocationsByTypeVnd tracked as parallel dictionary alongside allocationsByType in GetSummaryAsync — no schema change, same loop
- [Phase 30-critical-bug-fixes]: Donut chart tooltip currency-aware: uses allocation.valueVnd when isVnd=true, allocation.valueUsd otherwise
- [Phase 30-critical-bug-fixes]: POST /api/portfolio/assets endpoint validates assetType and nativeCurrency enums, checks duplicate ticker, returns 201 with created asset

**Phase 31 decisions:**
- Both 27-01 and 27-02 SUMMARY files list PRICE-04 since PriceFeedResult.IsStale is defined in plan 01 and VNDirect/OpenErApi providers in plan 02 also use it
- Both 28-01 and 28-02 SUMMARY files list PORT-04/PORT-05 since event handler (plan 01) and endpoints (plan 02) both contribute to auto-import/migration
- PORT-01 and PORT-03 traceability table updated to show Phase 26 as primary (Phase 30 was bug-fix)

**Phase 32 decisions (32-01):**
- MockHttpMessageHandler as shared helper rather than nested class — reduces duplication across 3 test files
- ThrowingHttpMessageHandler separate class for cleaner API failure simulation vs delegate-that-throws
- VNDirect stale SWR test: TaskCompletionSource with 3s timeout detects fire-and-forget background HTTP call
- InvariantCulture required for decimal formatting in JSON fixture strings — locale comma separator causes wrong parse
- IDistributedCache injected directly in minimal API method signature — auto-resolved from DI
- Portfolio endpoint catch block reads Redis directly for exchange rate fallback — handles resilience pipeline exceptions

### Known Risks

- VNDirect dchart-api is undocumented/unofficial — could change without notice (research valid until 2026-03-20)

### Pending Todos

None.

### Roadmap Evolution

v4.0 roadmap: 4 phases (26-29), 20 requirements, all mapped and completed.
Phase 30: 1 critical bug fix phase, all 3 bugs resolved.
Phase 31: 1 milestone verification phase, all 20 requirements formally verified and closed.
Phase 32: tech debt cleanup — 32-01 complete (price feed unit tests + exchange rate graceful degradation).

## Session Continuity

Last session: 2026-02-21
Stopped at: Phase 32 plan 01 complete — 14 unit tests for CoinGecko/VNDirect/OpenErApi price providers, exchange rate fallback fixed in PortfolioEndpoints.
Next step: Phase 32 plan 02 (remaining 2 Phase 32 success criteria).

---
*State updated: 2026-02-21 after Phase 32 plan 01 completion (14 price feed unit tests + exchange rate graceful degradation)*

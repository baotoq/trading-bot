---
phase: 32-tech-debt-cleanup
plan: 01
subsystem: testing
tags: [unit-tests, nsubstitute, fluentassertions, messagepack, redis, distributed-cache, price-feeds, coingecko, vndirect, openerapi]

# Dependency graph
requires:
  - phase: 27-price-feeds
    provides: CoinGeckoPriceProvider, VNDirectPriceProvider, OpenErApiProvider, PriceFeedEntry, PriceFeedResult
  - phase: 28-portfolio-management
    provides: PortfolioEndpoints with exchange rate consumption
provides:
  - Unit tests for CoinGeckoPriceProvider (5 scenarios)
  - Unit tests for VNDirectPriceProvider (4 scenarios)
  - Unit tests for OpenErApiProvider (5 scenarios)
  - MockHttpMessageHandler shared test helper
  - Exchange rate graceful degradation in portfolio summary and assets endpoints
affects: [future price feed providers, portfolio endpoint changes]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - MockHttpMessageHandler/ThrowingHttpMessageHandler for testable HTTP in unit tests
    - IDistributedCache direct read in endpoint catch blocks for resilience fallback

key-files:
  created:
    - tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/MockHttpMessageHandler.cs
    - tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/CoinGeckoPriceProviderTests.cs
    - tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/VNDirectPriceProviderTests.cs
    - tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/OpenErApiProviderTests.cs
  modified:
    - TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs

key-decisions:
  - "MockHttpMessageHandler as shared helper in Infrastructure/PriceFeeds/ rather than nested in each test (reduces duplication)"
  - "ThrowingHttpMessageHandler separate class for cleaner API failure simulation"
  - "VNDirect stale test uses TaskCompletionSource to detect fire-and-forget background refresh without blocking"
  - "InvariantCulture required for decimal formatting in JSON test fixtures (avoids comma-as-decimal-separator locale issues)"
  - "IDistributedCache injected directly into minimal API handler parameters (auto-resolved from DI)"
  - "Portfolio endpoint catch block reads Redis directly for exchange rate fallback — handles resilience pipeline exceptions that bypass OpenErApiProvider stale logic"

patterns-established:
  - "MockHttpMessageHandler pattern: Func<HttpRequestMessage, HttpResponseMessage> delegate for test HTTP responses"
  - "Stale cache test: serialize PriceFeedEntry with backdated FetchedAtUnixSeconds (well past freshness window)"
  - "InvariantCulture for decimal in JSON fixture strings to avoid locale-sensitive parsing"

requirements-completed: []

# Metrics
duration: 4min
completed: 2026-02-21
---

# Phase 32 Plan 01: Price Feed Provider Unit Tests and Exchange Rate Graceful Degradation Summary

**14 unit tests across 3 new test files covering all cache/API scenarios for CoinGecko, VNDirect, and OpenErApi providers; PortfolioEndpoints exchange rate catch blocks now fall back to Redis-cached rate instead of 0**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-20T17:49:30Z
- **Completed:** 2026-02-20T17:53:30Z
- **Tasks:** 2 of 2
- **Files modified:** 5 (4 created, 1 modified)

## Accomplishments

- 14 unit tests across 3 files: CoinGeckoPriceProviderTests (5), VNDirectPriceProviderTests (4), OpenErApiProviderTests (5)
- MockHttpMessageHandler and ThrowingHttpMessageHandler shared test helpers for all price feed tests
- Exchange rate failure in GetSummaryAsync and GetAssetsAsync now falls back to last Redis-cached rate instead of returning 0
- All 90 tests pass (81 existing + 9 new price feed tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add unit tests for all three price feed providers** - `58c1979` (feat)
2. **Task 2: Fix exchange rate graceful degradation in portfolio endpoints** - `913c98b` (fix)

**Plan metadata:** `(docs commit follows)`

## Files Created/Modified

- `tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/MockHttpMessageHandler.cs` - Shared HTTP handler helpers (MockHttpMessageHandler with delegate, ThrowingHttpMessageHandler)
- `tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/CoinGeckoPriceProviderTests.cs` - 5 tests: fresh cache hit, stale+API success (with API key header assertion), stale+API failure, empty+API success, empty+API failure
- `tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/VNDirectPriceProviderTests.cs` - 4 tests: fresh cache hit, stale SWR fire-and-forget refresh detection, empty+API success (VND conversion 20.29*1000), empty+API failure
- `tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/OpenErApiProviderTests.cs` - 5 tests: fresh cache hit, stale+API success, stale+API failure, empty+API success, empty+API failure
- `TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs` - Added IDistributedCache param to GetSummaryAsync and GetAssetsAsync; catch blocks now read Redis directly to return stale rate instead of 0

## Decisions Made

- MockHttpMessageHandler as shared helper file rather than nested classes — reduces duplication across 3 test files
- ThrowingHttpMessageHandler as a separate class — cleaner API failure simulation vs configuring delegate to throw
- VNDirect stale-while-revalidate test: TaskCompletionSource with 3s timeout to detect background HTTP call without artificial delays
- InvariantCulture required when formatting decimals in JSON test fixture strings — system locale may use comma as decimal separator (20.29 → "20,29" → parsed as 29 → 29000 VND instead of 20290)
- IDistributedCache injected directly in minimal API method signature — ASP.NET Core auto-resolves from DI
- Read Redis directly in catch block rather than extracting to method — keeps fallback path explicit and readable

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed locale-sensitive decimal in VNDirect JSON test fixture**
- **Found during:** Task 1 (VNDirectPriceProviderTests - EmptyCacheAndApiSuccess test)
- **Issue:** `$"{{\"c\":[{closePriceThousands}],...}}"` formats `20.29m` as `"20,29"` on Vietnamese/European locales, causing JSON parser to read `29` (not `20.29`), resulting in `29000` VND instead of `20290`
- **Fix:** Used `.ToString(System.Globalization.CultureInfo.InvariantCulture)` for the decimal value in the string interpolation
- **Files modified:** tests/TradingBot.ApiService.Tests/Infrastructure/PriceFeeds/VNDirectPriceProviderTests.cs
- **Verification:** Test passes with result.Price == 20290m
- **Committed in:** 58c1979 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Required for test correctness on non-invariant locales. No scope creep.

## Issues Encountered

None beyond the locale-sensitive decimal formatting bug (handled as auto-fix above).

## Next Phase Readiness

- 3 of 5 Phase 32 success criteria now closed (unit tests for all price providers + exchange rate graceful degradation)
- Ready for Phase 32 Plan 02 (remaining 2 success criteria)

---
*Phase: 32-tech-debt-cleanup*
*Completed: 2026-02-21*

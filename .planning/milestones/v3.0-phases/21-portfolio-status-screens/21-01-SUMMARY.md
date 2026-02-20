---
phase: 21-portfolio-status-screens
plan: 01
subsystem: api
tags: [flutter, dart, dio, riverpod, riverpod-generator, models, home-screen, dotnet, dto]

# Dependency graph
requires:
  - phase: 20-02
    provides: Dio HTTP client with ApiKeyInterceptor, dioProvider, typed ApiException hierarchy, error widgets
provides:
  - LiveStatusResponse extended with LastPurchaseMultiplier and LastPurchaseDropPercentage fields
  - PortfolioResponse Dart model (9 fields) matching .NET PortfolioResponse DTO
  - StatusResponse Dart model (11 fields) matching extended LiveStatusResponse DTO
  - HomeRepository with fetchPortfolio() and fetchStatus() Dio GET calls
  - HomeData record combining PortfolioResponse and StatusResponse
  - homeRepositoryProvider and homeDataProvider via @riverpod code generation
  - 30-second auto-refresh timer using Timer + ref.invalidateSelf() + ref.onDispose
  - HomeScreen wired to real homeDataProvider (placeholder UI showing totalBtc)
  - timeago ^3.7.0 package for relative date formatting
affects:
  - 21-02-portfolio-status-ui
  - 22-chart-history-screen

# Tech tracking
tech-stack:
  added:
    - "timeago ^3.7.0 — relative time formatting (2 hours ago, Yesterday at 3:00 PM)"
  patterns:
    - "Data layer isolation: HomeRepository owns all Dio calls; providers compose at the Riverpod layer"
    - "Parallel fetch: Future.wait([fetchPortfolio(), fetchStatus()]) fetches both endpoints in one network round"
    - "Silent auto-refresh: Timer(30s, ref.invalidateSelf) with ref.onDispose(timer.cancel) — no flash or animation"
    - "Manual fromJson: no code generation for DTO models — simple, explicit, no extra build step"

key-files:
  created:
    - TradingBot.Mobile/lib/features/home/data/models/portfolio_response.dart
    - TradingBot.Mobile/lib/features/home/data/models/status_response.dart
    - TradingBot.Mobile/lib/features/home/data/home_repository.dart
    - TradingBot.Mobile/lib/features/home/data/home_providers.dart
    - TradingBot.Mobile/lib/features/home/data/home_providers.g.dart
  modified:
    - TradingBot.ApiService/Endpoints/DashboardDtos.cs
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - TradingBot.Mobile/pubspec.yaml
    - TradingBot.Mobile/pubspec.lock
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart

key-decisions:
  - "Remove home_screen.g.dart and part directive: HomeScreen no longer has @riverpod annotations after moving provider to home_providers.dart; build_runner deletes the .g.dart automatically"
  - "Manual fromJson for DTO models: no json_serializable code generation dependency — simpler, explicit deserialization with (json['field'] as num).toDouble() for Vogen value objects that serialize as raw numbers"

patterns-established:
  - "Feature data layer: lib/features/{feature}/data/ contains models/, repository, and providers — clear separation from presentation"
  - "HomeData composite: single HomeData class aggregates both API responses so providers can watch a single provider"

requirements-completed: [PORT-01, PORT-02, PORT-03, PORT-04, PORT-05]

# Metrics
duration: 3min
completed: 2026-02-20
---

# Phase 21 Plan 01: Portfolio Status Screens — Data Layer Summary

**Dart DTO models, HomeRepository (Dio GET calls to /portfolio and /status), and homeDataProvider with 30-second auto-refresh via Timer + ref.invalidateSelf(); .NET LiveStatusResponse extended with LastPurchaseMultiplier and LastPurchaseDropPercentage**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-02-20T06:58:44Z
- **Completed:** 2026-02-20T07:02:00Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments

- Extended .NET LiveStatusResponse with LastPurchaseMultiplier and LastPurchaseDropPercentage nullable fields, populated from lastPurchase in GetLiveStatusAsync; .NET build and all 62 tests pass
- Created PortfolioResponse (9 fields) and StatusResponse (11 fields) Dart models with manual fromJson factories using camelCase keys — matching .NET Vogen value object JSON serialization (raw numbers)
- Created HomeRepository with parallel fetchPortfolio()/fetchStatus() Dio calls, HomeData composite record, and homeDataProvider with 30-second auto-refresh; HomeScreen wired to real data layer

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend LiveStatusResponse and create Dart DTO models** - `d258baf` (feat)
2. **Task 2: Create HomeRepository and Riverpod providers with 30-second auto-refresh** - `2ebbd1f` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/Endpoints/DashboardDtos.cs` - Added LastPurchaseMultiplier and LastPurchaseDropPercentage nullable fields to LiveStatusResponse
- `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` - Populated new fields from lastPurchase in GetLiveStatusAsync
- `TradingBot.Mobile/pubspec.yaml` - Added timeago ^3.7.0 dependency
- `TradingBot.Mobile/pubspec.lock` - Updated lock file
- `TradingBot.Mobile/lib/features/home/data/models/portfolio_response.dart` - Dart model matching .NET PortfolioResponse (9 fields, manual fromJson)
- `TradingBot.Mobile/lib/features/home/data/models/status_response.dart` - Dart model matching extended LiveStatusResponse (11 fields including 2 new multiplier fields)
- `TradingBot.Mobile/lib/features/home/data/home_repository.dart` - HomeRepository class with fetchPortfolio() and fetchStatus() Dio GET calls
- `TradingBot.Mobile/lib/features/home/data/home_providers.dart` - HomeData record + homeRepositoryProvider + homeDataProvider with 30s timer auto-refresh
- `TradingBot.Mobile/lib/features/home/data/home_providers.g.dart` - Generated Riverpod providers (homeRepositoryProvider, homeDataProvider)
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart` - Updated to use homeDataProvider from home_providers.dart; displays portfolio.totalBtc as placeholder

## Decisions Made

- **Remove part directive from home_screen.dart:** After moving the `@riverpod` homeData function to home_providers.dart, home_screen.dart had no @riverpod annotations. Build_runner automatically deleted home_screen.g.dart; the `part` directive was also removed to avoid a "URI does not exist" error.
- **Manual fromJson for Dart models:** json_serializable adds a code generation dependency for simple DTOs. Manual deserialization with explicit `(json['field'] as num).toDouble()` is clear, safe, and requires no annotation — consistent with how Vogen value objects serialize as raw JSON numbers.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Removed stale `part 'home_screen.g.dart'` directive**
- **Found during:** Task 2 (after updating home_screen.dart)
- **Issue:** Plan specified deleting home_screen.g.dart, but the `part` directive referencing it remained. Build_runner auto-deleted the .g.dart file (correctly, since no @riverpod annotations remain in home_screen.dart), but the part directive would cause a "URI does not exist" analysis error.
- **Fix:** Removed `part 'home_screen.g.dart';` from home_screen.dart
- **Files modified:** `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart`
- **Verification:** `dart analyze lib/` reports "No issues found!"
- **Committed in:** 2ebbd1f (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary fix — stale part directive would cause a compile error. No scope creep.

## Issues Encountered

None beyond the deviation documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Data layer fully operational: HomeRepository fetches from /api/dashboard/portfolio and /api/dashboard/status via Dio with ApiKeyInterceptor
- homeDataProvider auto-refreshes every 30 seconds silently; pull-to-refresh triggers immediate re-fetch
- HomeScreen wired to real data (placeholder showing totalBtc); ready for Plan 02 to build the full portfolio UI
- timeago package installed for relative date formatting (last purchase "2 hours ago")
- dart analyze passes with no issues; all 62 .NET tests pass

---
*Phase: 21-portfolio-status-screens*
*Completed: 2026-02-20*

## Self-Check: PASSED

All files found on disk. All commits verified in git log.

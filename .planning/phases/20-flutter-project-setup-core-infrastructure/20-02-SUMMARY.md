---
phase: 20-flutter-project-setup-core-infrastructure
plan: 02
subsystem: api
tags: [flutter, dart, dio, riverpod, interceptor, api-key, error-handling, snackbar]

# Dependency graph
requires:
  - phase: 20-01
    provides: Flutter scaffold with HookConsumerWidget screens, Riverpod code generation setup, dark theme
provides:
  - Dio HTTP client with ApiKeyInterceptor injecting x-api-key header on every request
  - Build-time config constants (kApiBaseUrl, kApiKey) via String.fromEnvironment
  - Typed exception hierarchy (AuthenticationException, NetworkException, ServerException)
  - showErrorSnackbar and showAuthErrorSnackbar floating SnackBar helpers
  - RetryWidget for cold-start failures ("Could not load data" + Retry button)
  - HomeScreen demonstrating full Riverpod async error handling pattern
affects:
  - 21-home-portfolio-screen
  - 22-chart-screen
  - 23-history-screen
  - 24-config-screen

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Build-time injection: const String.fromEnvironment for API URL and API key"
    - "Interceptor pattern: ApiKeyInterceptor wraps DioException with typed ApiException subclasses"
    - "Riverpod async pattern: ref.listen for snackbar side effects, switch on AsyncValue for UI states"
    - "Cached data pattern: homeData.value (T?) preserves previous value during re-fetch errors"
    - "Cold-start failure: AsyncError() with no cached value shows RetryWidget"

key-files:
  created:
    - TradingBot.Mobile/lib/core/api/config.dart
    - TradingBot.Mobile/lib/core/api/api_exception.dart
    - TradingBot.Mobile/lib/core/api/api_client.dart
    - TradingBot.Mobile/lib/core/api/api_client.g.dart
    - TradingBot.Mobile/lib/core/widgets/error_snackbar.dart
    - TradingBot.Mobile/lib/core/widgets/retry_widget.dart
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.g.dart
  modified:
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart

key-decisions:
  - "Use AsyncValue.value (T?) instead of non-existent valueOrNull — Riverpod 3.2.1 uses .value which returns T? for loading/error states, T for data"
  - "Extract cachedValue before switch expression — Dart type narrowing in switch arms removes .value getter from AsyncError<T>"

patterns-established:
  - "API layer: sealed ApiException hierarchy with AuthenticationException, NetworkException, ServerException"
  - "Error handling: ref.listen triggers snackbar, AsyncValue switch renders UI state"
  - "Pull-to-refresh: ref.refresh(provider.future) for full re-fetch; ref.invalidate(provider) for retry button"

requirements-completed: [APP-01, APP-02, APP-03, APP-06]

# Metrics
duration: 3min
completed: 2026-02-19
---

# Phase 20 Plan 02: HTTP Client and Error Handling Infrastructure Summary

**Dio HTTP client with ApiKeyInterceptor (x-api-key header injection), typed exception hierarchy (AuthenticationException/NetworkException/ServerException), and HomeScreen demonstrating full Riverpod async error handling with snackbar and RetryWidget**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-19T19:16:46Z
- **Completed:** 2026-02-19T19:19:40Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- Created Dio client with ApiKeyInterceptor that injects x-api-key on every request and wraps 401/403 as AuthenticationException, 500+ as ServerException, connection errors as NetworkException
- Created showErrorSnackbar/showAuthErrorSnackbar helpers and RetryWidget ("Could not load data" with Retry button) as shared error presentation components
- Upgraded HomeScreen to HookConsumerWidget with homeDataProvider demonstrating all 3 error scenarios: ref.listen for snackbar triggers, AsyncValue switch for cached/retry/loading states, RefreshIndicator with ref.refresh

## Task Commits

Each task was committed atomically:

1. **Task 1: Create build-time config, Dio API client with interceptors, and typed exceptions** - `d9e0dfb` (feat)
2. **Task 2: Create error presentation widgets and wire up Home screen demonstration** - `eeb459d` (feat)

## Files Created/Modified

- `TradingBot.Mobile/lib/core/api/config.dart` - const kApiBaseUrl and kApiKey via String.fromEnvironment
- `TradingBot.Mobile/lib/core/api/api_exception.dart` - sealed ApiException with AuthenticationException, NetworkException, ServerException
- `TradingBot.Mobile/lib/core/api/api_client.dart` - Dio factory with ApiKeyInterceptor and error wrapping; dioProvider via @riverpod
- `TradingBot.Mobile/lib/core/api/api_client.g.dart` - Generated dioProvider
- `TradingBot.Mobile/lib/core/widgets/error_snackbar.dart` - showErrorSnackbar and showAuthErrorSnackbar helpers
- `TradingBot.Mobile/lib/core/widgets/retry_widget.dart` - RetryWidget with wifi_slash icon, "Could not load data", Retry button
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart` - Upgraded to HookConsumerWidget with full error handling pattern
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.g.dart` - Generated homeDataProvider

## Decisions Made

- **AsyncValue.value instead of valueOrNull:** Riverpod 3.2.1 does not have `valueOrNull`. The `.value` getter (returning `T?`) serves the same purpose — returns the cached value during loading/error states when previous data exists.
- **Extract cachedValue before switch:** Dart's type narrowing in switch arms narrows `homeData` to `AsyncError<String>` within `AsyncError()` arms, removing access to `.value`. Extracting `final cachedValue = homeData.value` before the switch preserves access to the nullable cached value.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Applied super parameter syntax to ApiException subclasses**
- **Found during:** Task 1 (dart analyze)
- **Issue:** `use_super_parameters` lint: AuthenticationException and NetworkException used explicit `message` parameter + `: super(message)` forwarding
- **Fix:** Used super parameter syntax `([super.message = 'Authentication failed'])` — idiomatic Dart
- **Files modified:** `TradingBot.Mobile/lib/core/api/api_exception.dart`
- **Verification:** `dart analyze lib/core/` reports "No issues found!"
- **Committed in:** d9e0dfb (Task 1 commit)

**2. [Rule 1 - Bug] Fixed AsyncValue.valueOrNull does not exist in Riverpod 3.2.1**
- **Found during:** Task 2 (dart analyze)
- **Issue:** Plan specified `homeData.valueOrNull` which does not exist in Riverpod 3.2.1's `AsyncValue<T>` API
- **Fix:** Used `homeData.value` (returns `T?`) extracted to local `cachedValue` before switch expression to avoid type narrowing issue
- **Files modified:** `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart`
- **Verification:** `dart analyze` reports "No issues found!"
- **Committed in:** eeb459d (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 lint fix, 1 API compatibility fix)
**Impact on plan:** Both fixes necessary for correctness. The valueOrNull fix is equivalent behavior — same semantics, correct API for the installed Riverpod version.

## Issues Encountered

None beyond the deviations documented above.

## User Setup Required

None - no external service configuration required. Build-time API key and base URL are injected via `--dart-define` flags at run time.

## Next Phase Readiness

- HTTP client infrastructure complete; all feature screens can use dioProvider
- Error presentation components ready (snackbar helpers + RetryWidget)
- HomeScreen demonstrates the canonical error handling pattern for Phase 21-23 screens to follow
- dart analyze passes with zero errors
- Ready for Phase 21: Home Portfolio Screen (real portfolio data via Dio)

---
*Phase: 20-flutter-project-setup-core-infrastructure*
*Completed: 2026-02-19*

## Self-Check: PASSED

All files found on disk. All commits verified in git log.

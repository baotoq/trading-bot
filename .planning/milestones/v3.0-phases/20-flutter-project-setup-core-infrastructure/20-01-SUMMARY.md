---
phase: 20-flutter-project-setup-core-infrastructure
plan: 01
subsystem: ui
tags: [flutter, dart, go_router, riverpod, hooks_riverpod, dio, material3, dark-theme]

# Dependency graph
requires: []
provides:
  - Flutter iOS project with go_router 4-tab StatefulShellRoute.indexedStack navigation
  - Dark-only Material 3 theme with Bitcoin orange (#F7931A) accent and custom color constants
  - ProviderScope + MaterialApp.router entry point in main.dart
  - 4 placeholder ConsumerWidget screens (Home, Chart, History, Config) with RefreshIndicator scaffolding
  - Riverpod code generation setup (build_runner + riverpod_generator) validated
affects:
  - 20-02-PLAN.md
  - 21-home-portfolio-screen
  - 22-chart-screen
  - 23-history-screen
  - 24-config-screen

# Tech tracking
tech-stack:
  added:
    - go_router ^17.1.0 - declarative routing with StatefulShellRoute.indexedStack
    - hooks_riverpod ^3.2.1 - state management with hooks integration
    - riverpod_annotation ^4.0.2 - @riverpod code generation annotations
    - flutter_hooks any - stateful hooks for HookConsumerWidget
    - dio ^5.9.1 - HTTP client with interceptor chain
    - cupertino_icons ^1.0.8 - SF Symbols-style icons
    - build_runner any - code generation runner
    - riverpod_generator ^4.0.3 - provider code generation
    - riverpod_lint any - Riverpod-specific lint rules (uses analysis_server_plugin)
    - flutter_lints ^5.0.0 - Flutter recommended lints
  patterns:
    - Dark-only MaterialApp.router with no darkTheme/themeMode (forces dark always)
    - StatefulShellRoute.indexedStack with 5 GlobalKey<NavigatorState> for tab state preservation
    - NavigationBar with CupertinoIcons (NOT CupertinoTabBar - avoids known crash with StatefulShellRoute)
    - goBranch(index, initialLocation: index == currentIndex) for pop-to-root on active tab tap
    - ConsumerWidget as base class for all feature screens (Riverpod-aware)
    - RefreshIndicator wrapping CustomScrollView+SliverFillRemaining for pull-to-refresh scaffolding

key-files:
  created:
    - TradingBot.Mobile/lib/app/theme.dart
    - TradingBot.Mobile/lib/app/router.dart
    - TradingBot.Mobile/lib/shared/navigation_shell.dart
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart
    - TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart
    - TradingBot.Mobile/lib/features/history/presentation/history_screen.dart
    - TradingBot.Mobile/lib/features/config/presentation/config_screen.dart
  modified:
    - TradingBot.Mobile/pubspec.yaml
    - TradingBot.Mobile/analysis_options.yaml
    - TradingBot.Mobile/lib/main.dart

key-decisions:
  - "Removed custom_lint from pubspec (analyzer ^8.0.0 incompatible with riverpod_generator 4.x requiring analyzer ^9.0.0); riverpod_lint 3.x uses analysis_server_plugin directly"
  - "Dark-only theme: single ThemeData with Brightness.dark, no darkTheme or themeMode — ignores iOS system setting"
  - "NavigationBar (Material 3) with CupertinoIcons over CupertinoTabBar to avoid GlobalKey crash with StatefulShellRoute"
  - "ConsumerWidget base class for all feature screens (not StatelessWidget) — Riverpod-ready for future provider integration"

patterns-established:
  - "Router pattern: StatefulShellRoute.indexedStack with one GlobalKey per branch + root"
  - "Navigation shell: ScaffoldWithNavigation StatelessWidget taking StatefulNavigationShell as required param"
  - "Theme: AppTheme class with static color constants and single dark ThemeData getter"
  - "Feature screens: ConsumerWidget + RefreshIndicator + CustomScrollView + SliverFillRemaining"

requirements-completed: [APP-04, APP-05]

# Metrics
duration: 4min
completed: 2026-02-20
---

# Phase 20 Plan 01: Flutter Project Setup Summary

**Flutter iOS app scaffolded with go_router 17 4-tab StatefulShellRoute.indexedStack, dark-only Material 3 theme with Bitcoin orange accent, and Riverpod 3 ConsumerWidget placeholder screens with pull-to-refresh scaffolding**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-19T19:09:45Z
- **Completed:** 2026-02-19T19:13:52Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- Configured pubspec.yaml with full dependency stack (go_router, hooks_riverpod, dio, cupertino_icons, build_runner, riverpod_generator, riverpod_lint)
- Created dark-only Material 3 theme with Bitcoin orange seed color, custom color constants (profitGreen, lossRed, surfaceDark, navBarDark), and floating snackbar theme
- Built go_router with StatefulShellRoute.indexedStack 4-tab navigation and NavigationBar shell using CupertinoIcons
- Created 4 placeholder ConsumerWidget screens with RefreshIndicator scaffolding; dart analyze passes zero errors; build_runner validated

## Task Commits

Each task was committed atomically:

1. **Task 1: Configure project dependencies, structure, and dark theme** - `e401314` (chore)
2. **Task 2: Create go_router with 4-tab StatefulShellRoute and navigation shell** - `40479b5` (feat)
3. **Task 3: Create 4 placeholder feature screens with pull-to-refresh scaffold** - `fa7f22a` (feat)

## Files Created/Modified

- `TradingBot.Mobile/pubspec.yaml` - Updated with trading_bot_app name + full dependency stack
- `TradingBot.Mobile/analysis_options.yaml` - Simplified to flutter_lints only
- `TradingBot.Mobile/lib/main.dart` - ProviderScope + MaterialApp.router dark-only entry point
- `TradingBot.Mobile/lib/app/theme.dart` - AppTheme class with dark Material 3 ThemeData and Bitcoin orange accent
- `TradingBot.Mobile/lib/app/router.dart` - GoRouter with StatefulShellRoute.indexedStack and 4 branches
- `TradingBot.Mobile/lib/shared/navigation_shell.dart` - ScaffoldWithNavigation with NavigationBar + CupertinoIcons
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart` - HomeScreen ConsumerWidget placeholder
- `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` - ChartScreen ConsumerWidget placeholder
- `TradingBot.Mobile/lib/features/history/presentation/history_screen.dart` - HistoryScreen ConsumerWidget placeholder
- `TradingBot.Mobile/lib/features/config/presentation/config_screen.dart` - ConfigScreen ConsumerWidget placeholder

## Decisions Made

- **Removed custom_lint:** `riverpod_generator ^4.0.3` uses `analyzer ^9.0.0` while `custom_lint` (latest 0.8.1) requires `analyzer ^8.0.0` — irreconcilable conflict. `riverpod_lint 3.x` uses `analysis_server_plugin` directly and doesn't need `custom_lint` as a separate package.
- **Dark-only theme:** Single `ThemeData` with `Brightness.dark` seed color; no `darkTheme` or `themeMode` as per locked decision — app ignores iOS system setting.
- **NavigationBar over CupertinoTabBar:** Followed research recommendation to avoid known GlobalKey crash when using CupertinoTabBar with StatefulShellRoute.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Removed custom_lint due to analyzer version conflict with riverpod_generator 4.x**
- **Found during:** Task 1 (flutter pub get)
- **Issue:** `custom_lint any` resolved to 0.8.1 which requires `analyzer ^8.0.0`; `riverpod_generator ^4.0.3` requires `analyzer ^9.0.0` — dependency solving failed
- **Fix:** Removed `custom_lint` from pubspec.yaml dev_dependencies; `riverpod_lint 3.x` uses `analysis_server_plugin` (not `custom_lint`) and works independently
- **Files modified:** `TradingBot.Mobile/pubspec.yaml`, `TradingBot.Mobile/analysis_options.yaml`
- **Verification:** `flutter pub get` succeeds; `dart analyze` passes with zero issues
- **Committed in:** e401314 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking dependency conflict)
**Impact on plan:** Necessary resolution; riverpod_lint still provides Riverpod-specific lint rules without custom_lint peer dependency.

## Issues Encountered

None beyond the dependency conflict resolved above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Flutter project structure established; all feature screens exist as ConsumerWidget placeholders
- go_router navigation fully wired; tab switching with state preservation ready
- Riverpod code generation (build_runner + riverpod_generator) validated
- Ready for Plan 20-02: Dio HTTP client setup and API key interceptor

---
*Phase: 20-flutter-project-setup-core-infrastructure*
*Completed: 2026-02-20*

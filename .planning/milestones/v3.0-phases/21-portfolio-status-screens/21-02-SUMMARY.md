---
phase: 21-portfolio-status-screens
plan: 02
subsystem: mobile-ui
tags: [flutter, dart, riverpod, widgets, home-screen, portfolio, intl, timeago]

# Dependency graph
requires:
  - phase: 21-01
    provides: homeDataProvider, HomeData, PortfolioResponse, StatusResponse, HomeRepository with 30s auto-refresh
provides:
  - PortfolioStatsSection widget (hero value + 2x2 stat grid + P&L green/red)
  - HealthBadge widget (colored dot + label for Healthy/Warning/Down)
  - CountdownText widget (human-readable approximate countdown to next buy)
  - LastBuyCard widget (price prominent, multiplier badge, severity-coded drop %)
  - Complete HomeScreen assembled with SliverAppBar + RefreshIndicator + AsyncValue switch pattern
affects:
  - 22-chart-history-screen

# Tech tracking
tech-stack:
  added:
    - "intl: any — explicit dependency for NumberFormat currency and BTC formatting (was transitive, now declared)"
  patterns:
    - "Widget decomposition: all child widgets are StatelessWidget taking data as constructor parameters — no provider access inside child widgets"
    - "SliverAppBar with HealthBadge in actions — always visible in scroll-aware app bar"
    - "AsyncValue switch with stale cache: AsyncError + cachedValue != null shows stale data + snackbar instead of error screen"
    - "GridView.count with shrinkWrap + NeverScrollableScrollPhysics for 2x2 stat grid inside ScrollView"

key-files:
  created:
    - TradingBot.Mobile/lib/features/home/presentation/widgets/portfolio_stats_section.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/health_badge.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/countdown_text.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/last_buy_card.dart
  modified:
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart
    - TradingBot.Mobile/pubspec.yaml
    - TradingBot.Mobile/pubspec.lock

key-decisions:
  - "intl added as explicit dependency: intl was a transitive dependency via Flutter SDK; dart analyze's depend_on_referenced_packages lint flagged it as info-level. Added intl: any to pubspec.yaml to satisfy linter and make the dependency explicit"
  - "SliverAppBar over plain AppBar: using SliverAppBar with floating+snap gives the portfolio content more vertical space on scroll while keeping the health badge always accessible"

patterns-established:
  - "Presentation layer isolation: all 4 widgets in lib/features/home/presentation/widgets/ take plain Dart models as parameters — pure StatelessWidget components testable in isolation"
  - "AsyncValue stale cache pattern: extract homeData.value before switch, use AsyncError() when cachedValue != null branch to show stale UI + snackbar instead of error widget"

requirements-completed: [PORT-01, PORT-02, PORT-03, PORT-04, PORT-05]

# Metrics
duration: 2min
completed: 2026-02-20
---

# Phase 21 Plan 02: Portfolio Status Screens — UI Summary

**Complete HomeScreen UI with portfolio stats cards, live price display, health badge in SliverAppBar, countdown to next buy, and last purchase detail card — all bound to homeDataProvider from Plan 01**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-02-20T07:05:46Z
- **Completed:** 2026-02-20T07:08:07Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Created 4 extracted StatelessWidget components in `lib/features/home/presentation/widgets/`:
  - PortfolioStatsSection: hero portfolio value (currentPrice * totalBtc), 2x2 stat grid (total BTC, total cost, current price, avg cost), P&L with green/red coloring using AppTheme constants
  - HealthBadge: colored 8x8 dot + label based on healthStatus string ("Healthy"/"Warning"/"Down"); compact for app bar placement
  - CountdownText: parses ISO 8601 nextBuyTime, calculates difference from now, renders "Next buy in ~X days/hours/minutes" or "Buying soon..."
  - LastBuyCard: purchase price as headlineSmall, BTC amount, colored multiplier badge (bitcoinOrange for >1x, grey for base), drop % with 4-tier severity colors (grey/amber/orange/lossRed)
- Replaced HomeScreen placeholder with full layout: SliverAppBar with HealthBadge in actions, RefreshIndicator + CustomScrollView with SliverPadding containing PortfolioStatsSection -> CountdownText -> LastBuyCard
- Error handling follows Phase 20 pattern: ref.listen snackbar for auth vs generic errors; stale cached data remains visible during AsyncError; cold-start failure shows RetryWidget

## Task Commits

Each task was committed atomically:

1. **Task 1: Create extracted widget components (stats section, health badge, countdown, last buy card)** - `bd1c469` (feat)
2. **Task 2: Assemble HomeScreen with all widgets, error handling, and pull-to-refresh** - `390ef62` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.Mobile/lib/features/home/presentation/widgets/portfolio_stats_section.dart` - PortfolioStatsSection with hero value, 2x2 stat grid, P&L display using intl NumberFormat
- `TradingBot.Mobile/lib/features/home/presentation/widgets/health_badge.dart` - HealthBadge with colored dot (green/amber/red) and label
- `TradingBot.Mobile/lib/features/home/presentation/widgets/countdown_text.dart` - CountdownText with approximate human-readable countdown
- `TradingBot.Mobile/lib/features/home/presentation/widgets/last_buy_card.dart` - LastBuyCard with price, multiplier badge, severity-coded drop %
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart` - Full HomeScreen with SliverAppBar, RefreshIndicator, AsyncValue switch, all 4 widgets
- `TradingBot.Mobile/pubspec.yaml` - Added intl: any as explicit dependency

## Decisions Made

- **intl added as explicit dependency:** The intl package was already available as a transitive dependency via Flutter SDK, but dart analyze's `depend_on_referenced_packages` lint flagged `import 'package:intl/intl.dart'` with an info-level warning. Added `intl: any` to pubspec.yaml to satisfy the linter and make the dependency contract explicit.
- **SliverAppBar with floating+snap:** Chose SliverAppBar over a plain AppBar to give the portfolio content (hero number, stat cards) more vertical space on scroll while keeping the health badge in the actions slot and always accessible.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing] Added intl as explicit pubspec dependency**
- **Found during:** Task 1 (after creating portfolio_stats_section.dart and last_buy_card.dart)
- **Issue:** `dart analyze` reported `depend_on_referenced_packages` info for `package:intl/intl.dart` — the package was transitive but not declared as a direct dependency
- **Fix:** Added `intl: any` to `pubspec.yaml` dependencies and ran `flutter pub get`
- **Files modified:** `TradingBot.Mobile/pubspec.yaml`, `TradingBot.Mobile/pubspec.lock`
- **Committed in:** bd1c469 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 missing explicit dependency)
**Impact on plan:** Minor — linter compliance, no behavior change.

## Issues Encountered

None beyond the deviation documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- HomeScreen fully functional with all 5 PORT requirements satisfied (PORT-01 through PORT-05)
- All 4 widget components are isolated StatelessWidgets ready for testing
- dart analyze passes with zero issues on full lib/
- 30-second auto-refresh from Plan 01 continues to work silently in the background
- Pull-to-refresh and error handling follow established Phase 20 patterns
- Phase 21 complete — Phase 22 (Chart History Screen) can proceed

---
*Phase: 21-portfolio-status-screens*
*Completed: 2026-02-20*

## Self-Check: PASSED

All files found on disk. All commits verified in git log.

---
phase: 22-price-chart-purchase-history
plan: 01
subsystem: ui
tags: [flutter, fl_chart, riverpod, dart, charting, line-chart]

# Dependency graph
requires:
  - phase: 21-portfolio-status-screens
    provides: HomeScreen patterns (HookConsumerWidget, AsyncValue stale cache, RetryWidget, error_snackbar)
  - phase: 20-flutter-foundation
    provides: go_router /chart route wired, AppTheme colors, dioProvider, api_client pattern
provides:
  - fl_chart ^1.1.1 dependency added to pubspec.yaml
  - ChartResponse, PricePointDto, PurchaseMarkerDto Dart models with manual fromJson
  - ChartRepository.fetchChart(timeframe) calling /api/dashboard/chart?timeframe=X
  - chartRepositoryProvider and chartDataProvider(@riverpod) parameterized by timeframe string
  - TimeframeSelector widget with 6 ChoiceChip buttons (7D/1M/3M/6M/1Y/All)
  - PriceLineChart fl_chart widget with price line, purchase markers, avg cost dashed line, touch tooltip
  - ChartScreen assembling timeframe selector + chart with stale cache pattern and pull-to-refresh
affects: [22-02-purchase-history, future-chart-screens]

# Tech tracking
tech-stack:
  added: [fl_chart ^1.1.1]
  patterns:
    - Two-LineChartBarData pattern for purchase markers (second transparent bar with purchaseSpots at actual purchase prices)
    - chartDataProvider family parameterized by timeframe string
    - HookConsumerWidget with useState for local UI state (selected timeframe)
    - Stale cache pattern with cachedValue = chartData.value extracted before switch

key-files:
  created:
    - TradingBot.Mobile/lib/features/chart/data/models/chart_response.dart
    - TradingBot.Mobile/lib/features/chart/data/chart_repository.dart
    - TradingBot.Mobile/lib/features/chart/data/chart_providers.dart
    - TradingBot.Mobile/lib/features/chart/data/chart_providers.g.dart
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/timeframe_selector.dart
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart
  modified:
    - TradingBot.Mobile/pubspec.yaml (added fl_chart ^1.1.1)
    - TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart (replaced placeholder)

key-decisions:
  - "purchaseSpots uses actual purchase prices (not close prices) for second LineChartBarData — confirmed by research pitfall #1"
  - "Two-LineChartBarData approach confirmed valid (no fallback needed): purchase markers use FlDotData with checkToShowDot returning true for indices matching purchaseDayIndexSet"
  - "chartDataProvider family takes String timeframe argument (not an enum) to match backend query param string directly"

patterns-established:
  - "Two-LineChartBarData: main price line (first) + transparent purchase marker line (second) using purchaseSpots with FlDotData getDotPainter for tier-colored FlDotCirclePainter"
  - "x-axis uses day index (0,1,2...) not timestamps; dateLabels list maps index back to 'yyyy-MM-dd' for tooltip and axis labels"
  - "Tier color switch: '2x'->amber, '3x'->orange, '4x'->lossRed, _->bitcoinOrange (Base)"

requirements-completed: [CHART-01, CHART-02, CHART-03, CHART-04]

# Metrics
duration: 4min
completed: 2026-02-20
---

# Phase 22 Plan 01: Price Chart Data Layer + UI Summary

**fl_chart price chart screen with orange BTC line, tier-colored purchase dot markers at actual purchase prices, dashed average cost basis line, touch tooltip, and 6 timeframe selector**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-02-20T07:56:15Z
- **Completed:** 2026-02-20T08:00:00Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- Added fl_chart ^1.1.1 and created full chart data layer (ChartResponse models, ChartRepository, chartDataProvider family)
- Implemented PriceLineChart with two-LineChartBarData pattern: main price line with gradient fill + transparent purchase marker line rendering tier-colored FlDotCirclePainter dots at actual purchase prices
- Assembled ChartScreen with HookConsumerWidget (useState timeframe), TimeframeSelector (6 ChoiceChips), stale cache pattern, pull-to-refresh, and snackbar on error

## Task Commits

Each task was committed atomically:

1. **Task 1: Chart data layer (models, repository, providers) and fl_chart dependency** - `5c5e4d3` (feat)
2. **Task 2: Chart UI (timeframe selector, fl_chart line chart with markers, tooltip, assembled ChartScreen)** - `408daa9` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.Mobile/pubspec.yaml` - Added fl_chart ^1.1.1
- `TradingBot.Mobile/lib/features/chart/data/models/chart_response.dart` - ChartResponse, PricePointDto, PurchaseMarkerDto with manual fromJson
- `TradingBot.Mobile/lib/features/chart/data/chart_repository.dart` - ChartRepository.fetchChart(timeframe)
- `TradingBot.Mobile/lib/features/chart/data/chart_providers.dart` - chartRepositoryProvider + chartDataProvider family
- `TradingBot.Mobile/lib/features/chart/data/chart_providers.g.dart` - Generated Riverpod code
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/timeframe_selector.dart` - 6-button ChoiceChip row
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart` - fl_chart LineChart with markers, avg cost line, tooltip
- `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` - Assembled screen with stale cache pattern

## Decisions Made

- purchaseSpots uses actual purchase prices (not close prices) for the second LineChartBarData — this ensures markers appear at the correct y-position (the price paid) rather than the daily close price
- Two-LineChartBarData approach confirmed valid without fallback: the research risk from STATE.md ("scatter markers may need fallback") was resolved by using checkToShowDot + purchaseDayIndexSet
- chartDataProvider takes a String timeframe (not enum) matching the backend query param string directly, simplifying the provider call

## Deviations from Plan

None - plan executed exactly as written. The purchase marker approach from the research doc was followed: second LineChartBarData uses purchaseSpots (actual purchase prices) rather than priceSpots, with checkToShowDot delegating to purchaseDayIndexSet.

## Issues Encountered

None. Build runner, dart analyze, and iOS build all passed on first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ChartScreen is fully functional and wired to the /chart route in go_router
- chartDataProvider pattern established for use in any future chart-related features
- Ready for Phase 22 Plan 02: Purchase History screen with infinite scroll and filter bottom sheet

---
*Phase: 22-price-chart-purchase-history*
*Completed: 2026-02-20*

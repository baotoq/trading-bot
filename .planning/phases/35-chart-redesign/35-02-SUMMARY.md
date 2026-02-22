---
phase: 35-chart-redesign
plan: 02
subsystem: mobile-chart
tags: [flutter, fl_chart, glass-tooltip, backdrop-filter, touch-handling, hooks]
dependency_graph:
  requires:
    - phase: 35-01
      provides: PriceLineChart HookConsumerWidget with draw-in animation and GlowDotPainter
  provides:
    - GlassChartTooltip frosted glass overlay widget
    - PriceLineChart with custom touch tracking and dashed indicator line
    - ChartScreen simplified layout with transparent AppBar
  affects: [chart_screen.dart, price_line_chart.dart]
tech_stack:
  added: []
  patterns: [Stack-overlay-for-BackdropFilter-tooltip, handleBuiltInTouches-true-with-null-getTooltipItems, useState-for-touch-spot-tracking]
key_files:
  created:
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/glass_chart_tooltip.dart
  modified:
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart
    - TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart
key-decisions:
  - "handleBuiltInTouches: true with getTooltipItems returning null -- preserves vertical indicator line while suppressing built-in tooltip rectangle (Pitfall 4 solution from 35-RESEARCH.md)"
  - "Tooltip anchored at top:0 centered horizontally -- avoids complex pixel-coordinate calculation for LineBarSpot.x (Open Question #1 in research); reliable above chart content"
  - "SingleChildScrollView at top level with AlwaysScrollableScrollPhysics -- replaces Expanded+inner-scroll nesting; PriceLineChart with AspectRatio has intrinsic height so no Expanded needed"
  - "Transparent AppBar backgroundColor -- allows AmbientBackground orbs to show through chart screen app bar area"
patterns-established:
  - "Glass tooltip overlay: Stack(clipBehavior.none) + Positioned(top:0, left:16, right:16) + Center(GlassChartTooltip) -- reliable above-chart positioning without pixel math"
  - "Touch suppression pattern: handleBuiltInTouches:true + getTooltipItems:(_)=>null + touchCallback for state -- keeps indicator line, removes tooltip box, enables custom overlay"
requirements-completed: [CHART-04, SCRN-02]
duration: 2min
completed: 2026-02-22
---

# Phase 35 Plan 02: Frosted Glass Chart Tooltip and Premium ChartScreen Layout Summary

GlassChartTooltip BackdropFilter overlay on fl_chart touch via handleBuiltInTouches:true + null getTooltipItems + useState<LineBarSpot> tracking, with ChartScreen simplified to RefreshIndicator > SingleChildScrollView > Column and transparent AppBar.

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-22T15:03:48Z
- **Completed:** 2026-02-22T15:05:48Z
- **Tasks:** 2
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- Created `GlassChartTooltip` widget using `GlassCard` (stationary, full BackdropFilter blur) with date and locale-formatted price
- Updated `PriceLineChart` with `useState<LineBarSpot?>` touch tracking, custom `touchCallback`, dashed indicator line via `getTouchedSpotIndicator`, and `Stack` overlay positioning
- Simplified `ChartScreen` layout to `RefreshIndicator > SingleChildScrollView > Column` eliminating the `Expanded + inner SingleChildScrollView` nesting, with transparent `AppBar`

## Task Commits

Each task was committed atomically:

1. **Task 1: Create GlassChartTooltip and add custom touch handling to PriceLineChart** - `509f790` (feat)
2. **Task 2: Update ChartScreen layout for premium chart display** - `42d3823` (feat)

**Plan metadata:** (docs commit to follow)

## Files Created/Modified
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/glass_chart_tooltip.dart` - New `GlassChartTooltip` StatelessWidget rendering `GlassCard(stationary)` with date + price labels
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart` - Added `useState<LineBarSpot?>`, `touchCallback`, `getTouchedSpotIndicator`, `Stack` overlay with `_buildTooltipOverlay`
- `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` - Simplified layout: transparent AppBar, `RefreshIndicator > SingleChildScrollView(AlwaysScrollable) > Column > Padding > PriceLineChart`

## Decisions Made
- `handleBuiltInTouches: true` kept with `getTooltipItems: (_) => null` returns: This is the Pitfall 4 solution from 35-RESEARCH.md. Setting `handleBuiltInTouches: false` would remove the dashed vertical indicator line. The null returns suppress the tooltip rectangle while the vertical line (from the built-in touch rendering) remains visible.
- Tooltip anchored at `top: 0, left: 16, right: 16, Center(...)`: Avoids complex pixel-coordinate math for converting fl_chart's data-coordinate x-value to canvas pixel position (Open Question #1 in 35-RESEARCH.md). The tooltip appears centered above the chart which is clean and unambiguous.
- `SingleChildScrollView(AlwaysScrollableScrollPhysics)` at the top level: `PriceLineChart` uses `AspectRatio(1.6)` so it has intrinsic height -- no `Expanded` needed. The simplified flat layout (no `Expanded > SingleChildScrollView` nesting) also means the `Stack` inside `PriceLineChart` has a stable non-scrolling parent, preventing tooltip clipping (Pitfall 3 from research).
- `AppBar(backgroundColor: Colors.transparent, elevation: 0)`: Per project decision logged in STATE.md, screen Scaffolds must not set solid backgroundColor or AppBar backgrounds that paint over AmbientBackground orbs.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. `flutter analyze` passed with zero errors after both tasks.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 35 complete: all four chart requirements (CHART-01 through CHART-04) and SCRN-02 delivered across two plans
- Chart now has: enhanced gradient fill, draw-in animation, glow purchase dots, frosted glass tooltip
- Ready for Phase 36

## Self-Check: PASSED

### Created files exist:
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/glass_chart_tooltip.dart` -- FOUND
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart` -- FOUND
- `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` -- FOUND
- `.planning/phases/35-chart-redesign/35-02-SUMMARY.md` -- FOUND

### Commits exist:
- 509f790 -- FOUND
- 42d3823 -- FOUND

---
*Phase: 35-chart-redesign*
*Completed: 2026-02-22*

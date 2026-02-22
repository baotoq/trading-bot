---
phase: 35-chart-redesign
plan: 01
subsystem: mobile-chart
tags: [flutter, animation, chart, glow, gradient, hooks]
dependency_graph:
  requires: []
  provides: [GlowDotPainter, PriceLineChart-draw-in-animation]
  affects: [chart_screen.dart]
tech_stack:
  added: []
  patterns: [HookConsumerWidget, useAnimationController, useRef, FlDotPainter-subclass, RadialGradient-custom-painter]
key_files:
  created:
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/glow_dot_painter.dart
  modified:
    - TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart
decisions:
  - "GlowDotPainter implements lerp and props (EquatableMixin) -- required abstract members of FlDotPainter not documented in plan; auto-fixed per Rule 1"
  - "useRef(false) as hasAnimated guard -- prevents draw-in animation replay on tab revisit; fires only once per session"
  - "controller.isCompleted used for dot show guard -- ensures dots only appear when the full line is drawn"
metrics:
  duration: 3min
  completed: 2026-02-22
  tasks_completed: 2
  files_created: 1
  files_modified: 1
---

# Phase 35 Plan 01: Chart Visual Upgrade - GlowDotPainter and Draw-in Animation Summary

GlowDotPainter custom FlDotPainter with radial gradient glow halo plus PriceLineChart HookConsumerWidget with left-to-right 1s draw-in animation, enhanced 3-stop orange gradient fill, and gated Reduce Motion support.

## Objective

Deliver core chart visual upgrades (CHART-01, CHART-02, CHART-03): enhanced gradient fill, draw-in animation, and purchase dot glow halos that transform the price chart from basic fl_chart rendering into a premium visualization.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create GlowDotPainter and upgrade PriceLineChart | f6b98e4 | glow_dot_painter.dart (new), price_line_chart.dart (modified) |
| 2 | Update ChartScreen integration verification | f6b98e4 | No changes needed -- ChartScreen required zero modifications |

## What Was Built

### GlowDotPainter (`glow_dot_painter.dart`)

A new `FlDotPainter` subclass rendering two-layer purchase marker dots:

1. **Outer glow halo** -- `RadialGradient` at `radius * 2.5` from `effectiveGlowColor.withAlpha(153)` to transparent. Creates a warm orange halo around each purchase dot.
2. **Inner solid circle** -- Filled with `color` plus `strokeColor` stroke at 1.5 width.

Implements all required abstract members:
- `draw()` -- two-layer Canvas painting
- `mainColor` -- returns `color`
- `getSize()` -- returns `Size(radius * 5, radius * 5)` (full halo extent)
- `hitTest()` -- distance-based within `radius + extraThreshold`
- `lerp()` -- interpolates between two `GlowDotPainter` instances
- `props` -- `[color, radius, glowColor, strokeColor]` for equality

### PriceLineChart upgrade (`price_line_chart.dart`)

Changed from `StatelessWidget` to `HookConsumerWidget`:

- **Draw-in animation (CHART-02):** `useAnimationController(1000ms)` + `CurvedAnimation(easeInOut)` drives `priceSpots.sublist(0, visibleCount)` slicing. `useRef<bool>(false)` as `hasAnimated` guard fires animation only on first mount, not on tab revisits.
- **Reduce Motion gate:** `GlassCard.shouldReduceMotion(context)` checked before `controller.forward()`; sets `controller.value = 1.0` immediately when motion is disabled.
- **Enhanced gradient (CHART-01):** 3-stop `LinearGradient` with `withAlpha(77)` / `withAlpha(26)` / `withAlpha(0)` (was 2-stop with `withAlpha(51)` / transparent). Middle stop at 0.5 sustains glow persistence.
- **Glow dot painter (CHART-03):** `FlDotCirclePainter` replaced with `GlowDotPainter(radius: 5, color: tierColor, glowColor: AppTheme.bitcoinOrange)`.
- **Dot animation gate:** `FlDotData(show: purchaseSpots.isNotEmpty && controller.isCompleted)` -- dots hidden during draw-in, appear only when animation completes.
- **Clip + maxX:** `clipData: const FlClipData.all()` and `maxX: visiblePriceSpots.last.x` constrain the chart during animation to prevent overdraw.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Missing abstract member implementations in GlowDotPainter**
- **Found during:** Task 1 verification (first `flutter analyze` run)
- **Issue:** `FlDotPainter` mixes in `EquatableMixin` which requires `List<Object?> get props`, and also declares abstract `FlDotPainter lerp(FlDotPainter a, FlDotPainter b, double t)`. Plan did not mention these required members.
- **Fix:** Added `lerp()` interpolating between two `GlowDotPainter` instances using `Color.lerp` and `lerpDouble`, and `props` returning `[color, radius, glowColor, strokeColor]`.
- **Files modified:** `glow_dot_painter.dart`
- **Commit:** f6b98e4

## Self-Check

### Created files exist:
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/glow_dot_painter.dart` -- FOUND
- `TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart` -- FOUND

### Commits exist:
- f6b98e4 -- FOUND

## Self-Check: PASSED

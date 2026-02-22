# Phase 35: Chart Redesign - Research

**Researched:** 2026-02-22
**Domain:** Flutter fl_chart customization — gradient fill, draw-in animation, custom dot painters, frosted glass tooltip
**Confidence:** HIGH

## Summary

Phase 35 upgrades the existing `PriceLineChart` widget (already using `fl_chart ^1.1.1`) into a premium visualization. All four chart requirements can be satisfied within the existing fl_chart library — no new chart package is needed. The gradient fill area (CHART-01) already exists in the codebase and needs visual refinement. The left-to-right draw-in animation (CHART-02) is achieved by slicing `spots.sublist` driven by an `AnimationController` in a `HookConsumerWidget`. The glow marker dots (CHART-03) require a custom `FlDotPainter` subclass that draws a radial-gradient halo via `Canvas`. The frosted glass tooltip (CHART-04) is the highest-effort item: fl_chart's `LineTouchTooltipData.getTooltipColor` only returns a `Color` (not a widget), so a true `BackdropFilter` tooltip requires the custom-overlay pattern — disable built-in touches, use `touchCallback` + `LineTouchResponse` to track the touched spot in state, and `Stack` an absolutely-positioned `GlassCard` on top of the chart.

All accessibility guards from Phase 33/34 apply here: `GlassCard.shouldReduceMotion(context)` must gate the draw-in animation, and the `_hasAnimated` guard pattern from STATE.md must be applied so the draw-in fires only on the initial tab entry per session.

**Primary recommendation:** Upgrade `PriceLineChart` to `HookConsumerWidget`, add `useAnimationController` for draw-in, implement `GlowDotPainter extends FlDotPainter` for markers, and wrap with a `Stack` for the custom glass tooltip overlay. No new pub packages required.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CHART-01 | Price chart displays a gradient glow fill area beneath the line with orange-to-transparent gradient | `BarAreaData(gradient: LinearGradient(...))` already exists in codebase; needs opacity/visual tuning to appear "glowing". Existing implementation uses `withAlpha(51)` — research confirms `BarAreaData` supports full `LinearGradient` including multi-stop. |
| CHART-02 | Price chart animates left-to-right draw-in on initial tab entry | `spots.sublist(0, visibleCount)` driven by `AnimationController` (1000ms, `Curves.easeInOut`). `clipData: FlClipData.all()` prevents overdraw. `_hasAnimated` bool guard ensures draw-in fires only on first tab entry. Requires converting `PriceLineChart` to `HookConsumerWidget`. |
| CHART-03 | Purchase marker dots display an orange radial glow effect | `FlDotPainter` is an abstract class — implement `GlowDotPainter` that calls `canvas.drawCircle` twice: once with `RadialGradient` shader (halo) at 2.5× radius, once with solid orange fill at 1× radius. `RadialGradient.createShader(rect)` pattern confirmed in Flutter official docs. |
| CHART-04 | Chart tooltip uses a frosted glass style with rounded corners and formatted numbers | fl_chart `getTooltipColor` only returns `Color`, not a widget. True `BackdropFilter` tooltip requires custom overlay: set `handleBuiltInTouches: false`, use `touchCallback` to update a state variable holding the touched `LineBarSpot`, then `Stack` a `GlassCard`-wrapped `Column` positioned above the touch point. |
| SCRN-02 | Chart screen displays gradient glow chart with premium glass tooltip and animated draw-in | Composite of CHART-01 through CHART-04. Chart screen scaffold already uses `HookConsumerWidget`. Requires removing `AspectRatio` constraint and expanding chart to use `Expanded` layout so the glass tooltip overlay can be positioned correctly within a `Stack`. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| fl_chart | ^1.1.1 (already installed) | Line chart rendering, dot painters, touch handling | Already the project's chart library; all required features exist within it |
| flutter_hooks | any (already installed) | `useAnimationController` for draw-in animation | Project pattern — all animation controllers via hooks in `HookConsumerWidget` |
| hooks_riverpod | ^3.2.1 (already installed) | State management for chart data | Project standard |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| dart:ui | built-in | `RadialGradient.createShader()`, `ImageFilter.blur` | Canvas glow painter and glass tooltip blur |
| intl | any (already installed) | Locale-formatted price numbers in tooltip | Already used in `PriceLineChart` for `NumberFormat.currency` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom overlay tooltip | Keep built-in fl_chart tooltip | Built-in only accepts `Color` background, cannot host `BackdropFilter` widget tree — glass effect impossible |
| spots.sublist draw-in | `LineChartDataTween` lerp animation | Lerp morphs between two full datasets; sublist approach gives true sequential reveal from left edge |
| `GlowDotPainter` custom painter | `FlDotCirclePainter` with `Shadow` | `Shadow` on dots is flat drop-shadow; radial gradient halo requires custom painter for correct glow spread |

**Installation:** No new packages required. All dependencies already in `pubspec.yaml`.

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.Mobile/lib/features/chart/
├── presentation/
│   ├── chart_screen.dart          # HookConsumerWidget — draw-in guard, data fetch
│   └── widgets/
│       ├── price_line_chart.dart  # Core chart — upgrade to HookConsumerWidget
│       ├── glow_dot_painter.dart  # NEW: FlDotPainter subclass for glow markers
│       ├── glass_chart_tooltip.dart  # NEW: GlassCard-based overlay tooltip widget
│       └── timeframe_selector.dart   # Unchanged
```

### Pattern 1: Left-to-Right Draw-In via spots.sublist

**What:** Drive `visibleCount = (animValue * spots.length).round()` and pass `spots.sublist(0, max(1, visibleCount))` to `LineChartBarData`. Add `clipData: FlClipData.all()` to prevent the chart from drawing beyond the visible x-range. Use `_hasAnimated` bool to ensure the animation only plays on first tab entry.

**When to use:** Any time a line chart should animate progressively from left to right on initial view.

**Example:**
```dart
// Source: fl_chart official docs (minX/maxX + clipData approach confirmed)
// Pattern: useAnimationController in HookConsumerWidget per STATE.md decision

class PriceLineChart extends HookConsumerWidget {
  const PriceLineChart({required this.data, required this.timeframe, super.key});

  final ChartResponse data;
  final String timeframe;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final hasAnimated = useRef(false);
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 1000),
    );
    final animation = useAnimation(
      CurvedAnimation(parent: controller, curve: Curves.easeInOut),
    );

    useEffect(() {
      if (!hasAnimated.value && !GlassCard.shouldReduceMotion(context)) {
        hasAnimated.value = true;
        controller.forward();
      } else {
        controller.value = 1.0; // skip animation if reduce motion or revisit
      }
      return null;
    }, [data]);

    final totalSpots = data.prices.length;
    final visibleCount = GlassCard.shouldReduceMotion(context)
        ? totalSpots
        : max(1, (animation * totalSpots).round());

    final priceSpots = data.prices
        .asMap()
        .entries
        .take(visibleCount)
        .map((e) => FlSpot(e.key.toDouble(), e.value.price))
        .toList();

    return LineChart(
      LineChartData(
        clipData: const FlClipData.all(), // prevents drawing beyond visible region
        lineBarsData: [
          LineChartBarData(
            spots: priceSpots,
            // ... other properties
          ),
        ],
      ),
    );
  }
}
```

### Pattern 2: Custom GlowDotPainter

**What:** Implement `FlDotPainter` abstract interface to draw a two-layer dot: outer radial-gradient halo (2.5× radius, orange-to-transparent) and inner solid circle (tier color).

**When to use:** Whenever purchase markers need a glow halo effect distinct from the dot itself.

**Example:**
```dart
// Source: FlDotPainter abstract class — pub.dev/documentation/fl_chart
// Source: RadialGradient.createShader — api.flutter.dev/flutter/painting/RadialGradient-class

class GlowDotPainter extends FlDotPainter {
  const GlowDotPainter({
    required this.color,
    this.radius = 5.0,
    this.glowColor,
    this.strokeColor = Colors.white,
  });

  final Color color;
  final Color? glowColor;
  final double radius;
  final Color strokeColor;

  @override
  Color get mainColor => color;

  @override
  void draw(Canvas canvas, FlSpot spot, Offset offsetInCanvas) {
    final effectiveGlowColor = glowColor ?? color;
    final haloRadius = radius * 2.5;

    // Outer glow halo — radial gradient orange → transparent
    final glowPaint = Paint()
      ..shader = RadialGradient(
        colors: [
          effectiveGlowColor.withAlpha(153), // ~0.6 opacity at center
          effectiveGlowColor.withAlpha(0),   // transparent at edge
        ],
        stops: const [0.0, 1.0],
      ).createShader(
        Rect.fromCircle(center: offsetInCanvas, radius: haloRadius),
      );
    canvas.drawCircle(offsetInCanvas, haloRadius, glowPaint);

    // Inner solid dot — fill
    final dotPaint = Paint()..color = color;
    canvas.drawCircle(offsetInCanvas, radius, dotPaint);

    // Stroke border
    final strokePaint = Paint()
      ..color = strokeColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;
    canvas.drawCircle(offsetInCanvas, radius, strokePaint);
  }

  @override
  Size getSize(FlSpot spot) => Size(radius * 2.5 * 2, radius * 2.5 * 2);

  @override
  bool hitTest(FlSpot spot, Offset touched, Offset center, double extraThreshold) {
    return (touched - center).distance <= radius + extraThreshold;
  }
}
```

### Pattern 3: Frosted Glass Tooltip Overlay

**What:** Disable fl_chart's built-in tooltip (`handleBuiltInTouches: false`), use `touchCallback` to track the currently touched `LineBarSpot` in a `useState` variable, then overlay a `GlassCard` positioned via `Stack` + `Positioned` above the touch coordinate.

**When to use:** Any time a chart tooltip must render an arbitrary Flutter widget tree (e.g., BackdropFilter glass card) instead of a flat colored rectangle.

**Example:**
```dart
// Source: LineTouchData constructor — pub.dev/documentation/fl_chart
// Source: GlassCard — project Phase 33 design system

// In PriceLineChart build():
final touchedSpot = useState<LineBarSpot?>(null);

// In LineChartData:
lineTouchData: LineTouchData(
  handleBuiltInTouches: false,
  touchCallback: (FlTouchEvent event, LineTouchResponse? response) {
    if (event is FlTapUpEvent || event is FlPanEndEvent || event is FlLongPressEnd) {
      touchedSpot.value = null;
      return;
    }
    if (response?.lineBarSpots != null && response!.lineBarSpots!.isNotEmpty) {
      // Only show tooltip for price line (bar index 0)
      final spot = response.lineBarSpots!
          .firstWhereOrNull((s) => s.barIndex == 0);
      touchedSpot.value = spot;
    }
  },
),

// Wrap the LineChart in a Stack:
Stack(
  children: [
    LineChart(LineChartData(...)),
    if (touchedSpot.value != null)
      _buildGlassTooltip(context, touchedSpot.value!, dateLabels),
  ],
)

// Tooltip widget:
Widget _buildGlassTooltip(BuildContext context, LineBarSpot spot, List<String> labels) {
  final xIndex = spot.x.toInt();
  final dateStr = xIndex < labels.length ? labels[xIndex] : '';
  final priceFormat = NumberFormat.currency(symbol: '\$', decimalDigits: 0);
  return Positioned(
    // Position above the touched x coordinate — calculate from chart dimensions
    left: /* computed offset */ 0,
    top: 8,
    child: GlassCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      borderRadius: 12,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(dateStr, style: const TextStyle(color: Colors.white70, fontSize: 11)),
          Text(
            priceFormat.format(spot.y),
            style: AppTheme.moneyStyle.copyWith(
              color: Colors.white,
              fontSize: 14,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    ),
  );
}
```

### Anti-Patterns to Avoid

- **Animating on every tab revisit:** The `_hasAnimated` guard (via `useRef`) must prevent re-triggering the draw-in each time the user switches back to the Chart tab. STATE.md explicitly documents this as the project pattern.
- **Using `withOpacity` for gradient colors:** Project decision mandates `withAlpha(int)` over `withOpacity(float)`. Apply to all `Color` values in `GlowDotPainter` and gradient stops.
- **Placing BackdropFilter inside LineChart:** fl_chart renders through its own `CustomPainter`; Flutter widget APIs including `BackdropFilter` cannot be injected into the chart canvas. The glass tooltip must be overlaid externally via `Stack`.
- **Animating purchase spots during draw-in:** The purchase spots line (bar index 1) should not be sliced — only the main price line is animated. Show purchase dots only when `controller.isCompleted` to avoid markers appearing at wrong positions mid-animation.
- **Forgetting `clipData: FlClipData.all()`:** Without this, fl_chart draws the full line outside the chart bounds even when `spots` is a sublist, because the axis range (minX/maxX) may not match the visible range. Set `maxX` to match `priceSpots.last.x` in addition to clipData.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Radial gradient halo | Custom radial math | `RadialGradient.createShader(rect)` | Built-in Flutter API handles all gradient interpolation |
| Chart animation tween | Custom interpolation | `spots.sublist` + `AnimationController` | Simpler and correct; fl_chart handles line rendering from whatever spots list it receives |
| Tooltip positioning math | Pixel-perfect calculation from scratch | `Stack` + `Positioned` with `fitInsideHorizontally: true` pattern | For exact pixel coordinates, read from `LineTouchResponse.lineBarSpots[0].offset` if exposed, otherwise use a fixed top offset with horizontal clamping |
| Glass tooltip from scratch | Custom `ClipRRect` + `BackdropFilter` | `GlassCard` (stationary variant) | GlassCard already implements the blur+tint+border pattern with accessibility fallbacks |

**Key insight:** fl_chart's `FlDotPainter` abstraction is the correct extension point for glow markers. Never paint custom chart elements on a `CustomPainter` layered over the chart widget — hit-testing breaks.

## Common Pitfalls

### Pitfall 1: draw-in animation re-fires on tab revisit
**What goes wrong:** User switches tabs and back; the chart draws in again from left edge, breaking the premium feel.
**Why it happens:** `useEffect` dependency on `data` triggers whenever the widget rebuilds with the same data object reference.
**How to avoid:** Use `useRef<bool>(false)` as `_hasAnimated` guard. Only call `controller.forward()` when `!hasAnimated.value`. On subsequent builds, set `controller.value = 1.0` to show the full chart immediately.
**Warning signs:** Animation fires on every tab switch.

### Pitfall 2: Purchase dots appear at wrong positions during draw-in
**What goes wrong:** Purchase marker dots display at x positions that are past the animated visible line, floating in empty space.
**Why it happens:** The purchase dots are on a separate invisible `LineChartBarData` (bar index 1). If that line uses the full `purchaseSpots` list while the price line is still animating, dots appear beyond the visible portion.
**How to avoid:** Filter `purchaseSpots` to only include entries where `spot.x <= visibleCount - 1`, or hide purchase dots entirely until `controller.isCompleted`.
**Warning signs:** Orange dots visible to the right of the animated line endpoint.

### Pitfall 3: Glass tooltip clips behind the chart widget
**What goes wrong:** The GlassCard tooltip is clipped or invisible because it's inside an `AspectRatio` + `Padding` container that clips overflow.
**Why it happens:** `AspectRatio` does not pass overflow to parent; tooltip positioned at `top: 0` in a child `Stack` may be clipped.
**How to avoid:** Move the `Stack` to wrap the entire chart container (outside `AspectRatio`), or use `OverflowBox` + `Positioned`. The simplest fix: place the `Stack` at the `Expanded` level in `ChartScreen`, above the `PriceLineChart` widget.
**Warning signs:** Tooltip invisible or partially visible at chart edges.

### Pitfall 4: `handleBuiltInTouches: false` removes the touch indicator line
**What goes wrong:** When disabling built-in touches, the vertical indicator line (crosshair) on the chart also disappears because it's part of the built-in touch rendering.
**Why it happens:** `handleBuiltInTouches: false` disables the full built-in touch response, including the indicator line.
**How to avoid:** Use `showingTooltipIndicators` + `LineChartData.lineTouchData.getTouchedSpotIndicator` to manually render the indicator line when a spot is touched. Alternatively, keep `handleBuiltInTouches: true` and accept that the built-in tooltip (styled via `getTooltipColor`) appears alongside the custom overlay — but hide it via `getTooltipItems: (_) => []`.
**Warning signs:** No visual feedback on chart touch after disabling built-in touches.

### Pitfall 5: `withOpacity` used instead of `withAlpha`
**What goes wrong:** Code review catches `withOpacity(float)` calls in glow painter or gradient definitions.
**Why it happens:** Developer habit from pre-project-decision code.
**How to avoid:** All Color alpha values must use `withAlpha(int)` per project decision logged in STATE.md. Convert: `withOpacity(0.6)` → `withAlpha(153)` (0.6 * 255 = 153).
**Warning signs:** Any `withOpacity` call in new Phase 35 code.

## Code Examples

Verified patterns from official sources:

### FlDotPainter Abstract Interface (fl_chart)
```dart
// Source: https://pub.dev/documentation/fl_chart/latest/fl_chart/FlDotPainter-class
abstract class FlDotPainter {
  Color get mainColor;
  void draw(Canvas canvas, FlSpot spot, Offset offsetInCanvas);
  Size getSize(FlSpot spot);
  bool hitTest(FlSpot spot, Offset touched, Offset center, double extraThreshold);
}
```

### BarAreaData Gradient Fill (fl_chart)
```dart
// Source: https://pub.dev/documentation/fl_chart/latest/fl_chart/BarAreaData-class
belowBarData: BarAreaData(
  show: true,
  gradient: LinearGradient(
    colors: [
      AppTheme.bitcoinOrange.withAlpha(77),   // ~0.30 at top for "glow" feel
      AppTheme.bitcoinOrange.withAlpha(0),    // transparent at bottom
    ],
    stops: const [0.0, 1.0],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  ),
),
```

### RadialGradient Shader for Canvas Glow (Flutter)
```dart
// Source: https://api.flutter.dev/flutter/painting/RadialGradient-class.html
final Paint glowPaint = Paint()
  ..shader = RadialGradient(
    colors: [
      color.withAlpha(153),  // 0.6 opacity at center
      color.withAlpha(0),    // transparent at edge
    ],
    stops: const [0.0, 1.0],
  ).createShader(Rect.fromCircle(center: center, radius: haloRadius));
canvas.drawCircle(center, haloRadius, glowPaint);
```

### LineTouchData Disable Built-in (fl_chart)
```dart
// Source: https://pub.dev/documentation/fl_chart/latest/fl_chart/LineTouchData/LineTouchData
lineTouchData: LineTouchData(
  handleBuiltInTouches: false,
  touchCallback: (FlTouchEvent event, LineTouchResponse? response) {
    // Update state based on touch position
  },
),
```

### useAnimationController Hook Pattern (project convention)
```dart
// Source: STATE.md — "All AnimationControllers in HookConsumerWidget via useAnimationController"
final controller = useAnimationController(
  duration: const Duration(milliseconds: 1000),
);
final animation = useAnimation(
  CurvedAnimation(parent: controller, curve: Curves.easeInOut),
);
```

### GlassCard Stationary for Tooltip (project Phase 33)
```dart
// Source: lib/core/widgets/glass_card.dart — GlassVariant.stationary is default
GlassCard(
  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
  borderRadius: 12,
  child: Column(mainAxisSize: MainAxisSize.min, children: [...]),
)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `tooltipBgColor` parameter | `getTooltipColor` callback | fl_chart 0.67.0 | Migration required if upgrading from older versions — already on 1.1.1 so not relevant |
| `swapAnimationDuration` / `swapAnimationCurve` | `duration` / `curve` on `LineChart` constructor | fl_chart ~0.71.0 | Old parameter names deprecated; using `duration`/`curve` is current |
| `FlDotCirclePainter` (built-in) | Custom `FlDotPainter` subclass | Always available | Built-in painter doesn't support glow — custom subclass is the correct extension point |

**Deprecated/outdated:**
- `tooltipBgColor`: Removed; use `getTooltipColor: (_) => color` instead
- `swapAnimationDuration`: Deprecated; use `duration` parameter on `LineChart`
- `swapAnimationCurve`: Deprecated; use `curve` parameter on `LineChart`

## Open Questions

1. **Tooltip X-position calculation**
   - What we know: `LineTouchResponse.lineBarSpots[n]` gives `x` value (data coordinate). Converting to canvas pixel position requires chart geometry.
   - What's unclear: Whether fl_chart exposes the canvas-to-widget coordinate transform. The `offset` field on `LineBarSpot` is not documented in Context7 results.
   - Recommendation: Use a fixed `top: 8` for the tooltip and derive horizontal offset from `touchX / maxX * chartWidth` using `MediaQuery` or `LayoutBuilder` width. Alternatively, use the simpler approach of keeping the built-in tooltip styled via `getTooltipColor` + `tooltipBorderRadius` + `tooltipBorder` as a glass-adjacent approximation (opaque dark with border), and only implement the full custom overlay if the design review requires true blur.

2. **Purchase dot visibility during draw-in animation**
   - What we know: Purchase dots are on a separate `LineChartBarData` (bar index 1) using a separate `purchaseSpots` list.
   - What's unclear: Whether filtering `purchaseSpots` by `spot.x <= visibleCount - 1` is sufficient, or if it creates visible list-length-change artifacts.
   - Recommendation: Simplest approach is to hide the purchase dot line entirely (`show: false` in `FlDotData`) until `controller.isCompleted`, then switch to showing dots. This avoids mid-animation filtering complexity.

3. **Tooltip BackdropFilter performance**
   - What we know: The tooltip appears and disappears on touch, so BackdropFilter is not in a scroll context.
   - What's unclear: Whether a `BackdropFilter` inside a `Stack` overlaid on the chart causes perf issues on mid-range devices.
   - Recommendation: Proceed with `GlassCard.stationary` (BackdropFilter) since the tooltip is brief and stationary. If physical device testing shows jank, fall back to `GlassCard.scrollItem` (no blur, opaque tint).

## Sources

### Primary (HIGH confidence)
- `/websites/pub_dev_fl_chart` (Context7) — `FlDotPainter` interface, `BarAreaData`, `LineTouchData`, `LineTouchTooltipData`, `LineChart` constructor, `FlDotData`
- `https://pub.dev/documentation/fl_chart/latest/fl_chart/` — All fl_chart API types confirmed
- `https://api.flutter.dev/flutter/painting/RadialGradient-class.html` — `RadialGradient.createShader` API and example
- `https://api.flutter.dev/flutter/rendering/CustomPainter-class.html` — `CustomPainter.paint` Canvas coordinate system
- Project source: `lib/core/widgets/glass_card.dart` — `GlassCard`, `GlassVariant`, `GlassTheme`
- Project source: `lib/features/chart/presentation/widgets/price_line_chart.dart` — existing implementation baseline
- Project source: `lib/core/widgets/ambient_background.dart` — `RadialGradient` usage pattern in project
- Project STATE.md decisions — `_hasAnimated` guard, `useAnimationController`, `withAlpha` requirement

### Secondary (MEDIUM confidence)
- `https://github.com/imaNNeo/fl_chart/blob/main/repo_files/documentations/handle_animations.md` — confirmed implicit animation via `duration`/`curve`, no built-in draw-in
- `https://github.com/imaNNeo/fl_chart/blob/main/repo_files/documentations/line_chart.md` — confirmed `clipData`, `minX`/`maxX` for boundary control

### Tertiary (LOW confidence)
- WebSearch ecosystem scan — general pattern consensus on `spots.sublist` approach for draw-in animations; no single authoritative source, but pattern appears across multiple Flutter community implementations

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — fl_chart already installed, all needed APIs confirmed in Context7
- Architecture patterns: HIGH — `FlDotPainter` interface confirmed, `useAnimationController` is project pattern, `GlassCard` exists
- Draw-in animation technique: MEDIUM — `spots.sublist` + `clipData` pattern verified via fl_chart minX/maxX docs; no official "draw-in" tutorial exists but the mechanism is sound
- Glass tooltip overlay: MEDIUM — `handleBuiltInTouches: false` + `touchCallback` confirmed in fl_chart API; pixel-coordinate conversion for tooltip positioning has an open question
- Pitfalls: HIGH — derived from codebase analysis and confirmed fl_chart API constraints

**Research date:** 2026-02-22
**Valid until:** 2026-03-22 (fl_chart 1.1.x is stable; no breaking changes expected in 30 days)

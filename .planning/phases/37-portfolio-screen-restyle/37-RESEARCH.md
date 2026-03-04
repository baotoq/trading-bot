# Phase 37: Portfolio Screen Restyle - Research

**Researched:** 2026-03-04
**Domain:** Flutter UI — donut chart ambient glow, slot-flip animation for currency toggle, glass card layout polish
**Confidence:** HIGH

## Summary

Phase 37 targets three focused improvements to the Portfolio screen, which already has a solid glassmorphism foundation from Phases 35.1 and 35.2. The existing `AllocationDonutChart` (`StatefulWidget`, fl_chart `PieChart`) and `PortfolioHeroHeader` (`GlassCard.stationary`) are the primary canvases. The hero header, sticky tab bar, filter chips, donut chart, and `GlassVariant.scrollItem` asset rows all exist. This phase adds: (1) a colored ambient glow halo around the donut chart matching the dominant asset color, (2) a slot-flip (vertical slot-machine) animation on the currency value labels when the VND/USD toggle fires, and (3) confirmation that asset rows use the correct non-blur glass style (already done — `GlassVariant.scrollItem` is already in place).

The ambient glow is a static `BoxShadow` or `Container` with `RadialGradient` placed behind the donut `PieChart` inside a `Stack`. The dominant color maps to the largest-percentage `AllocationDto.assetType` via the existing `_colorForType` switch. The slot-flip animation uses a single explicit `AnimationController` with a `SlideTransition` on a vertical axis — old value slides out upward, new value slides in from below. Currency-sensitive value labels that need this effect are the total value in `PortfolioHeroHeader` and the per-asset value column in `PortfolioAssetListItem`. The toggle itself lives in `CurrencyToggle` (Riverpod state); the animation is owned by the label widgets responding to `isVnd` changes.

The slot animation approach: wrap value Text in an `AnimatedSwitcher` with a custom `transitionBuilder` using `SlideTransition` (offset Y: -1 → 0 entering, 0 → 1 exiting). `AnimatedSwitcher` already handles the key-change → re-animate lifecycle; use the formatted value string as the key to trigger animation only when the displayed text actually changes.

**Primary recommendation:** Implement the donut glow as a `Stack`-layered `Container` with `RadialGradient` behind the `PieChart`. Implement the slot-flip as `AnimatedSwitcher` with `SlideTransition` transitionBuilder. Both require no new packages — only Flutter core and fl_chart (already present).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCRN-05 | Portfolio screen uses glass cards with animated allocation donut and per-asset glass rows | AllocationDonutChart already uses fl_chart PieChart; wrap with ambient glow Stack layer; per-asset rows already use GlassVariant.scrollItem — verify and confirm no regression |
| ANIM-06 | Currency toggle animates value labels with a slot-flip effect | AnimatedSwitcher with SlideTransition transitionBuilder on value Text widgets; triggered by isVnd state changes; applies in PortfolioHeroHeader total value and PortfolioAssetListItem trailing value |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| flutter (core) | SDK | AnimatedSwitcher, SlideTransition, Stack, BoxShadow, RadialGradient | All built-in Flutter widgets — no additional packages |
| fl_chart | ^1.1.1 | PieChart for AllocationDonutChart | Already in use — no upgrade required |
| flutter_hooks | any | useAnimationController for slot-flip if explicit controller needed | Project pattern for all AnimationControllers |
| hooks_riverpod | ^3.2.1 | currencyPreferenceProvider state access | Project state management |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| intl | any | Number formatting for slot-flip value display | Already used across all value widgets |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AnimatedSwitcher + SlideTransition | Explicit AnimationController + AnimatedBuilder with Tween<Offset> | AnimatedSwitcher is simpler — no manual controller, no dispose; handles key-based re-trigger automatically. Use AnimatedSwitcher. |
| RadialGradient Container behind PieChart | External glow package (e.g., glow_container) | No new packages needed; project already uses RadialGradient in AmbientBackground and GlowDotPainter — same pattern. Use native RadialGradient. |
| Stack-based glow behind PieChart | BoxDecoration boxShadow on wrapping container | BoxShadow only applies to the widget boundary, not the circular PieChart shape. RadialGradient Container is more flexible and precise for circular glow. Use RadialGradient. |

**Installation:** No new packages required — all dependencies already in pubspec.yaml.

---

## Architecture Patterns

### Files to Modify / Create

```
lib/features/portfolio/presentation/
├── widgets/
│   ├── allocation_donut_chart.dart   (MODIFY — add ambient glow Stack layer)
│   ├── portfolio_hero_header.dart    (MODIFY — wrap total value Text in SlotFlipValue)
│   ├── portfolio_asset_list_item.dart (MODIFY — wrap trailing value Text in SlotFlipValue)
│   └── slot_flip_value.dart          (NEW — reusable AnimatedSwitcher slot widget)
```

The `CurrencyToggle` widget itself does NOT change — it only fires `currencyPreferenceProvider.toggle()`. The animation is owned by the value display widgets that react to `isVnd` changes.

### Pattern 1: Ambient Glow Behind Donut Chart

**What:** A `Container` with `RadialGradient` (center color → transparent) placed behind the `PieChart` in a `Stack`. The glow color is derived from the dominant allocation asset type.
**When to use:** AllocationDonutChart only — the same RadialGradient orb technique used in `AmbientBackground`.

```dart
// Inside AllocationDonutChart._buildDonutStack():
// Determine dominant color (largest percentage allocation)
Color _dominantColor() {
  if (widget.allocations.isEmpty) return AppTheme.bitcoinOrange;
  final dominant = widget.allocations.reduce(
    (a, b) => a.percentage >= b.percentage ? a : b,
  );
  return _colorForType(dominant.assetType);
}

// Stack layout (within the 200px SizedBox):
Stack(
  alignment: Alignment.center,
  children: [
    // Layer 1: Ambient glow halo — sized to match donut outer radius
    Container(
      width: 180,
      height: 180,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [
            _dominantColor().withAlpha(51),   // ~0.20 opacity at center
            _dominantColor().withAlpha(0),     // transparent at edge
          ],
          stops: const [0.0, 1.0],
        ),
      ),
    ),
    // Layer 2: PieChart (existing code)
    PieChart(PieChartData(...)),
    // Layer 3: Center label (existing code)
    Column(...),
  ],
)
```

**Glow intensity:** `withAlpha(51)` (~0.20 opacity) at center. This is stronger than AmbientBackground orbs (28/26/20) but weaker than GlowDotPainter center (153) — appropriate for the donut's large circular area. Adjust if too prominent during implementation.

**Color mapping (existing `_colorForType`):**
- `'Crypto'` → `AppTheme.bitcoinOrange` (orange)
- `'ETF'` → `Color(0xFF42A5F5)` (blue)
- `'FixedDeposit'` → `AppTheme.profitGreen` (green)

### Pattern 2: Slot-Flip Value Widget (ANIM-06)

**What:** A reusable `SlotFlipValue` widget that wraps a formatted value String in an `AnimatedSwitcher` with a vertical `SlideTransition` transitionBuilder. When `value` changes (because `isVnd` toggled), the old text slides out upward and the new text slides in from below — simulating a mechanical slot display.
**When to use:** Any value label that shows currency-denominated text that flips on toggle: total value in `PortfolioHeroHeader`, holding value in `PortfolioAssetListItem` trailing column.

```dart
// NEW: lib/features/portfolio/presentation/widgets/slot_flip_value.dart

/// A value label that animates with a vertical slot-flip effect when
/// the displayed [value] string changes.
///
/// Uses [AnimatedSwitcher] with a [SlideTransition] transitionBuilder
/// to create a slot-machine reveal. The incoming child slides in from
/// below (offset Y: 1 → 0) while the outgoing child slides out upward
/// (offset Y: 0 → -1).
///
/// Respect reduce-motion: when [GlassCard.shouldReduceMotion] is true,
/// the switcher duration is set to zero so values update instantly.
class SlotFlipValue extends StatelessWidget {
  const SlotFlipValue({
    required this.value,
    required this.style,
    super.key,
  });

  final String value;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final reduceMotion = GlassCard.shouldReduceMotion(context);
    return AnimatedSwitcher(
      duration: reduceMotion
          ? Duration.zero
          : const Duration(milliseconds: 250),
      switchInCurve: Curves.easeOut,
      switchOutCurve: Curves.easeIn,
      transitionBuilder: (child, animation) {
        // Incoming child: slide in from bottom (+1Y → 0)
        // Outgoing child: slide out to top (0 → -1Y)
        // AnimatedSwitcher passes the same animation for both;
        // distinguish by checking if child is the current value.
        return SlideTransition(
          position: Tween<Offset>(
            begin: const Offset(0, 1),  // enter from below
            end: Offset.zero,
          ).animate(animation),
          child: FadeTransition(
            opacity: animation,
            child: child,
          ),
        );
      },
      child: Text(
        value,
        key: ValueKey(value),  // key change triggers re-animation
        style: style,
      ),
    );
  }
}
```

**Critical: `ValueKey(value)` is mandatory.** `AnimatedSwitcher` only triggers the transition when the widget's `key` changes. Using the formatted value string as key ensures the flip fires when VND ↔ USD produces a different formatted string.

**Outgoing slide direction:** The `AnimatedSwitcher` transitionBuilder receives a single `animation` for both incoming and outgoing child. The outgoing child's animation runs in reverse (1.0 → 0.0). Using `Tween<Offset>(begin: Offset(0, 1), end: Offset.zero)` reversed means the outgoing child goes from `Offset.zero` → `Offset(0, 1)` (down), while the incoming child goes `Offset(0, 1)` → `Offset.zero`. This creates upward exit + downward entry. To invert (outgoing exits upward, incoming enters from below), use a `ReverseAnimation` for the outgoing child — detect via checking child key or use `ClipRect` + layout approach.

**Simpler corrected approach using layoutBuilder** for proper directional slot:

```dart
// Correct slot direction: old value exits UP, new value enters from BELOW
transitionBuilder: (child, animation) {
  return SlideTransition(
    position: animation.status == AnimationStatus.reverse
        // outgoing: slide out upward (0 → -1)
        ? Tween<Offset>(begin: const Offset(0, -1), end: Offset.zero)
            .animate(animation)
        // incoming: slide in from below (1 → 0)
        : Tween<Offset>(begin: const Offset(0, 1), end: Offset.zero)
            .animate(animation),
    child: FadeTransition(opacity: animation, child: child),
  );
},
```

**Practical simplification:** Both enter-from-below and exit-downward still give a "slot" feel. The exact direction is a UX detail the planner can choose. The key constraint is: one direction only (not scale, not fade-only), duration ~200-300ms, and `ValueKey(value)` triggers.

### Pattern 3: GlassCard Summary Wrapper (SCRN-05)

**What:** Confirm the existing Portfolio screen uses `GlassCard.stationary` for the hero header (already done) and `GlassVariant.scrollItem` for asset rows (already done). This is a verification task, not a build task.

The existing code already satisfies SCRN-05 at the card-level. Phase 37's SCRN-05 contribution is the animated donut (glow) and ensuring the overall layout is polished with the new animations integrated.

**Existing correct state (no changes needed):**
- `PortfolioHeroHeader` → `GlassCard()` (stationary/default variant) ✓
- `PortfolioAssetListItem` → `GlassCard(variant: GlassVariant.scrollItem)` ✓
- `AllocationDonutChart` → currently no GlassCard wrapper (sits directly in padding) — Phase 37 should wrap it in a `GlassCard.stationary` for visual consistency with the hero header

### Anti-Patterns to Avoid

- **AnimatedSwitcher without ValueKey:** Without `ValueKey`, Flutter reuses the existing widget and skips the transition entirely. Always pass `key: ValueKey(value)` to the Text child.
- **BackdropFilter on glow Container:** The glow `Container` sits inside a scrollable section (`SliverToBoxAdapter`). Do NOT use `BackdropFilter` on the glow. It is a pure `Container` with `RadialGradient` — zero blur cost.
- **Using AnimationController for slot-flip when AnimatedSwitcher suffices:** `AnimatedSwitcher` handles the trigger (key change), the reverse animation for the outgoing child, and the forward for the incoming child automatically. An explicit controller adds complexity for no benefit.
- **Applying slot-flip to all Text in the widget tree:** Only apply `SlotFlipValue` to the primary value labels (total portfolio value, per-asset holding value). The PnL labels and quantity labels do NOT need this — overuse dilutes the effect.
- **Missing `ClipRect` around AnimatedSwitcher:** Without `ClipRect`, the outgoing/incoming children may paint outside the parent bounds during the slide. Wrap `SlotFlipValue` output in `ClipRect` (or add it inside the widget).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Slot-flip animation lifecycle | Custom AnimationController with addStatusListener + manual reverse | `AnimatedSwitcher` with `transitionBuilder` | AnimatedSwitcher handles outgoing-vs-incoming child lifecycle automatically; no manual dispose needed |
| Circular glow behind chart | CustomPainter with circular gradient | `Container` with `BoxDecoration(gradient: RadialGradient(...))` | RadialGradient is already the project's pattern (AmbientBackground, GlowDotPainter); zero new code surface |
| Dominant color calculation | Server-side sorting | Client-side `allocations.reduce((a,b) => a.percentage >= b.percentage ? a : b)` | Allocations are already on the client; reduce is O(n), not worth a new API field |

**Key insight:** Both features are pure Flutter core — no new dependencies. The slot-flip reuses the same `Animation<Offset>` + `SlideTransition` composition already used in Phase 36's staggered card entrance. The donut glow reuses the `RadialGradient` + `Container` pattern already established in `AmbientBackground`.

---

## Common Pitfalls

### Pitfall 1: AnimatedSwitcher Skips Transition When Key Is Unchanged
**What goes wrong:** The slot-flip never fires; values update instantly.
**Why it happens:** `AnimatedSwitcher` only animates when the child widget's `key` changes between builds. If `key` is omitted or is always null, Flutter determines it can reuse the existing widget in place.
**How to avoid:** Pass `key: ValueKey(value)` (the formatted String) to the `Text` child inside `SlotFlipValue`. The formatted string changes when currency switches (e.g., `"$1,234.56"` → `"₫30,500,000"`), guaranteeing a key change.
**Warning signs:** Currency toggle updates the displayed value with no animation.

### Pitfall 2: Slide Direction Confusion in AnimatedSwitcher transitionBuilder
**What goes wrong:** Both incoming and outgoing children slide in the same direction, or the outgoing child slides in from below instead of exiting upward.
**Why it happens:** `AnimatedSwitcher` passes a single `animation` to the `transitionBuilder`. For the outgoing (departing) child, the animation plays in **reverse** (from 1.0 to 0.0). For the incoming child it plays forward (0.0 to 1.0). If you use a single Tween without checking direction, both children move in the same direction.
**How to avoid:** Check `animation.status` to differentiate, or use `ReverseAnimation` for the outgoing child. Simplest correct approach:
```dart
transitionBuilder: (child, animation) {
  final isIncoming = animation.status != AnimationStatus.reverse;
  final offset = isIncoming
      ? Tween<Offset>(begin: const Offset(0, 1), end: Offset.zero)
      : Tween<Offset>(begin: const Offset(0, -1), end: Offset.zero);
  return ClipRect(
    child: SlideTransition(
      position: offset.animate(animation),
      child: child,
    ),
  );
},
```
**Warning signs:** Both old and new values slide the same direction simultaneously.

### Pitfall 3: Glow Bleeds Through PieChart Segments
**What goes wrong:** The ambient glow Container is visible through the PieChart's centerSpaceColor area but not through the segments — creates an uneven look.
**Why it happens:** PieChart segments have their own color fill; the gradient behind them is naturally occluded. The `centerSpaceColor` in the existing code is `AppTheme.surfaceDark` (solid Color(0xFF121212)). If the glow is behind the PieChart, the solid center space paints over the glow's center.
**How to avoid:** Change `centerSpaceColor` from `AppTheme.surfaceDark` to `Colors.transparent` so the glow shows through the donut hole. The GlassCard background behind the entire chart section provides the surface color, so transparent center is safe.
**Warning signs:** The glow is invisible in the donut center; only visible around the outer edge.

### Pitfall 4: SlotFlipValue Overflows Parent Layout
**What goes wrong:** During the animation, the outgoing child is still in the widget tree while the incoming child appears. If the parent has tight constraints, both children may overflow or cause layout issues.
**Why it happens:** `AnimatedSwitcher` keeps both children in the tree during transition. Without `ClipRect`, the outgoing child's slide-out position may paint outside the parent's bounds.
**How to avoid:** Wrap the entire `AnimatedSwitcher` in `ClipRect`. Also ensure the `SlotFlipValue` widget has the same intrinsic size as a static `Text` — using a `SizedBox` or letting the parent constrain it.
**Warning signs:** Yellow overflow bars during currency toggle, or visible text outside the card boundary.

### Pitfall 5: Reduce Motion Not Respected for Slot-Flip
**What goes wrong:** Slot-flip animation plays despite the user having enabled Reduce Motion in iOS Settings.
**Why it happens:** `AnimatedSwitcher` doesn't automatically check `MediaQuery.disableAnimations`. It always runs the `transitionBuilder` for the configured duration.
**How to avoid:** Inside `SlotFlipValue.build()`, read `GlassCard.shouldReduceMotion(context)`. If true, set `duration: Duration.zero` on `AnimatedSwitcher`. With `Duration.zero`, the switcher performs an instant swap. The `transitionBuilder` still runs but the animation is already complete, resulting in no visible motion.
**Warning signs:** Animation plays on devices with Reduce Motion enabled.

---

## Code Examples

Verified patterns from official Flutter docs and project codebase:

### SlotFlipValue Widget (Complete Implementation)
```dart
// Source: Flutter AnimatedSwitcher official docs + project GlassCard.shouldReduceMotion pattern
// lib/features/portfolio/presentation/widgets/slot_flip_value.dart

import 'package:flutter/material.dart';
import '../../../../core/widgets/glass_card.dart';

class SlotFlipValue extends StatelessWidget {
  const SlotFlipValue({
    required this.value,
    this.style,
    super.key,
  });

  final String value;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final reduceMotion = GlassCard.shouldReduceMotion(context);

    return ClipRect(
      child: AnimatedSwitcher(
        duration: reduceMotion
            ? Duration.zero
            : const Duration(milliseconds: 250),
        switchInCurve: Curves.easeOut,
        switchOutCurve: Curves.easeIn,
        transitionBuilder: (child, animation) {
          // Distinguish incoming (forward) vs outgoing (reverse) child.
          // Outgoing: animation runs 1.0→0.0 (reverse), exits upward.
          // Incoming: animation runs 0.0→1.0 (forward), enters from below.
          final isIncoming = animation.status != AnimationStatus.reverse;
          final slideTween = isIncoming
              ? Tween<Offset>(begin: const Offset(0, 1), end: Offset.zero)
              : Tween<Offset>(begin: const Offset(0, -1), end: Offset.zero);
          return SlideTransition(
            position: slideTween.animate(animation),
            child: FadeTransition(opacity: animation, child: child),
          );
        },
        // CRITICAL: ValueKey triggers re-animation when value string changes.
        child: Text(value, key: ValueKey(value), style: style),
      ),
    );
  }
}
```

### Ambient Glow Layer in AllocationDonutChart
```dart
// Source: AmbientBackground orb pattern (project codebase) + GlowDotPainter RadialGradient
// Replaces the inner Stack inside AllocationDonutChart.build()

/// Returns the color of the largest-percentage allocation segment.
Color _dominantColor() {
  if (widget.allocations.isEmpty) return AppTheme.bitcoinOrange;
  final dominant = widget.allocations.reduce(
    (a, b) => a.percentage >= b.percentage ? a : b,
  );
  return _colorForType(dominant.assetType);
}

// In build(), replace the existing Stack with:
Stack(
  alignment: Alignment.center,
  children: [
    // Ambient glow — RadialGradient behind the donut ring
    Container(
      width: 160,  // slightly smaller than donut outer diameter (55+55 = 110 radius, ~220 diameter → 160 for inner glow)
      height: 160,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [
            _dominantColor().withAlpha(51),  // ~0.20 at center
            _dominantColor().withAlpha(0),   // transparent at edge
          ],
          stops: const [0.0, 1.0],
        ),
      ),
    ),
    // Existing PieChart (unchanged)
    PieChart(PieChartData(...)),
    // Existing center label (unchanged)
    Column(mainAxisSize: MainAxisSize.min, children: [...]),
  ],
)
```

### GlassCard Wrapper for AllocationDonutChart
```dart
// Wrap the existing AllocationDonutChart padding in a GlassCard for visual consistency
// In PortfolioScreen._buildContent(), replace:
//   SliverToBoxAdapter(child: AllocationDonutChart(...))
// with:
SliverToBoxAdapter(
  child: GlassCard(
    margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    padding: const EdgeInsets.all(16),
    child: AllocationDonutChart(
      allocations: data.summary.allocations,
      totalValue: totalValue,
      isVnd: isVnd,
    ),
  ),
),
```

### SlotFlipValue in PortfolioHeroHeader
```dart
// Replace the static total value Text with SlotFlipValue
// In PortfolioHeroHeader.build():

// BEFORE:
Text(
  _formatValue(summary.totalValueUsd, summary.totalValueVnd),
  style: Theme.of(context).textTheme.headlineLarge?.merge(...),
)

// AFTER:
SlotFlipValue(
  value: _formatValue(summary.totalValueUsd, summary.totalValueVnd),
  style: Theme.of(context).textTheme.headlineLarge?.merge(
    AppTheme.moneyStyle.copyWith(fontWeight: FontWeight.bold),
  ),
)
```

### SlotFlipValue in PortfolioAssetListItem
```dart
// Replace trailing value Text in PortfolioAssetListItem build():
// BEFORE:
Text(
  _formatValue(asset.currentValueUsd, asset.currentValueVnd),
  style: AppTheme.moneyStyle.copyWith(fontWeight: FontWeight.w600, fontSize: 14),
)

// AFTER:
SlotFlipValue(
  value: _formatValue(asset.currentValueUsd, asset.currentValueVnd),
  style: AppTheme.moneyStyle.copyWith(fontWeight: FontWeight.w600, fontSize: 14),
)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AllocationDonutChart` with plain PieChart, no glow | Donut with RadialGradient ambient glow halo matching dominant asset color | Phase 37 | Visual premium matching AmbientBackground orb aesthetic |
| `CurrencyToggle` updates values instantly (Text re-render) | Value labels flip with vertical slot animation on toggle | Phase 37 | Tactile feedback for currency switch, reinforces the toggle action |
| `AllocationDonutChart` sits in raw `Padding` | Wrapped in `GlassCard.stationary` | Phase 37 | Visual consistency with hero header glass surface |
| `PortfolioSummaryCard` uses plain Material `Card` | Widget still exists but is not used on the main screen (replaced by `PortfolioHeroHeader`) | Phase 35.1 | Portfolio summary card is a legacy widget — do not re-introduce |

**Deprecated/outdated:**
- `PortfolioSummaryCard` — replaced by `PortfolioHeroHeader` in Phase 35.1; do not use
- `AllocationDonutChart` as `StatefulWidget` — converting to `HookConsumerWidget` not needed for Phase 37; the existing `setState` for `_touchedIndex` is sufficient and correct

---

## Open Questions

1. **Slot-flip scope: also apply to PnL values in PortfolioHeroHeader?**
   - What we know: ANIM-06 says "value labels flip" when the toggle fires. The P&L row in `PortfolioHeroHeader` also changes between VND and USD.
   - What's unclear: Whether P&L labels are included in ANIM-06 or only the total value.
   - Recommendation: Apply `SlotFlipValue` to total value (primary) AND P&L amount (secondary). Skip the P&L percent (%) label since it does not change on currency toggle.

2. **Glow intensity calibration**
   - What we know: `withAlpha(51)` (~20% center opacity) is the proposed starting value. AmbientBackground uses 28/26/20. GlowDotPainter uses 153 at center.
   - What's unclear: Whether 51 is too prominent or too subtle on a physical device.
   - Recommendation: Use 51 (~20%) as the baseline. Document as a tunable constant (`const _kGlowCenterAlpha = 51`) so it can be adjusted without searching.

3. **AllocationDonutChart GlassCard wrapping — does it need to update the skeleton too?**
   - What we know: The skeleton in `PortfolioScreen._buildLoadingSkeleton()` already has a donut placeholder `Bone(height: 200)`. Adding a GlassCard wrapper around the real chart should match the skeleton's `GlassCard` shape.
   - What's unclear: Whether the skeleton already uses a GlassCard container shape that matches.
   - Recommendation: Update the skeleton's donut placeholder to be inside a `GlassCard(scrollItem: false)` wrapper matching the live chart's GlassCard margin/padding. The existing skeleton uses a raw `Bone(height: 200)` inside `Padding` — update to wrap in `GlassCard` for visual continuity.

---

## Validation Architecture

> `workflow.nyquist_validation` key is absent from `.planning/config.json` — treated as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Flutter widget tests (flutter_test SDK) |
| Config file | none — standard `flutter test` discovery |
| Quick run command | `cd /Users/baotoq/Work/trading-bot/TradingBot.Mobile && flutter test` |
| Full suite command | `cd /Users/baotoq/Work/trading-bot/TradingBot.Mobile && flutter test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCRN-05 | Portfolio glass cards and GlassVariant.scrollItem on asset rows render correctly | Widget test / Visual | `flutter test test/portfolio_screen_test.dart` | ❌ Wave 0 |
| ANIM-06 | SlotFlipValue triggers AnimatedSwitcher when value key changes | Widget test | `flutter test test/slot_flip_value_test.dart` | ❌ Wave 0 |

**Note:** The Flutter mobile project has no existing test directory or test files. All validation is currently manual (visual inspection on simulator/device). The widget tests above represent what Wave 0 would need to create if automated testing is desired for this phase. Given no existing test infrastructure in the mobile project, the planner may choose to treat all validation as manual-only for this phase.

### Sampling Rate
- **Per task commit:** Manual visual check on iOS Simulator
- **Per wave merge:** Run `flutter analyze` (no errors) + visual smoke test on Simulator
- **Phase gate:** `flutter analyze` clean + visual verification of donut glow and slot-flip on device/simulator before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `test/slot_flip_value_test.dart` — covers ANIM-06 (AnimatedSwitcher key trigger)
- [ ] `test/portfolio_screen_test.dart` — covers SCRN-05 (GlassCard rendering)

*(No existing flutter test infrastructure in TradingBot.Mobile — creating tests is optional for this phase given the project has no prior mobile test baseline.)*

---

## Sources

### Primary (HIGH confidence)
- Project codebase: `lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart` — existing donut chart to modify
- Project codebase: `lib/features/portfolio/presentation/widgets/portfolio_hero_header.dart` — total value label to wrap
- Project codebase: `lib/features/portfolio/presentation/widgets/portfolio_asset_list_item.dart` — trailing value label to wrap
- Project codebase: `lib/features/portfolio/presentation/widgets/currency_toggle.dart` — toggle mechanism (no change required)
- Project codebase: `lib/features/portfolio/presentation/portfolio_screen.dart` — screen structure review
- Project codebase: `lib/core/widgets/glass_card.dart` — GlassVariant, shouldReduceMotion, BackdropFilter constraint
- Project codebase: `lib/core/widgets/ambient_background.dart` — RadialGradient orb pattern for glow implementation
- Project codebase: `lib/features/chart/presentation/widgets/glow_dot_painter.dart` — RadialGradient glow technique in Canvas
- Project codebase: `lib/app/theme.dart` — GlassTheme tokens, AppTheme colors
- Project codebase: `lib/features/home/presentation/home_screen.dart` — Phase 36 animation patterns (stagger, reduceMotion guard)
- Project STATE.md — established decisions: GlassVariant.scrollItem for lists, shouldReduceMotion pattern, Color.withAlpha over withOpacity
- Flutter official docs: `AnimatedSwitcher` — `transitionBuilder`, `ValueKey` requirement, reverse animation for outgoing child

### Secondary (MEDIUM confidence)
- Flutter docs: `AnimatedSwitcher` transitionBuilder reverse animation behavior — confirmed in multiple Flutter cookbook examples and flutter.dev docs; outgoing child animation plays in reverse

### Tertiary (LOW confidence)
- Glow alpha value `51` (~20%) — proposed based on project's existing alpha conventions (AmbientBackground: 20-28, GlowDotPainter: 153); exact value requires visual calibration on physical device

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already in pubspec.yaml; zero new dependencies
- Ambient glow pattern: HIGH — RadialGradient Container matches existing AmbientBackground + GlowDotPainter patterns exactly
- Slot-flip AnimatedSwitcher: HIGH — AnimatedSwitcher + ValueKey is the Flutter canonical approach for this pattern; confirmed in Flutter official docs
- Glow alpha intensity: LOW — requires physical device calibration; 51 is a starting estimate

**Research date:** 2026-03-04
**Valid until:** 2026-04-04 (stable libraries; fl_chart, flutter_hooks, hooks_riverpod are stable; 30-day window)

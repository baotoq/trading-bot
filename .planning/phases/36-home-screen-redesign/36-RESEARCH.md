# Phase 36: Home Screen Redesign - Research

**Researched:** 2026-02-22
**Domain:** Flutter UI — animated dashboard layout, staggered entrance, count-up counter, page transitions
**Confidence:** HIGH

## Summary

Phase 36 replaces the current plain HomeScreen (portfolio value text + 2x2 stat grid + CountdownText + LastBuyCard) with a glassmorphism dashboard layout. The new layout requires four sections in glass cards: a hero balance card (total portfolio value), a mini donut allocation chart, the last 3 purchase activity items, and quick action buttons. On first load the cards cascade in with staggered entrance and the hero balance value count-up animates from zero to the actual value.

Tab navigation and modal routes (the bot-detail full-screen push) also need smooth fade+scale transitions instead of the default Material slide. The project already uses go_router `^17.1.0` with `StatefulShellRoute.indexedStack`; custom transitions are applied per-route using `pageBuilder` + `CustomTransitionPage`.

All animation controllers must follow the project-established patterns: `useAnimationController` via `flutter_hooks` in `HookConsumerWidget`, the `useRef(false)` `_hasAnimated` guard so animations fire only on first mount, and `GlassCard.shouldReduceMotion(context)` to skip all animation when the platform accessibility flag is set.

**Primary recommendation:** Use a single `AnimationController` with `Interval`-based stagger for card entrance; use `TweenAnimationBuilder<double>` for the count-up number (no controller needed); apply `CustomTransitionPage` with `FadeTransition` + `ScaleTransition` to every `GoRoute.pageBuilder` for the tab-level and modal transitions.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCRN-01 | Home screen uses dashboard overview layout with hero balance, mini allocation chart, recent activity, and quick actions — all within glass cards | GlassCard.stationary for all 4 sections; AllocationDonutChart already exists in portfolio feature — mini variant (smaller height, no legend); recent activity reuses PurchaseListItem or a simplified version; data from existing homeDataProvider + portfolioPageDataProvider |
| ANIM-02 | Portfolio balance and key stat values animate with a count-up effect on first load | TweenAnimationBuilder<double>(begin: 0, end: actualValue) drives a formatted number display; _hasAnimated useRef guard prevents replay on tab revisit |
| ANIM-03 | Dashboard cards cascade in with staggered entrance animation (40-60ms offset per REQUIREMENTS.md) | Single AnimationController; each GlassCard wrapped in FadeTransition + SlideTransition driven by Interval-based CurvedAnimation; useRef(false) _hasAnimated guard fires only on first mount |
| ANIM-05 | Tab and modal routes use smooth fade+scale page transitions instead of a hard cut | CustomTransitionPage with FadeTransition + ScaleTransition(scale: 0.95→1.0) applied via pageBuilder on every GoRoute; covers StatefulShellBranch routes and the bot-detail parentNavigatorKey push |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| flutter_hooks | any | `useAnimationController`, `useRef`, `useState` | Project pattern — all animated screens use HookConsumerWidget |
| hooks_riverpod | ^3.2.1 | `ref.watch(homeDataProvider)` / `portfolioPageDataProvider` | Project state management |
| fl_chart | ^1.1.1 | Mini donut chart (PieChart) | Already used in AllocationDonutChart |
| go_router | ^17.1.0 | `CustomTransitionPage` for fade+scale route transitions | Already in use for all navigation |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| skeletonizer | ^1.4.0 | Skeleton loading for home dashboard sections | Use AppShimmer + Bone shapes matching real content |
| intl | any | Number formatting for count-up display | Already in use across screens |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single AnimationController + Interval | Multiple controllers chained with .then() | Single controller is simpler, all cards share one vsync tick — use this |
| TweenAnimationBuilder for count-up | Explicit AnimationController + AnimatedBuilder | TweenAnimationBuilder requires no dispose management — correct for simple count-up |
| CustomTransitionPage per route | Global transitionBuilder on GoRouter | Per-route is more granular and won't affect routes outside this phase |

**Installation:** No new packages required — all dependencies already in pubspec.yaml.

---

## Architecture Patterns

### Recommended Project Structure

The home feature already has this structure. New widgets go in:
```
lib/features/home/presentation/
├── home_screen.dart                  (REPLACE existing — full redesign)
└── widgets/
    ├── home_hero_balance_card.dart   (NEW — hero balance + count-up)
    ├── home_mini_donut_card.dart     (NEW — mini allocation donut in GlassCard)
    ├── home_recent_activity_card.dart (NEW — last 3 purchases in GlassCard)
    ├── home_quick_actions_card.dart  (NEW — quick action buttons in GlassCard)
    ├── portfolio_stats_section.dart  (KEEP — used by DcaBotDetailScreen)
    ├── last_buy_card.dart            (KEEP — still used, but not on home)
    └── ...existing widgets...
```

Router change:
```
lib/app/router.dart                   (UPDATE — wrap GoRoute builders in CustomTransitionPage)
```

### Pattern 1: Staggered Card Entrance (ANIM-03)

**What:** Single AnimationController drives N cards via Interval-based CurvedAnimations. Each card i starts at `i * staggerFraction` of the total timeline.
**When to use:** Any time multiple sibling widgets need sequential reveal.

Requirements specify 40-60ms offset. With 4 cards and 50ms stagger:
- Card animation duration: 300ms
- Total controller duration = 300ms + (50ms * 3) = 450ms
- Card i interval: start = (i * 50) / 450, end = (i * 50 + 300) / 450

```dart
// Source: .agents/skills/flutter-animations/references/staggered.md
// Inside HookConsumerWidget.build():

final hasAnimated = useRef(false);
final controller = useAnimationController(
  duration: const Duration(milliseconds: 450), // 300 + 3*50
);

useEffect(() {
  if (!hasAnimated.value && !GlassCard.shouldReduceMotion(context)) {
    hasAnimated.value = true;
    controller.forward();
  } else if (GlassCard.shouldReduceMotion(context)) {
    // Snap to final state — no animation
    controller.value = 1.0;
  }
  return null;
}, const []);

// Per-card animation factory:
Animation<double> _cardAnim(AnimationController ctrl, int index) {
  const staggerMs = 50.0;
  const animMs = 300.0;
  const totalMs = animMs + staggerMs * 3; // 450ms
  final start = (index * staggerMs) / totalMs;
  final end = (index * staggerMs + animMs) / totalMs;
  return CurvedAnimation(
    parent: ctrl,
    curve: Interval(start, end, curve: Curves.easeOut),
  );
}

// Usage on each card (fade + slide up):
AnimatedBuilder(
  animation: controller,
  builder: (context, child) {
    final anim = _cardAnim(controller, cardIndex);
    return FadeTransition(
      opacity: anim,
      child: SlideTransition(
        position: Tween<Offset>(
          begin: const Offset(0, 0.06), // subtle upward slide
          end: Offset.zero,
        ).animate(anim),
        child: child,
      ),
    );
  },
  child: /* the GlassCard widget */,
)
```

### Pattern 2: Count-Up Number Animation (ANIM-02)

**What:** `TweenAnimationBuilder<double>` animates a numeric value from 0 to the target, driving a formatted Text widget. Requires the same `_hasAnimated` guard.
**When to use:** Hero balance value, key stats that should count up on first load only.

```dart
// Source: .agents/skills/flutter-animations/SKILL.md
// Pattern: TweenAnimationBuilder — no controller, no dispose needed

final hasAnimated = useRef(false); // shared with stagger controller

// Count-up is triggered by listening to controller progress OR
// simply driven by a separate TweenAnimationBuilder once data is loaded.
// Simplest approach: TweenAnimationBuilder driven by shouldAnimate flag.

final shouldAnimate = !hasAnimated.value && !GlassCard.shouldReduceMotion(context);

TweenAnimationBuilder<double>(
  tween: Tween<double>(begin: 0, end: portfolioValue),
  duration: const Duration(milliseconds: 1200),
  curve: Curves.easeOut,
  builder: (context, value, _) {
    return Text(
      _usdFmt.format(value),
      style: textTheme.displaySmall?.merge(AppTheme.moneyStyle),
    );
  },
)
```

**Important:** `TweenAnimationBuilder` re-animates whenever `tween.end` changes (e.g., on refresh). To animate only on first load, the `begin` value must be set to `end` after the first animation completes, or the widget can be conditionally replaced. The simplest project-aligned approach: use `begin: shouldAnimate ? 0 : portfolioValue` so subsequent rebuilds snap directly to the correct value without animating again.

### Pattern 3: Fade+Scale Page Transition (ANIM-05)

**What:** `CustomTransitionPage` with combined `FadeTransition` + `ScaleTransition` (scale 0.95 → 1.0) replaces default Material slide.
**When to use:** Every `GoRoute.pageBuilder` for tab screens and full-screen pushes.

```dart
// Source: go_router docs — CustomTransitionPage
// Applied in lib/app/router.dart

GoRoute(
  path: '/home',
  pageBuilder: (context, state) => CustomTransitionPage<void>(
    key: state.pageKey,
    child: const HomeScreen(),
    transitionDuration: const Duration(milliseconds: 200),
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      return FadeTransition(
        opacity: animation,
        child: ScaleTransition(
          scale: Tween<double>(begin: 0.95, end: 1.0).animate(
            CurvedAnimation(parent: animation, curve: Curves.easeOut),
          ),
          child: child,
        ),
      );
    },
  ),
),
```

This `pageBuilder` pattern must be applied to ALL tab routes (`/home`, `/chart`, `/history`, `/config`, `/portfolio`) and the modal pushes (`/home/bot-detail`, `/portfolio/add-transaction`, etc.) for consistent transitions.

### Pattern 4: Mini Donut Chart (SCRN-01)

**What:** Reuse `AllocationDonutChart` from portfolio feature, but sized smaller (height ~140px) and without the legend row. Alternative: extract a `MiniAllocationDonut` that only renders the PieChart + center label.
**When to use:** Home screen only — the portfolio screen continues using the full `AllocationDonutChart`.

Data source: `portfolioPageDataProvider` from `lib/features/portfolio/data/portfolio_providers.dart`. The home screen needs to watch this provider in addition to `homeDataProvider`. Both are already defined and cached independently.

```dart
// The existing AllocationDonutChart takes:
// - allocations: List<AllocationDto>
// - totalValue: double
// - isVnd: bool
// Source data from: portfolioPageDataProvider.value?.summary

// Quick approach: pass allocations from PortfolioSummaryResponse.allocations
// This adds one more provider watch on HomeScreen — acceptable pattern per project
```

### Pattern 5: Recent Activity (SCRN-01)

**What:** Last 3 purchases from `purchaseHistoryProvider` (already exists), displayed in a GlassCard. Reuse `PurchaseListItem` widget or a simplified row variant.
**When to use:** Home screen only — shows 3 items, no pagination.

Data availability: `purchaseHistoryProvider` already loads the first page (cursor: null). The first 3 items are `purchaseHistory.value?.take(3)`. This is already fetched as part of the DcaBotDetailScreen's _EventHistoryPreviewSection — same pattern applies here.

```dart
// Reuse existing PurchaseListItem widget:
// lib/features/history/presentation/widgets/purchase_list_item.dart
// It renders inside GlassVariant.scrollItem cards naturally.
// For the home screen, use GlassVariant.scrollItem on the outer card
// if items are inside a scrollable list, OR stationary GlassCard wrapping
// a Column of 3 items (no scroll = BackdropFilter safe).
```

**Decision needed:** BackdropFilter safety — 3 static items in a Column inside a `GlassCard.stationary` (no scrolling) = safe. Do NOT put items in a scrollable list inside the GlassCard.

### Anti-Patterns to Avoid

- **Re-animating on refresh:** `TweenAnimationBuilder` re-runs when `tween.end` changes. Use `begin: shouldAnimate ? 0.0 : portfolioValue` pattern to prevent count-up replay after auto-refresh.
- **BackdropFilter in scrollable lists:** Already a project constraint — recent activity items should be in a Column (max 3), not a ListView, inside the glass card.
- **Multiple AnimationControllers for stagger:** Use a single controller with `Interval` timing. Multiple controllers for simple sequential reveals is unnecessary complexity.
- **Forgetting reduce-motion:** Check `GlassCard.shouldReduceMotion(context)` before calling `controller.forward()`. When reduce-motion is set, snap to `controller.value = 1.0` immediately.
- **pageBuilder on sub-routes without rootNavigatorKey:** The `/home/bot-detail` route uses `parentNavigatorKey: rootNavigatorKey`. It still accepts `pageBuilder` — confirm this works with the rootNavigatorKey.
- **useEffect with stale context:** The `GlassCard.shouldReduceMotion(context)` call inside `useEffect([], const [])` captures a stale context. Use `didChangeDependencies` pattern or check in the build method before calling forward. Project precedent from Phase 34: `didChangeDependencies` was used for the shouldReduceMotion check in PressableScale. For HookConsumerWidget, using `useMemoized` or checking in build before forwarding is safer.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Count-up number animation | Custom AnimationController + manual tween | `TweenAnimationBuilder<double>` | Built-in, no dispose needed, handles begin/end/curve cleanly |
| Staggered list reveal | Separate delayed Futures | Single AnimationController + `Interval` | One vsync tick, no Timer-based delays, no memory leaks |
| Donut allocation chart | Custom `CustomPainter` pie | `PieChart` from `fl_chart` | Already used in portfolio, identical API |
| Fade+scale route transition | Wrap Navigator.push with custom builder | `CustomTransitionPage` via go_router `pageBuilder` | Integrates with GoRouter's page stack, handles push/pop correctly |

**Key insight:** All the building blocks exist in the codebase — `GlassCard`, `AllocationDonutChart`, `PurchaseListItem`, `AppShimmer`. This phase is primarily composition and animation wiring.

---

## Common Pitfalls

### Pitfall 1: Count-Up Replay After Auto-Refresh
**What goes wrong:** `homeDataProvider` auto-refreshes every 30 seconds. `TweenAnimationBuilder` with `begin: 0` will re-run the count-up every refresh.
**Why it happens:** `TweenAnimationBuilder` detects a new `tween.end` value and re-animates from `begin`.
**How to avoid:** Set `begin: shouldAnimate ? 0.0 : portfolioValue` where `shouldAnimate` is a `useRef<bool>` that goes false after first animation. After first load, `begin == end == portfolioValue`, so no animation occurs.
**Warning signs:** Balance flickers to zero every 30 seconds.

### Pitfall 2: Stagger Fires on Every Tab Revisit
**What goes wrong:** Cards animate in every time the user taps back to the Home tab.
**Why it happens:** `HookConsumerWidget` rebuilds when the tab is revisited; `useEffect` with empty deps fires on mount — but with `StatefulShellRoute.indexedStack`, the widget stays mounted between tab visits, so `useEffect` fires only once. This means stagger is naturally guarded. Still, add `useRef(false)` guard for defensive coding and consistency with project conventions (established in Phase 35).
**How to avoid:** `useRef(false)` hasAnimated guard in `useEffect`. Project decision from STATE.md: "useRef(false) as hasAnimated guard -- fires draw-in animation only on first mount, not on tab revisit".
**Warning signs:** Cards animate every time Home tab is selected.

### Pitfall 3: AllocationDonutChart Data Not Available on Home Tab
**What goes wrong:** `portfolioPageDataProvider` is only watched from `PortfolioScreen`. If the user hasn't visited the Portfolio tab yet, the provider hasn't been initialized.
**Why it happens:** Riverpod providers are lazy — they initialize on first watch.
**How to avoid:** HomeScreen watching `portfolioPageDataProvider` is sufficient to trigger initialization. The provider is independent of the Portfolio tab and will load when HomeScreen first builds. Show skeleton/placeholder if `AsyncLoading`.
**Warning signs:** Mini donut chart shows skeleton indefinitely or crashes with null.

### Pitfall 4: CustomTransitionPage on Routes with parentNavigatorKey
**What goes wrong:** Routes using `parentNavigatorKey: rootNavigatorKey` (bot-detail, add-transaction, etc.) may not animate if `pageBuilder` is applied incorrectly.
**Why it happens:** No known issue — `CustomTransitionPage` works with `parentNavigatorKey`. This is a precaution.
**How to avoid:** Apply `pageBuilder` to all routes consistently. Test the bot-detail push explicitly.
**Warning signs:** Full-screen routes appear without transition.

### Pitfall 5: shouldReduceMotion Check in useEffect
**What goes wrong:** `GlassCard.shouldReduceMotion(context)` uses `MediaQuery.of(context)`. Called inside `useEffect(() {}, const [])`, it captures a potentially stale context.
**Why it happens:** `useEffect` with empty deps runs post-first-build. The captured context should be valid, but the project convention (Phase 34 decision) is to check reduce-motion via `didChangeDependencies`.
**How to avoid:** In `HookConsumerWidget`, check reduce-motion in the `build` method and pass the result as a variable into `useEffect` deps, or use a build-time check:
```dart
final reduceMotion = GlassCard.shouldReduceMotion(context);
useEffect(() {
  if (!hasAnimated.value) {
    hasAnimated.value = true;
    if (reduceMotion) {
      controller.value = 1.0;
    } else {
      controller.forward();
    }
  }
  return null;
}, [reduceMotion]); // reduceMotion in deps so it re-evaluates if accessibility changes
```
**Warning signs:** Animations play despite Reduce Motion being enabled.

### Pitfall 6: Quick Actions Layout — What Goes Here?
**What goes wrong:** The requirement says "quick action buttons" but doesn't specify what they do.
**Why it happens:** Specification is intentionally open.
**How to avoid:** Reasonable quick actions for this screen: "View Bot" (navigates to `/home/bot-detail`), "View Chart" (switches to Chart tab via `goBranch`), "History" (switches to History tab). These are navigation shortcuts — no API calls needed.
**Warning signs:** N/A — design decision for the planner.

---

## Code Examples

Verified patterns from project codebase and skill files:

### Count-Up Hero Balance (First-Load Only)
```dart
// Pattern: begin == end when shouldAnimate is false → no animation
final hasAnimated = useRef(false);
final reduceMotion = GlassCard.shouldReduceMotion(context);
final shouldAnimate = !hasAnimated.value && !reduceMotion;

// Mark animated after first render with data
if (!hasAnimated.value && portfolioValue > 0) {
  hasAnimated.value = true;
}

TweenAnimationBuilder<double>(
  tween: Tween<double>(
    begin: shouldAnimate ? 0.0 : portfolioValue,
    end: portfolioValue,
  ),
  duration: const Duration(milliseconds: 1200),
  curve: Curves.easeOut,
  builder: (context, value, _) {
    return Text(
      NumberFormat.currency(symbol: '\$', decimalDigits: 2).format(value),
      style: Theme.of(context).textTheme.displaySmall
          ?.copyWith(fontWeight: FontWeight.bold, color: Colors.white)
          .merge(AppTheme.moneyStyle),
    );
  },
)
```

### Staggered 4-Card Entrance
```dart
// Source: .agents/skills/flutter-animations/assets/templates/staggered_animation.dart
// 4 cards, 50ms stagger, 300ms per card → 450ms total
const _kStaggerMs = 50.0;
const _kAnimMs = 300.0;
const _kCardCount = 4;
const _kTotalMs = _kAnimMs + _kStaggerMs * (_kCardCount - 1); // 450ms

Animation<double> _cardEntrance(AnimationController ctrl, int index) {
  final start = (index * _kStaggerMs) / _kTotalMs;
  final end = (index * _kStaggerMs + _kAnimMs) / _kTotalMs;
  return CurvedAnimation(
    parent: ctrl,
    curve: Interval(start, end.clamp(0.0, 1.0), curve: Curves.easeOut),
  );
}

Widget _animatedCard(AnimationController ctrl, int index, Widget card) {
  return AnimatedBuilder(
    animation: ctrl,
    builder: (context, child) {
      final anim = _cardEntrance(ctrl, index);
      return FadeTransition(
        opacity: anim,
        child: SlideTransition(
          position: Tween<Offset>(
            begin: const Offset(0, 0.06),
            end: Offset.zero,
          ).animate(anim),
          child: child,
        ),
      );
    },
    child: card,
  );
}
```

### Fade+Scale CustomTransitionPage
```dart
// Source: go_router docs — CustomTransitionPage
// Reusable factory function for consistency across all routes

CustomTransitionPage<void> _fadeScalePage({
  required LocalKey key,
  required Widget child,
}) {
  return CustomTransitionPage<void>(
    key: key,
    child: child,
    transitionDuration: const Duration(milliseconds: 200),
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      return FadeTransition(
        opacity: CurvedAnimation(parent: animation, curve: Curves.easeOut),
        child: ScaleTransition(
          scale: Tween<double>(begin: 0.95, end: 1.0).animate(
            CurvedAnimation(parent: animation, curve: Curves.easeOut),
          ),
          child: child,
        ),
      );
    },
  );
}

// In router.dart:
GoRoute(
  path: '/home',
  pageBuilder: (context, state) => _fadeScalePage(
    key: state.pageKey,
    child: const HomeScreen(),
  ),
),
```

### Mini Donut Chart in GlassCard
```dart
// Minimal adaptation of AllocationDonutChart for home screen
// No legend, smaller height (140px), no touch interaction needed

GlassCard(
  margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
  padding: const EdgeInsets.all(16),
  child: Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text('Allocation', style: theme.textTheme.titleSmall),
      const SizedBox(height: 8),
      SizedBox(
        height: 140,
        child: PieChart(
          PieChartData(
            centerSpaceRadius: 40,
            centerSpaceColor: Colors.transparent,
            sectionsSpace: 2,
            sections: allocations.map((a) => PieChartSectionData(
              value: a.percentage,
              color: _colorForType(a.assetType),
              radius: 45,
              showTitle: false,
            )).toList(),
          ),
        ),
      ),
    ],
  ),
),
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| StatelessWidget + Card | HookConsumerWidget + GlassCard | Phase 33-34 | All new screens use glass surface |
| AnimationController in StatefulWidget | useAnimationController in HookConsumerWidget | Phase 34 | No lifecycle leaks across 5 tabs |
| Hard-cut route transitions (builder:) | CustomTransitionPage (pageBuilder:) | This phase | Smooth fade+scale between all routes |
| Straight balance text display | count-up TweenAnimationBuilder | This phase | Premium first-load UX |

**Deprecated/outdated in this project:**
- `builder:` on GoRoute for screens that need transitions → use `pageBuilder:` + `CustomTransitionPage`
- Plain `Card` widget → use `GlassCard` for all surfaces in v5.0

---

## Open Questions

1. **Quick action buttons — specific actions**
   - What we know: Requirement says "quick action buttons" within a GlassCard
   - What's unclear: Exactly which actions (navigate to bot-detail? refresh? external link?)
   - Recommendation: Use navigation shortcuts — "View Bot", "View Chart", "View History" — these are zero-risk and deliver genuine utility without requiring any new API endpoints

2. **AllocationDonutChart data source on HomeScreen**
   - What we know: `portfolioPageDataProvider` provides `PortfolioSummaryResponse.allocations` with `List<AllocationDto>` that the full chart uses
   - What's unclear: Whether the planner should watch `portfolioPageDataProvider` directly from HomeScreen or create a dedicated `homeAllocationsProvider` that re-exposes only the allocation slice
   - Recommendation: Watch `portfolioPageDataProvider` directly — it is already cached by Riverpod and there is no performance penalty for a second watcher. Adding a thin provider re-export would add indirection for no gain.

3. **Transition on StatefulShellRoute itself (tab switches)**
   - What we know: `StatefulShellRoute.indexedStack` switches between branches by index, not by route push. This is NOT a GoRouter page transition — it's an `IndexedStack` swap, which has no built-in animation.
   - What's unclear: ANIM-05 says "Tab and modal routes use smooth fade+scale page transitions" — does this cover tab-tab switching or only full-route pushes?
   - Recommendation: `CustomTransitionPage` covers push/pop transitions (bot-detail, add-transaction, etc.). For tab-tab switching within `IndexedStack`, a separate `AnimatedSwitcher` or `AnimatedIndexedStack` wrapper in `ScaffoldWithNavigation` would be needed. The REQUIREMENTS.md language "tab and modal routes" most likely refers to route pushes (modals), not tab-bar index changes. Clarify with planner: implement `CustomTransitionPage` for all pushes, and optionally add `AnimatedSwitcher` in `ScaffoldWithNavigation` for the `navigationShell` body if ANIM-05 explicitly targets tab switching.

---

## Sources

### Primary (HIGH confidence)
- `.agents/skills/flutter-animations/SKILL.md` — animation type decision tree, TweenAnimationBuilder, AnimationController patterns
- `.agents/skills/flutter-animations/references/staggered.md` — Interval-based stagger, duration calculation, list animation
- `.agents/skills/flutter-animations/assets/templates/staggered_animation.dart` — production-ready stagger template
- `/websites/pub_dev_packages_go_router` (Context7) — `CustomTransitionPage`, `pageBuilder`, `transitionsBuilder` API
- Project codebase: `lib/features/home/presentation/home_screen.dart` — current HomeScreen structure being replaced
- Project codebase: `lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart` — existing donut chart to adapt
- Project codebase: `lib/features/history/presentation/widgets/purchase_list_item.dart` — reusable purchase row
- Project codebase: `lib/app/router.dart` — current GoRouter setup to extend with pageBuilder
- Project codebase: `lib/features/home/data/home_providers.dart` — HomeData model (portfolio + status)
- Project codebase: `lib/features/portfolio/data/portfolio_providers.dart` — portfolioPageDataProvider (allocations source)
- Project codebase: `.planning/STATE.md` — `useRef(false)` _hasAnimated pattern, shouldReduceMotion conventions

### Secondary (MEDIUM confidence)
- go_router README / pub.dev docs — `CustomTransitionPage` for `StatefulShellBranch` routes confirmed in multiple examples

### Tertiary (LOW confidence)
- Open Question 3 (tab switching with AnimatedSwitcher in IndexedStack) — not confirmed against go_router source; interpretation of ANIM-05 scope is planner's call

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already in pubspec.yaml, no new additions
- Architecture: HIGH — all patterns verified against existing codebase and skill templates
- Stagger animation: HIGH — verified via skill template and existing Phase 35 draw-in animation pattern
- Count-up animation: HIGH — TweenAnimationBuilder is Flutter core, pattern is documented
- Page transitions: HIGH — CustomTransitionPage confirmed via Context7 go_router docs
- Mini donut chart: HIGH — AllocationDonutChart source code read and adaptation is trivial
- Open Question 3 (tab switching): LOW — scope ambiguity in ANIM-05 not fully resolved

**Research date:** 2026-02-22
**Valid until:** 2026-03-22 (stable libraries, 30-day window)

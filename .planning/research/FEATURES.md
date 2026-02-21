# Feature Research

**Domain:** Premium glassmorphism UI redesign — Flutter iOS fintech/crypto portfolio app
**Researched:** 2026-02-21
**Confidence:** MEDIUM-HIGH (NN/g usability guidelines HIGH confidence; blur performance from official Flutter issue tracker HIGH confidence; design pattern specifics MEDIUM confidence from community consensus)

---

## Context

This is a redesign milestone (v5.0), not a greenfield build. All five tabs (Home, Chart, History, Config, Portfolio) already function with generic Material 3 dark styling. The goal is transforming the app into a premium glassmorphism design system with rich animations, gradient glow charts, and a polished typography hierarchy.

**No new API calls. No data model changes. All features are UI-layer only.**

**Existing theme tokens in `lib/app/theme.dart`:**
- `surfaceDark: #121212` — scaffold background
- `navBarDark: #1A1A1A` — nav bar
- `bitcoinOrange: #F7931A` — primary accent
- `profitGreen: #00C087` — gain indicator
- `lossRed: #FF4D4D` — loss indicator

**Existing chart library:** `fl_chart` (already powering line chart and donut chart)

**Existing packages already in use:** Riverpod, go_router, fl_chart, Dio, shared_preferences

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that define "premium glassmorphism" in 2025-era fintech apps. Missing any of these makes the design look half-finished or internally inconsistent.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Frosted glass card widget | The defining visual of glassmorphism; solid cards on dark background look like Material 2 | MEDIUM | `BackdropFilter` + `ImageFilter.blur(sigmaX: 8, sigmaY: 8)` + semi-transparent tint fill + 1px white border at 10-15% opacity. Clip with `ClipRRect(borderRadius: 16)`. Wrap in `RepaintBoundary` to limit GPU repaint scope. Build as a shared `GlassCard` widget used across all screens |
| Dark ambient gradient background | Glass on solid black (`#121212`) is invisible — glass refracts the light behind it | MEDIUM | Replace flat scaffold with a `Stack`: base `#0A0A0F` + 2-3 static `RadialGradient` orbs (e.g., deep blue-purple `#1A1040` and orange-tinted `#3D1800` at ~15% opacity). Static is sufficient — animated backgrounds repaint every frame and destroy scroll performance |
| Consistent corner radius system | Sharp corners break the glass illusion; inconsistent radii look accidental | LOW | Standardize: 16px for cards, 12px for chips/badges/small tags, 24px for bottom sheets and modals, 8px for input fields |
| Thin luminous border on glass cards | The border signals "glass edge" and separates the card surface from the background | LOW | `Border.all(color: Colors.white.withOpacity(0.12), width: 1.0)` applied to all `GlassCard` instances. 1px only — thicker looks fake |
| Shimmer loading skeletons | Generic `CircularProgressIndicator` looks out-of-place in a premium glass UI; skeleton loaders preserve layout during load | MEDIUM | Use `shimmer` package. Skeleton shapes must mirror real card dimensions exactly — not generic grey boxes. Apply to: portfolio summary card, home stats section, purchase history list items, chart loading state |
| Typography hierarchy overhaul | Material 3 Roboto defaults have no visual personality; financial numbers need tabular figures to prevent layout shift | MEDIUM | Use system SF Pro on iOS (via `String fontFamily = '.SF Pro Display'`). Apply `FontFeature.tabularFigures()` to all monetary values via `fontFeatures` property. Size scale: display=32sp/w700, title=20sp/w600, body=15sp/w400, label=13sp/w500, caption=12sp/w400 |
| Gradient fill area under line chart | A flat line on dark background looks empty; gradient fill is standard in every 2025 crypto chart app | MEDIUM | `LineChartBarData.belowBarData: BarAreaData(show: true, gradient: LinearGradient(colors: [bitcoinOrange.withOpacity(0.35), Colors.transparent], begin: Alignment.topCenter, end: Alignment.bottomCenter))`. Add `shadow: Shadow(color: bitcoinOrange.withOpacity(0.4), blurRadius: 8)` on the line itself |
| Animated number counters on balance display | Static number render on load feels cheap; a count-up confirms data arrived and adds polish | LOW | Use `animated_flip_counter` package (implicit animation, supports decimals). Apply to: total portfolio value, BTC balance on home, P&L values. Trigger only on initial screen entry — not on every background poll refresh |
| Bottom nav visual distinction | Generic Material nav indicator looks generic; active tab needs a glow or gradient treatment | LOW | Replace `NavigationBar` selected indicator with a gradient pill: `LinearGradient` from `bitcoinOrange` to transparent. Or use custom `NavigationDestination` with an icon glow effect |

### Differentiators (Competitive Advantage)

Features that push beyond "it has glassmorphism" into a genuinely premium feel that personal crypto tracker apps rarely achieve.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Chart draw-in animation on tab entry | The line chart animating left-to-right when the tab is opened creates a "data arriving" moment — charts that just appear feel dead | MEDIUM | fl_chart supports `swapAnimationDuration` + `swapAnimationCurve`. Implement: start with empty `spots` list, delay 100ms after `initState`, then `setState` to full data. Use `Curves.easeOut`, ~800ms duration. Repeat on explicit refresh, not on background polls |
| Staggered card entrance on screen load | Cards cascading in (40-60ms offset per item) makes a dashboard feel curated rather than dumped onto screen simultaneously | MEDIUM | `flutter_staggered_animations` package: wrap each card in `AnimationConfiguration.staggeredList(position: index, child: SlideAnimation(verticalOffset: 24, child: FadeInAnimation(child: card)))`. Trigger only on initial load — not on pull-to-refresh, which would be annoying |
| Press/scale micro-interaction on tappable cards | Cards that physically depress on touch give physical feedback that glass cards need to feel interactive rather than decorative | LOW | `GestureDetector` wrapping each tappable card: `onTapDown` → `AnimatedScale(scale: 0.97, duration: 80ms, curve: Curves.easeIn)`, `onTapUp/Cancel` → scale back to 1.0 with 120ms easeOut. Combine with `HapticFeedback.lightImpact()` on `onTap` |
| Premium frosted-glass tooltip on chart touch | A floating glass-style tooltip showing date + price + BTC amount elevates the chart from "chart from a tutorial" to "something polished" | MEDIUM | Customize `LineChartData.lineTouchData.touchTooltipData`: set `tooltipBgColor` to `Colors.white.withOpacity(0.12)`, add `tooltipBorder: BorderSide(color: Colors.white24)`, `tooltipRoundedRadius: 12`. Format numbers with tabular font in tooltip text |
| Glow dot on purchase markers in chart | Purchase event dots on the price chart with an orange radial glow draw attention to DCA buy events | MEDIUM | Override `FlDotData.getDotPainter` to return a custom `FlDotCirclePainter` with `strokeWidth: 2`, `color: Colors.white`, plus a `BoxShadow`-equivalent via Canvas `drawCircle` with a large blur radius in `bitcoinOrange`. Optionally: slow pulse animation via `AnimationController` + `Curves.easeInOut` loop |
| Currency toggle with slot-flip animation | The existing currency toggle switches value instantly; animating the number change makes it feel premium and intentional | LOW | Wrap value `Text` widgets in `AnimatedSwitcher(duration: 200ms, transitionBuilder: (child, anim) => FadeTransition + SlideTransition(verticalOffset: 0.3))`. Gives a "slot flip" on VND↔USD toggle. Applies to portfolio summary card and all currency-toggle-aware values |
| Colored ambient glow behind allocation donut | The donut chart on a flat glass card looks decorative but flat; a per-segment glow behind it adds dimensionality | LOW | Add `BoxDecoration.boxShadow` with a blurred spread in the dominant asset's color, applied to the `Container` holding the donut chart. Static glow only — no animation needed |
| Tab-to-tab page transitions | Default Material tab swipe looks generic; a subtle fade+scale cross-fade between tabs feels more premium | LOW | Override `go_router` transitions or use `TabController` with `AnimatedSwitcher`. A 150ms fade with slight scale (0.98→1.0) is sufficient — avoids the "sliding newspaper" feel of default swipe |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Full-screen backdrop blur (every screen) | "More glass everywhere = more premium" | Blur cost scales directly with screen area. A full-screen `BackdropFilter` on a scrolling list causes jank on every scroll frame. Flutter issue #32804 confirms this as a documented GPU bottleneck | Apply blur only to discrete, bounded cards/panels. Full-screen blur only for modal overlays and bottom sheets (which are not scrollable list content). Wrap all `BackdropFilter` in `RepaintBoundary` |
| Animated ambient background (moving gradient orbs) | Looks impressive in Dribbble demos and design presentations | Background repaints on every frame consume CPU/GPU continuously; competes with scroll performance; is visually distracting from financial data, which requires user focus | Static radial gradient orbs placed with `Positioned` in a `Stack`. They are visually indistinguishable from animated orbs in normal use. Zero per-frame cost after first paint |
| BackdropFilter blur on every list item | Glass purchase cards look great in design mockups | Nested `BackdropFilter` widgets stack GPU cost multiplicatively. A 50-item history list with blur on each item = 50 concurrent blur rasterization layers. The single most common glassmorphism performance mistake in Flutter | Give list items a semi-transparent flat tint: `Color(0xFFFFFFFF).withOpacity(0.05)` with a thin white border. This is "glass-style" without real blur and is visually close in practice. Reserve real `BackdropFilter` for cards containing summary/headline data only |
| Neon-heavy color palette (hot pink, bright cyan) | "Crypto apps should look cyberpunk" | Neon accents on dark glass destroy text contrast ratios (fail WCAG AA). Financial apps need legible numbers above all else. Doesn't match the existing orange/green/red brand | Use the existing `bitcoinOrange` and `profitGreen`/`lossRed` as accent colors. Deep blue-purple (`#1A1040`) is only for ambient background gradients — never as text color |
| Real-time chart redraw animation on every data poll | "Live charts feel more premium and reactive" | fl_chart fires its implicit animation on every `setState`. If the chart animates on each 5-minute background refresh, the chart is in constant motion while the user is reading it. Disorienting and wastes GPU | Animate only on: (a) initial screen entry, (b) explicit user pull-to-refresh. Track `_isInitialLoad` bool in state. Background polls update data silently; user sees new numbers but no visual disruption |
| Lottie/Rive animations for loading and empty states | Polished loading animations are a hallmark of premium apps | Lottie/Rive files add 200-500KB per animation to bundle size. A personal single-user app has one developer — maintaining animation files adds ongoing cost. Shimmer skeletons deliver 80% of the visual quality at <5KB overhead | Use `shimmer` package for loading states. Reserve Lottie/Rive for at most one "success" confirmation animation if explicitly desired, and only if the asset is <50KB |
| Parallax header on home screen | Popular in dribbble/design showcases; creates 3D depth | Requires reworking scroll architecture significantly — `CustomScrollView` + `SliverAppBar` + custom physics. High effort relative to payoff for a personal app. Risk of breaking existing Riverpod scroll state management | Use scroll-triggered opacity fade on the hero balance section instead (FAR simpler: `ScrollController` listener + `Opacity` widget). Gives subtle depth without architectural changes |

---

## Feature Dependencies

```
[Ambient Gradient Background]
    └──required-by──> [Frosted Glass Cards]  (glass needs light to refract)
                          └──required-by──> [Press Micro-interaction]
                          └──required-by──> [Staggered Entrance Animation]
                          └──required-by──> [Shimmer Skeletons] (skeletons must match card shape)

[Typography Overhaul]
    └──required-by──> [Animated Flip Counters]  (counter wraps a Text; font must be set first)
    └──required-by──> [Premium Chart Tooltip]   (tooltip text uses same font scale)

[Gradient Glow Line Chart]
    └──enhanced-by──> [Chart Draw-in Animation]
    └──enhanced-by──> [Glow Purchase Marker Dots]
    └──enhanced-by──> [Premium Glass Chart Tooltip]

[Press Micro-interaction]
    └──enhanced-by──> [HapticFeedback.lightImpact()]  (native iOS haptic API, no package needed)

[Currency Toggle Animation]
    └──depends-on──> existing currency toggle widget (already built in v4.0)
    └──no new dependencies──> AnimatedSwitcher is Flutter built-in

[Staggered Entrance Animation]
    └──requires──> flutter_staggered_animations package
    └──depends-on──> final card layouts being stable  (skeleton shapes must mirror these)
```

### Dependency Notes

- **Background gradient must be built first.** Every other glass feature depends on having colored light behind cards. The `GlassCard` widget's backdrop blur effect is invisible on a solid black surface.
- **`GlassCard` widget is the foundation.** Build it as a single shared component with blur, tint, border, and corner radius baked in. All screens adopt it. Without this shared widget, inconsistency will appear across tabs.
- **Typography before animated counters.** The flip counter animation wraps a `Text` widget. If font and size are changed after the counter is implemented, it needs re-tuning.
- **Gradient chart fill before chart animations.** A draw-in animation on a flat line is underwhelming. The gradient fill is what makes the animation impactful — implement fill first, add draw-in animation second.
- **Shimmer shapes depend on final card layouts.** Build cards first, then create skeletons that match. If done in reverse, skeletons will need rework.

---

## MVP Definition

This is a redesign milestone — the "MVP" is the minimum needed for the app to feel genuinely premium rather than like Material 3 with a dark theme.

### Launch With (v5.0 core — the foundation that makes everything cohesive)

Missing any of these leaves a visible seam — some screens look premium and others look unfinished.

- [ ] **`GlassCard` shared widget** — The frosted glass card component (BackdropFilter, tint, border, radius) that every screen will use. This is the design system primitive everything else builds on.
- [ ] **Ambient gradient background system** — Replace flat `#121212` scaffold with a deep dark base + static colored gradient orbs. Required before glass cards look correct.
- [ ] **Typography overhaul across all 5 tabs** — System SF Pro font, explicit size/weight scale, tabular figures on all monetary values. Low effort, highest visible impact-per-hour of any single feature.
- [ ] **Gradient glow line chart** — Area gradient fill + line shadow on the BTC price chart. fl_chart is already in use; this is a data parameter change, not an architectural change.
- [ ] **Shimmer loading skeletons** — Replace all `CircularProgressIndicator` instances with shimmer skeletons shaped to match real content. Keeps the premium feel even during slow loads.
- [ ] **Press micro-interaction + haptics** — `AnimatedScale` depress effect + `HapticFeedback.lightImpact()` on all tappable cards. Low cost, and makes every interaction feel physical rather than digital.

### Add After Foundation (v5.0 polish — elevate the foundation)

Once the six core items are done and visually consistent across all tabs:

- [ ] **Staggered card entrance animation** — Add to home and portfolio screens; trigger only on initial load.
- [ ] **Animated flip counters** — Apply to total portfolio value and BTC balance on home screen.
- [ ] **Chart draw-in animation** — Trigger on Chart tab entry; fl_chart `swapAnimationDuration` with initial empty→full data state.
- [ ] **Glow purchase marker dots** — Custom `FlDotPainter` with orange shadow on chart tab.
- [ ] **Currency toggle slot-flip animation** — `AnimatedSwitcher` with vertical slide on portfolio value labels.
- [ ] **Colored donut glow** — `BoxShadow` behind allocation donut chart.
- [ ] **Premium glass chart tooltip** — Custom glass-styled `LineTouchTooltipData`.
- [ ] **Tab transition animation** — Subtle fade+scale between tabs (override go_router or TabController).

### Future Consideration (v5.x or later)

- [ ] **Scroll-triggered opacity fade on home hero balance** — Simpler alternative to parallax that gives depth without architectural changes. Evaluate when foundation is stable.
- [ ] **Pulsing glow dot animation on purchase markers** — Requires per-dot `AnimationController`; adds significant complexity. Only if explicitly wanted after v5.0 ships.
- [ ] **Glass bottom nav bar** — `BackdropFilter` on nav bar affects every tab transition. Evaluate performance on real iPhone device before committing. May cause perceptible lag.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Ambient gradient background | HIGH | LOW | P1 |
| `GlassCard` shared widget | HIGH | MEDIUM | P1 |
| Typography overhaul + tabular figures | HIGH | LOW | P1 |
| Gradient glow line chart | HIGH | LOW | P1 |
| Shimmer loading skeletons | HIGH | MEDIUM | P1 |
| Press micro-interaction + haptics | HIGH | LOW | P1 |
| Staggered card entrance animation | MEDIUM | LOW | P2 |
| Animated flip counter (balance) | MEDIUM | LOW | P2 |
| Chart draw-in animation | MEDIUM | LOW | P2 |
| Glow purchase marker dots | MEDIUM | MEDIUM | P2 |
| Currency toggle slot-flip animation | LOW | LOW | P2 |
| Colored donut ambient glow | LOW | LOW | P2 |
| Premium glass chart tooltip | MEDIUM | MEDIUM | P2 |
| Tab-to-tab page transition | MEDIUM | LOW | P2 |
| Scroll-triggered hero opacity fade | LOW | LOW | P3 |
| Pulsing glow dot animation | LOW | HIGH | P3 |
| Glass bottom nav bar | MEDIUM | HIGH | P3 |

**Priority key:**
- P1: Required for v5.0 — without these the app still looks generic
- P2: Polish that elevates the P1 foundation into something memorable
- P3: Nice to have; only if time and device performance allow

---

## Competitor Feature Analysis

| Feature | Robinhood iOS | Delta (crypto tracker) | Our Approach |
|---------|---------------|------------------------|--------------|
| Chart area fill | Gradient fill, color-coded green/red by position | Strong gradient fill with asset color | Gradient fill in `bitcoinOrange`; existing avg cost line colors buy-below vs buy-above |
| Card treatment | Clean flat cards; no glass | Some glass in header hero; flat in lists | Glass (`GlassCard`) for summary and stat cards; flat-glass-style (semi-transparent, no blur) for list items |
| Loading states | Shimmer skeleton matching exact card shape | Shimmer skeleton | Shimmer skeleton mirroring `GlassCard` shape |
| Typography | SF Pro Display; tabular numbers throughout | Custom display font; tabular numbers | System SF Pro (iOS-native); `FontFeature.tabularFigures()` on monetary values |
| Haptics | Light impact on trade confirm actions | Minimal | Light impact on all tappable cards; medium on actions (currency toggle, refresh) |
| Entrance animations | Subtle fade; no stagger | Staggered list entrance | Staggered 40-60ms offset on home and portfolio initial load only |
| Background | Flat dark; subtle gradients on card elements | Dark with soft color accents | Deep dark base + static ambient gradient orbs |
| Chart interactivity | Custom premium tooltip with share CTA | Tooltip with price and change % | Glass-style tooltip with date, price, BTC amount |

---

## Technical Constraints for This Milestone

- **`BackdropFilter` limit per screen:** Target no more than 3 concurrent `BackdropFilter` instances on any single screen. More than this risks perceptible frame drops on older iPhones.
- **List items must not use `BackdropFilter`.** Use semi-transparent flat color for list items. Glass styling via tint + border only.
- **Background gradient must be static.** No animation on the gradient background — this is the most common source of glassmorphism performance problems.
- **Test blur on device, not simulator.** iOS Simulator does not accurately represent `BackdropFilter` GPU cost. Performance must be validated on a real iPhone.
- **Blur sigma range: 6-12.** Values below 6 look under-blurred (glass looks smudgy); above 15 increases GPU cost significantly with diminishing visual return.

---

## Sources

- [NN/g Glassmorphism usability guidelines](https://www.nngroup.com/articles/glassmorphism/) — HIGH confidence; authoritative UX research on contrast requirements and when glassmorphism fails
- [Flutter BackdropFilter performance issue #32804](https://github.com/flutter/flutter/issues/32804) — HIGH confidence; official Flutter issue tracker confirming nested BackdropFilter GPU cost
- [Flutter shimmer loading cookbook](https://docs.flutter.dev/cookbook/effects/shimmer-loading) — HIGH confidence; official Flutter docs
- [Flutter staggered animations docs](https://docs.flutter.dev/ui/animations/staggered-animations) — HIGH confidence; official Flutter docs
- [Flutter hero animations docs](https://docs.flutter.dev/ui/animations/hero-animations) — HIGH confidence; official Flutter docs
- [fl_chart pub.dev](https://pub.dev/packages/fl_chart) — HIGH confidence; official package page confirming `swapAnimationDuration`, `belowBarData`, `LineTouchTooltipData` APIs
- [animated_flip_counter pub.dev](https://pub.dev/packages/animated_flip_counter) — MEDIUM confidence; community package, actively maintained
- [flutter_staggered_animations pub.dev](https://pub.dev/documentation/flutter_staggered_animations/latest/) — MEDIUM confidence; community package, widely used
- [Flutter BackdropFilter optimization](https://trushitkasodiary.medium.com/flutter-backdrop-filter-optimization-improve-ui-performance-81746bc1fd55) — MEDIUM confidence; community-verified blur sigma and RepaintBoundary patterns
- [Dark glassmorphism design patterns 2026](https://medium.com/@developer_89726/dark-glassmorphism-the-aesthetic-that-will-define-ui-in-2026-93aa4153088f) — MEDIUM confidence; design community consensus on ambient gradient orb requirement
- [Glassmorphism accessibility analysis](https://axesslab.com/glassmorphism-meets-accessibility-can-frosted-glass-be-inclusive/) — MEDIUM confidence; accessibility-focused analysis confirming contrast failure risks for text on glass
- [Fintech typography best practices — Smashing Magazine](https://www.smashingmagazine.com/2023/10/choose-typefaces-fintech-products-guide-part1/) — MEDIUM confidence; typography scale and tabular figures guidance for financial UI

---

*Feature research for: v5.0 premium glassmorphism Flutter iOS UI redesign*
*Researched: 2026-02-21*

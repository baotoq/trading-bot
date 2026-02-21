# Pitfalls Research

**Domain:** Premium glassmorphism Flutter UI redesign added to existing production iOS app
**Researched:** 2026-02-21
**Confidence:** HIGH for BackdropFilter/Impeller performance (GitHub issues + official tracker confirmed), HIGH for animation lifecycle (official Flutter docs), MEDIUM for design system migration (multiple community sources), HIGH for accessibility (WCAG standards + Flutter a11y docs)

---

## Critical Pitfalls

### Pitfall 1: BackdropFilter on iOS with Impeller Causes Frame Drops in Scrollable Lists

**What goes wrong:**
When `BackdropFilter(filter: ImageFilter.blur(...))` is placed inside scrolling widgets (ListView, CustomScrollView, SingleChildScrollView), Impeller — Flutter's current rendering backend on iOS — exhibits significantly degraded performance compared to the legacy Skia backend. Measured benchmarks show Impeller averaging 16ms/frame (raster thread) vs Skia's 6ms/frame, with max spikes to 24ms. Any frame above 16.67ms causes a visible dropped frame at 60fps.

This is not a theoretical concern: multiple open GitHub issues confirm the regression (flutter/flutter#161297, #126353, #138615, #139449), and the regression is pronounced specifically when BackdropFilter is inside scrolling containers. The History tab and Portfolio tab in this app both have scrollable lists where glass card effects would naturally appear.

**Why it happens:**
Impeller must re-render everything beneath the blur region on every frame when the content scrolls — it cannot reuse the previous frame's blur result because the underlying content changed. Skia had a more aggressive layer caching strategy for this case. The Impeller team is actively working on fixes but as of early 2026 the issue remains partially unresolved.

**How to avoid:**
1. Never place `BackdropFilter` inside a `ListView` or `CustomScrollView` unless the card is stationary (pinned header). Use non-blurring glass alternatives for scrolling list items: gradient overlays with opacity, `Container` with `BoxDecoration` gradient + border instead of actual blur.
2. For non-scrolling glass surfaces (modal sheets, the fixed home dashboard header), `BackdropFilter` is acceptable but must be bounded: wrap in `ClipRRect` to limit the blur region to the card boundary, not the full screen.
3. Use `BackdropGroup` (Flutter 3.x) to batch multiple backdrop filters into a single rendering operation — this reduces overhead when multiple glass elements are visible simultaneously on a static screen.
4. Use `RepaintBoundary` around static glass layers to prevent cascading repaints when animated children update.
5. Prefer `sigma` values of 10–20. Values of 40+ cause Impeller artifacts (colors "jiggle" when content scrolls beneath the blur — flutter/flutter#143947).
6. If glass on scroll is essential, consider pre-blurring a static screenshot of the background and using it as an `Image` widget — fake blur, but zero runtime cost.

**Warning signs:**
- Frame time in Flutter DevTools Performance tab exceeds 16ms on the raster thread while scrolling History or Portfolio
- `debugRepaintRainbowEnabled = true` shows the entire screen repainting on every scroll frame
- `BackdropFilter` widget appears inside a `ListView.builder` builder function
- Blur sigma > 30 used anywhere

**Phase to address:** Design system foundation phase (first phase). The glass card component must be designed from the start to use non-blur alternatives in scrolling contexts. Retrofitting is expensive — every card would need to be reworked.

---

### Pitfall 2: AnimationController Leak from Insufficient Disposal in Multi-Screen App

**What goes wrong:**
Adding rich animations (page transitions, animated counters, shimmer loading, chart draw-in) across 5 tabs means many `AnimationController` instances are created. Each controller holds a `Ticker` that fires 60 times per second until explicitly disposed. In this Riverpod-based app, animations defined inside `ConsumerStatefulWidget.State` objects are safe if `dispose()` is called — but animations defined inside `ConsumerWidget` (stateless) with `SingleTickerProviderStateMixin` are a common mistake: `ConsumerWidget` is immutable and does not have a `dispose()` lifecycle, so the ticker runs indefinitely.

Additionally: using `ref.listen()` or `ref.watch()` to trigger animation controllers without guarding against `mounted` state causes "setState called after dispose" exceptions when fast navigation happens (user switches tabs before an async operation completes).

**Why it happens:**
Developers add `with SingleTickerProviderStateMixin` to a `ConsumerStatefulWidget` correctly, but then extract a sub-widget into a `ConsumerWidget` for cleanliness and move the controller there without converting to `ConsumerStatefulWidget`. The app appears to work in dev (controller gets GC'd eventually) but causes memory leaks and battery drain in production.

**How to avoid:**
1. All `AnimationController` instances must live in `ConsumerStatefulWidget` (with `State` class), never in `ConsumerWidget`.
2. For simple implicit animations (fade, slide, scale on value change), use Flutter's built-in implicit animation widgets (`AnimatedOpacity`, `AnimatedContainer`, `AnimatedSlide`, `TweenAnimationBuilder`) which manage their own lifecycle — no controller needed.
3. For the chart draw-in animation (custom `AnimationController` driving `fl_chart`), keep the controller in the `ChartScreen`'s `State` class and dispose in `dispose()`.
4. Before calling `animationController.forward()` or `setState()` in async callbacks, check `if (!mounted) return;`.
5. Use `TickerProviderStateMixin` (plural) only when a single State class genuinely needs multiple controllers; otherwise use `SingleTickerProviderStateMixin`.

**Warning signs:**
- Flutter DevTools Memory tab shows growing object counts for `AnimationController` or `Ticker` over time
- "setState called on a disposed widget" exceptions in logs on fast tab switching
- `ConsumerWidget` (stateless) class has `AnimationController` as a field
- No `dispose()` override in a `State` class that has any `AnimationController`

**Phase to address:** Animation system phase. Establish the rule once, enforce it via code review for every animated widget added.

---

### Pitfall 3: Business Logic Broken by Theme Refactor (Implicit Theme Dependency)

**What goes wrong:**
The existing ~5,500 lines of Dart use `Theme.of(context)` throughout for colors, text styles, and spacing. A glassmorphism redesign typically introduces a custom design token system alongside or replacing `ThemeData`. The pitfall: during migration, widgets that previously relied on `Theme.of(context).colorScheme.primary` (which happened to equal `bitcoinOrange`) stop using the theme entirely when someone hardcodes colors in the new design system, breaking dark mode consistency and any platform brightness detection.

More insidiously: the `AppTheme` class currently defines constants (`bitcoinOrange`, `profitGreen`, `lossRed`). If these are duplicated into a new design token file without removing the originals, two sources of truth exist. A developer fixing a color in one place misses the other — color inconsistencies appear on some screens but not others.

A second failure mode specific to this app: the P&L color logic (`profitGreen` for positive, `lossRed` for negative) is not just cosmetic — users rely on it to read financial data at a glance. If the redesign's new gradient/glow palette changes these semantic colors to "look better," it becomes a functional regression.

**Why it happens:**
Visual redesigns are done screen-by-screen. Each designer/developer independently picks colors that "look right" in their screen's context without checking the global token. The original `AppTheme` class is never formally deprecated, so it remains imported and used in half the screens while the new token system is used in the other half.

**How to avoid:**
1. Before adding any new design tokens, do a single refactor: consolidate ALL color/spacing references to a single `AppTheme` source. Delete duplicate color definitions. This is a prerequisite, not optional.
2. Introduce the glassmorphism design system via Flutter's `ThemeExtension` mechanism — it integrates with `Theme.of(context)` and participates in `Theme.lerp()` for transitions, maintaining single source of truth.
3. Keep semantic color names (`profitGreen`, `lossRed`) as non-negotiable constants that the glassmorphism theme MUST preserve. Never replace them with gradient colors for financial P&L indicators.
4. The migration order is: (a) centralize existing tokens → (b) add new tokens as extensions → (c) migrate screens one by one → (d) delete old `AppTheme` class. Never (b) and (c) in parallel across multiple screens simultaneously.

**Warning signs:**
- `AppTheme.bitcoinOrange` and a new `GlassTokens.accentOrange` both defined with different values
- A screen shows the wrong P&L color (e.g., green for negative values) after redesign
- `Theme.of(context)` and raw `Color(0xFF...)` hardcodes appear in the same file
- Unit tests for color logic pass but visual inspection shows wrong color on a specific screen

**Phase to address:** Design system foundation phase. Token consolidation must be the first commit of the milestone, before any visual changes.

---

### Pitfall 4: Over-Animation on Financial Data Screens Creates Cognitive Load

**What goes wrong:**
Applying animations enthusiastically to every data element on financial screens (animated number counters on every value, staggered list entries on every load, chart redraw animation on every data refresh, parallax scroll on the home screen) creates an experience that feels impressive in demos but exhausting in daily use. Users who open the app multiple times per day to check their portfolio value do not want to wait 600ms for the balance counter to tick up from $0 before they can read it.

Specific failure modes observed in financial apps:
- Animated counters that interpolate through intermediate values (e.g., $0 → $45,123) display nonsense financial data during the animation — a momentary $23,456 is meaningless and could be misread
- Charts that redraw from scratch on every pull-to-refresh make the user think the data changed, creating unnecessary anxiety
- Shimmer loading on all elements including ones that load instantly from cache creates flicker on fast network conditions

**Why it happens:**
The goal of the milestone is "premium feel," so developers add animations to demonstrate effort. The distinction between "on first load" animations (acceptable) and "on every interaction" animations (harmful for daily apps) is not established upfront.

**How to avoid:**
1. Apply a strict rule: animations run ONCE on first screen load, then are suppressed on subsequent navigations back to the same tab (track `_hasAnimated` bool in State, or use a Riverpod `StateProvider<bool>` per tab).
2. Animated number counters should only animate on the initial load and on significant data changes (a new DCA purchase was made today). Use a threshold: only animate if the value changed by more than 0.5%.
3. Chart draw-in animation plays once per app session, not per data fetch. On pull-to-refresh, update data in place without re-running the draw animation.
4. Shimmer loading should only appear if the load takes longer than 200ms — use a `FutureBuilder` with an initial delay before showing shimmer to avoid flash-of-skeleton on cache hits.
5. Parallax effects: limit to the home screen header only, and ensure the parallax offset is subtle (max 10-15px travel) — aggressive parallax on financial data cards looks playful, not premium.

**Warning signs:**
- The animated counter animates every time the user opens the Portfolio tab (not just first load)
- Chart redraws with full draw-in animation on every pull-to-refresh
- Shimmer appears briefly then immediately disappears when data loads from cache (< 100ms)
- Multiple animations playing simultaneously on the same screen (counter + chart draw + shimmer all at once)

**Phase to address:** Animation guidelines established in the design system phase, enforced in every subsequent screen implementation phase.

---

### Pitfall 5: Glassmorphism Fails WCAG Contrast Ratios on Financial Data

**What goes wrong:**
Glassmorphism's aesthetic depends on low-opacity backgrounds, blurred content, and subtle borders. These properties are fundamentally in conflict with WCAG 2.2 contrast requirements: body text requires 4.5:1 contrast ratio against its background; UI element labels require 3:1. A frosted glass card with 30% white overlay on a dark background creates an unpredictable background luminance depending on what is visible behind the glass — text contrast cannot be statically computed or guaranteed.

For a financial app, this has real consequences: the user needs to reliably read P&L percentages, portfolio values, and DCA multiplier tiers. A "premium" design that makes "+2.3%" illegible in direct sunlight or for users with low vision is a functional failure, not just an aesthetic one.

**Why it happens:**
Designers test glassmorphism on their development device in a controlled lighting environment against a static dark background. The dynamic nature of what appears beneath the glass — a gradient, a chart, a colored card — is not tested. The aesthetic looks correct in Figma; the implementation varies at runtime.

**How to avoid:**
1. Apply a minimum blur overlay tint to every glass card: a `Color(0xFF1A1A1A).withOpacity(0.60)` layer on top of the blur ensures the background contribution to text contrast is bounded. This sacrifices some transparency but is non-negotiable for readability.
2. Text on glass must NEVER be set in a color lighter than `Colors.white.withOpacity(0.87)` (Material's `highEmphasis` on dark). Avoid semi-transparent white text (e.g., `Colors.white54`) for primary financial values.
3. Semantic colors for P&L (`profitGreen`, `lossRed`) must pass 4.5:1 against the glass surface. Verify with the Flutter accessibility audit tool or the Accessibility Insights app on device.
4. Honor `MediaQuery.of(context).accessibilityFeatures.reduceTransparency` — when iOS Reduce Transparency is enabled, replace `BackdropFilter` with a fully opaque fallback surface (`Color(0xFF1E1E1E)`).
5. Honor `MediaQuery.of(context).disableAnimations` — skip all animations when the user has enabled Reduce Motion in iOS Accessibility settings.

**Warning signs:**
- Glass card text uses `Colors.white.withOpacity(0.6)` or lighter for primary values
- `MediaQuery.of(context).accessibilityFeatures` is never checked anywhere in the codebase
- No check for `MediaQuery.of(context).disableAnimations` in any animation trigger
- P&L text is green/red but the specific shade has not been checked against the glass background luminance
- The glassmorphism implementation looks fine on simulator but is hard to read in a screenshot taken outdoors

**Phase to address:** Design system foundation phase. Token definitions must include minimum-contrast-compliant values from day one.

---

### Pitfall 6: fl_chart Gradient Glow Requiring CustomPainter Causes shouldRepaint Thrash

**What goes wrong:**
The existing `PriceLineChart` uses `fl_chart` which internally paints with a `CustomPainter`. Adding gradient glow effects (e.g., a luminous line with a blurred shadow glow beneath the price curve) requires either extending fl_chart's configuration or wrapping the chart in a custom `CustomPainter`. The pitfall: a poorly implemented `shouldRepaint()` override that always returns `true` causes the chart to repaint on every frame — even when no data changed — consuming significant GPU time on a screen that also has backdrop blurs.

A second issue: reusing the chart gradient glow with Riverpod means the chart widget rebuilds every time any provider it watches changes. If the chart watches both the price data AND a UI state provider (e.g., the selected timeframe, or a tooltip visibility state), a tooltip interaction triggers a full chart repaint including gradient recalculation.

**Why it happens:**
`shouldRepaint()` is easy to stub as `return true;` during development because it "works." The performance cost is invisible in small charts but becomes a bottleneck when combined with the other visual effects on the chart screen.

**How to avoid:**
1. For glow effects, prefer `BoxDecoration` with `BoxShadow` (spread + blur) over `CustomPainter` when possible — Flutter composites shadows natively without custom repaint logic.
2. If a `CustomPainter` is required for the glow, implement `shouldRepaint()` to compare the previous and new painter's data fields: only return `true` if `oldDelegate.data != data || oldDelegate.glowColor != glowColor`.
3. Separate the chart from its glow layer using a `Stack`: the chart widget on top, a `CustomPaint` glow layer below. The glow layer repaints only when the price data changes; the chart overlay (tooltips, markers) repaints on interaction.
4. Wrap the entire chart in `RepaintBoundary` to isolate it from the rest of the screen's repaint cycle.
5. Precompute `Paint` objects (gradient shader, glow shadow) as `late final` fields on the painter — never create `Paint` or `LinearGradient` inside `paint()`.

**Warning signs:**
- `shouldRepaint()` returns `true` unconditionally
- `Paint()` or `LinearGradient()` constructed inside the `paint(value, canvas, size)` method body
- Flutter DevTools Rendering tab shows the chart canvas repainting at 60fps even when the screen is idle
- `PriceLineChart` is watching a Riverpod provider that changes on tooltip touch events

**Phase to address:** Chart redesign phase. The `CustomPainter` architecture must be designed before glow effects are added.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| `BackdropFilter` in `ListView` items | True glassmorphism effect on scroll | Impeller frame drops; 16ms+ raster times; requires Impeller disable to fix | Never in scrolling lists; use gradient overlay alternative |
| Hardcode `Color(0xFF...)` in new screens instead of design tokens | Faster per-screen development | Two sources of truth; color fixes missed on some screens; inconsistent glass tint | Never after design system is established |
| Return `true` from `shouldRepaint()` | No bugs from stale paint | Chart GPU thrash on every frame; compound with BackdropFilter = guaranteed jank | Only during initial prototype, must be fixed before visual polish phase |
| Animate numbers on every rebuild | "Impressive" in dev demo | Animations play mid-session on every tab switch; UX fatigue for daily users | Never; use `_hasAnimated` guard |
| Skip `reduceTransparency` / `disableAnimations` check | Fewer conditional branches | Accessibility failure; Apple App Store review can reject for accessibility non-compliance | Never |
| Duplicate `AppTheme` constants in new token file | Avoid touching existing code | Color drift between old and new screens; P&L semantic colors get inconsistent | Never; consolidate first |

---

## Integration Gotchas

Common mistakes when wiring the new design system into the existing Riverpod + go_router app.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| go_router custom page transitions | Add `CustomTransitionPage` to every route independently with different durations | Define one `defaultTransition` in the router configuration; override per-route only for screens that need different behavior |
| Riverpod + AnimationController | Watch a provider inside a `ConsumerWidget` and start an animation in `ref.listen()` without `mounted` guard | Use `ConsumerStatefulWidget`; guard async animation starts with `if (!mounted) return;` |
| Shimmer with Riverpod `AsyncValue` | Show shimmer in `loading` state including on cache-hit refreshes | Gate shimmer on `value.isLoading && !value.hasValue` (first load only), not on `isLoading` alone (which also fires on background refresh) |
| fl_chart with redesigned gradient | Add gradient to `belowBarData` — existing code already has this — and add glow on top | Wrap existing `PriceLineChart` in a `DecoratedBox` with `BoxShadow` glow before introducing `CustomPainter` complexity |
| `AppTheme` constants vs new glass tokens | Import both `AppTheme` and `GlassTokens` in the same file | Migrate the file fully; remove the old import. Never have both in the same widget file. |
| iOS `Info.plist` Impeller flag | Disable Impeller globally to fix BackdropFilter performance | Keep Impeller enabled; fix the architecture (no blur in scroll) instead of disabling the renderer |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| `BackdropFilter` in scrolling list | Raster thread >16ms while scrolling History/Portfolio tab | Replace with gradient overlay for scrollable cards | Immediately on any mid-range iPhone (iPhone 11 or earlier) |
| `shouldRepaint` always true on chart | GPU usage elevated even on idle chart screen | Implement proper equality check in `shouldRepaint` | Immediately when chart screen is visible alongside backdrop blurs |
| Multiple simultaneous `AnimationController.repeat()` | Battery drain; device warm in pocket | Only run looping animations when screen is visible; stop in `dispose()` or when app goes to background (use `WidgetsBindingObserver`) | With 3+ looping animations active simultaneously |
| Shimmer on cached data (< 100ms load) | Brief skeleton flash on every load; feels broken | Gate shimmer with 200ms delay via `Future.delayed` | On every load with warm Riverpod cache |
| Creating `Paint`/`Gradient` inside `paint()` method | Chart paint method allocates new objects at 60fps | Pre-create as `late final` painter fields | Immediately; causes GC pressure at 60fps |
| Page transition animations in deeply nested `ShellRoute` | Transitions don't play correctly inside shell routes | Test transitions in the actual ShellRoute context, not a simple push test | go_router ShellRoute with nested navigation |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Glass effect makes primary financial values (balance, P&L) harder to read | User cannot quickly read their portfolio value; purpose of the app is defeated | Apply minimum 60% opaque tint overlay on glass before any text; treat readability as non-negotiable |
| Animated counter interpolates through intermediate values ($0 → $45,123) | User momentarily sees incorrect financial data; could cause alarm or misread | Animate opacity (fade in) or scale, not the numeric value; or animate only from previous known value, not zero |
| All 5 tabs receive equal animation treatment regardless of use frequency | Home and Portfolio tabs (opened 10x/day) feel slow; Config tab (opened weekly) over-engineered | Apply rich animations to the 2 high-frequency tabs; keep Config and History tabs minimal |
| Parallax scroll on financial data | Data appears unstable/moving; bad on small screens | Reserve parallax for decorative backgrounds only; all financial data must be on a stationary layer |
| Gradient glow chart looks premium in static screenshot but distorts during draw-in animation | Chart feels unpolished during animation; users see gradient banding | Test the draw-in animation at 0.5x speed in DevTools; ensure glow gradient does not produce visible banding during interpolation |
| New typography scale makes existing number formatting look wrong | Amounts clip or overflow on smaller iPhone models | Test all formatted values at iPhone SE (375px wide) at the new type scale before finalizing |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Glass cards in History tab:** Card looks correct in static preview — verify it does NOT use `BackdropFilter` (check for any `ImageFilter.blur` in the scrollable list builder)
- [ ] **Animated counters:** Counter animates on first load — verify it does NOT re-animate when navigating away and returning to the tab (check `_hasAnimated` guard exists)
- [ ] **Reduce Transparency:** App looks correct normally — verify glass cards degrade to opaque surface when iOS Reduce Transparency is enabled (`MediaQuery.of(context).accessibilityFeatures.reduceTransparency`)
- [ ] **Reduce Motion:** Animations play correctly normally — verify ALL animations are skipped when iOS Reduce Motion is enabled (`MediaQuery.of(context).disableAnimations`)
- [ ] **P&L color contrast:** Green/red P&L text looks correct on dark background — verify 4.5:1 contrast ratio against the glass card's minimum tint value, not just against solid `#121212`
- [ ] **Chart RepaintBoundary:** Chart looks correct — verify the chart widget is wrapped in `RepaintBoundary` and the raster thread idle time is < 2ms when the screen is static
- [ ] **AnimationController disposal:** Animations work correctly — verify Flutter DevTools Memory tab shows no growing `Ticker` count after navigating between tabs 10 times
- [ ] **Design tokens:** New screens use new glass tokens — verify no screen contains both `AppTheme.bitcoinOrange` import and a new `GlassTokens` reference (single source of truth)
- [ ] **iPhone SE layout:** All screens look correct on simulator — verify on iPhone SE 3rd gen (375pt wide) with new typography scale that no values clip or overflow
- [ ] **Shimmer timing:** Shimmer appears on slow loads — verify shimmer does NOT appear (or flashes < 100ms then disappears) when Riverpod cache is warm

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| BackdropFilter in scrolling lists (frame drops discovered post-ship) | HIGH | Audit every scrollable widget for `BackdropFilter`; replace with gradient overlay card component; this is a partial visual redesign of the affected screens |
| AnimationController leak (memory grows over session) | MEDIUM | Identify leaking controllers via DevTools Memory tab; convert `ConsumerWidget` classes with controllers to `ConsumerStatefulWidget`; add `dispose()` |
| Design token split (AppTheme + GlassTokens both in use) | MEDIUM | Create a single migration PR: search for all `AppTheme` imports, migrate to new tokens, delete `AppTheme` class. Single PR keeps the change reviewable. |
| Contrast failure discovered in accessibility audit | LOW-MEDIUM | Increase overlay tint opacity on glass cards; check all semantic color values against new background luminance. Code-only change, no architecture rework. |
| Over-animation fatigue reported (animations play every tab switch) | LOW | Add `_hasAnimated` guard to each animated screen's State class; one-line fix per screen |
| fl_chart glow shouldRepaint thrash | LOW | Implement proper `shouldRepaint` comparison; pre-create Paint objects as fields. Localized to painter class. |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| BackdropFilter in scrolling lists (#1) | Design system foundation phase — glass card component defined as non-blur for scrollable contexts | DevTools raster thread < 8ms while scrolling History/Portfolio at 60fps |
| AnimationController leak (#2) | Animation system phase — lifecycle rules established before any controller is added | Memory tab shows stable `Ticker` count after 10 tab switch cycles |
| Business logic broken by theme refactor (#3) | Design system foundation phase — token consolidation is the first commit | P&L colors correct on all 5 screens; no `AppTheme` import coexists with new token imports |
| Over-animation on financial screens (#4) | Animation guidelines established in design system phase | Home tab counters do NOT re-animate on second visit within same session |
| Glassmorphism fails WCAG contrast (#5) | Design system foundation phase — tokens defined with minimum tint enforcement | All text on glass cards passes 4.5:1; `reduceTransparency` shows opaque fallback |
| fl_chart shouldRepaint thrash (#6) | Chart redesign phase — painter architecture designed before glow added | Idle raster thread usage < 2ms on chart screen with no interaction |

---

## Sources

- [iOS BackdropFilter Performance Issues with Impeller Engine — flutter/flutter#161297](https://github.com/flutter/flutter/issues/161297) — HIGH confidence, open GitHub issue with reproduction steps
- [Impeller Blur BackdropFilter Performance Degradation — flutter/flutter#126353](https://github.com/flutter/flutter/issues/126353) — HIGH confidence, benchmark data: Impeller 16ms vs Skia 6ms raster average
- [Impeller Janky Scrolling with Backdrop Filter Blur 3.16 — flutter/flutter#138615](https://github.com/flutter/flutter/issues/138615) — HIGH confidence
- [Impeller Backdrop Blurs Bad Performance vs Skia for Small Regions — flutter/flutter#149368](https://github.com/flutter/flutter/issues/149368) — HIGH confidence
- [Impeller Artifacting High Sigma Values — flutter/flutter#143947](https://github.com/flutter/flutter/issues/143947) — HIGH confidence, sigma 40+ causes color jiggle
- [Flutter Glassmorphism UI Design — TheLinuxCode Production Guide](https://thelinuxcode.com/flutter-glassmorphism-ui-design-for-apps-a-practical-production-ready-guide/) — MEDIUM confidence, production best practices
- [Flutter BackdropFilter Optimization — Trushit Kasodiya, Medium](https://trushitkasodiya.medium.com/flutter-backdrop-filter-optimization-improve-ui-performance-81746bc1fd55) — MEDIUM confidence
- [Glassmorphism Meets Accessibility — Axess Lab](https://axesslab.com/glassmorphism-meets-accessibility-can-frosted-glass-be-inclusive/) — HIGH confidence, WCAG contrast analysis for glass effects
- [Flutter Accessibility Docs — docs.flutter.dev](https://docs.flutter.dev/ui/accessibility) — HIGH confidence, official docs on `MediaQuery.disableAnimations` and `reduceTransparency`
- [Flutter RepaintBoundary Official Docs](https://api.flutter.dev/flutter/widgets/RepaintBoundary-class.html) — HIGH confidence
- [AnimationController Official Docs — api.flutter.dev](https://api.flutter.dev/flutter/animation/AnimationController-class.html) — HIGH confidence, lifecycle rules
- [Flutter Rendering Optimization Tips — gskinner blog](https://blog.gskinner.com/archives/2022/09/flutter-rendering-optimization-tips.html) — MEDIUM confidence
- [Building Reusable Design System with ThemeExtension — vibe-studio.ai](https://vibe-studio.ai/insights/building-a-reusable-design-system-in-flutter-with-theme-extensions) — MEDIUM confidence
- [go_router Transition Animations Topic — pub.dev](https://pub.dev/documentation/go_router/latest/topics/Transition%20animations-topic.html) — HIGH confidence, official docs
- [CustomPainter shouldRepaint — api.flutter.dev](https://api.flutter.dev/flutter/rendering/CustomPainter-class.html) — HIGH confidence, official docs
- Existing codebase inspection: `AppTheme`, `PriceLineChart`, `home_screen.dart`, `history_screen.dart`, `app/router.dart` — HIGH confidence

---
*Pitfalls research for: Premium glassmorphism Flutter UI redesign (v5.0 Stunning Mobile UI)*
*Researched: 2026-02-21*

# Project Research Summary

**Project:** TradingBot.Mobile — v5.0 Stunning Mobile UI (Flutter Glassmorphism Redesign)
**Domain:** Premium mobile UI redesign — glassmorphism design system for existing Flutter iOS fintech app
**Researched:** 2026-02-21
**Confidence:** HIGH

## Executive Summary

This milestone is a pure UI-layer redesign of an existing, fully functional Flutter iOS app (5 tabs, all data working) using glassmorphism aesthetics. The approach is additive: no new API calls, no data model changes, no Riverpod provider changes. The stack adds exactly three packages (`flutter_animate`, `google_fonts`, `animations`) plus one new dependency (`skeletonizer`) to the existing Flutter 3.41/Dart 3.11/Riverpod/go_router/flutter_hooks stack. The design is built around a `GlassTheme` ThemeExtension as the single source of design tokens, a shared `GlassCard` primitive widget, and a new `design/` directory that separates the design system from feature code. Glassmorphism itself requires zero third-party packages — `BackdropFilter + ImageFilter.blur + ClipRRect + Container` is the canonical native Flutter pattern, and the previously dominant `glassmorphism` pub package was abandoned in 2021 and must not be used.

The recommended implementation sequence is strict and dependency-ordered: design tokens and `GlassTheme` must be built first because every other component depends on them. The `GlassCard` shared widget and shimmer skeleton follow, then the chart redesign, then the high-frequency tabs (Home, Portfolio), and finally the remaining tabs and navigation shell. This ordering mirrors the feature dependency graph (ambient gradient background is a prerequisite for glass cards; glass cards are a prerequisite for shimmer skeletons) and the 6-phase build order identified by architecture research.

The central risk of the entire milestone is `BackdropFilter` performance on iOS with Impeller: using it inside scrollable lists (History, Portfolio) causes measurable frame drops to 16ms+ on the raster thread, confirmed by multiple open Flutter GitHub issues. The mitigation is architectural and must be decided upfront — glass cards in scrollable lists must use non-blur alternatives (opacity tint + border only), reserving actual `BackdropFilter` for stationary surfaces (home dashboard, modals, app bar). A second systemic risk is over-animation: financial apps opened 10+ times daily require strict animation guards (`_hasAnimated` booleans) to prevent counters and chart draw-ins from replaying on every tab revisit, which would display momentarily incorrect financial values and degrade UX for daily users.

---

## Key Findings

### Recommended Stack

The existing stack (Flutter 3.41, Riverpod, go_router, fl_chart 1.1.1, flutter_hooks via hooks_riverpod) is validated and unchanged. The redesign adds only what is necessary and uses Flutter built-in APIs wherever possible, explicitly avoiding abandoned or low-adoption packages.

`flutter_animate` replaces all animation boilerplate — shimmer, stagger, fade, slide, blur entrance effects are covered by one well-maintained package (gskinner, 4.1k likes). `google_fonts` provides Inter typeface for premium typography without manual font file management. The `animations` package (Flutter team, official) provides Container Transform and Shared Axis page transitions that go_router's `CustomTransitionPage` wraps cleanly. `skeletonizer` wraps existing widget trees to generate shimmer loading skeletons without duplicating layout code. The `BackdropGroup + BackdropFilter.grouped` API (available since Flutter 3.29, present in 3.41) batches multiple blur passes into a single GPU operation and is the key optimization for screens with more than one glass card simultaneously visible.

**Core technologies:**
- `flutter_animate ^4.5.2` — unified animation layer (shimmer, stagger, fade, slide, blur) — replaces AnimationController boilerplate for all effect types; gskinner publisher, actively maintained
- `google_fonts ^8.0.2` — Inter typeface for premium financial UI typography — published 2026-02-19, requires Flutter 3.35+, compatible with 3.41
- `animations ^2.1.1` — Container Transform and Shared Axis page transitions — official Flutter team package, stable
- `skeletonizer` (new dep) — shimmer loading skeletons by wrapping existing widget trees with placeholder data
- `BackdropFilter + ImageFilter.blur` (Flutter SDK) — frosted glass effect — zero packages needed; canonical pattern
- `BackdropGroup + BackdropFilter.grouped` (Flutter 3.29+) — batches multiple backdrop blur passes into one GPU operation — use on any screen with 2+ glass cards
- `TweenAnimationBuilder<double>` (Flutter SDK) — animated number counters — more reliable than any counter package; `countup` abandoned since 2022
- `fl_chart 1.1.1` (existing) — gradient fill and glow chart via built-in `belowBarData.gradient` and `LineChartBarData.gradient` — no chart library change needed
- `useAnimationController` from flutter_hooks (existing, via hooks_riverpod) — explicit animation controller management with auto-dispose; zero new dependency

**Do not add:**
- `glassmorphism` pub package — not updated since August 2021, 522 likes, unverified publisher
- `countup` package — last published March 2022, 130 likes, effectively abandoned
- `shimmer` package as standalone — `flutter_animate` already includes `ShimmerEffect`; adding `shimmer` duplicates the capability

### Expected Features

This is a redesign milestone. Every feature is UI-layer only. Features are classified by whether the app looks "finished" without them.

**Must have — v5.0 core (app looks generic without these; missing any creates visible seam across tabs):**
- `GlassCard` shared widget — the frosted glass surface (`BackdropFilter` + tint + border + corner radius) that every screen builds on; prerequisite for all other glass effects
- Ambient gradient background — replace flat `#121212` scaffold with deep dark base + 2-3 static radial gradient orbs; glass is invisible on solid black; this is a required prerequisite for glass cards
- Typography overhaul across all 5 tabs — Inter (via `google_fonts`) or system SF Pro on iOS, explicit size/weight scale, `FontFeature.tabularFigures()` on all monetary values
- Gradient glow line chart — `fl_chart` `belowBarData.gradient` + `BoxShadow` glow on container wrapping chart
- Shimmer loading skeletons — replace all `CircularProgressIndicator` with skeletons shaped to match real card dimensions
- Press micro-interaction + haptics — `AnimatedScale(0.97)` on tap-down + `HapticFeedback.lightImpact()` on all tappable cards

**Should have — v5.0 polish (elevates foundation into something memorable):**
- Staggered card entrance animation (40-60ms offset per item, initial load only — not on pull-to-refresh)
- Animated flip counters on hero balance and P&L values
- Chart draw-in animation (left-to-right reveal on Chart tab entry via data slicing)
- Glow purchase marker dots on chart (custom `FlDotPainter` with orange shadow)
- Currency toggle slot-flip animation (`AnimatedSwitcher` with vertical slide)
- Premium glass chart tooltip (custom `LineTouchTooltipData` with glass background)
- Tab-to-tab page transition (fade + scale, 150ms, via `CustomTransitionPage`)
- Colored ambient glow behind allocation donut chart (`BoxShadow` in dominant asset color)

**Defer — v5.x or later:**
- Glass bottom nav bar — `BackdropFilter` on full-width nav affects every tab transition; requires device performance testing before committing; fallback is opacity-tint-only nav
- Pulsing glow dot animation — per-dot `AnimationController` loop adds significant complexity for minimal payoff
- Scroll-triggered hero opacity fade — simpler parallax alternative; evaluate when foundation is stable

**Anti-features to explicitly reject:**
- `BackdropFilter` on every list item in History/Portfolio — confirmed GPU bottleneck by multiple Flutter GitHub issues; use opacity tint + border only for scrollable list items
- Animated ambient background (moving gradient orbs) — full-screen repaints on every frame; use static positioned orbs instead
- Neon-heavy color palette — fails WCAG contrast on financial data; use existing `bitcoinOrange` / `profitGreen` / `lossRed`
- Lottie/Rive animations for loading states — 200-500KB bundle cost for marginal gain over shimmer skeletons

### Architecture Approach

The architecture introduces a new `design/` top-level directory as the bounded scope for the entire design system — deliberately separate from `app/` (router/theme entry points), `core/`, `features/`, and `shared/`. It is organized in three sub-layers: `design/tokens/` (pure Dart constants: colors, spacing, typography — no Flutter widget imports, importable anywhere without circular dependencies), `design/theme/` (`GlassTheme` as `ThemeExtension` registered in `AppTheme.dark`, accessed via `Theme.of(context).glass.*`), and `design/widgets/` (shared glass primitives used across 2+ features: GlassCard, GlassAppBar, GlassBottomNav, GlowLineChart, AnimatedCounter, ShimmerSkeleton). Feature code in `features/` consumes design system components; the data and state layers (all `features/*/data/`, all Riverpod providers, repositories, Dio, .NET API) remain completely unchanged — this is a hard constraint.

**Major components:**
1. `GlassTheme` (ThemeExtension) — single source of truth for blur sigmas (card: 12, appBar: 16, nav: 20), glass opacity (0.12), glow color, border opacity (0.25); implements `copyWith` and `lerp` for future light mode; accessed via `Theme.of(context).glass.*` with no provider lookup
2. `GlassCard` — core reusable glass surface (`ClipRRect` bounds the blur → `BackdropFilter.grouped` applies blur → `Container` with tint + border); non-blur variant (tint + border only, no BackdropFilter) used for scrollable list items in History and Portfolio
3. `GlowLineChart` — wraps existing `PriceLineChart` with draw-in animation (progress 0→1 clips visible data count to `ceil(prices.length * progress)`), gradient fill via `belowBarData`, and `BoxShadow` glow on container — glow via BoxShadow, NOT BackdropFilter
4. `ShimmerSkeleton` — `Skeletonizer` wrapper providing consistent shimmer loading; requires `SkeletonizerConfigData.dark()` and placeholder data objects per screen to shape the skeleton bones correctly
5. `AnimatedCounter` — `TweenAnimationBuilder<double>` widget that animates from 0 to target value on first build; used for hero balance and P&L numbers
6. Screen redesigns (HomeScreen, ChartScreen) and restyles (History, Config, Portfolio) — all consuming design primitives; explicit animation controllers live in `HookConsumerWidget` using `useAnimationController` from flutter_hooks (auto-dispose, no manual ticker management)

**Anti-patterns to enforce:**
- Never place `BackdropFilter` inside `AnimatedBuilder` — causes full compositor repaint at 60fps
- Always wrap `BackdropFilter` with `ClipRRect` — without it, blur extends beyond card boundaries
- Never put `AnimationController` in `ConsumerWidget` (stateless) — no `dispose()` lifecycle; use `HookConsumerWidget` + `useAnimationController` instead
- Glow effects via `BoxDecoration.boxShadow`, never via a second `BackdropFilter`

### Critical Pitfalls

1. **BackdropFilter in scrollable lists causes Impeller frame drops** — Impeller averages 16ms/frame (above the 16.67ms threshold) when `BackdropFilter` is inside `ListView` or `CustomScrollView`, confirmed by flutter/flutter#161297, #126353, #138615. The History tab and Portfolio tab are the affected screens. Mitigation: design `GlassCard` with an explicit non-blur variant (opacity tint + border only) for scrollable list items from the start. `BackdropFilter` is reserved for stationary cards. Decide this in Phase 1 (design system) — retrofitting after list screens are built is expensive.

2. **Theme refactor creates two sources of truth and breaks P&L semantic colors** — Adding new design tokens alongside existing `AppTheme` constants without a consolidation step creates color drift. The specific danger: `profitGreen` and `lossRed` semantic colors for financial P&L could be silently overridden by gradient colors during redesign, creating a functional regression. Mitigation: token consolidation is the first commit of the milestone; old `AppTheme` constants are deprecated before any visual changes start; `profitGreen` and `lossRed` are preserved as non-negotiable semantic constants.

3. **AnimationController leaks in multi-screen app** — `AnimationController` in stateless `ConsumerWidget` has no `dispose()` lifecycle; each controller holds a Ticker firing 60x/second indefinitely. With animations across 5 tabs, this causes memory leaks and battery drain detectable via Flutter DevTools Memory tab. Mitigation: all explicit controllers live in `HookConsumerWidget` using `useAnimationController` (auto-managed by flutter_hooks); use implicit animations (`TweenAnimationBuilder`, `AnimatedSwitcher`) for micro-interactions — they manage their own lifecycle.

4. **Over-animation fatigue on daily-use financial screens** — Animations running on every tab revisit (not just first load) make a daily-use app feel slow. Counter animating through intermediate values ($0 → $45,123) shows momentarily incorrect financial data that could be misread. Chart redrawing on every background data poll creates visual noise. Mitigation: `_hasAnimated` guard per animated screen; animate only on initial load or explicit user pull-to-refresh; chart draw-in plays once per session; shimmer only appears if load exceeds 200ms to avoid flash-of-skeleton on cache hits.

5. **Glassmorphism fails WCAG contrast on financial data** — Glass cards with low-opacity backgrounds produce unpredictable text contrast depending on what renders behind the glass. For a financial app, illegible P&L values are a functional failure. Mitigation: minimum 60% opaque dark tint overlay on all glass cards; text never lighter than `Colors.white.withOpacity(0.87)`; P&L semantic colors must pass 4.5:1 against glass background; honor `MediaQuery.accessibilityFeatures.reduceTransparency` (replace `BackdropFilter` with fully opaque fallback) and `disableAnimations`.

6. **fl_chart CustomPainter shouldRepaint thrash** — A `shouldRepaint()` that always returns `true` causes the chart to repaint at 60fps even when data has not changed, consuming significant GPU time on a screen that also has backdrop blurs. Mitigation: implement `shouldRepaint()` to compare previous and new data fields; pre-create `Paint` and `Gradient` objects as `late final` painter fields, never inside the `paint()` method; wrap chart in `RepaintBoundary`.

---

## Implications for Roadmap

Based on combined research, the build must follow strict dependency ordering. The design system foundation is a hard prerequisite for every other phase — no visual work should start before tokens, `GlassTheme`, and `GlassCard` exist as stable components. The BackdropFilter-in-scroll architectural decision must be made in Phase 1/2, not discovered during Phase 6.

### Phase 1: Design System Foundation
**Rationale:** Every other component depends on GlassTheme tokens and the ambient background. Token consolidation must be the first commit to prevent two-source-of-truth color drift (Pitfall #2). WCAG contrast constraints must be baked into token values from day one (Pitfall #5). The ambient gradient background must exist before any glass card can render correctly (glass is invisible on solid black).
**Delivers:** `AppColors`, `AppTypography`, `AppSpacing` token constants (`design/tokens/`); `GlassTheme` ThemeExtension registered in `AppTheme.dark`; ambient gradient background replacing flat `#121212` scaffold (static radial orbs, zero per-frame cost); existing `AppTheme` color constants deprecated and consolidated; animation guidelines (`_hasAnimated` pattern) documented.
**Addresses:** Table stakes — ambient gradient background (required-by for glass), typography overhaul foundation.
**Avoids:** Pitfall #2 (single source of truth for colors established before any screen is touched), Pitfall #5 (minimum-contrast-compliant tint values defined in tokens from day one).

### Phase 2: Shared Glass Primitives
**Rationale:** GlassCard and supporting primitives are the implementation of Phase 1's design tokens. Once stable, all five feature screens can adopt them. The critical architectural decision — no `BackdropFilter` in scrollable lists — is encoded in the GlassCard design here. Build order within this phase follows dependencies: GlassCard → GlassAppBar → AnimatedCounter → ShimmerSkeleton → GlassBottomNav.
**Delivers:** `GlassCard` widget (blur variant for stationary cards + non-blur opacity-tint variant for scrollable list items); `GlassAppBar` (frosted `SliverAppBar`); `AnimatedCounter` (`TweenAnimationBuilder<double>`); `ShimmerSkeleton` (skeletonizer wrapper — add skeletonizer dep here); `GlassBottomNav` component (without `BackdropFilter` initially — see Phase 6 validation).
**Addresses:** Table stakes — frosted glass cards, shimmer loading skeletons, press micro-interaction (`AnimatedScale` + haptics).
**Avoids:** Pitfall #1 (non-blur glass variant for scrollable contexts designed out at component level), Pitfall #3 (useAnimationController pattern established and documented for all future screens).

### Phase 3: Chart Redesign
**Rationale:** The chart is the most complex visual element and the only one requiring explicit animation controller work (`GlowLineChart` draw-in). Isolating it in its own phase reduces risk. Building `GlowLineChart` before applying it to `ChartScreen` validates the data-slice animation approach. `fl_chart` is an existing dependency — this phase adds no new packages.
**Delivers:** `GlowLineChart` widget (data-slice draw-in animation via `AnimatedBuilder` + `progress.value` clipping; `BoxShadow` glow on container; gradient fill via `belowBarData`); `ChartScreen` redesign with `useAnimationController`; timeframe pill selector restyle; glow purchase marker dots (custom `FlDotPainter`); premium glass chart tooltip (`LineTouchTooltipData` with glass background).
**Addresses:** Table stakes — gradient glow line chart; polish features — chart draw-in animation, glow dots, glass tooltip.
**Avoids:** Pitfall #6 (fl_chart `shouldRepaint` architecture and pre-created `Paint` objects designed before glow effects are added).

### Phase 4: Home Screen Redesign
**Rationale:** Home is the highest-frequency tab (opened 10x/day) and has the most new sub-components. Building it after shared primitives are proven ensures `GlassCard` is stable before assembling the most complex layout. `AnimatedCounter` and staggered entrance animation appear here first — establishing the `_hasAnimated` guard pattern that all subsequent animated screens copy.
**Delivers:** Home screen full redesign — `HeroBalanceCard` (large glass card + `AnimatedCounter`), `MiniAllocationChart` (small donut in glass card), `RecentActivityCard` (last 3 purchases as glass list, non-blur variant), `QuickActionsRow` (glass action buttons); staggered card entrance animation (flutter_animate, initial load only); press micro-interaction + haptics on all tappable cards.
**Addresses:** All P1 table-stakes features visible on the most-used screen; P2 staggered entrance and animated counter.
**Avoids:** Pitfall #4 (over-animation — `_hasAnimated` guard established here as the canonical pattern for the remaining screens).

### Phase 5: Portfolio Screen Restyle
**Rationale:** Portfolio is the second highest-frequency tab. Like Home, it uses `AnimatedCounter` and currency toggle animation. Doing it after Home means animation patterns are copied rather than invented twice. This phase also completes the colored donut glow and slot-flip currency toggle.
**Delivers:** `PortfolioSummaryCard` → `GlassCard` + `AnimatedCounter`; `AllocationDonutChart` restyle (colored `BoxShadow` glow behind donut); `AssetRow` glass rows (non-blur variant); currency toggle slot-flip animation (`AnimatedSwitcher` with vertical slide on value labels).
**Addresses:** P2 polish — currency toggle animation, donut ambient glow; consistent glass treatment on the second most-used tab.
**Avoids:** Pitfall #4 (animation guard copied from Phase 4 pattern; counter does not re-animate on tab revisit).

### Phase 6: History, Config, and Navigation Shell
**Rationale:** History and Config are lower-frequency tabs requiring restyles only (no new components to design). The navigation shell change (`GlassBottomNav` integration) touches shared infrastructure — lowest risk to leave last. `CustomTransitionPage` on modal routes is a small `router.dart` change that caps the milestone.
**Delivers:** `HistoryScreen` restyle — glass list container, tier glow badge on `PurchaseListItem` (non-blur glass row — verified no `BackdropFilter` in scroll); `ConfigScreen` restyle — glass section cards, improved form field typography; `GlassBottomNav` integration into `ScaffoldWithNavigation` (with device performance gate before committing `BackdropFilter` on nav); `CustomTransitionPage` on modal routes; tab-to-tab fade+scale transition.
**Addresses:** Completing consistent glass treatment across all 5 tabs; tab and modal page transitions.
**Avoids:** Pitfall #1 (History list items confirmed to use opacity-tint-only glass, not `BackdropFilter`); GlassBottomNav `BackdropFilter` gated on physical device testing before commit.

### Phase Ordering Rationale

- Foundation-first ordering eliminates blocked work. Phase 1 and 2 produce the design system primitives every subsequent phase consumes. No feature screen can start without them.
- The `BackdropFilter`-in-scroll architectural decision (Pitfall #1) must be encoded in `GlassCard` during Phase 2 — if discovered during Phase 6 when History is built, it requires full rework of both History and Portfolio list items.
- High-frequency tabs (Home, Portfolio) before low-frequency tabs (History, Config) ensures the most user-visible work gets the most implementation attention and testing time.
- Navigation shell modification last because it modifies shared infrastructure that all other phases depend on for development-time testing.
- Chart phase (Phase 3) before Home (Phase 4) because `GlowLineChart` is an independent, complex component best isolated before it is embedded in a busy screen. Also, the chart gradient fill must be implemented before the chart draw-in animation is meaningful.

### Research Flags

Phases needing validation during planning or early implementation:

- **Phase 2 (GlassCard blur sigma values):** Token values (card: 12, appBar: 16, nav: 20) are based on community guidance, not measured against this specific device/background combination. Must be calibrated on a physical iPhone during Phase 2 before being locked into `GlassTheme` tokens. iOS Simulator does not accurately represent `BackdropFilter` GPU cost or visual appearance.
- **Phase 2 (ShimmerSkeleton dark-mode config):** Exact `baseColor` and `highlightColor` for the dark-glass shimmer effect need tuning on device. Research provides the pattern (`SkeletonizerConfigData.dark()`); values against `Color(0xFF0D0D0F)` background are empirical.
- **Phase 3 (GlowLineChart data-slice approach):** MEDIUM confidence — the `prices.length * progress.value` slicing approach achieves left-to-right reveal but has not been validated against the existing `ChartResponse` data model. Verify `ChartResponse` can expose a sliced version cleanly before committing.
- **Phase 6 (GlassBottomNav BackdropFilter):** Full-width blur on nav bar affects every tab transition. Explicitly flagged P3 in features research. Must be tested on physical iPhone before committing; fallback is opacity-tint-only nav bar.

Phases with well-documented standard patterns (skip dedicated research):

- **Phase 1 (ThemeExtension + token consolidation):** `ThemeExtension` is an official, extensively documented Flutter API. `AppColors` / `AppTypography` are pure Dart constants. No research needed.
- **Phase 4 (Home screen widget composition):** Uses established GlassCard, AnimatedCounter, and flutter_animate stagger. Standard widget composition.
- **Phase 5 (Portfolio restyle):** Identical pattern to Phase 4. Copy `_hasAnimated` guard pattern.
- **Phase 6 (History/Config restyle):** Non-blur glass list row is a single `Container` with `BoxDecoration` — trivial to implement once `GlassCard` is available.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All package recommendations verified against official pub.dev, Flutter release notes, and official API docs. `BackdropGroup` confirmed in Flutter 3.29+ release notes. `flutter_animate`, `google_fonts`, `animations` all verified compatible with Flutter 3.41/Dart 3.11. Abandoned packages (`glassmorphism`, `countup`) confirmed via pub.dev publication dates. |
| Features | MEDIUM-HIGH | P1 feature set is clear and internally consistent. Anti-features (BackdropFilter in scroll, animated background) are HIGH confidence based on confirmed Flutter performance issues. P2 polish features have some uncertainty around which specific micro-interactions will feel right on physical device — requires design taste judgments and device testing during implementation. |
| Architecture | HIGH | `design/` directory structure, `GlassTheme` ThemeExtension pattern, `GlassCard` implementation, `useAnimationController` lifecycle pattern — all verified against official Flutter API docs and the existing codebase's `HookConsumerWidget` conventions. `GlowLineChart` data-slice approach is MEDIUM (mechanically sound, not yet validated against actual `ChartResponse` model). |
| Pitfalls | HIGH | `BackdropFilter`/Impeller performance confirmed by multiple open GitHub issues with benchmark data (16ms vs 6ms raster). `AnimationController` lifecycle is official Flutter docs. WCAG contrast analysis from authoritative accessibility sources (NN/g, Axess Lab). `fl_chart` `shouldRepaint` pattern from official `CustomPainter` docs. |

**Overall confidence:** HIGH

### Gaps to Address

- **Blur sigma calibration on physical device:** Tokens define blur sigma values (card: 12, appBar: 16, nav: 20) based on community guidance. These should be calibrated on a physical iPhone during Phase 2 before being locked into `GlassTheme` tokens. Values below 6 look under-blurred; above 15 increases GPU cost with diminishing visual return; above 30 causes Impeller color-jiggle artifacts.
- **GlowLineChart data-slice implementation:** Needs verification that `ChartResponse` can expose a sliced-data variant cleanly (e.g., `data.withPricesSlicedTo(count)` or equivalent). If the data model does not support this easily, an alternative approach (drawing an opacity mask overlay) may be needed.
- **skeletonizer dark-mode shimmer tuning:** Exact `baseColor` and `highlightColor` values for dark-glass shimmer need device testing. Research provides the correct approach (`ShimmerEffect` inside `Skeletonizer`); the specific alpha values are empirical.
- **GlassBottomNav performance gate:** Treated as experimental in Phase 6. If `BackdropFilter` on the nav bar causes perceptible lag on physical device, the fallback is an opacity-tint-only nav bar that still looks premium without real blur.
- **iPhone SE layout validation:** New typography scale (display: 48sp/700, headline: 24sp/600) requires verification that no formatted values clip or overflow on 375pt viewport (iPhone SE 3rd gen). Test on simulator before finalizing type scale.
- **shimmer-before-data threshold:** Research recommends a 200ms delay before showing shimmer to avoid flash-of-skeleton on Riverpod cache hits. Gate shimmer on `value.isLoading && !value.hasValue` rather than `isLoading` alone to suppress shimmer on background refreshes.

---

## Sources

### Primary (HIGH confidence)
- [Flutter BackdropFilter Impeller issues — github.com/flutter/flutter #161297, #126353, #138615, #149368](https://github.com/flutter/flutter/issues/161297) — Impeller performance benchmarks confirming 16ms vs 6ms raster times in scrolling contexts
- [Impeller high-sigma artifacting — flutter/flutter #143947](https://github.com/flutter/flutter/issues/143947) — sigma 40+ confirmed to cause color jiggle on content beneath blur
- [Flutter 3.29 release notes — docs.flutter.dev](https://docs.flutter.dev/release/release-notes/release-notes-3.29.0) — `BackdropGroup` and `BackdropFilter.grouped` introduced
- [Flutter 3.41 release notes — docs.flutter.dev](https://docs.flutter.dev/release/release-notes/release-notes-3.41.0) — bounded blur fix and iOS style blurring confirmed
- [BackdropGroup API docs — api.flutter.dev](https://api.flutter.dev/flutter/widgets/BackdropGroup-class.html) — official API documentation
- [ThemeExtension docs — api.flutter.dev](https://api.flutter.dev/flutter/material/ThemeExtension-class.html) — ThemeExtension pattern, copyWith, lerp requirements
- [AnimationController docs — api.flutter.dev](https://api.flutter.dev/flutter/animation/AnimationController-class.html) — lifecycle and disposal requirements
- [Flutter accessibility docs — docs.flutter.dev](https://docs.flutter.dev/ui/accessibility) — `reduceTransparency`, `disableAnimations` MediaQuery properties
- [RepaintBoundary docs — api.flutter.dev](https://api.flutter.dev/flutter/widgets/RepaintBoundary-class.html) — isolation for animation-adjacent blur layers
- [CustomPainter shouldRepaint — api.flutter.dev](https://api.flutter.dev/flutter/rendering/CustomPainter-class.html) — equality check requirements for efficient repaint
- [fl_chart changelog — pub.dev/packages/fl_chart/changelog](https://pub.dev/packages/fl_chart/changelog) — `gradient` and `belowBarData.gradient` confirmed in 1.1.0/1.1.1
- [flutter_animate pub.dev](https://pub.dev/packages/flutter_animate) — version 4.5.2, gskinner publisher, `ShimmerEffect` built-in confirmed
- [google_fonts pub.dev](https://pub.dev/packages/google_fonts) — version 8.0.2, Flutter 3.35+ minimum confirmed
- [animations pub.dev](https://pub.dev/packages/animations) — version 2.1.1, Flutter team official package
- [go_router transition animations — pub.dev docs](https://pub.dev/documentation/go_router/latest/topics/Transition%20animations-topic.html) — `CustomTransitionPage` usage confirmed
- [Glassmorphism accessibility — axesslab.com](https://axesslab.com/glassmorphism-meets-accessibility-can-frosted-glass-be-inclusive/) — WCAG 2.2 contrast requirements for glass effects
- [NN/g glassmorphism usability — nngroup.com](https://www.nngroup.com/articles/glassmorphism/) — contrast requirements and documented failure modes

### Secondary (MEDIUM confidence)
- [Flutter BackdropFilter optimization — trushitkasodiya.medium.com](https://trushitkasodiary.medium.com/flutter-backdrop-filter-optimization-improve-ui-performance-81746bc1fd55) — `RepaintBoundary` and sigma range guidance
- [Glassmorphism in Flutter production guide — thelinuxcode.com](https://thelinuxcode.com/flutter-glassmorphism-ui-design-for-apps-a-practical-production-ready-guide/) — production best practices for card layering
- [Building design system with ThemeExtension — vibe-studio.ai](https://vibe-studio.ai/insights/building-a-reusable-design-system-in-flutter-with-theme-extensions) — ThemeExtension integration pattern and convenience extension
- [Flutter custom theme with ThemeExtension — medium.com](https://medium.com/@alexandersnotes/flutter-custom-theme-with-themeextension-792034106abc) — copyWith and lerp implementation pattern
- [Flutter rendering optimization tips — blog.gskinner.com](https://blog.gskinner.com/archives/2022/09/flutter-rendering-optimization-tips.html) — repaint boundary and layer caching strategies
- [Fintech typography best practices — smashingmagazine.com](https://www.smashingmagazine.com/2023/10/choose-typefaces-fintech-products-guide-part1/) — tabular figures and type scale for financial UI
- [Dark glassmorphism design patterns 2026 — medium.com](https://medium.com/@developer_89726/dark-glassmorphism-the-aesthetic-that-will-define-ui-in-2026-93aa4153088f) — ambient gradient orb as prerequisite for glass appearance
- [skeletonizer 2.1.3 — pub.dev](https://pub.dev/packages/skeletonizer) — dark-mode config, placeholder data shaping pattern
- [useAnimationController — flutter_hooks docs](https://pub.dev/documentation/flutter_hooks/latest/flutter_hooks/useAnimationController.html) — auto-dispose and vsync management

### Tertiary (LOW confidence)
- [glassmorphism pub.dev](https://pub.dev/packages/glassmorphism) — confirmed abandoned August 2021 (522 likes); referenced only to confirm it must be avoided
- [countup pub.dev](https://pub.dev/packages/countup) — confirmed abandoned March 2022 (130 likes); referenced only to confirm `TweenAnimationBuilder<double>` is the correct alternative

---
*Research completed: 2026-02-21*
*Ready for roadmap: yes*

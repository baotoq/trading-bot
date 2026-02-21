# Architecture Research

**Domain:** Flutter glassmorphism design system — premium UI redesign for existing MVVM + Riverpod app
**Researched:** 2026-02-21
**Confidence:** HIGH (Flutter native BackdropFilter, ThemeExtension, flutter_hooks animations, fl_chart, go_router transitions — all verified against official docs and existing codebase), MEDIUM (skeletonizer dark-mode customization, fl_chart draw-in clipping approach)

## Standard Architecture

### System Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                   DESIGN SYSTEM LAYER (NEW)                      │
│                                                                  │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────────┐   │
│  │  GlassTheme     │  │  AppColors       │  │  AppTypography │   │
│  │  ThemeExtension │  │  gradients/glow/ │  │  display/head/ │   │
│  │  blur/opacity/  │  │  semantic colors │  │  body/caption  │   │
│  │  border tokens  │  │                  │  │  TextStyles    │   │
│  └────────┬────────┘  └────────┬─────────┘  └────────┬───────┘   │
│           └───────────────────┴────────────────────-─┘           │
│                    registered in AppTheme.dark ThemeData         │
├──────────────────────────────────────────────────────────────────┤
│                 SHARED GLASS WIDGET LAYER (NEW)                  │
│                                                                  │
│  ┌────────────┐  ┌────────────┐  ┌─────────────────────────┐     │
│  │  GlassCard │  │  GlassApp  │  │  GlassBottomNav         │     │
│  │  (reusable │  │  Bar       │  │  (frosted tab bar,      │     │
│  │  surface)  │  │            │  │  replaces NavigationBar) │    │
│  └────────────┘  └────────────┘  └─────────────────────────┘     │
│  ┌────────────┐  ┌────────────┐  ┌─────────────────────────┐     │
│  │ GlowLine   │  │ Animated   │  │  ShimmerSkeleton        │     │
│  │ Chart      │  │ Counter    │  │  (skeletonizer wrap)    │     │
│  │ (fl_chart  │  │ (tween     │  │                         │     │
│  │  +draw-in) │  │  builder)  │  │                         │     │
│  └────────────┘  └────────────┘  └─────────────────────────┘     │
├──────────────────────────────────────────────────────────────────┤
│                  ANIMATION LAYER (NEW — no new dependencies)     │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Explicit: useAnimationController (flutter_hooks, exists) │    │
│  │  Implicit: AnimatedOpacity, AnimatedContainer,            │    │
│  │            TweenAnimationBuilder, AnimatedSwitcher        │    │
│  │  Page:     CustomTransitionPage (go_router, exists)       │    │
│  └──────────────────────────────────────────────────────────┘    │
├──────────────────────────────────────────────────────────────────┤
│              FEATURE SCREENS LAYER (EXISTING — MODIFIED)         │
│                                                                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────────┐   │
│  │  Home    │  │  Chart   │  │ History  │  │ Config /       │   │
│  │  Screen  │  │  Screen  │  │  Screen  │  │ Portfolio      │   │
│  │(REDESIGN)│  │(REDESIGN)│  │(RESTYLE) │  │ Screens        │   │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────────┬───────┘   │
│       └─────────────┴─────────────┴────────────────-─┘           │
│        consume GlassCard, GlowLineChart, AnimatedCounter,        │
│        ShimmerSkeleton, and AppTypography throughout             │
├──────────────────────────────────────────────────────────────────┤
│              STATE / DATA LAYER (EXISTING — UNCHANGED)           │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Riverpod providers (homeDataProvider, chartProvider..)  │    │
│  │  Repositories (HomeRepository, ChartRepository..)       │    │
│  │  Dio + ApiKeyInterceptor  →  .NET API                    │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Status |
|-----------|----------------|--------|
| **GlassTheme (ThemeExtension)** | Single source of truth for blur sigmas, glass opacity, glow color, border opacity tokens. Registered in ThemeData; consumed via `Theme.of(context).glass`. | NEW |
| **AppColors** | Static color constants: gradient stops, glow colors, semantic profit/loss colors, surface backgrounds. Extends/replaces existing `AppTheme` statics. | NEW (replaces AppTheme constants) |
| **AppTypography** | TextStyle hierarchy: display, headline, body, label, caption. Sizes, weights, letter-spacing. Registered into `ThemeData.textTheme`. | NEW |
| **GlassCard** | Core reusable glass surface: `ClipRRect` + `BackdropFilter` + gradient tint container + gradient border. All glass surfaces in the app use this one widget. | NEW |
| **GlassAppBar** | `SliverAppBar` with frosted glass background using `BackdropFilter`. Replaces plain `SliverAppBar` in all screens. | NEW |
| **GlassBottomNav** | Custom frosted navigation bar replacing Material `NavigationBar`. Wraps existing 5-tab structure. | NEW |
| **GlowLineChart** | `fl_chart` `LineChart` with draw-in animation (progress 0→1 reveals data left-to-right), gradient glow fill, and `BoxShadow` glow on container. | NEW |
| **AnimatedCounter** | `TweenAnimationBuilder<double>` widget that animates from 0 to a target numeric value on first build. Used for hero balance and P&L numbers. | NEW |
| **ShimmerSkeleton** | `Skeletonizer` wrapper providing consistent shimmer loading across all screens. Replaces `CircularProgressIndicator`. | NEW (requires new dep) |
| **HomeScreen** | Redesigned: hero balance glass card, mini allocation chart, recent activity list, quick-action row. | REDESIGN |
| **ChartScreen** | Redesigned: full-screen `GlowLineChart` with glass timeframe pill selector. | REDESIGN |
| **HistoryScreen** | Restyled: glass list container, tier badge glow on `PurchaseListItem`. | RESTYLE |
| **ConfigScreen** | Restyled: glass section cards, improved form field typography. | RESTYLE |
| **PortfolioScreen** | Restyled: glass asset cards, animated donut chart, glass currency toggle. | RESTYLE |
| **AppTheme** | Extended: `GlassTheme` ThemeExtension registered, `textTheme` updated. Surface colors deepened. | MODIFIED |
| **ScaffoldWithNavigation** | `GlassBottomNav` replaces `NavigationBar`. | MODIFIED |
| **router.dart** | `pageBuilder` with `CustomTransitionPage` added to modal routes. Tab routes unchanged. | LIGHTLY MODIFIED |
| **Riverpod providers** | Unchanged. UI redesign makes no data contract changes. | UNCHANGED |
| **Repositories + Dio** | Unchanged. | UNCHANGED |

## Recommended Project Structure

```
TradingBot.Mobile/lib/
│
├── app/
│   ├── theme.dart          # MODIFIED: GlassTheme registered, AppTypography wired in
│   └── router.dart         # LIGHTLY MODIFIED: pageBuilder on modal routes
│
├── core/
│   ├── api/                # UNCHANGED
│   └── widgets/            # UNCHANGED (error_snackbar, retry_widget)
│
├── design/                 # NEW — complete design system
│   ├── tokens/
│   │   ├── app_colors.dart       # Color constants: gradients, glow, semantic colors
│   │   ├── app_spacing.dart      # Spacing scale: 4/8/12/16/24/32 dp constants
│   │   └── app_typography.dart   # TextStyle hierarchy: display/headline/body/caption
│   │
│   ├── theme/
│   │   └── glass_theme.dart      # ThemeExtension with blur/opacity/glow/border tokens
│   │
│   └── widgets/                  # Shared glass primitives (used across 2+ features)
│       ├── glass_card.dart        # Core frosted glass surface
│       ├── glass_app_bar.dart     # Frosted SliverAppBar
│       ├── glass_bottom_nav.dart  # Frosted bottom navigation bar
│       ├── glow_line_chart.dart   # fl_chart + draw-in animation + BoxShadow glow
│       ├── animated_counter.dart  # TweenAnimationBuilder number widget
│       └── shimmer_skeleton.dart  # Skeletonizer wrapper with dark-mode config
│
├── features/
│   ├── home/
│   │   ├── data/                      # UNCHANGED
│   │   └── presentation/
│   │       ├── home_screen.dart        # REDESIGN: hero balance + activity + actions
│   │       └── widgets/
│   │           ├── hero_balance_card.dart     # NEW: large glass card + AnimatedCounter
│   │           ├── mini_allocation_chart.dart # NEW: small donut in glass card
│   │           ├── recent_activity_card.dart  # NEW: last 3 purchases glass list
│   │           ├── quick_actions_row.dart     # NEW: shortcut glass buttons
│   │           ├── health_badge.dart          # RESTYLE: glass pill badge
│   │           ├── countdown_text.dart        # RESTYLE: typography upgrade
│   │           ├── last_buy_card.dart         # RESTYLE: → GlassCard
│   │           └── portfolio_stats_section.dart # RESTYLE: → AnimatedCounter
│   │
│   ├── chart/
│   │   ├── data/                      # UNCHANGED
│   │   └── presentation/
│   │       ├── chart_screen.dart       # REDESIGN: full-screen chart experience
│   │       └── widgets/
│   │           ├── price_line_chart.dart   # REDESIGN: data → GlowLineChart
│   │           └── timeframe_selector.dart # RESTYLE: glass pill selector
│   │
│   ├── history/
│   │   ├── data/                      # UNCHANGED
│   │   └── presentation/
│   │       ├── history_screen.dart        # RESTYLE: glass container
│   │       └── widgets/
│   │           ├── purchase_list_item.dart  # RESTYLE: glass row, tier glow badge
│   │           └── filter_bottom_sheet.dart # RESTYLE: glass bottom sheet
│   │
│   ├── config/
│   │   ├── data/                      # UNCHANGED
│   │   └── presentation/
│   │       ├── config_screen.dart          # RESTYLE: glass sections
│   │       └── widgets/
│   │           ├── config_view_section.dart # RESTYLE: → GlassCard section
│   │           ├── config_edit_form.dart    # RESTYLE: glass form fields
│   │           └── tier_list_editor.dart    # RESTYLE: glass list items
│   │
│   └── portfolio/
│       ├── data/                      # UNCHANGED
│       └── presentation/
│           ├── portfolio_screen.dart        # RESTYLE: glass cards throughout
│           └── widgets/
│               ├── allocation_donut_chart.dart  # RESTYLE: animated + glass center
│               ├── asset_row.dart               # RESTYLE: glass row
│               ├── asset_type_section.dart      # RESTYLE: glass section header
│               ├── portfolio_summary_card.dart  # RESTYLE: → GlassCard + AnimatedCounter
│               ├── currency_toggle.dart         # RESTYLE: glass pill toggle
│               ├── fixed_deposit_row.dart       # RESTYLE: glass row
│               └── staleness_label.dart         # RESTYLE: muted glass badge
│
└── shared/
    └── navigation_shell.dart   # MODIFIED: GlassBottomNav replaces NavigationBar
```

### Structure Rationale

- **`design/`:** New top-level folder. Deliberately separate from `app/` (router + theme entry points) and `shared/` (navigation shell). Contains the entire design system: tokens, ThemeExtension, and primitive widgets. Clear bounded scope — if it's a design atom used by multiple features, it belongs here.
- **`design/tokens/`:** Pure Dart constants, zero Flutter widget dependencies. Can be imported anywhere without circular references.
- **`design/theme/`:** `ThemeExtension` subclass that travels with `ThemeData`. Accessed anywhere via `Theme.of(context).extension<GlassTheme>()` or the convenience extension method `Theme.of(context).glass`. No provider lookup needed.
- **`design/widgets/`:** The rule for inclusion: used by two or more features. `GlassCard` is used by all five screens. `GlowLineChart` is only used in `chart/` but is complex enough to deserve its own file in `design/widgets/` for isolation and testing.
- **Feature widget files:** Restyled in-place — same file names, same data contracts, glass treatment applied to internals. This minimizes diff surface area and keeps feature-level ownership clear.
- **No changes to `data/` folders:** The redesign touches zero data, provider, or repository files. This is a hard constraint of the architecture.

## Architectural Patterns

### Pattern 1: GlassTheme as ThemeExtension

**What:** A `ThemeExtension<GlassTheme>` subclass carrying all glassmorphism design tokens. Registered once in `AppTheme.dark`. Accessed from any widget via `Theme.of(context).glass.blurSigmaCard`. Implements `copyWith` and `lerp` to support potential future light mode or animated theme transitions.

**When to use:** Any widget that needs blur sigma, glass opacity, glow color, or border opacity. Eliminates magic numbers scattered in widget files.

**Trade-offs:** Requires implementing `copyWith` and `lerp` once. After that, access is clean, centralized, and refactor-safe. Static constants like `AppColors` complement this — `AppColors` holds raw `Color` values, `GlassTheme` holds opacity and blur parameters.

**Example:**
```dart
// design/theme/glass_theme.dart
@immutable
class GlassTheme extends ThemeExtension<GlassTheme> {
  const GlassTheme({
    required this.blurSigmaCard,
    required this.blurSigmaAppBar,
    required this.blurSigmaNav,
    required this.glassOpacity,
    required this.glassBorderOpacity,
    required this.glowColor,
    required this.glowBlurRadius,
  });

  // Blur tiers — stronger blur = more depth, more GPU cost
  final double blurSigmaCard;   // 12.0 — moderate for cards
  final double blurSigmaAppBar; // 16.0 — slightly stronger for app bar
  final double blurSigmaNav;    // 20.0 — strongest for nav bar (full width)

  // Opacity
  final double glassOpacity;        // 0.12 — base white tint
  final double glassBorderOpacity;  // 0.25 — border highlight

  // Glow (used on chart container BoxShadow)
  final Color glowColor;       // AppColors.btcOrange.withAlpha(180)
  final double glowBlurRadius; // 8.0

  @override
  GlassTheme copyWith({...}) => GlassTheme(
    blurSigmaCard: blurSigmaCard ?? this.blurSigmaCard,
    // ...
  );

  @override
  GlassTheme lerp(GlassTheme? other, double t) {
    if (other is! GlassTheme) return this;
    return GlassTheme(
      blurSigmaCard: lerpDouble(blurSigmaCard, other.blurSigmaCard, t)!,
      glowColor: Color.lerp(glowColor, other.glowColor, t)!,
      // ...
    );
  }
}

// app/theme.dart — add to existing ThemeData
class AppTheme {
  static const Color btcOrange = Color(0xFFF7931A);  // renamed from bitcoinOrange
  static const Color profitGreen = Color(0xFF00C087);
  static const Color lossRed    = Color(0xFFFF4D4D);
  static const Color surface    = Color(0xFF0D0D0F); // deepened from 0x121212

  static final _glassTheme = GlassTheme(
    blurSigmaCard: 12.0,
    blurSigmaAppBar: 16.0,
    blurSigmaNav: 20.0,
    glassOpacity: 0.12,
    glassBorderOpacity: 0.25,
    glowColor: btcOrange.withAlpha(180),
    glowBlurRadius: 8.0,
  );

  static ThemeData get dark => ThemeData(
    // ... existing fields unchanged ...
    extensions: [_glassTheme],  // ADDED
  );
}

// Convenience extension — no boilerplate at call sites
extension GlassThemeX on ThemeData {
  GlassTheme get glass => extension<GlassTheme>()!;
}

// Usage anywhere in widget tree:
// final blur = Theme.of(context).glass.blurSigmaCard;
```

### Pattern 2: GlassCard — Core Reusable Glass Surface

**What:** One `GlassCard` widget encapsulates the layering pattern for glassmorphism: `ClipRRect` (bounds the blur) → `BackdropFilter` (applies blur) → `Container` (tint + border). All glass surfaces in the app use this widget. Feature widgets pass content as `child`.

**When to use:** Any card, panel, or floating surface needing frosted glass treatment. Pass `blurSigmaOverride` only when a specific screen needs a different intensity than the theme default.

**Trade-offs:** One `BackdropFilter` per card instance. Multiple on-screen simultaneously have additive GPU cost. On iOS with Impeller (Flutter 3.10+, enabled by default), this is handled efficiently. Profile on physical device — simulator GPU results are not representative.

**Example:**
```dart
// design/widgets/glass_card.dart
class GlassCard extends StatelessWidget {
  const GlassCard({
    required this.child,
    this.padding,
    this.borderRadius,
    this.blurSigmaOverride,
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final BorderRadius? borderRadius;
  final double? blurSigmaOverride;

  @override
  Widget build(BuildContext context) {
    final glass = Theme.of(context).glass;
    final radius = borderRadius ?? BorderRadius.circular(20);
    final sigma = blurSigmaOverride ?? glass.blurSigmaCard;

    // ClipRRect MUST wrap BackdropFilter — bounds the blur to card area
    return ClipRRect(
      borderRadius: radius,
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: sigma, sigmaY: sigma),
        child: Container(
          padding: padding ?? const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius: radius,
            color: Colors.white.withOpacity(glass.glassOpacity),
            border: Border.all(
              color: Colors.white.withOpacity(glass.glassBorderOpacity),
              width: 1.0,
            ),
          ),
          child: child,
        ),
      ),
    );
  }
}
```

### Pattern 3: Animation via useAnimationController (flutter_hooks)

**What:** All explicit animations in `HookConsumerWidget` screens use `useAnimationController` from `flutter_hooks` (already a project dependency via `hooks_riverpod`). This auto-manages the `TickerProvider` and auto-disposes the controller. Pair with `useEffect` to trigger animations after first build. Use implicit animations (`TweenAnimationBuilder`, `AnimatedOpacity`, `AnimatedSwitcher`) for micro-interactions — they need zero boilerplate.

**When to use:**
- **Explicit** (`useAnimationController`): Chart draw-in, page entrance animations, anything needing controller (`forward()`, `reverse()`, `repeat()`).
- **Implicit** (`TweenAnimationBuilder` etc.): Number counters, opacity fades, color transitions driven by state changes.

**Trade-offs:** `useAnimationController` requires `HookConsumerWidget` (already the project standard for all screens). Mixing `StatefulWidget` just for animations would break the pattern.

**Example — explicit chart draw-in:**
```dart
// In ChartScreen (already a HookConsumerWidget)
class ChartScreen extends HookConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final animController = useAnimationController(
      duration: const Duration(milliseconds: 1200),
    );
    final drawProgress = CurvedAnimation(
      parent: animController,
      curve: Curves.easeOutCubic,
    );

    // Trigger once after first frame
    useEffect(() {
      animController.forward();
      return null;
    }, const []);

    final chartData = ref.watch(chartProvider);
    return chartData.when(
      data: (data) => GlowLineChart(data: data, progress: drawProgress),
      loading: () => const ShimmerSkeleton(),
      error: (e, _) => RetryWidget(onRetry: () => ref.invalidate(chartProvider)),
    );
  }
}

// Implicit — animated balance counter (zero boilerplate)
TweenAnimationBuilder<double>(
  tween: Tween(begin: 0.0, end: portfolioValue),
  duration: const Duration(milliseconds: 800),
  curve: Curves.easeOutCubic,
  builder: (context, value, _) => Text(
    NumberFormat.currency(symbol: '\$', decimalDigits: 2).format(value),
    style: Theme.of(context).glass   // access glass theme
        == Theme.of(context).glass    // (example; real usage below)
        ? Theme.of(context).textTheme.displaySmall
        : null,
  ),
)
```

### Pattern 4: ShimmerSkeleton via Skeletonizer

**What:** Wrap existing widget trees with `Skeletonizer(enabled: isLoading, child: layoutBuilderCall(_placeholderData))`. Skeletonizer auto-converts the layout to shimmer bones. Define a `_placeholderData` const for each screen with zero/empty-string values matching the real data shape.

**When to use:** Replace all `CircularProgressIndicator` loading states on content screens. The placeholder data shapes the skeleton bones — an empty layout produces an empty skeleton.

**Trade-offs:** Complex custom painters (like `fl_chart` widgets) may need `Skeleton.ignore` annotation to suppress during loading. Use `SkeletonizerConfigData.dark()` to match the app's dark theme — dark-mode shimmer defaults look off against `Color(0xFF0D0D0F)` background.

**Example:**
```dart
// design/widgets/shimmer_skeleton.dart
class ShimmerSkeleton extends StatelessWidget {
  const ShimmerSkeleton({required this.child, super.key});
  final Widget child;

  @override
  Widget build(BuildContext context) => Skeletonizer(
    enabled: true,
    effect: ShimmerEffect(
      baseColor: Colors.white.withOpacity(0.05),
      highlightColor: Colors.white.withOpacity(0.15),
    ),
    child: child,
  );
}

// In HomeScreen:
switch (homeData) {
  AsyncData(:final value) => _buildContent(value),
  AsyncLoading() || AsyncError() when homeData.value == null =>
    ShimmerSkeleton(child: _buildContent(_homePlaceholder)),
  AsyncError() => RetryWidget(onRetry: () => ref.invalidate(homeDataProvider)),
}
```

### Pattern 5: GlowLineChart — fl_chart with Draw-In Animation

**What:** Accept an `Animation<double>` progress (0.0 → 1.0). Clip visible data to `(prices.length * progress.value).ceil()` entries. Apply `BoxShadow` with `glowColor` on the container wrapping `PriceLineChart` to create the glow. The existing `PriceLineChart` widget is unchanged — `GlowLineChart` composes around it.

**When to use:** Chart screen initial data load and timeframe switches (reset controller, forward again).

**Trade-offs:** fl_chart does not expose a native "reveal from left" animation. Slicing the data list achieves the same effect. For timeframe switching, reset the animation controller before forwarding again. Glow via `BoxShadow` is GPU-cheap compared to `BackdropFilter`.

**Example:**
```dart
// design/widgets/glow_line_chart.dart
class GlowLineChart extends StatelessWidget {
  const GlowLineChart({
    required this.data,
    required this.progress,
    super.key,
  });

  final ChartResponse data;
  final Animation<double> progress;

  @override
  Widget build(BuildContext context) {
    final glass = Theme.of(context).glass;
    return AnimatedBuilder(
      animation: progress,
      builder: (context, _) {
        final count = (data.prices.length * progress.value)
            .ceil()
            .clamp(1, data.prices.length);
        return Container(
          decoration: BoxDecoration(
            // Glow via BoxShadow — NOT BackdropFilter. Much cheaper.
            boxShadow: [
              BoxShadow(
                color: glass.glowColor.withOpacity(0.15),
                blurRadius: glass.glowBlurRadius * 3,
                spreadRadius: 1,
              ),
            ],
          ),
          child: PriceLineChart(
            data: data.withPricesSlicedTo(count),
            timeframe: data.timeframe,
          ),
        );
      },
    );
  }
}
```

### Pattern 6: Page Transitions via CustomTransitionPage

**What:** Replace `builder:` with `pageBuilder:` returning `CustomTransitionPage` for modal routes pushed over the tab bar. The 5 main tab routes keep instant switching — tabs feel instant on tap. Modal routes get a 300ms fade + slide-up.

**When to use:** Routes with `parentNavigatorKey: rootNavigatorKey` — `add-transaction`, `transaction-history`, `fixed-deposit/:id`, `edit`. Not tab-level routes.

**Trade-offs:** Minor `router.dart` edit. Modal transitions feel more premium than the default platform push animation when going into glass-styled sub-screens.

**Example:**
```dart
// app/router.dart — modal routes only (tab routes unchanged)
GoRoute(
  path: 'add-transaction',
  parentNavigatorKey: rootNavigatorKey,
  pageBuilder: (context, state) => CustomTransitionPage(
    key: state.pageKey,
    transitionDuration: const Duration(milliseconds: 300),
    child: const AddTransactionScreen(),
    transitionsBuilder: (context, animation, secondary, child) {
      return FadeTransition(
        opacity: CurveTween(curve: Curves.easeOut).animate(animation),
        child: SlideTransition(
          position: Tween(
            begin: const Offset(0, 0.08),
            end: Offset.zero,
          ).chain(CurveTween(curve: Curves.easeOutCubic)).animate(animation),
          child: child,
        ),
      );
    },
  ),
),
```

## Data Flow

### Design System Consumption Flow

```
AppTheme.dark (ThemeData)
  └─ extensions: [GlassTheme(...)]
        ↓ MaterialApp propagates ThemeData through widget tree
  Any widget:
    Theme.of(context).glass.blurSigmaCard  →  12.0
    Theme.of(context).glass.glowColor      →  Color(0xF7931AB4)
        ↓
    GlassCard(child: ...) — uses sigma, opacity, border from GlassTheme
        ↓
    ClipRRect → BackdropFilter(ImageFilter.blur) → Container decoration
        ↓
    Rendered frosted glass surface
```

### Animation State Flow

```
Riverpod AsyncValue<ChartData> (chartProvider)
    AsyncLoading  →  ShimmerSkeleton(child: skeletonLayout)
    AsyncData     →  useAnimationController.forward() triggered via useEffect
                         ↓
                     CurvedAnimation(progress: 0.0 → 1.0, easeOutCubic, 1200ms)
                         ↓
                     GlowLineChart receives progress Animation<double>
                         ↓
                     AnimatedBuilder rebuilds: slices prices to visibleCount
                         ↓
                     PriceLineChart renders sliced data
                         ↓
                     Chart draws in left-to-right over 1.2 seconds
```

### Typography Flow

```
AppTypography (static TextStyle constants)
    ↓ registered via ThemeData.textTheme in AppTheme.dark
    ↓ accessible via Theme.of(context).textTheme.*

displayLarge  → Hero portfolio balance on HomeScreen
headlineMedium → Card section titles
titleMedium   → Stat card values, asset names
bodyMedium    → Labels, secondary info
labelSmall    → Tier badges, timestamps, staleness labels
```

### Key Data Flows

1. **GlassCard rendering:** Widget looks up `GlassTheme` from `Theme.of(context)` — O(1) Flutter inherited widget lookup, no provider calls. All glass surfaces share identical parameters from one source.

2. **Chart draw-in:** `useAnimationController` in `ChartScreen` (already a `HookConsumerWidget`) provides the controller. `useEffect` calls `forward()` once after first build. `AnimatedBuilder` rebuilds only the `PriceLineChart` subtree — Riverpod state and repository layers are untouched.

3. **Shimmer → content transition:** When `AsyncData` arrives, Riverpod triggers a rebuild. `AnimatedSwitcher` wrapping the content/skeleton choice provides a fade crossfade at 200ms. Users see shimmer → smooth crossfade → real content.

## Integration Points

### New vs Existing Components — Complete Table

| Component | Type | Consumes | Consumed By |
|-----------|------|----------|-------------|
| **GlassTheme** | NEW ThemeExtension | AppTheme.dark ThemeData | GlassCard, GlassAppBar, GlassBottomNav, GlowLineChart, any widget |
| **AppColors** | NEW static constants | Nothing (leaf) | GlassTheme, feature widgets |
| **AppTypography** | NEW static TextStyles | Nothing (leaf) | AppTheme.dark textTheme, feature widgets |
| **GlassCard** | NEW widget | GlassTheme | All feature screen widgets (replaces `Card`) |
| **GlassAppBar** | NEW widget | GlassTheme | All 5 screen SliverAppBars |
| **GlassBottomNav** | NEW widget | GlassTheme | ScaffoldWithNavigation |
| **GlowLineChart** | NEW widget | GlassTheme, fl_chart (existing dep), PriceLineChart | ChartScreen |
| **AnimatedCounter** | NEW widget | TweenAnimationBuilder (Flutter SDK, no new dep) | HomeScreen (hero balance), PortfolioScreen (summary card) |
| **ShimmerSkeleton** | NEW widget | skeletonizer (NEW dep) | All 5 screens (replaces CircularProgressIndicator) |
| **AppTheme** | MODIFIED | GlassTheme (new), AppColors (new), AppTypography (new) | main.dart |
| **ScaffoldWithNavigation** | MODIFIED | GlassBottomNav | go_router |
| **router.dart** | LIGHTLY MODIFIED | CustomTransitionPage (go_router built-in) | 4 modal GoRoutes |
| **HomeScreen** | REDESIGN | GlassCard, AnimatedCounter, GlassAppBar, ShimmerSkeleton | go_router |
| **ChartScreen** | REDESIGN | GlowLineChart, GlassAppBar, useAnimationController | go_router |
| **HistoryScreen** | RESTYLE | GlassCard, GlassAppBar, ShimmerSkeleton | go_router |
| **ConfigScreen** | RESTYLE | GlassCard, GlassAppBar, ShimmerSkeleton | go_router |
| **PortfolioScreen** | RESTYLE | GlassCard, GlassAppBar, AnimatedCounter, ShimmerSkeleton | go_router |
| **Riverpod providers** | UNCHANGED | Dio, repositories | Feature screens (contract unchanged) |
| **Repositories** | UNCHANGED | Dio | Riverpod providers |
| **.NET API** | UNCHANGED | — | Dio |

### Suggested Build Order

Dependencies flow top-to-bottom. Build in this sequence to minimize blocked work:

```
Phase 1: Design foundation (no dependencies — pure Dart/constants)
  AppColors → AppTypography → GlassTheme → AppTheme update
  (unblocks everything below)

Phase 2: Shared glass primitives (depends on Phase 1)
  GlassCard
  GlassAppBar
  GlassBottomNav
  AnimatedCounter   ← no glass dep, just TweenAnimationBuilder
  ShimmerSkeleton   ← add skeletonizer dep here

Phase 3: Chart redesign (depends on Phase 1 + 2; fl_chart already exists)
  GlowLineChart
  ChartScreen redesign (useAnimationController + GlowLineChart)
  TimeframeSelector restyle

Phase 4: Home screen redesign (depends on Phase 1 + 2; highest visible impact)
  HeroBalanceCard (GlassCard + AnimatedCounter)
  MiniAllocationChart (glass donut in card)
  RecentActivityCard (glass list)
  QuickActionsRow
  HomeScreen assembly

Phase 5: Portfolio screen restyle (depends on Phase 1 + 2)
  PortfolioSummaryCard → GlassCard + AnimatedCounter
  AllocationDonutChart restyle
  AssetRow → GlassCard row
  PortfolioScreen assembly

Phase 6: History + Config + navigation shell (depends on Phase 1 + 2)
  PurchaseListItem restyle (glass row + tier glow badge)
  HistoryScreen restyle
  ConfigViewSection → GlassCard
  ConfigScreen restyle
  GlassBottomNav integration into ScaffoldWithNavigation
  CustomTransitionPage on modal routes in router.dart
```

### External Service Boundaries

| Service | Integration Pattern | Impact of Redesign |
|---------|--------------------|--------------------|
| **.NET API** | Dio HTTP, unchanged | Zero — pure UI redesign |
| **Riverpod providers** | `ref.watch()` in HookConsumerWidget | Zero — data contract unchanged |
| **fl_chart 1.1.1** | GlowLineChart wraps PriceLineChart | Same ChartResponse data. GlowLineChart adds animation + container glow layer around PriceLineChart |
| **flutter_hooks** | `useAnimationController` for chart + entrance animations | Already a project dependency via `hooks_riverpod`. No version change needed. |
| **skeletonizer** | NEW dependency. Wraps existing layouts as shimmer bones | Add to pubspec.yaml. Wrap async loading branches. No data changes. |
| **go_router 17.1.0** | `pageBuilder` with `CustomTransitionPage` on modal routes | Only router.dart changes — no route structure changes |

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 3-5 glass cards per screen (current target) | One BackdropFilter per GlassCard is fine on iOS Impeller. Profile on physical device before assuming jank. |
| 6+ glass surfaces on one screen | Profile first. If jank: lower blur sigma, wrap animating children in RepaintBoundary, or consider pre-rendering glass texture as a static image layer. |
| Future light mode | GlassTheme.lerp already implemented. Light mode requires a second GlassTheme token set (lower opacity, higher contrast border). AppTheme.light would register the light variant. |
| More animated screens | Each useAnimationController creates one Ticker. This is cheap. The cost is BackdropFilter + animation overlap — see Anti-Patterns. |

### Scaling Priorities

1. **First bottleneck — BackdropFilter + AnimatedBuilder overlap:** If an `AnimatedBuilder` causes repaints inside or adjacent to a `BackdropFilter`, Flutter recomposites the glass layer on every frame. Wrap animating content inside glass cards with `RepaintBoundary` to isolate the repaints.

2. **Second bottleneck — too many BackdropFilters on one screen:** The `GlassBottomNav` + `GlassAppBar` + multiple `GlassCard`s may sum to 5+ blur passes on one screen. iOS Impeller handles this better than Skia. If needed: convert the least-visible glass surface to a non-blur equivalent (solid translucent container with no BackdropFilter).

## Anti-Patterns

### Anti-Pattern 1: Magic Blur Numbers Scattered in Widget Files

**What people do:** Write `ImageFilter.blur(sigmaX: 15, sigmaY: 15)` directly in each glass widget file.

**Why it's wrong:** Inconsistent intensities across the app. A design change requires hunting down 10+ files. No single source of truth to adjust the "feel" of the entire system.

**Do this instead:** Use `GlassTheme` from `ThemeExtension`. `Theme.of(context).glass.blurSigmaCard` is the only place to read blur values. Change once in `AppTheme`, updates everywhere.

### Anti-Pattern 2: BackdropFilter Without ClipRRect

**What people do:** Apply `BackdropFilter` without a `ClipRRect` parent wrapping it.

**Why it's wrong:** Without clipping, the blur extends beyond the card boundaries. Flutter's compositor blurs the entire rendering layer, not just the card area. Adjacent widgets become partially blurred, and GPU cycles are wasted on out-of-bounds pixels.

**Do this instead:** Always use `ClipRRect(borderRadius: ..., child: BackdropFilter(...))`. The clip bounds the blur precisely to the card area.

### Anti-Pattern 3: Nesting BackdropFilter Inside AnimatedBuilder

**What people do:** Put `BackdropFilter` inside an `AnimatedBuilder` to animate blur intensity or opacity.

**Why it's wrong:** `AnimatedBuilder` rebuilds on every animation frame (~60fps). A `BackdropFilter` rebuild forces a full compositor repaint of that layer. This is one of the highest-cost operations in Flutter and will cause visible jank on any device.

**Do this instead:** Keep `BackdropFilter` (the glass surface) static. Animate only the content inside the glass card. Wrap the animating content with `RepaintBoundary` to prevent its repaints from propagating to the glass layer.

### Anti-Pattern 4: Using StatefulWidget Instead of useAnimationController

**What people do:** Convert a `HookConsumerWidget` screen to `StatefulWidget` with `SingleTickerProviderStateMixin` to add an animation.

**Why it's wrong:** Breaks the project's established widget type standardization. `SingleTickerProviderStateMixin` requires manual `dispose()` — a common source of memory leaks when forgotten. Mixing widget types increases cognitive overhead.

**Do this instead:** Stay in `HookConsumerWidget`. `useAnimationController(duration: ...)` handles vsync, initialization, and disposal automatically. All screens in this project already extend `HookConsumerWidget`.

### Anti-Pattern 5: Applying Glow via BackdropFilter or ImageFiltered

**What people do:** Add a second `BackdropFilter` around a chart widget to produce a glow blur effect.

**Why it's wrong:** `BackdropFilter` blurs what is behind a widget, not the widget itself. It's semantically wrong for a glow effect and computationally expensive.

**Do this instead:** Apply `BoxDecoration(boxShadow: [BoxShadow(color: glowColor, blurRadius: 24)])` on the `Container` wrapping the chart. This is a single GPU paint operation — cheap, correct, and visually identical to a glow.

### Anti-Pattern 6: Skeletonizer Without Placeholder Data

**What people do:** Use `Skeletonizer(enabled: true, child: SizedBox())` or a child with no content to create a loading skeleton.

**Why it's wrong:** Skeletonizer needs to render the child widget tree to know where to draw bones. An empty child produces an empty skeleton — no useful loading indicator.

**Do this instead:** Define `static const _placeholderData = MyScreenData(value: 0, name: '', ...)` for each screen. Pass it to the layout builder function when loading. Skeletonizer converts those text/icon/value widgets into shimmer bones at their correct positions and sizes.

## Sources

- [Flutter BackdropFilter Optimization — trushitkasodiya.medium.com](https://trushitkasodiya.medium.com/flutter-backdrop-filter-optimization-improve-ui-performance-81746bc1fd55)
- [Flutter ThemeExtension class — api.flutter.dev](https://api.flutter.dev/flutter/material/ThemeExtension-class.html)
- [Building a Reusable Design System with ThemeExtension — vibe-studio.ai](https://vibe-studio.ai/insights/building-a-reusable-design-system-in-flutter-with-theme-extensions)
- [Flutter Custom Theme with ThemeExtension — medium.com/alexandersnotes](https://medium.com/@alexandersnotes/flutter-custom-theme-with-themeextension-792034106abc)
- [Glassmorphism in Flutter: Production-Ready Guide — thelinuxcode.com](https://thelinuxcode.com/flutter-glassmorphism-ui-design-for-apps-a-practical-production-ready-guide/)
- [Implementing Glassmorphism Effects in Flutter — vibe-studio.ai](https://vibe-studio.ai/insights/implementing-glassmorphism-effects-in-flutter-uis)
- [flutter_hooks 0.21.3 — pub.dev](https://pub.dev/packages/flutter_hooks)
- [useAnimationController — pub.dev/documentation/flutter_hooks](https://pub.dev/documentation/flutter_hooks/latest/flutter_hooks/useAnimationController.html)
- [skeletonizer 2.1.3 — pub.dev](https://pub.dev/packages/skeletonizer)
- [go_router Transition Animations — pub.dev](https://pub.dev/documentation/go_router/latest/topics/Transition%20animations-topic.html)
- [5 Hidden Uses of RepaintBoundary in Flutter — medium.com](https://medium.com/@workflow094093/5-hidden-uses-of-repaintboundary-in-flutter-supercharge-your-ui-2de246b2e63a)
- [Flutter Performance Best Practices — docs.flutter.dev](https://docs.flutter.dev/perf/best-practices)
- [fl_chart 1.1.1 — pub.dev](https://pub.dev/packages/fl_chart)
- [Flutter Implicit Animations — docs.flutter.dev](https://docs.flutter.dev/ui/animations/implicit-animations)
- [Adding Micro-Interactions with AnimatedSwitcher — kodeco.com](https://www.kodeco.com/24345609-adding-micro-interactions-with-animatedswitcher)

---
*Architecture research for: Flutter glassmorphism UI redesign — premium design system integration with existing Riverpod + HookConsumerWidget architecture*
*Researched: 2026-02-21*

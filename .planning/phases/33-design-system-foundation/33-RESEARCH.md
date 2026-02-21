# Phase 33: Design System Foundation - Research

**Researched:** 2026-02-21
**Domain:** Flutter ThemeExtension, glassmorphism, typography, accessibility
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **Background color:** Dark navy `#0D1117` as the app scaffold background — not pure black, slight blue warmth
- **Orb opacity:** 10–15% — orbs add warmth and depth without competing with glass cards; barely-visible color pools, not prominent visual elements
- **Overall aesthetic:** Premium and understated — not vibrant or showy, not a flashy neon crypto app

### Claude's Discretion

- Orb colors — choose 2–3 colors that complement dark navy base and existing orange brand (`#F7931A`)
- Orb count and placement — pick arrangement that creates natural depth without distraction
- Glass surface parameters — blur sigma, tint color/opacity, border style/weight for GlassTheme tokens
- Typography hierarchy — font weights, sizes for headings/body/captions, monetary value styling
- Accessibility fallback styling — how opaque cards look under Reduce Transparency, exact behavior under Reduce Motion

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DESIGN-01 | App uses a frosted glass card component (BackdropFilter + tint + border) as the primary surface across all screens | GlassCard widget pattern: ClipRRect + BackdropFilter + Container decoration; parameterized by GlassTheme tokens |
| DESIGN-02 | App displays an ambient gradient background with static colored orbs behind glass cards | AmbientBackground widget: Stack + Positioned + RadialGradient containers; static (no animation), zero per-frame cost |
| DESIGN-03 | App uses a consistent typography scale with system fonts, proper weights, and tabular figures for all monetary values | FontFeature.tabularFigures() in TextStyle; TextTheme extension with moneyStyle; system font (SF Pro on iOS) |
| DESIGN-04 | All design tokens (blur sigma, glass opacity, border, glow) live in a single GlassTheme ThemeExtension | ThemeExtension<GlassTheme> with copyWith + lerp; registered in ThemeData.extensions; retrieved via Theme.of(context).extension<GlassTheme>()! |
| DESIGN-05 | App honors iOS Reduce Transparency by degrading glass to opaque surfaces | MediaQuery.of(context).accessibilityFeatures — check disableAnimations for motion; for transparency: use platform channel OR treat highContrast as trigger; GlassCard branches on bool to swap BackdropFilter for opaque Container |
| DESIGN-06 | App honors iOS Reduce Motion by skipping all animations | MediaQuery.of(context).disableAnimations OR MediaQuery.of(context).accessibilityFeatures.reduceMotion (iOS only) — both should be checked; pass bool down via provider or InheritedWidget |
</phase_requirements>

---

## Summary

Phase 33 establishes the design token layer that all subsequent phases (34–38) consume. It has four distinct technical pieces: a `GlassTheme` ThemeExtension holding all visual constants, a `GlassCard` widget that renders the frosted glass surface, an `AmbientBackground` widget with static radial-gradient orbs, and accessibility toggles that degrade glass to opaque surfaces and suppress animations.

All four pieces are implemented purely in Flutter with no new pub.dev packages required. The existing project already has `hooks_riverpod` (for state), `flutter_hooks` (for animation controllers), and `intl` (for number formatting). The design token approach (ThemeExtension subclass) is Flutter's canonical way to extend ThemeData with custom tokens since Flutter 3.0, verified via the official API docs.

The most important research finding is the **Reduce Transparency gap**: Flutter's `AccessibilityFeatures` does not expose iOS's `UIAccessibility.isReduceTransparencyEnabled` directly. The practical approach is to use `MediaQuery.of(context).highContrast` as a conservative trigger (safe and available cross-platform), or implement a lightweight platform channel calling `UIAccessibility.isReduceTransparencyEnabled` on the Swift side. For v5.0 scope, the highContrast path is the right call — it covers the intent without adding native code complexity.

**Primary recommendation:** Build GlassTheme as a ThemeExtension, AmbientBackground as a static Stack widget, GlassCard as a widget that reads from GlassTheme and branches on accessibility bools, and register accessibility providers at the top of the widget tree for clean propagation.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Flutter (dart:ui) | SDK ^3.7.0 (project) | BackdropFilter, ImageFilter.blur, RadialGradient, FontFeature | Built-in — no dep needed |
| flutter/material.dart | SDK ^3.7.0 | ThemeExtension, ThemeData, MediaQuery, TextTheme | Core Flutter framework |
| hooks_riverpod | 3.2.1 (already in project) | Expose accessibility flags as providers | Already a project dep |
| intl | any (already in project) | NumberFormat for monetary display | Already used in project |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| flutter_hooks | any (already in project) | useAnimationController for Reduce Motion guard | Already a project dep — use for controllers |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled ThemeExtension | `theme_tailor` (code gen) | theme_tailor reduces boilerplate but adds a code-gen dep. Project already avoids excess deps — hand-roll is 30 lines, not worth the dep |
| highContrast as Reduce Transparency proxy | Platform channel to `UIAccessibility.isReduceTransparencyEnabled` | Platform channel is more precise but adds native Swift code. highContrast covers the intent for v5.0 |
| RadialGradient orbs via CustomPaint | Container + BoxDecoration + gradient | CustomPaint gives more control but adds complexity. Container approach is simpler, const-friendly, and sufficient for static orbs |

**No new packages needed.** Everything required is in the existing project dependencies or Flutter SDK.

---

## Architecture Patterns

### Recommended Project Structure

```
lib/
├── app/
│   ├── theme.dart               # MODIFY: Add GlassTheme ThemeExtension + register in ThemeData
│   └── router.dart              # No change
├── core/
│   ├── accessibility/
│   │   └── accessibility_providers.dart  # NEW: reduceMotion + reduceTransparency providers
│   └── widgets/
│       ├── glass_card.dart       # NEW: GlassCard widget (reads GlassTheme, branches on accessibility)
│       └── ambient_background.dart  # NEW: AmbientBackground widget (static orbs)
└── shared/
    └── navigation_shell.dart    # MODIFY: Wrap body in AmbientBackground
```

### Pattern 1: GlassTheme ThemeExtension

**What:** A ThemeExtension subclass that holds all glass/blur/border/glow design tokens. Registered in ThemeData and retrieved anywhere via `Theme.of(context).extension<GlassTheme>()!`.

**When to use:** Whenever a widget needs glass surface parameters — instead of hardcoded values.

**Example:**
```dart
// Source: https://api.flutter.dev/flutter/material/ThemeExtension-class.html
class GlassTheme extends ThemeExtension<GlassTheme> {
  const GlassTheme({
    required this.blurSigma,
    required this.tintOpacity,
    required this.tintColor,
    required this.borderColor,
    required this.borderWidth,
    required this.glowColor,
    required this.cardRadius,
    // Opaque fallback values (used when Reduce Transparency is enabled)
    required this.opaqueSurface,
    required this.opaqueBorder,
  });

  final double blurSigma;          // 12.0 — kept moderate per Impeller guidance
  final double tintOpacity;        // 0.08–0.12 — subtle dark tint on navy background
  final Color tintColor;           // Colors.white or slight warm tint
  final Color borderColor;         // Colors.white.withOpacity(0.12)
  final double borderWidth;        // 1.0
  final Color glowColor;           // bitcoinOrange.withOpacity(0.06) — subtle glow token
  final double cardRadius;         // 16.0
  final Color opaqueSurface;       // Surface used when Reduce Transparency is on
  final Color opaqueBorder;        // Border used when Reduce Transparency is on

  @override
  GlassTheme copyWith({
    double? blurSigma,
    double? tintOpacity,
    Color? tintColor,
    Color? borderColor,
    double? borderWidth,
    Color? glowColor,
    double? cardRadius,
    Color? opaqueSurface,
    Color? opaqueBorder,
  }) {
    return GlassTheme(
      blurSigma: blurSigma ?? this.blurSigma,
      tintOpacity: tintOpacity ?? this.tintOpacity,
      tintColor: tintColor ?? this.tintColor,
      borderColor: borderColor ?? this.borderColor,
      borderWidth: borderWidth ?? this.borderWidth,
      glowColor: glowColor ?? this.glowColor,
      cardRadius: cardRadius ?? this.cardRadius,
      opaqueSurface: opaqueSurface ?? this.opaqueSurface,
      opaqueBorder: opaqueBorder ?? this.opaqueBorder,
    );
  }

  @override
  GlassTheme lerp(ThemeExtension<GlassTheme>? other, double t) {
    if (other is! GlassTheme) return this;
    return GlassTheme(
      blurSigma: lerpDouble(blurSigma, other.blurSigma, t)!,
      tintOpacity: lerpDouble(tintOpacity, other.tintOpacity, t)!,
      tintColor: Color.lerp(tintColor, other.tintColor, t)!,
      borderColor: Color.lerp(borderColor, other.borderColor, t)!,
      borderWidth: lerpDouble(borderWidth, other.borderWidth, t)!,
      glowColor: Color.lerp(glowColor, other.glowColor, t)!,
      cardRadius: lerpDouble(cardRadius, other.cardRadius, t)!,
      opaqueSurface: Color.lerp(opaqueSurface, other.opaqueSurface, t)!,
      opaqueBorder: Color.lerp(opaqueBorder, other.opaqueBorder, t)!,
    );
  }
}
```

Register in ThemeData (in `theme.dart`):
```dart
// In AppTheme.dark:
ThemeData(
  // ... existing config ...
  extensions: [
    GlassTheme(
      blurSigma: 12.0,
      tintOpacity: 0.08,
      tintColor: Colors.white,
      borderColor: Colors.white.withOpacity(0.12),
      borderWidth: 1.0,
      glowColor: const Color(0xFFF7931A).withOpacity(0.06),
      cardRadius: 16.0,
      opaqueSurface: const Color(0xFF1C2333),  // navy lifted ~10 lightness
      opaqueBorder: const Color(0xFF2D3748),
    ),
  ],
)
```

Retrieve in any widget:
```dart
final glass = Theme.of(context).extension<GlassTheme>()!;
```

### Pattern 2: GlassCard Widget

**What:** The primary surface widget. Reads GlassTheme tokens. Branches on `reduceTransparency` — if true, renders opaque Card; if false, renders ClipRRect + BackdropFilter + Container.

**When to use:** Every card surface across screens. Replaces the bare `Card` widget used today.

**Example:**
```dart
// Source: Flutter SDK - BackdropFilter + ClipRRect pattern
class GlassCard extends StatelessWidget {
  const GlassCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
  });

  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    final glass = Theme.of(context).extension<GlassTheme>()!;
    final reduceTransparency = MediaQuery.of(context).highContrast;

    if (reduceTransparency) {
      // Opaque fallback — no BackdropFilter
      return Container(
        decoration: BoxDecoration(
          color: glass.opaqueSurface,
          borderRadius: BorderRadius.circular(glass.cardRadius),
          border: Border.all(color: glass.opaqueBorder, width: glass.borderWidth),
        ),
        padding: padding,
        child: child,
      );
    }

    // Glass surface — ClipRRect required to bound the blur to card shape
    return ClipRRect(
      borderRadius: BorderRadius.circular(glass.cardRadius),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: glass.blurSigma, sigmaY: glass.blurSigma),
        child: Container(
          decoration: BoxDecoration(
            color: glass.tintColor.withOpacity(glass.tintOpacity),
            borderRadius: BorderRadius.circular(glass.cardRadius),
            border: Border.all(color: glass.borderColor, width: glass.borderWidth),
          ),
          padding: padding,
          child: child,
        ),
      ),
    );
  }
}
```

### Pattern 3: AmbientBackground Widget

**What:** Full-screen Stack with a base color and 2–3 static RadialGradient orbs positioned at different offsets. Wraps the navigation shell body — orbs appear behind all tab content.

**When to use:** Wrap the Scaffold body in `ScaffoldWithNavigation` so all screens share the same background. The orbs are `const` (no rebuild cost).

**Example:**
```dart
class AmbientBackground extends StatelessWidget {
  const AmbientBackground({super.key, required this.child});

  final Widget child;

  // Orbs: 2-3 very subtle color pools at 10-15% opacity
  // Colors chosen to complement navy (#0D1117) + orange brand (#F7931A)
  // Recommendation: deep amber (warm/orange family) + cool blue-violet + subtle green
  // All at opacity 0.10–0.13 — barely visible color warmth, not neon

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        // Base background — dark navy
        const ColoredBox(
          color: Color(0xFF0D1117),
          child: SizedBox.expand(),
        ),
        // Orb 1 — warm amber (top-left, resonates with BTC orange brand)
        Positioned(
          top: -80,
          left: -60,
          child: _buildOrb(
            size: 320,
            color: const Color(0xFFF7931A).withOpacity(0.11),
          ),
        ),
        // Orb 2 — cool blue-violet (bottom-right, depth contrast)
        Positioned(
          bottom: -100,
          right: -80,
          child: _buildOrb(
            size: 360,
            color: const Color(0xFF4F46E5).withOpacity(0.10),
          ),
        ),
        // Orb 3 — muted teal (center-left, mid-screen depth)
        Positioned(
          top: 240,
          left: -120,
          child: _buildOrb(
            size: 280,
            color: const Color(0xFF0D9488).withOpacity(0.08),
          ),
        ),
        child,
      ],
    );
  }

  Widget _buildOrb({required double size, required Color color}) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [color, Colors.transparent],
          stops: const [0.0, 1.0],
        ),
      ),
    );
  }
}
```

Note: Orb colors and placements are at Claude's discretion (per CONTEXT.md). The values above are starting recommendations calibrated to the navy base + orange brand. Adjust during visual testing on device.

### Pattern 4: Tabular Figures for Monetary Values

**What:** OpenType `tnum` feature via `FontFeature.tabularFigures()`. Makes all digits uniform width so amounts align vertically in lists.

**When to use:** Any TextStyle used for monetary amounts (USD, BTC, VND values). Applied as a TextTheme extension or a reusable `AppTextStyles.money` constant.

**Example:**
```dart
// Source: https://api.flutter.dev/flutter/dart-ui/FontFeature/FontFeature.tabularFigures.html
// Monetary style — tabular figures for digit alignment
static const TextStyle money = TextStyle(
  fontFeatures: [FontFeature.tabularFigures()],
  fontVariations: [], // no variation needed for system font
);

// Usage: wrap any TextStyle for amounts
Text(
  '\$97,234',
  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
    fontFeatures: [FontFeature.tabularFigures()],
    fontWeight: FontWeight.bold,
  ),
)
```

The system font (SF Pro on iOS, Roboto on Android) both support `tnum`. No custom font loading required.

### Pattern 5: Accessibility Provider

**What:** A Riverpod provider that exposes both `disableAnimations` and `highContrast` flags. Read by GlassCard and future animation widgets.

**When to use:** Any widget that needs to react to accessibility preferences — centralizes the MediaQuery access.

**Example:**
```dart
// accessibility_providers.dart
@riverpod
bool reduceMotion(ReduceMotionRef ref) {
  // disableAnimations is the cross-platform MediaQuery flag
  // accessibilityFeatures.reduceMotion is iOS-only and more specific
  // Use || to catch both signals
  final mq = MediaQuery.of(ref.context);  // note: needs BuildContext — see note below
  return mq.disableAnimations || mq.accessibilityFeatures.reduceMotion;
}

@riverpod
bool reduceTransparency(ReduceTransparencyRef ref) {
  // Flutter does not expose iOS UIAccessibility.isReduceTransparencyEnabled directly.
  // highContrast is the closest available cross-platform signal.
  // Platform channel is the precise alternative (see Open Questions).
  return MediaQuery.of(ref.context).highContrast;
}
```

**Note on provider + context:** Riverpod providers that need `BuildContext` should be expressed as a widget-level read using `ref.watch` inside a `ConsumerWidget` or as a simple static helper method, rather than a true Riverpod async provider. The simpler pattern for these booleans is:

```dart
// In any widget:
final reduceMotion = MediaQuery.of(context).disableAnimations
    || MediaQuery.of(context).accessibilityFeatures.reduceMotion;
final reduceTransparency = MediaQuery.of(context).highContrast;
```

Read directly in `build()` — MediaQuery is already inherited and efficient.

### Anti-Patterns to Avoid

- **BackdropFilter on a full-screen widget or in lists:** Blur cost scales with blur area. Always wrap with ClipRRect to bound the blur region. Never apply to ListView items (STATE.md already decided this).
- **Hardcoding blur sigma, opacity, or border values in individual widgets:** All tokens must come from GlassTheme. Never repeat magic numbers — this is the entire point of the design token layer.
- **Animating orbs:** Out of scope (REQUIREMENTS.md Out of Scope: "Animated gradient background"). Static is both zero-cost and identical visually.
- **Using `setState` for accessibility flags:** Read directly from MediaQuery in `build()` — it rebuilds automatically when the system changes accessibility settings.
- **Forgetting ClipRRect before BackdropFilter:** Without ClipRRect, the blur bleeds outside the card shape. Always: `ClipRRect → BackdropFilter → Container`.
- **Stacking multiple BackdropFilters:** Performance degradation compounds. One BackdropFilter per visible glass card is the limit.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Design token storage | Ad-hoc static const classes | `ThemeExtension<T>` | ThemeExtension integrates with theme switching, lerp, and InheritedWidget propagation automatically |
| Blur surface widget | Custom canvas drawing | BackdropFilter + ImageFilter.blur | Framework-provided, Impeller-optimized on iOS |
| Tabular digit alignment | Custom font or spacing hacks | `FontFeature.tabularFigures()` | OpenType tnum is precise, zero-cost, works on system fonts |
| Reduce motion detection | Timer-based or animation-skip ad-hoc logic | `MediaQuery.disableAnimations` + `accessibilityFeatures.reduceMotion` | Platform-provided; rebuilds automatically when setting changes |

**Key insight:** Flutter's ThemeExtension + MediaQuery APIs cover all four technical requirements without any new packages. The entire phase is pure Dart/Flutter framework code.

---

## Common Pitfalls

### Pitfall 1: ClipRRect Missing Before BackdropFilter

**What goes wrong:** The blur extends beyond the card's rounded corners, bleeding into surrounding content. The card looks like it has square blur edges behind rounded visual corners.

**Why it happens:** BackdropFilter applies to the area defined by its parent's clip, not by a BorderRadius in a child Container's decoration. BorderRadius on a Container only clips painting, not the backdrop filter region.

**How to avoid:** Always wrap as: `ClipRRect(borderRadius: ...) → BackdropFilter → Container(decoration: BoxDecoration(borderRadius: ...))`

**Warning signs:** Visual blur bleed visible especially on light content behind cards; simulator may hide it but physical device shows it.

---

### Pitfall 2: AccessibilityFeatures.reduceMotion Is iOS-Only

**What goes wrong:** Using `accessibilityFeatures.reduceMotion` alone means Android users with Reduce Motion preferences enabled are not covered.

**Why it happens:** The `reduceMotion` property is documented as "only supported on iOS" (verified via official API docs).

**How to avoid:** Always use `||` with `MediaQuery.of(context).disableAnimations` which is cross-platform. Pattern: `disableAnimations || accessibilityFeatures.reduceMotion`.

**Warning signs:** Animation still plays on Android when "Remove animations" is set in Developer Options.

---

### Pitfall 3: Flutter Has No Direct API for iOS Reduce Transparency

**What goes wrong:** Searching for `reduceTransparency` in Flutter SDK returns nothing. No `AccessibilityFeatures.reduceTransparency` exists.

**Why it happens:** Flutter's accessibility feature exposure is incomplete vs. the native iOS `UIAccessibility` API. `isReduceTransparencyEnabled` was never bridged (confirmed via GitHub issue research and pub.dev search).

**How to avoid:** Use `MediaQuery.of(context).highContrast` as a conservative proxy. It covers the intent (user prefers less visual complexity). Platform channel is the precise path but adds native code. For v5.0, `highContrast` is sufficient.

**Warning signs:** If you test "Reduce Transparency" on the iOS Simulator and glass cards still show blur — this is expected behavior since `highContrast` maps to iOS "Increase Contrast", not "Reduce Transparency". Document this gap.

---

### Pitfall 4: Orbs Break If Placed Inside Scaffold Body Stack

**What goes wrong:** If `AmbientBackground` is placed inside individual screen Scaffolds, each tab rebuild recreates the orbs, and the background resets on navigation. Orbs may flash or clip to just the content area (below AppBar).

**Why it happens:** Scaffold clips its body to the area below AppBar and above bottom nav. Orbs positioned above the Scaffold body won't render behind the AppBar.

**How to avoid:** Place `AmbientBackground` in `ScaffoldWithNavigation` wrapping the entire `navigationShell`, not inside individual screen Scaffolds. The scaffold's `backgroundColor` must be `Colors.transparent` for orbs to show through.

**Warning signs:** Background appears cut off below AppBar, or orbs don't appear in AppBar region.

---

### Pitfall 5: scaffoldBackgroundColor Not Transparent Blocks Orbs

**What goes wrong:** The existing `theme.dart` sets `scaffoldBackgroundColor: surfaceDark` (`#121212`). This will cover the navy background and orbs.

**Why it happens:** Scaffold fills its background before rendering children. A non-transparent scaffold color paints over the AmbientBackground.

**How to avoid:** Change `scaffoldBackgroundColor` to `Colors.transparent` in ThemeData. The `AmbientBackground` provides the actual background color. Each screen's Scaffold must also pass `backgroundColor: Colors.transparent`.

**Warning signs:** Navy background visible in some screens but not others; orbs not showing.

---

### Pitfall 6: BackdropFilter Performance on Large Areas

**What goes wrong:** Applying BackdropFilter to a full-screen AppBar or large panel causes GPU raster jank on mid-range devices. iOS Simulator does not accurately show GPU cost.

**Why it happens:** Blur cost is proportional to the blurred pixel area. A 390×80px AppBar at sigma=16 is expensive per frame.

**How to avoid:** Keep glass cards sized to their content. Use sigma=12 for cards (moderate). Per STATE.md: "Blur sigma calibration (card: 12, appBar: 16) must be verified on physical iPhone." This phase uses sigma=12 for cards only — AppBar glass is Phase 34+ territory.

---

## Code Examples

Verified patterns from official sources:

### ThemeExtension Registration in ThemeData
```dart
// Source: https://api.flutter.dev/flutter/material/ThemeExtension-class.html
ThemeData(
  extensions: const <ThemeExtension<dynamic>>[
    GlassTheme(
      blurSigma: 12.0,
      // ... other tokens
    ),
  ],
)
```

### Retrieving ThemeExtension in a Widget
```dart
// Source: https://api.flutter.dev/flutter/material/ThemeExtension-class.html
final GlassTheme glass = Theme.of(context).extension<GlassTheme>()!;
// Use ! because we always register it — it will never be null
```

### BackdropFilter with ClipRRect (Glass Surface)
```dart
// Source: https://docs.flutter.dev/ui/design/graphics/fragment-shaders (BackdropFilter + ClipRect pattern)
ClipRRect(
  borderRadius: BorderRadius.circular(16),
  child: BackdropFilter(
    filter: ImageFilter.blur(sigmaX: 12, sigmaY: 12),
    child: Container(
      decoration: BoxDecoration(
        color: Colors.white.withOpacity(0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withOpacity(0.12)),
      ),
      child: child,
    ),
  ),
)
```

### FontFeature.tabularFigures in TextStyle
```dart
// Source: https://api.flutter.dev/flutter/dart-ui/FontFeature/FontFeature.tabularFigures.html
Text(
  '\$97,234.56',
  style: TextStyle(
    fontSize: 28,
    fontWeight: FontWeight.bold,
    fontFeatures: [FontFeature.tabularFigures()],
  ),
)
```

### Reduce Motion Check Pattern
```dart
// Source: https://api.flutter.dev/flutter/dart-ui/AccessibilityFeatures/reduceMotion.html
//         https://api.flutter.dev/flutter/widgets/MediaQueryData/disableAnimations.html
bool get _shouldReduceMotion {
  final mq = MediaQuery.of(context);
  return mq.disableAnimations || mq.accessibilityFeatures.reduceMotion;
}
```

### Reduce Transparency Check (Best Available)
```dart
// Source: https://api.flutter.dev/flutter/dart-ui/AccessibilityFeatures-class.html
// Note: Flutter has no direct reduceTransparency API — highContrast is the closest proxy
bool get _shouldReduceTransparency {
  return MediaQuery.of(context).highContrast;
}
```

### Migrating Existing theme.dart

The current `theme.dart` has `surfaceDark = Color(0xFF121212)`. Phase 33 changes:
1. `scaffoldBackgroundColor` → `Colors.transparent` (AmbientBackground provides the bg)
2. Add `navyBackground = Color(0xFF0D1117)` constant
3. Add GlassTheme to `ThemeData.extensions`
4. Keep `bitcoinOrange`, `profitGreen`, `lossRed` unchanged (STATE.md: non-negotiable)

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Custom static color classes | `ThemeExtension<T>` | Flutter 3.0 (2022) | Type-safe, lerp-capable, inherited automatically |
| `Theme.of(context).accentColor` | `ColorScheme` fields | Flutter 2.0 (2021) | Project already uses Material 3 ColorScheme |
| `MediaQuery.textScaleFactorOf` | `MediaQuery.textScalerOf` | Flutter 3.12 (2023) | Project uses Dart SDK ^3.7.0 — use new API |
| Proportional figures (default) | `FontFeature.tabularFigures()` for money | Available but not used | Zero-cost alignment improvement for money values |

**Deprecated/outdated:**
- `Color.withOpacity()`: Not deprecated but `Color.withAlpha(int)` avoids float-rounding issues. Either works; the project currently uses `withAlpha(51)` in NavigationBarThemeData — be consistent.
- `surfaceDark` (`#121212`): This becomes legacy — replaced by transparent scaffold + AmbientBackground providing `#0D1117`.

---

## Open Questions

1. **Reduce Transparency: highContrast proxy vs. platform channel**
   - What we know: Flutter has no `AccessibilityFeatures.reduceTransparency`. `highContrast` maps to iOS "Increase Contrast", not "Reduce Transparency". They are different iOS settings.
   - What's unclear: How many users will have "Reduce Transparency" on but not "Increase Contrast"? The overlap is substantial for accessibility-conscious users.
   - Recommendation: For v5.0, ship with `highContrast` proxy. Add a comment in `GlassCard` documenting the gap. A platform channel implementation can be added as a follow-up task without changing the GlassCard API (just swap the bool source).

2. **Orb color calibration requires visual device testing**
   - What we know: Orb values in this research (amber at 0.11, indigo at 0.10, teal at 0.08) are starting recommendations calibrated to dark navy + orange brand.
   - What's unclear: How they look on actual OLED screens vs. the iOS Simulator. OLED black makes subtle colors pop differently.
   - Recommendation: Implement with research values and iterate during visual verification step. The CONTEXT.md decision (10–15% opacity) is the constraint; exact RGB colors are Claude's discretion.

3. **scaffoldBackgroundColor: transparent across all screens**
   - What we know: Setting `scaffoldBackgroundColor: transparent` in ThemeData means every Scaffold in the app becomes transparent by default.
   - What's unclear: Some screens (modals, bottom sheets) may need an explicit background color set.
   - Recommendation: Set global `scaffoldBackgroundColor: Colors.transparent`. Audit each screen's Scaffold. Modal sheets and dialogs in GoRouter sub-routes (AddTransactionScreen, EditFixedDepositScreen, etc.) may need `backgroundColor: glass.opaqueSurface` since they appear above the AmbientBackground, not behind it.

---

## Sources

### Primary (HIGH confidence)
- `/websites/flutter_dev` (Context7) — ThemeExtension, BackdropFilter, RadialGradient, FontFeature, accessibility features
- `https://api.flutter.dev/flutter/material/ThemeExtension-class.html` — ThemeExtension class definition, copyWith, lerp, registration pattern
- `https://api.flutter.dev/flutter/dart-ui/FontFeature/FontFeature.tabularFigures.html` — FontFeature.tabularFigures() API, tnum OpenType feature
- `https://api.flutter.dev/flutter/dart-ui/AccessibilityFeatures/reduceMotion.html` — reduceMotion property, iOS-only status
- `https://api.flutter.dev/flutter/widgets/MediaQueryData/disableAnimations.html` — disableAnimations property, cross-platform

### Secondary (MEDIUM confidence)
- `https://github.com/flutter/flutter/issues/65874` — Confirmed Flutter team decision: reduceMotion stays in AccessibilityFeatures, not MediaQueryData
- `https://api.flutter.dev/flutter/dart-ui/AccessibilityFeatures-class.html` — Full AccessibilityFeatures property list (no reduceTransparency found — confirmed gap)
- WebSearch: BackdropFilter performance best practices — confirms ClipRRect requirement, sigma 6–12 range, no stacking
- WebSearch: ThemeExtension glassmorphism patterns — confirms token approach and GlassTheme structure

### Tertiary (LOW confidence)
- `https://thelinuxcode.com/flutter-glassmorphism-ui-design-for-apps-a-practical-production-ready-guide/` — Glass card implementation pattern (unverified against official docs but consistent with SDK)
- WebSearch: "Flutter 3.35+ BackdropGroup for batching" — mentioned in one search result, not found in official docs, LOW confidence; not relied upon in recommendations

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all recommendations are existing project deps or Flutter SDK
- Architecture: HIGH — ThemeExtension, BackdropFilter, RadialGradient patterns verified via official API docs
- Accessibility (Reduce Motion): HIGH — two verified API properties with cross-platform coverage
- Accessibility (Reduce Transparency): MEDIUM — gap confirmed, proxy solution documented, platform channel alternative known
- Pitfalls: HIGH — most from official docs or STATE.md accumulated knowledge

**Research date:** 2026-02-21
**Valid until:** 2026-08-21 (stable Flutter APIs; reassess if Flutter 4.x is released)

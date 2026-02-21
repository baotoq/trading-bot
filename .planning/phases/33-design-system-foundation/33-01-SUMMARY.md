---
phase: 33-design-system-foundation
plan: 01
subsystem: ui
tags: [flutter, glassmorphism, theme-extension, design-tokens, typography, ambient-background]

# Dependency graph
requires: []
provides:
  - GlassTheme ThemeExtension with 9 glass design tokens registered in ThemeData.extensions
  - AmbientBackground widget with dark navy base and 3 static radial gradient orbs
  - Navigation shell integration so all tab screens share the ambient background
  - moneyStyle TextStyle constant with FontFeature.tabularFigures() for monetary values
  - navyBackground Color(0xFF0D1117) constant in AppTheme
  - transparent scaffoldBackgroundColor enabling glass surfaces across all screens
affects: [34-glass-card, 35-home-screen, 36-portfolio-screen, 37-history-screen, 38-chart-screen]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ThemeExtension<T> for custom design tokens (GlassTheme) — retrieved via Theme.of(context).extension<GlassTheme>()!
    - AmbientBackground StatelessWidget wrapping navigation shell body for shared background
    - FontFeature.tabularFigures() on monetary TextStyles for digit alignment
    - transparent scaffoldBackgroundColor + AmbientBackground separation of concerns

key-files:
  created:
    - TradingBot.Mobile/lib/core/widgets/ambient_background.dart
  modified:
    - TradingBot.Mobile/lib/app/theme.dart
    - TradingBot.Mobile/lib/shared/navigation_shell.dart

key-decisions:
  - "GlassTheme as ThemeExtension<GlassTheme> — single source of truth for all glass tokens, never hardcode blur/opacity/border values in widgets"
  - "scaffoldBackgroundColor: Colors.transparent in ThemeData — AmbientBackground provides the actual navy base, Scaffold must not paint over it"
  - "AmbientBackground wraps navigationShell body (not individual screens) — prevents per-tab-switch orb recreation and ensures orbs appear in AppBar region"
  - "Color.withAlpha(int) used instead of withOpacity(float) — consistent with existing project pattern in NavigationBarThemeData"
  - "moneyStyle is a partial TextStyle (feature flag only) — consumers merge via copyWith/merge rather than getting a complete style"

patterns-established:
  - "Pattern 1: Theme.of(context).extension<GlassTheme>()! — retrieval pattern for all glass tokens in subsequent widget phases"
  - "Pattern 2: AmbientBackground(child: ...) wrapping at navigation shell level — do NOT place inside individual screen Scaffolds"
  - "Pattern 3: AppTheme.moneyStyle merge for monetary values — style.merge(AppTheme.moneyStyle) or style.copyWith(fontFeatures: [FontFeature.tabularFigures()])"

requirements-completed: [DESIGN-02, DESIGN-03, DESIGN-04]

# Metrics
duration: 2min
completed: 2026-02-21
---

# Phase 33 Plan 01: Design System Foundation Summary

**GlassTheme ThemeExtension with 9 design tokens, AmbientBackground widget with 3 static radial gradient orbs on dark navy (#0D1117), and tabular figure typography — all wired into the navigation shell as the glassmorphism foundation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-21T07:35:03Z
- **Completed:** 2026-02-21T07:37:11Z
- **Tasks:** 2
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- Established GlassTheme ThemeExtension as the single source of truth for all glass design tokens (blurSigma, tintOpacity, tintColor, borderColor, borderWidth, glowColor, cardRadius, opaqueSurface, opaqueBorder) with proper copyWith and lerp implementations
- Created AmbientBackground StatelessWidget with dark navy base (#0D1117) and 3 static radial gradient orbs (warm amber top-left, cool indigo bottom-right, muted teal center-left) at 8-11% opacity — placed in navigation shell so all tab screens share the background
- Added moneyStyle TextStyle constant with FontFeature.tabularFigures() for monetary value alignment, and changed scaffoldBackgroundColor to Colors.transparent to allow AmbientBackground to render through

## Task Commits

Each task was committed atomically:

1. **Task 1: GlassTheme ThemeExtension and design tokens** - `aa0bfb4` (feat)
2. **Task 2: AmbientBackground widget and navigation shell integration** - `dd4726e` (feat)

**Plan metadata:** (pending docs commit)

## Files Created/Modified

- `TradingBot.Mobile/lib/app/theme.dart` - Added GlassTheme ThemeExtension class, navyBackground constant, moneyStyle TextStyle, transparent scaffoldBackgroundColor, GlassTheme registered in ThemeData.extensions
- `TradingBot.Mobile/lib/core/widgets/ambient_background.dart` - New AmbientBackground StatelessWidget with SizedBox.expand ColoredBox base and 3 Positioned radial gradient orb Containers
- `TradingBot.Mobile/lib/shared/navigation_shell.dart` - Added AmbientBackground import, wrapped navigationShell with AmbientBackground, set Scaffold backgroundColor to Colors.transparent

## Decisions Made

- Used `Color.withAlpha(int)` instead of `withOpacity(double)` for orb colors, consistent with the existing `bitcoinOrange.withAlpha(51)` pattern in NavigationBarThemeData. Avoids float-rounding inconsistencies.
- moneyStyle defined as a partial TextStyle (fontFeatures only) rather than a complete style. Consumers merge it into their existing TextStyle to preserve font size, weight, and color from the text theme.
- AmbientBackground wraps the `body` parameter of the ScaffoldWithNavigation Scaffold, not the entire Scaffold. This keeps orbs behind content while the NavigationBar retains its own navBarDark background.
- GlassTheme registered without `const` keyword in the extensions list since `withAlpha()` calls cannot be made const. The ThemeExtension instance is created once at theme construction time.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Flutter analyzer found no issues in any of the 3 files on all three analysis runs (per-file and full project).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- GlassTheme tokens ready for Phase 34 GlassCard implementation: `Theme.of(context).extension<GlassTheme>()!` retrieval pattern established
- AmbientBackground in place behind all 5 tab screens — subsequent screen phases don't need to set up background
- scaffoldBackgroundColor is transparent globally — individual screen Scaffolds must NOT set a solid backgroundColor unless they are modals/dialogs that appear above the ambient background
- moneyStyle ready for monetary TextStyle merging in Home, Portfolio, and History screens

---
phase: 34-shared-glass-primitives
plan: 02
subsystem: ui
tags: [flutter, skeletonizer, shimmer, loading-states, accessibility, glassmorphism]

# Dependency graph
requires:
  - phase: 34-01
    provides: GlassCard with shouldReduceMotion helper used by AppShimmer
  - phase: 33-02
    provides: GlassTheme tokens (opaqueSurface, opaqueBorder) used as shimmer colors

provides:
  - skeletonizer package integrated into Flutter mobile app
  - AppShimmer wrapper widget with dark-themed colors and reduce-motion support
  - Shimmer skeleton loading states on all 5 tab screens replacing CircularProgressIndicator

affects: [35-home-screen-redesign, 36-chart-screen, 37-history-screen, 38-config-portfolio-screens]

# Tech tracking
tech-stack:
  added: [skeletonizer ^1.4.0 (resolved 1.4.3)]
  patterns:
    - AppShimmer wraps Skeletonizer for app-wide dark-themed shimmer configuration
    - _buildLoadingSkeleton() private method per screen for loading state widget tree
    - Bone.text/Bone.icon/Bone widgets for semantic skeleton placeholder shapes
    - PulseEffect (reduce motion) vs ShimmerEffect (normal) selected via GlassCard.shouldReduceMotion

key-files:
  created:
    - TradingBot.Mobile/lib/core/widgets/shimmer_loading.dart
  modified:
    - TradingBot.Mobile/pubspec.yaml
    - TradingBot.Mobile/pubspec.lock
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart
    - TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart
    - TradingBot.Mobile/lib/features/history/presentation/history_screen.dart
    - TradingBot.Mobile/lib/features/config/presentation/config_screen.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/portfolio_screen.dart

key-decisions:
  - "skeletonizer ^1.4.0 pinned per plan — resolved to 1.4.3 (latest stable matching constraint)"
  - "AppShimmer uses GlassTheme opaqueSurface (0xFF1C2333) as baseColor and opaqueBorder (0xFF2D3748) as highlightColor for visual continuity with glass card surfaces"
  - "Reduce Motion: PulseEffect (gentle vertical fade) when disableAnimations=true; ShimmerEffect (lateral sweep) otherwise — consistent with GlassCard.shouldReduceMotion pattern"
  - "Pagination load-more CircularProgressIndicator in HistoryScreen retained — it is not an initial loading state, distinct from the _buildLoadingSkeleton() path"
  - "SoldColorEffect skipped — PulseEffect chosen as reduce-motion fallback since it still communicates loading state via subtle fade without lateral motion"

patterns-established:
  - "Screen-level loading skeleton: each screen has _buildLoadingSkeleton() returning AppShimmer wrapping a structurally-identical placeholder widget tree"
  - "Bone.text(words: N, fontSize: F) for text placeholders matching approximate real text dimensions"
  - "Bone(width, height, borderRadius) for custom-shaped placeholders (charts, badges, icons)"
  - "Bone.icon() for icon-sized circular placeholders in list tiles"

requirements-completed: [ANIM-01]

# Metrics
duration: 6min
completed: 2026-02-21
---

# Phase 34 Plan 02: Shimmer Skeleton Loading States Summary

**skeletonizer 1.4.3 integrated with AppShimmer wrapper using dark glassmorphism colors (0xFF1C2333/0xFF2D3748), replacing CircularProgressIndicator on all 5 tab screens with layout-matched skeleton placeholders**

## Performance

- **Duration:** 6 min
- **Started:** 2026-02-21T09:42:59Z
- **Completed:** 2026-02-21T09:49:00Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- Added skeletonizer ^1.4.0 (resolved 1.4.3) and created `AppShimmer` — a single-source-of-truth wrapper that configures dark-themed shimmer colors matching GlassTheme tokens and selects PulseEffect vs ShimmerEffect based on Reduce Motion accessibility preference
- Replaced initial `CircularProgressIndicator` loading states on all 5 tab screens (Home, Chart, History, Config, Portfolio) with `_buildLoadingSkeleton()` methods that use layout-matched `Bone` widget trees
- Full `flutter analyze` passes with zero errors across the entire mobile project after all changes

## Task Commits

Each task was committed atomically:

1. **Task 1: Add skeletonizer package and create AppShimmer wrapper widget** - `cdc8c8e` (feat)
2. **Task 2: Replace CircularProgressIndicator with shimmer skeletons on all 5 screens** - `dd4a8b0` (feat)

## Files Created/Modified

- `TradingBot.Mobile/pubspec.yaml` - Added `skeletonizer: ^1.4.0` dependency after fl_chart
- `TradingBot.Mobile/pubspec.lock` - Resolved skeletonizer 1.4.3
- `TradingBot.Mobile/lib/core/widgets/shimmer_loading.dart` - AppShimmer widget with dark-themed shimmer and reduce-motion support
- `TradingBot.Mobile/lib/features/home/presentation/home_screen.dart` - Skeleton: portfolio value hero + 2x2 stat grid + countdown + last buy card
- `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` - Skeleton: 300px chart area + price label bones
- `TradingBot.Mobile/lib/features/history/presentation/history_screen.dart` - Skeleton: 7 fake purchase rows matching PurchaseListItem layout
- `TradingBot.Mobile/lib/features/config/presentation/config_screen.dart` - Skeleton: 3 card groups (DCA Settings, Market Analysis, Multiplier Tiers)
- `TradingBot.Mobile/lib/features/portfolio/presentation/portfolio_screen.dart` - Skeleton: summary card + 200px donut chart + 3 asset rows

## Decisions Made

- skeletonizer `^1.4.0` pinned per plan spec; resolved to 1.4.3 (closest available matching version)
- `AppShimmer` uses `GlassTheme.opaqueSurface` (0xFF1C2333) as base and `GlassTheme.opaqueBorder` (0xFF2D3748) as highlight — these opaque surface tokens guarantee visual continuity between skeleton and real glassmorphism card surfaces
- `PulseEffect` chosen as reduce-motion fallback over `SoldColorEffect` because it still communicates loading state via a subtle alpha fade, while `SoldColorEffect` is completely static (no indication of ongoing load); `SoldColorEffect` is more appropriate for accessibility-critical contexts with full animation disable
- Pagination `CircularProgressIndicator` in `HistoryScreen._buildList()` retained — it fires during end-of-page load-more, not the initial loading state (separate code path from `_buildLoadingSkeleton()`)

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- Flutter binary not on shell PATH for Claude Code sessions. Found at `/Users/baotoq/flutter/bin/flutter`. Used full path prefix `export PATH="/Users/baotoq/flutter/bin:$PATH"` for all `flutter` commands. No code changes required.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- All 5 tab screens now show glassmorphism-consistent shimmer skeletons during loading — the loading experience matches the premium dark aesthetic
- AppShimmer is the established pattern for any future screens added in phases 35-38
- Reduce Motion accessibility is handled centrally via AppShimmer, future screens inherit it automatically

---
*Phase: 34-shared-glass-primitives*
*Completed: 2026-02-21*

---
phase: 34-shared-glass-primitives
plan: 01
subsystem: ui
tags: [flutter, dart, glassmorphism, animation, accessibility, haptics]

# Dependency graph
requires:
  - phase: 33-design-system-foundation
    provides: GlassCard widget, GlassTheme tokens, shouldReduceMotion static method
provides:
  - GlassVariant enum (stationary, scrollItem) for GlassCard rendering mode selection
  - GlassCard.scrollItem non-blur rendering path for scroll-safe list items
  - PressableScale widget for press-scale micro-interaction with haptic feedback
affects:
  - phases 35-38 (all feature screens using GlassCard and PressableScale)
  - History screen (scrollItem variant for list items)
  - Portfolio screen (scrollItem variant for list items)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - GlassVariant enum for rendering mode selection without call-site changes beyond passing variant parameter
    - didChangeDependencies for accessibility checks requiring BuildContext (not initState)
    - AnimatedBuilder + Transform.scale pattern for explicit press-scale animation
    - HapticFeedback.lightImpact() on onTapDown for immediate haptic response

key-files:
  created:
    - TradingBot.Mobile/lib/core/widgets/pressable_scale.dart
  modified:
    - TradingBot.Mobile/lib/core/widgets/glass_card.dart

key-decisions:
  - "GlassVariant.scrollItem renders Container without BackdropFilter — same GlassTheme tokens, no blur pass — prevents Impeller frame drops on scroll"
  - "didChangeDependencies used for shouldReduceMotion check — context not available in initState, didChangeDependencies is first lifecycle with valid context"
  - "AnimationController reverseDuration not set separately — both forward (100ms) and reverse use same duration for simplicity per plan spec"

patterns-established:
  - "GlassCard variant parameter pattern: additive enum param with stationary default — zero call-site changes for existing code"
  - "PressableScale accessibility gate: _reduceMotion bool set in didChangeDependencies, checked in all gesture handlers before animation"

requirements-completed: [ANIM-04]

# Metrics
duration: 4min
completed: 2026-02-21
---

# Phase 34 Plan 01: Shared Glass Primitives Summary

**GlassCard scroll-safe variant (non-blur tint+border for scrollable lists) and PressableScale press-scale micro-interaction wrapper with haptic feedback and Reduce Motion gate**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-21T09:42:51Z
- **Completed:** 2026-02-21T09:46:56Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- GlassCard now accepts `variant: GlassVariant.scrollItem` to render a non-blur tint+border surface, preventing Impeller frame drops in scrollable lists while keeping visual consistency with GlassTheme tokens
- PressableScale widget created — wraps any child with 0.97 scale shrink on tap down (100ms easeInOut), haptic pulse via HapticFeedback.lightImpact(), and spring back on release
- Both widgets respect accessibility: reduceTransparency/highContrast skips blur in GlassCard; Reduce Motion skips animation in PressableScale (GlassCard.shouldReduceMotion gate)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add GlassVariant enum and scroll-safe rendering path to GlassCard** - `0abcc19` (feat)
2. **Task 2: Create PressableScale micro-interaction wrapper widget** - `19fc109` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.Mobile/lib/core/widgets/glass_card.dart` - Added GlassVariant enum, variant constructor param, scrollItem rendering path (three total: reduceTransparency, scrollItem, stationary)
- `TradingBot.Mobile/lib/core/widgets/pressable_scale.dart` - New PressableScale StatefulWidget with AnimationController, Tween<double>(1.0→0.97), HapticFeedback, and GlassCard.shouldReduceMotion gate

## Decisions Made

- GlassVariant.scrollItem uses the same tintColor/tintOpacity/borderColor/borderWidth/cardRadius tokens as the stationary path — visual consistency without blur overhead
- `didChangeDependencies` chosen for shouldReduceMotion check (not initState) because MediaQuery.of(context) requires a valid BuildContext only available after the first dependency resolution
- AnimationController uses single 100ms duration for both forward and reverse directions for simplicity (reverse takes the same 100ms — adequate for the subtle 0.97 scale effect)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- GlassCard now has all three rendering paths needed by feature screens (phases 35-38)
- PressableScale ready to wrap any tappable GlassCard — import from `lib/core/widgets/pressable_scale.dart`
- All widgets pass `flutter analyze` with zero issues
- Phase 34 Plan 02 (shimmer skeletons) can proceed

---
*Phase: 34-shared-glass-primitives*
*Completed: 2026-02-21*

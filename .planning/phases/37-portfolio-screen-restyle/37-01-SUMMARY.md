---
phase: 37-portfolio-screen-restyle
plan: 01
subsystem: ui
tags: [flutter, animation, glassmorphism, animated-switcher, radial-gradient, fl-chart, dart]

# Dependency graph
requires:
  - phase: 35.1-portfolio-overview
    provides: AllocationDonutChart, PortfolioHeroHeader, PortfolioAssetListItem, GlassVariant.scrollItem asset rows
  - phase: 33-glass-foundation
    provides: GlassCard.shouldReduceMotion, GlassVariant enum, BackdropFilter safe usage
provides:
  - SlotFlipValue reusable widget (AnimatedSwitcher + SlideTransition + ClipRect + reduce-motion guard)
  - AllocationDonutChart ambient glow halo (RadialGradient behind PieChart, dominant-color matched)
  - SlotFlipValue integration in PortfolioHeroHeader (total value + PnL amount)
  - SlotFlipValue integration in PortfolioAssetListItem (trailing holding value)
  - AllocationDonutChart GlassCard wrapper (SCRN-05)
  - Loading skeleton donut placeholder updated to match live GlassCard layout
affects: [38-remaining-screens, any-phase-adding-currency-sensitive-labels]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SlotFlipValue pattern: AnimatedSwitcher + ClipRect + SlideTransition with animation.status != reverse for directional slot-flip"
    - "_kGlowCenterAlpha tunable constant: int alpha constant for glow opacity calibration"
    - "_dominantColor() method: client-side reduce() to find largest-percentage allocation color"
    - "centerSpaceColor: Colors.transparent when glow layer is behind PieChart"

key-files:
  created:
    - TradingBot.Mobile/lib/features/portfolio/presentation/widgets/slot_flip_value.dart
  modified:
    - TradingBot.Mobile/lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/widgets/portfolio_hero_header.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/widgets/portfolio_asset_list_item.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/portfolio_screen.dart

key-decisions:
  - "SlotFlipValue uses animation.status != AnimationStatus.reverse to distinguish incoming vs outgoing child — single animation drives both directions with different Tweens"
  - "centerSpaceColor changed to Colors.transparent so RadialGradient glow shows through the donut hole (Pitfall 3)"
  - "ClipRect wraps AnimatedSwitcher in SlotFlipValue to prevent overflow during slide transition (Pitfall 4)"
  - "_kGlowCenterAlpha = 51 (~20% opacity) as tunable constant — adjustable without searching the codebase"
  - "PnL percent label and quantity labels not wrapped in SlotFlipValue — they do not change on currency toggle"
  - "AllocationDonutChart wrapped in GlassCard.stationary in portfolio_screen.dart for visual consistency with hero header"

patterns-established:
  - "SlotFlipValue: reusable slot-flip widget — import from same-directory relative path; use for any currency-sensitive label"
  - "Ambient glow: Stack with RadialGradient Container as Layer 1, PieChart as Layer 2, center label as Layer 3"

requirements-completed: [SCRN-05, ANIM-06]

# Metrics
duration: 2min
completed: 2026-03-04
---

# Phase 37 Plan 01: Portfolio Screen Restyle Summary

**SlotFlipValue vertical slot-flip animation on currency toggle and RadialGradient ambient glow halo around donut chart with GlassCard wrapping**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-04T11:46:45Z
- **Completed:** 2026-03-04T11:48:45Z
- **Tasks:** 2
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- Created `SlotFlipValue` reusable widget with AnimatedSwitcher, ClipRect, SlideTransition, ValueKey trigger, and reduce-motion guard (Duration.zero when disableAnimations is set)
- Added RadialGradient ambient glow halo behind the donut chart matching the dominant asset color; changed centerSpaceColor to transparent so glow shows through the donut hole
- Integrated SlotFlipValue into PortfolioHeroHeader (total value + PnL amount) and PortfolioAssetListItem (trailing holding value) for ANIM-06
- Wrapped AllocationDonutChart in GlassCard.stationary in portfolio_screen.dart for SCRN-05 visual consistency; updated loading skeleton to match

## Task Commits

Each task was committed atomically:

1. **Task 1: Create SlotFlipValue widget and add donut ambient glow** - `ad1591c` (feat)
2. **Task 2: Integrate SlotFlipValue into hero header and asset list items** - `262d572` (feat)

## Files Created/Modified
- `TradingBot.Mobile/lib/features/portfolio/presentation/widgets/slot_flip_value.dart` - Reusable slot-flip animation widget (71 lines)
- `TradingBot.Mobile/lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart` - Added _dominantColor(), _kGlowCenterAlpha, RadialGradient glow Stack layer, SlotFlipValue for center label, Colors.transparent centerSpaceColor
- `TradingBot.Mobile/lib/features/portfolio/presentation/widgets/portfolio_hero_header.dart` - Replaced total value Text and PnL amount Text with SlotFlipValue
- `TradingBot.Mobile/lib/features/portfolio/presentation/widgets/portfolio_asset_list_item.dart` - Replaced trailing holding value Text with SlotFlipValue
- `TradingBot.Mobile/lib/features/portfolio/presentation/portfolio_screen.dart` - Wrapped AllocationDonutChart in GlassCard; updated skeleton donut placeholder to use GlassCard

## Decisions Made
- `animation.status != AnimationStatus.reverse` used to distinguish incoming vs outgoing child in AnimatedSwitcher transitionBuilder — single animation drives directional slot with different Tweens per direction
- `centerSpaceColor: Colors.transparent` so the RadialGradient glow shows through the donut hole (RESEARCH.md Pitfall 3)
- `ClipRect` wraps entire AnimatedSwitcher to prevent overflow during slide transition (RESEARCH.md Pitfall 4)
- `_kGlowCenterAlpha = 51` defined as top-level tunable constant (project pattern: Color.withAlpha(int) over withOpacity(float))
- PnL percent label and quantity labels intentionally not wrapped in SlotFlipValue — values do not change on currency toggle per RESEARCH.md Open Question 1 recommendation

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Portfolio screen visual enhancements (SCRN-05, ANIM-06) are complete
- SlotFlipValue widget is reusable and available for any future screen needing currency-sensitive label animations
- Phase 38 remaining screens can import SlotFlipValue from the portfolio widgets directory or it can be promoted to core/widgets if needed across screens

---
*Phase: 37-portfolio-screen-restyle*
*Completed: 2026-03-04*

---
phase: 33-design-system-foundation
plan: 02
subsystem: ui
tags: [flutter, glassmorphism, glass-card, backdrop-filter, accessibility, reduce-transparency, reduce-motion, media-query]

# Dependency graph
requires:
  - phase: 33-01
    provides: GlassTheme ThemeExtension with 9 glass design tokens registered in ThemeData.extensions
provides:
  - GlassCard StatelessWidget with frosted glass surface (ClipRRect + BackdropFilter) using GlassTheme tokens
  - Opaque dark card fallback rendered when MediaQuery.highContrast is true (Reduce Transparency proxy)
  - GlassCard.shouldReduceMotion(BuildContext) static helper for centralized Reduce Motion check
affects: [34-home-screen, 35-portfolio-screen, 36-history-screen, 37-chart-screen, 38-settings-screen]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ClipRRect always wraps BackdropFilter to bound blur to card shape (prevents blur bleed beyond rounded corners)
    - MediaQuery.highContrast used as Reduce Transparency proxy (no direct Flutter API for iOS reduceTransparency)
    - MediaQuery.disableAnimations as centralized Reduce Motion check (covers both iOS and Android accessibility settings)
    - glass.tintColor.withAlpha((glass.tintOpacity * 255).round()) — opacity fraction to alpha int conversion pattern

key-files:
  created:
    - TradingBot.Mobile/lib/core/widgets/glass_card.dart
  modified: []

key-decisions:
  - "MediaQuery.highContrast used as Reduce Transparency proxy — Flutter has no direct reduceTransparency API; highContrast is the nearest cross-platform equivalent; documented in code comments referencing 33-RESEARCH.md Open Question #1"
  - "shouldReduceMotion uses MediaQuery.disableAnimations only — research found no separate Flutter AccessibilityFeatures.reduceMotion; disableAnimations is already sourced from platform accessibility features under the hood"
  - "ClipRRect mandatory before BackdropFilter — enforced by code structure; documented as pitfall #1 from research to inform all downstream phases"
  - "No hardcoded values in GlassCard — all blur sigma, tint opacity, border width, corner radius, colors sourced from GlassTheme tokens"

patterns-established:
  - "Pattern 1: GlassCard(child: ...) as the primary surface widget — use instead of bare Containers on all redesigned screens"
  - "Pattern 2: if (!GlassCard.shouldReduceMotion(context)) { _controller.forward(); } — motion gate pattern for all animation controllers in phases 34-38"
  - "Pattern 3: highContrast branch has no BackdropFilter — always check reduceTransparency before constructing blur widgets"

requirements-completed: [DESIGN-01, DESIGN-05, DESIGN-06]

# Metrics
duration: 23min
completed: 2026-02-21
---

# Phase 33 Plan 02: Design System Foundation Summary

**GlassCard StatelessWidget with BackdropFilter frosted glass surface, opaque dark fallback for Reduce Transparency (highContrast), and a centralized shouldReduceMotion() static helper — the primary card surface ready for all 5 redesigned screens**

## Performance

- **Duration:** 23 min
- **Started:** 2026-02-21T07:44:31Z
- **Completed:** 2026-02-21T08:07:09Z
- **Tasks:** 2 (1 auto, 1 human-verify checkpoint)
- **Files modified:** 1 (created)

## Accomplishments

- Created GlassCard widget that reads all design values from GlassTheme tokens — zero hardcoded blur sigma, opacity, border, or radius values
- Implemented dual rendering paths: glass surface (ClipRRect + BackdropFilter) for normal mode, opaque dark Container for highContrast/Reduce Transparency mode
- Added GlassCard.shouldReduceMotion(BuildContext) static method providing a single, consistent Reduce Motion check point for all downstream phases (34-38)
- Human verified the complete design system foundation visually on iOS Simulator — dark navy background, ambient orbs, and accessibility toggles confirmed working without crashes

## Task Commits

Each task was committed atomically:

1. **Task 1: Create GlassCard widget with accessibility-aware glass/opaque rendering** - `ca176ba` (feat)
2. **Task 2: Visual verification of design system foundation on iOS Simulator** - Human checkpoint approved

**Plan metadata:** (pending docs commit)

## Files Created/Modified

- `TradingBot.Mobile/lib/core/widgets/glass_card.dart` - GlassCard StatelessWidget with frosted glass surface, opaque fallback, and shouldReduceMotion static helper; imports dart:ui for ImageFilter

## Decisions Made

- Used `MediaQuery.highContrast` as the Reduce Transparency proxy. Flutter exposes no direct `reduceTransparency` API. `highContrast` maps to iOS "Increase Contrast" (not "Reduce Transparency") but is the closest available cross-platform signal; this limitation is documented in code comments referencing the research findings.
- `shouldReduceMotion()` uses only `MediaQuery.disableAnimations`. The plan spec included `mq.accessibilityFeatures.reduceMotion` as a dual-check, but Flutter's `AccessibilityFeatures.reduceMotion` does not exist as a separate property — `disableAnimations` is already sourced from the platform's accessibility system and covers both iOS Reduce Motion and Android's disable animations setting. The comment in the file explains this to prevent future confusion.
- `glass.tintColor.withAlpha((glass.tintOpacity * 255).round())` converts the opacity fraction to an integer alpha value, consistent with the `Color.withAlpha(int)` pattern established in Plan 01 (no float rounding issues).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Removed non-existent AccessibilityFeatures.reduceMotion from shouldReduceMotion**
- **Found during:** Task 1 (GlassCard implementation)
- **Issue:** Plan specified `mq.accessibilityFeatures.reduceMotion` as part of the dual-check, but this property does not exist in Flutter's `AccessibilityFeatures` API. Using it would cause a compile error.
- **Fix:** Implemented `shouldReduceMotion` using only `MediaQuery.disableAnimations`, which is the correct cross-platform flag. Updated the method's doc comment to explain why it's a single check rather than dual.
- **Files modified:** TradingBot.Mobile/lib/core/widgets/glass_card.dart
- **Verification:** `flutter analyze` passed with no errors.
- **Committed in:** ca176ba (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug fix for non-existent API)
**Impact on plan:** Required fix for code to compile. The single-flag approach is more correct than the dual-check specified in the plan because `disableAnimations` already aggregates the platform signal. No scope creep.

## Issues Encountered

None beyond the API deviation above. `flutter analyze` reported no issues on the created file.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- GlassCard ready for adoption by all 5 tab screen redesign phases (34-38): `GlassCard(child: ...)` replaces bare Container surfaces
- `GlassCard.shouldReduceMotion(context)` available for all animation controllers — phases should gate every `_controller.forward()` call behind this check
- Both accessibility paths tested on iOS Simulator: glass path (normal) and opaque path (highContrast) confirmed crash-free
- All 9 GlassTheme tokens consumed correctly: blurSigma, tintOpacity, tintColor, borderColor, borderWidth, cardRadius, opaqueSurface, opaqueBorder — design system foundation is complete

## Self-Check: PASSED

- FOUND: TradingBot.Mobile/lib/core/widgets/glass_card.dart
- FOUND: .planning/phases/33-design-system-foundation/33-02-SUMMARY.md
- FOUND: ca176ba (feat(33-02): create GlassCard widget with frosted glass surface and accessibility fallbacks)

---
*Phase: 33-design-system-foundation*
*Completed: 2026-02-21*

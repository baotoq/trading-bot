---
phase: 36-home-screen-redesign
plan: 02
subsystem: ui
tags: [flutter, go_router, animation, transitions, CustomTransitionPage]

# Dependency graph
requires:
  - phase: 35-chart-redesign
    provides: premium glassmorphism UI foundation, tab and modal routing structure
provides:
  - fadeScalePage() factory function for consistent 200ms fade+scale transitions
  - All 10 GoRoute entries using CustomTransitionPage (ANIM-05)
affects: [37-portfolio, 38-config, any future routes added to router.dart]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - fadeScalePage factory function pattern for reusable CustomTransitionPage transitions

key-files:
  created: []
  modified:
    - TradingBot.Mobile/lib/app/router.dart

key-decisions:
  - "fadeScalePage factory function defined at file level (not inside appRouter) for reuse clarity"
  - "StatefulShellRoute.indexedStack builder: left unchanged — it is the navigation shell, not a page route"
  - "All parentNavigatorKey assignments preserved — full-screen push routes retain their root navigator context"

patterns-established:
  - "fadeScalePage(key: state.pageKey, child: Widget): Standard pattern for all GoRoute pageBuilder entries"
  - "CustomTransitionPage<void> with 200ms easeOut FadeTransition + ScaleTransition(0.95→1.0) as project-wide page transition"

requirements-completed: [ANIM-05]

# Metrics
duration: 1min
completed: 2026-02-23
---

# Phase 36 Plan 02: Smooth Page Transitions Summary

**Unified 200ms fade+scale (0.95 to 1.0) CustomTransitionPage applied to all 10 GoRoute entries via fadeScalePage() factory, replacing default Material slide transition (ANIM-05)**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-23T04:28:17Z
- **Completed:** 2026-02-23T04:29:08Z
- **Tasks:** 1 completed
- **Files modified:** 1

## Accomplishments
- Added `fadeScalePage()` factory function with 200ms `FadeTransition` + `ScaleTransition` (0.95 to 1.0, `Curves.easeOut`)
- Replaced all 10 GoRoute `builder:` with `pageBuilder:` using the factory
- Preserved `StatefulShellRoute.indexedStack` builder (navigation shell, not a page route)
- Preserved all `parentNavigatorKey` assignments for full-screen push routes above tab shell
- File passes `flutter analyze` with zero errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Add CustomTransitionPage to all GoRouter routes** - `f62aae0` (feat)

## Files Created/Modified
- `TradingBot.Mobile/lib/app/router.dart` - Added fadeScalePage factory, converted all 10 GoRoute builder: to pageBuilder: with CustomTransitionPage

## Decisions Made
- `StatefulShellRoute.indexedStack` builder left unchanged — it manages the navigation shell widget, not individual page transitions, so CustomTransitionPage does not apply
- `fadeScalePage` defined as a top-level function (not inside the router) for clarity and potential reuse by future routes
- All `parentNavigatorKey: rootNavigatorKey` assignments preserved to keep full-screen push behaviour for bot-detail, add-transaction, transaction-history, fixed-deposit routes

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Page transitions are now unified across all routes; the app has a cohesive premium feel when navigating between screens and opening full-screen modals
- Any new GoRoute added to router.dart should follow the `pageBuilder: (context, state) => fadeScalePage(key: state.pageKey, child: ...)` pattern

---
*Phase: 36-home-screen-redesign*
*Completed: 2026-02-23*

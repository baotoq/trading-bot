---
phase: 36-home-screen-redesign
plan: "01"
subsystem: mobile-ui
tags: [flutter, glassmorphism, animation, home-screen, dashboard]
dependency_graph:
  requires: [GlassCard, AppShimmer, PurchaseListItem, AllocationDonutChart, homeDataProvider, portfolioPageDataProvider, purchaseHistoryProvider]
  provides: [HomeScreen, HomeHeroBalanceCard, HomeMiniDonutCard, HomeRecentActivityCard, HomeQuickActionsCard]
  affects: [home-tab-UX]
tech_stack:
  added: []
  patterns: [TweenAnimationBuilder count-up, AnimationController+Interval stagger, useRef hasAnimated guard, GlassCard.shouldReduceMotion check]
key_files:
  created:
    - TradingBot.Mobile/lib/features/home/presentation/widgets/home_hero_balance_card.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/home_mini_donut_card.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/home_recent_activity_card.dart
    - TradingBot.Mobile/lib/features/home/presentation/widgets/home_quick_actions_card.dart
  modified:
    - TradingBot.Mobile/lib/features/home/presentation/home_screen.dart
decisions:
  - "[Phase 36-01]: HomeHeroBalanceCard uses pnlSign prefix string for positive P&L formatting — avoids locale-specific currency sign placement issues with NumberFormat +/- patterns"
  - "[Phase 36-01]: _animatedCard and _cardEntrance defined as top-level functions outside HomeScreen class — they have no instance state and are reusable without needing BuildContext"
  - "[Phase 36-01]: PurchaseListItem renders its own Container with dark background — HomeRecentActivityCard wraps in GlassCard.stationary (BackdropFilter-safe: 3 items in Column, no scroll)"
  - "[Phase 36-01]: portfolioValue computed as currentPrice * totalBtc when available, falls back to totalCost — consistent with DcaBotDetailScreen computation pattern"
metrics:
  duration: 2min
  completed: "2026-02-23"
  tasks: 2
  files: 5
---

# Phase 36 Plan 01: Home Screen Redesign - SUMMARY

**One-liner:** Premium glassmorphism Home dashboard with 4 GlassCard sections, staggered entrance (50ms offset via AnimationController+Interval), and count-up balance animation on first load.

## What Was Built

Replaced the plain text HomeScreen (portfolio stats grid + CountdownText + LastBuyCard) with a polished glassmorphism dashboard layout consisting of 4 animated glass card sections:

1. **HomeHeroBalanceCard** — hero balance with TweenAnimationBuilder count-up (0 → actual), P&L row (color-coded green/red with arrow icon), optional HealthBadge
2. **HomeMiniDonutCard** — compact PieChart allocation donut (height 140px, no touch), Wrap labels below, empty state
3. **HomeRecentActivityCard** — static Column of up to 3 PurchaseListItem widgets (BackdropFilter-safe, no ListView)
4. **HomeQuickActionsCard** — 3 GestureDetector buttons (View Bot → context.push, Chart/History → StatefulNavigationShell.goBranch)

HomeScreen rebuilt as HookConsumerWidget with:
- Single AnimationController (450ms) driving staggered card entrance via Interval-based CurvedAnimation
- `useRef(false)` hasAnimated guard — entrance fires only on first mount, not on tab revisit
- `shouldAnimateCountUp` useRef — count-up fires only once on first data load, not on 30-second auto-refresh
- `GlassCard.shouldReduceMotion(context)` — snaps controller to 1.0 when Reduce Motion is enabled
- Three provider watches: homeDataProvider, portfolioPageDataProvider, purchaseHistoryProvider
- Loading skeleton using AppShimmer + GlassCard + Bone shapes matching real layout
- Error handling preserved: full RetryWidget for no-data error, snackbar for stale-data error

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check

- [x] `lib/features/home/presentation/widgets/home_hero_balance_card.dart` exists
- [x] `lib/features/home/presentation/widgets/home_mini_donut_card.dart` exists
- [x] `lib/features/home/presentation/widgets/home_recent_activity_card.dart` exists
- [x] `lib/features/home/presentation/widgets/home_quick_actions_card.dart` exists
- [x] `lib/features/home/presentation/home_screen.dart` updated
- [x] Task 1 commit: 7e8c113
- [x] Task 2 commit: d97a461
- [x] `flutter analyze` — zero issues across entire TradingBot.Mobile project

## Self-Check: PASSED

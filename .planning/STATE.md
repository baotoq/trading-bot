# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v5.0 Stunning Mobile UI
**Updated:** 2026-02-23

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-21)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA -- now with a premium glassmorphism UI
**Current focus:** Phase 36 in progress -- Home Screen Redesign, 36-02 complete (fade+scale CustomTransitionPage on all GoRouter routes, ANIM-05)

## Current Position

Phase: 36 of 38 (Home Screen Redesign)
Plan: 2 of 2 complete
Status: In Progress
Last activity: 2026-02-23 -- 36-02 complete (unified fade+scale CustomTransitionPage on all 10 GoRoute entries via fadeScalePage() factory)

Progress: [██░░░░░░░░] 10% (v5.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 72 (across v1.0-v5.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (12 plans)
- v4.0: 2 days (15 plans)
- v5.0: In progress (6 plans so far)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25.1 | 12 | Complete |
| v4.0 | 26-32 | 15 | Complete |
| v5.0 | 33-38 | TBD | In progress |

**Recent Plan Metrics:**

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 33 | 01 | 2min | 2 | 3 |
| 33 | 02 | 23min | 2 | 1 |
| 34 | 01 | 4min | 2 | 2 |
| 34 | 02 | 6min | 2 | 7 |
| 35.1 | 01 | 3min | 2 | 4 |
| 35.1 | 02 | 3min | 2 | 2 |
| 35.2 | 01 | 3min | 2 | 5 |
| 35.2 | 02 | 3min | 2 | 3 |
| Phase 35.2 P03 | 1 | 1 tasks | 1 files |
| 35 | 01 | 3min | 2 | 2 |
| Phase 35 P02 | 2min | 2 tasks | 3 files |
| Phase 36 P02 | 1min | 1 tasks | 1 files |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

Recent decisions affecting v5.0 (phase 34 additions):
- No BackdropFilter in scrollable lists (History, Portfolio) -- Impeller frame drops confirmed; use opacity-tint + border non-blur GlassCard variant
- GlassTheme as single ThemeExtension -- consolidated before any screen is touched to prevent two-source-of-truth color drift
- profitGreen and lossRed are non-negotiable semantic constants -- must survive token consolidation
- All AnimationControllers in HookConsumerWidget via useAnimationController -- prevents lifecycle leaks across 5 tabs
- _hasAnimated guard pattern -- counters and chart draw-in animate only on initial load, not on every tab revisit
- BackdropFilter on nav bar (GlassBottomNav) deferred to v5.x -- requires physical device performance gate before committing
- Color.withAlpha(int) over withOpacity(float) -- consistent with existing project pattern, avoids float-rounding issues
- moneyStyle as partial TextStyle (fontFeatures only) -- consumers merge via copyWith to preserve their font size/weight/color
- AmbientBackground wraps body not entire Scaffold -- keeps orbs behind content only, nav bar retains its own background
- scaffoldBackgroundColor: Colors.transparent globally -- individual screens must not set solid backgroundColor unless they are modals/dialogs above AmbientBackground
- [Phase 33]: MediaQuery.highContrast used as Reduce Transparency proxy — no direct Flutter reduceTransparency API exists
- [Phase 33]: GlassCard.shouldReduceMotion uses MediaQuery.disableAnimations only — AccessibilityFeatures.reduceMotion does not exist in Flutter API
- [Phase 33]: ClipRRect always wraps BackdropFilter in GlassCard — enforced by code structure to prevent blur bleed
- [Phase 34]: GlassVariant.scrollItem renders Container without BackdropFilter — same GlassTheme tokens, no blur pass — prevents Impeller frame drops on scroll
- [Phase 34]: didChangeDependencies used for shouldReduceMotion check in PressableScale — context not available in initState
- [Phase 34]: AnimationController uses single 100ms duration for both forward and reverse in PressableScale — adequate for subtle 0.97 scale effect
- [Phase 34]: skeletonizer 1.4.3 integrated with AppShimmer wrapper using GlassTheme opaqueSurface/opaqueBorder as shimmer colors for visual continuity
- [Phase 34]: PulseEffect chosen as reduce-motion fallback over SoldColorEffect — still communicates loading state via gentle alpha fade
- [Phase 35.1-01]: _buildContent returns List<Widget> slivers spread at call site — switch expression with spread not valid in Dart list literals
- [Phase 35.1-01]: StickyTabBarDelegate uses Material(transparent) wrapper so AmbientBackground orbs show through the pinned tab bar region
- [Phase 35.1-01]: PortfolioTabBar uses ValueListenableBuilder internally — parent passes ValueNotifier<int> so only tab buttons rebuild on tap
- [Phase 35.1-01]: CurrencyToggle moved from SliverAppBar actions into PortfolioHeroHeader — co-located with the value it controls
- [Phase 35.1-02]: PortfolioAssetListItem uses GlassVariant.scrollItem — no BackdropFilter in list items, preserving 60fps scroll per Impeller constraint from Phase 34
- [Phase 35.1-02]: ticker.hashCode.abs() % 6 for badge palette color — .abs() guards against negative hashCode on some Dart runtimes
- [Phase 35.1-02]: Skeleton rebuilt with actual GlassCard(scrollItem) for asset row bones — surface color matches loaded state without separate color values
- [Phase 35.2]: DcaBotDetailScreen uses no Scaffold backgroundColor — global transparent allows AmbientBackground orbs through
- [Phase 35.2]: parentNavigatorKey: rootNavigatorKey on /home/bot-detail — full-screen push above tab shell
- [Phase 35.2]: Frequency label 'Daily at HH:MM / N' — correct for once-daily DCA, reference screenshot was from different product
- [Phase 35.2]: _PnlChartSection and _EventHistoryPreviewSection isolated as private HookConsumerWidgets to scope provider subscriptions
- [Phase 35.2]: BotInfoCard derives Bot ID as firstPurchaseDate.millisecondsSinceEpoch.toString() — stable unique ID without server-side bot ID endpoint
- [Phase 35.2]: History tab uses useEffect + useScrollController hook; pagination triggers at maxScrollExtent - 200px threshold
- [Phase 35.2]: GestureDetector chosen over PressableScale for PortfolioStatsSection navigation tap -- large content section; scale animation too heavy
- [Phase 35.2]: _buildContent accepts BuildContext as first parameter to enable context.push from helper methods
- [Phase 35-01]: GlowDotPainter implements lerp + props (EquatableMixin) -- required abstract members of FlDotPainter not noted in plan; auto-fixed
- [Phase 35-01]: useRef(false) as hasAnimated guard -- fires draw-in animation only on first mount, not on tab revisit
- [Phase 35-01]: controller.isCompleted used for purchase dot show guard -- dots only appear when full line is drawn
- [Phase 35-02]: handleBuiltInTouches:true with null getTooltipItems -- preserves vertical indicator line while suppressing built-in tooltip (Pitfall 4 solution)
- [Phase 35-02]: Tooltip anchored at top:0 centered horizontally -- avoids complex pixel-coordinate calculation for LineBarSpot.x canvas position
- [Phase 35-02]: SingleChildScrollView(AlwaysScrollable) at top level replaces Expanded+inner-scroll nesting -- PriceLineChart AspectRatio has intrinsic height, no Expanded needed
- [Phase 35-02]: Transparent AppBar backgroundColor in ChartScreen -- allows AmbientBackground orbs to show through app bar area
- [Phase 36]: fadeScalePage factory function defined at file level for reuse clarity; StatefulShellRoute.indexedStack builder: left unchanged (navigation shell, not page route); all parentNavigatorKey assignments preserved for full-screen push routes

### Roadmap Evolution

- Phase 35.1 inserted after Phase 35: Portfolio Overview Screen (URGENT)
- Phase 35.2 inserted after Phase 35: DCA Bot Detail Screen (URGENT)

### Known Risks

- VNDirect dchart-api is undocumented/unofficial -- could change without notice (research valid until 2026-03-20)
- GlassBottomNav BackdropFilter: full-width blur affects every tab transition; fallback is opacity-tint-only nav (safe)
- GlowLineChart data-slice: ChartResponse data model compatibility not yet validated against prices.length * progress approach
- Blur sigma calibration (card: 12, appBar: 16) must be verified on physical iPhone -- iOS Simulator does not accurately represent BackdropFilter GPU cost
- Screen Scaffolds with solid backgroundColor will paint over AmbientBackground orbs -- audit all screen Scaffolds in phases 34-38

### Pending Todos

None.

## Session Continuity

Last session: 2026-02-23
Stopped at: Completed 36-02-PLAN.md. Unified fade+scale CustomTransitionPage applied to all 10 GoRoute entries via fadeScalePage() factory (ANIM-05). Phase 36 plan 02 complete.
Next step: Continue Phase 36 (if more plans exist) or execute Phase 37

---
*State updated: 2026-02-23 after 36-02 complete*

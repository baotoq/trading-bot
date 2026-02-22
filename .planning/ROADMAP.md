# Roadmap: BTC Smart DCA Bot

## Milestones

- ✅ **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 Backtesting Engine** -- Phases 5-8 (shipped 2026-02-13) -- [archive](milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 Web Dashboard** -- Phases 9-12 (shipped 2026-02-14) -- [archive](milestones/v1.2-ROADMAP.md)
- ✅ **v2.0 DDD Foundation** -- Phases 13-19 (shipped 2026-02-20) -- [archive](milestones/v2.0-ROADMAP.md)
- ✅ **v3.0 Flutter Mobile** -- Phases 20-25.1 (shipped 2026-02-20) -- [archive](milestones/v3.0-ROADMAP.md)
- ✅ **v4.0 Portfolio Tracker** -- Phases 26-32 (shipped 2026-02-21) -- [archive](milestones/v4.0-ROADMAP.md)
- 🚧 **v5.0 Stunning Mobile UI** -- Phases 33-38 (in progress)

## Phases

<details>
<summary>✅ v1.0 Daily BTC Smart DCA (Phases 1-4) -- SHIPPED 2026-02-12</summary>

- [x] Phase 1: Foundation & Hyperliquid Client (3/3 plans) -- completed 2026-02-12
- [x] Phase 2: Core DCA Engine (3/3 plans) -- completed 2026-02-12
- [x] Phase 3: Smart Multipliers (3/3 plans) -- completed 2026-02-12
- [x] Phase 4: Enhanced Notifications & Observability (3/3 plans) -- completed 2026-02-12

</details>

<details>
<summary>✅ v1.1 Backtesting Engine (Phases 5-8) -- SHIPPED 2026-02-13</summary>

- [x] Phase 5: MultiplierCalculator Extraction (1/1 plan) -- completed 2026-02-13
- [x] Phase 6: Backtest Simulation Engine (2/2 plans) -- completed 2026-02-13
- [x] Phase 7: Historical Data Pipeline (2/2 plans) -- completed 2026-02-13
- [x] Phase 8: API Endpoints & Parameter Sweep (2/2 plans) -- completed 2026-02-13

</details>

<details>
<summary>✅ v1.2 Web Dashboard (Phases 9-12) -- SHIPPED 2026-02-14</summary>

- [x] Phase 9: Infrastructure & Aspire Integration (2/2 plans) -- completed 2026-02-13
- [x] Phase 9.1: Migrate Dashboard to Fresh Nuxt Setup (1/1 plan) -- completed 2026-02-13
- [x] Phase 10: Dashboard Core (3/3 plans) -- completed 2026-02-13
- [x] Phase 11: Backtest Visualization (4/4 plans) -- completed 2026-02-14
- [x] Phase 12: Configuration Management (2/2 plans) -- completed 2026-02-14

</details>

<details>
<summary>✅ v2.0 DDD Foundation (Phases 13-19) -- SHIPPED 2026-02-20</summary>

- [x] Phase 13: Strongly-Typed IDs (2/2 plans) -- completed 2026-02-18
- [x] Phase 14: Value Objects (2/2 plans) -- completed 2026-02-18
- [x] Phase 15: Rich Aggregate Roots (2/2 plans) -- completed 2026-02-19
- [x] Phase 16: Result Pattern (2/2 plans) -- completed 2026-02-19
- [x] Phase 17: Domain Event Dispatch (3/3 plans) -- completed 2026-02-19
- [x] Phase 18: Specification Pattern (3/3 plans) -- completed 2026-02-19
- [x] Phase 19: Dashboard Nullable Price Fix (1/1 plan) -- completed 2026-02-19

</details>

<details>
<summary>✅ v3.0 Flutter Mobile (Phases 20-25.1) -- SHIPPED 2026-02-20</summary>

- [x] Phase 20: Flutter Project Setup + Core Infrastructure (2/2 plans) -- completed 2026-02-19
- [x] Phase 21: Portfolio + Status Screens (2/2 plans) -- completed 2026-02-20
- [x] Phase 22: Price Chart + Purchase History (2/2 plans) -- completed 2026-02-20
- [x] Phase 23: Configuration Screen (1/1 plan) -- completed 2026-02-20
- [x] Phase 24: Push Notifications (3/3 plans) -- completed 2026-02-20
- [x] Phase 25: Nuxt Deprecation (1/1 plan) -- completed 2026-02-20
- [x] Phase 25.1: Cross-Cutting Notification Handler Split (1/1 plan) -- completed 2026-02-20

</details>

<details>
<summary>✅ v4.0 Portfolio Tracker (Phases 26-32) -- SHIPPED 2026-02-21</summary>

- [x] Phase 26: Portfolio Domain Foundation (3/3 plans) -- completed 2026-02-20
- [x] Phase 27: Price Feed Infrastructure (2/2 plans) -- completed 2026-02-20
- [x] Phase 28: Portfolio Backend API (2/2 plans) -- completed 2026-02-20
- [x] Phase 29: Flutter Portfolio UI (3/3 plans) -- completed 2026-02-20
- [x] Phase 30: Critical Bug Fixes (1/1 plan) -- completed 2026-02-20
- [x] Phase 31: Milestone Verification (1/1 plan) -- completed 2026-02-21
- [x] Phase 32: Tech Debt Cleanup (3/3 plans) -- completed 2026-02-21

</details>

### v5.0 Stunning Mobile UI (In Progress)

**Milestone Goal:** Transform the Flutter app from generic Material 3 into a premium glassmorphism design with rich animations, gradient glow charts, and polished data visualization across all 5 tabs.

- [x] **Phase 33: Design System Foundation** - GlassTheme tokens, ambient background, and typography scale as the prerequisite foundation (completed 2026-02-21)
- [x] **Phase 34: Shared Glass Primitives** - GlassCard widget, shimmer skeletons, and press micro-interactions as the shared building blocks (completed 2026-02-21)
- [x] **Phase 35: Chart Redesign** - Gradient glow chart, animated draw-in, glow markers, and glass tooltip on ChartScreen (completed 2026-02-22)
- [ ] **Phase 36: Home Screen Redesign** - Dashboard overview layout with hero balance, mini allocation chart, staggered entrance, and animated counters
- [ ] **Phase 37: Portfolio Screen Restyle** - Glass cards, animated donut, per-asset glass rows, and currency toggle slot-flip
- [ ] **Phase 38: History, Config, and Navigation Shell** - Glass restyles for remaining tabs and smooth page transitions

## Phase Details

### Phase 33: Design System Foundation
**Goal**: The design token layer and ambient visual foundation exist so that every subsequent phase can build on a single source of truth
**Depends on**: Phase 32
**Requirements**: DESIGN-01, DESIGN-02, DESIGN-03, DESIGN-04, DESIGN-05, DESIGN-06
**Success Criteria** (what must be TRUE):
  1. App scaffold background is a deep dark color with 2-3 static radial gradient orbs visible behind all screens (not flat black)
  2. All glass cards rendered anywhere in the app share identical blur sigma, tint opacity, and border values sourced from a single GlassTheme ThemeExtension
  3. All monetary values across the app display with tabular figures so digits align vertically in lists
  4. On a device with Reduce Transparency enabled, glass surfaces render as fully opaque dark cards with no BackdropFilter applied
  5. On a device with Reduce Motion enabled, no animations play anywhere in the app
**Plans:** 2/2 plans complete
Plans:
- [ ] 33-01-PLAN.md -- GlassTheme ThemeExtension tokens, AmbientBackground widget with orbs, typography scale with tabular figures
- [ ] 33-02-PLAN.md -- GlassCard widget with glass/opaque rendering, accessibility fallbacks (Reduce Transparency + Reduce Motion)

### Phase 34: Shared Glass Primitives
**Goal**: The shared GlassCard widget, shimmer loading skeletons, and press micro-interactions exist as stable, reusable components that all five feature screens can adopt
**Depends on**: Phase 33
**Requirements**: ANIM-01, ANIM-04
**Success Criteria** (what must be TRUE):
  1. Loading states on any screen display shimmer skeleton placeholders shaped to match the real content layout, not a spinner
  2. Tapping any card produces a visible press-scale shrink (to ~0.97) with a haptic pulse, then springs back on release
  3. A GlassCard widget renders a frosted glass surface (blur variant) for stationary cards and a non-blur tint-plus-border surface for scrollable list items without any code changes at the call site
**Plans:** 2/2 plans complete
Plans:
- [ ] 34-01-PLAN.md -- GlassCard scroll-safe variant (GlassVariant enum) and PressableScale micro-interaction wrapper widget
- [ ] 34-02-PLAN.md -- Skeletonizer shimmer skeletons replacing CircularProgressIndicator on all 5 tab screens

### Phase 35: Chart Redesign
**Goal**: The price chart is a premium gradient glow visualization with animated left-to-right draw-in, glowing purchase markers, and a frosted glass tooltip
**Depends on**: Phase 34
**Requirements**: CHART-01, CHART-02, CHART-03, CHART-04, SCRN-02
**Success Criteria** (what must be TRUE):
  1. The Chart tab's price line chart shows an orange-to-transparent gradient fill area beneath the line
  2. On first entry to the Chart tab each session, the chart line draws in from left to right over approximately 1 second
  3. Purchase marker dots on the chart display an orange radial glow halo distinct from the dot itself
  4. Touching the chart displays a frosted glass tooltip with rounded corners and locale-formatted numbers
**Plans:** 2/2 plans complete
Plans:
- [ ] 35-01-PLAN.md -- GlowDotPainter, draw-in animation, enhanced gradient glow fill on PriceLineChart
- [ ] 35-02-PLAN.md -- Frosted glass tooltip overlay, ChartScreen layout update for premium chart

### Phase 35.2: DCA Bot Detail Screen (INSERTED)

**Goal:** A dedicated DCA Bot Detail screen with 3-tab layout (Overview, History, Parameters) showing bot identity, stats grid, PnL chart, bot info, event history, purchase list, and read-only config -- all using glass primitives and existing API data
**Depends on:** Phase 34
**Requirements:** DCA-UI-01, DCA-UI-02, DCA-UI-03, DCA-UI-04, DCA-UI-05, DCA-UI-06, DCA-UI-07
**Success Criteria** (what must be TRUE):
  1. Navigating to /home/bot-detail pushes a full-screen detail page above the tab bar with back navigation
  2. Overview tab shows BTC identity header, 6-field stats grid, action buttons, PnL area chart, bot info card, and 5 most recent purchases
  3. History tab shows paginated purchase history with PurchaseListItem and scroll-to-load-more
  4. Parameters tab shows DCA config in 3 GlassCard sections (Schedule, Strategy, Tiers) read-only
  5. PnL chart gradient fill is colored green for profit, red for loss, derived from existing price + average cost data
  6. Bot ID is copyable to clipboard with SnackBar confirmation
**Plans:** 3/3 plans complete

Plans:
- [ ] 35.2-01-PLAN.md -- Screen shell, route, Overview tab with identity header, stats grid, action buttons
- [ ] 35.2-02-PLAN.md -- PnL chart, Bot info card, event history, History tab, Parameters tab, loading skeleton
- [ ] 35.2-03-PLAN.md -- Gap closure: Add tappable navigation entry point from HomeScreen to DCA Bot Detail

### Phase 35.1: Portfolio Overview Screen (INSERTED)

**Goal:** The Portfolio tab is rebuilt as a CMC-style overview with a glass hero header, sticky tab bar, filter chips, and flat glass-styled asset list replacing the old Card/ExpansionTile layout
**Depends on:** Phase 34
**Requirements:** PORT-UI-01, PORT-UI-02, PORT-UI-03
**Success Criteria** (what must be TRUE):
  1. Portfolio screen shows a glass hero header with total value and all-time P&L (absolute + percentage)
  2. A sticky tab bar (Overview/Transactions) pins at the top during scroll; switching tabs shows different content
  3. Filter chips (Holding amount, Cumulative profit, Analysis) sort the flat asset list
  4. Asset rows render in GlassCard scrollItem variant with colored ticker badge, price + P&L%, holding value + quantity
  5. AmbientBackground orbs are visible behind all portfolio content
**Plans:** 2/2 plans complete

Plans:
- [ ] 35.1-01-PLAN.md -- Glass hero header, sticky tab bar, filter chips, and PortfolioScreen CustomScrollView rebuild
- [ ] 35.1-02-PLAN.md -- PortfolioAssetListItem with glass scrollItem variant and updated loading skeleton

### Phase 36: Home Screen Redesign
**Goal**: The Home tab is a dashboard overview with a hero balance section, a mini allocation chart, recent activity, and quick actions -- using glass cards with staggered entrance animation and animated counters on first load
**Depends on**: Phase 34
**Requirements**: SCRN-01, ANIM-02, ANIM-03, ANIM-05
**Success Criteria** (what must be TRUE):
  1. Home tab shows a hero balance card (total portfolio value), a mini donut allocation chart, the last 3 purchase activity items, and quick action buttons -- all within glass cards
  2. On first opening the Home tab after app launch, glass cards cascade in with staggered entrance (each card slightly delayed after the previous)
  3. The hero balance value animates from zero to the actual value on first load rather than appearing instantly
  4. Navigating between tabs and opening modals uses a smooth fade-and-scale transition instead of a hard cut
**Plans**: TBD

### Phase 37: Portfolio Screen Restyle
**Goal**: The Portfolio tab displays all assets and totals in glass cards with a visually prominent animated allocation donut and a slot-flip animation when the user toggles currency
**Depends on**: Phase 36
**Requirements**: SCRN-05, ANIM-06
**Success Criteria** (what must be TRUE):
  1. Portfolio summary and each per-asset row render inside glass-styled cards (non-blur variant for the scrollable asset list)
  2. The allocation donut chart is surrounded by a colored ambient glow matching the dominant asset color
  3. Tapping the VND/USD currency toggle causes the displayed value labels to flip with a vertical slot animation rather than updating instantly
**Plans**: TBD

### Phase 38: History, Config, and Navigation Shell
**Goal**: History and Config tabs receive glass restyles, and the navigation shell gains smooth tab-to-tab page transitions -- completing visual consistency across all 5 tabs
**Depends on**: Phase 37
**Requirements**: SCRN-03, SCRN-04
**Success Criteria** (what must be TRUE):
  1. History list items render with glass-styled rows (non-blur tint + border) without BackdropFilter inside the scroll view
  2. History filter bottom sheet is glass-styled with frosted appearance
  3. Config screen settings are grouped inside glass section cards with polished form field typography
  4. Tab switching uses a consistent fade-and-scale transition matching the modal transition established in Phase 36
**Plans**: TBD

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation & Hyperliquid Client | v1.0 | 3/3 | Complete | 2026-02-12 |
| 2. Core DCA Engine | v1.0 | 3/3 | Complete | 2026-02-12 |
| 3. Smart Multipliers | v1.0 | 3/3 | Complete | 2026-02-12 |
| 4. Enhanced Notifications & Observability | v1.0 | 3/3 | Complete | 2026-02-12 |
| 5. MultiplierCalculator Extraction | v1.1 | 1/1 | Complete | 2026-02-13 |
| 6. Backtest Simulation Engine | v1.1 | 2/2 | Complete | 2026-02-13 |
| 7. Historical Data Pipeline | v1.1 | 2/2 | Complete | 2026-02-13 |
| 8. API Endpoints & Parameter Sweep | v1.1 | 2/2 | Complete | 2026-02-13 |
| 9. Infrastructure & Aspire Integration | v1.2 | 2/2 | Complete | 2026-02-13 |
| 9.1 Migrate Dashboard to Fresh Nuxt Setup | v1.2 | 1/1 | Complete | 2026-02-13 |
| 10. Dashboard Core | v1.2 | 3/3 | Complete | 2026-02-13 |
| 11. Backtest Visualization | v1.2 | 4/4 | Complete | 2026-02-14 |
| 12. Configuration Management | v1.2 | 2/2 | Complete | 2026-02-14 |
| 13. Strongly-Typed IDs | v2.0 | 2/2 | Complete | 2026-02-18 |
| 14. Value Objects | v2.0 | 2/2 | Complete | 2026-02-18 |
| 15. Rich Aggregate Roots | v2.0 | 2/2 | Complete | 2026-02-19 |
| 16. Result Pattern | v2.0 | 2/2 | Complete | 2026-02-19 |
| 17. Domain Event Dispatch | v2.0 | 3/3 | Complete | 2026-02-19 |
| 18. Specification Pattern | v2.0 | 3/3 | Complete | 2026-02-19 |
| 19. Dashboard Nullable Price Fix | v2.0 | 1/1 | Complete | 2026-02-19 |
| 20. Flutter Project Setup + Core Infrastructure | v3.0 | 2/2 | Complete | 2026-02-19 |
| 21. Portfolio + Status Screens | v3.0 | 2/2 | Complete | 2026-02-20 |
| 22. Price Chart + Purchase History | v3.0 | 2/2 | Complete | 2026-02-20 |
| 23. Configuration Screen | v3.0 | 1/1 | Complete | 2026-02-20 |
| 24. Push Notifications | v3.0 | 3/3 | Complete | 2026-02-20 |
| 25. Nuxt Deprecation | v3.0 | 1/1 | Complete | 2026-02-20 |
| 25.1 Cross-Cutting Notification Handler Split | v3.0 | 1/1 | Complete | 2026-02-20 |
| 26. Portfolio Domain Foundation | v4.0 | 3/3 | Complete | 2026-02-20 |
| 27. Price Feed Infrastructure | v4.0 | 2/2 | Complete | 2026-02-20 |
| 28. Portfolio Backend API | v4.0 | 2/2 | Complete | 2026-02-20 |
| 29. Flutter Portfolio UI | v4.0 | 3/3 | Complete | 2026-02-20 |
| 30. Critical Bug Fixes | v4.0 | 1/1 | Complete | 2026-02-20 |
| 31. Milestone Verification | v4.0 | 1/1 | Complete | 2026-02-21 |
| 32. Tech Debt Cleanup | v4.0 | 3/3 | Complete | 2026-02-21 |
| 33. Design System Foundation | 2/2 | Complete    | 2026-02-21 | - |
| 34. Shared Glass Primitives | 2/2 | Complete   | 2026-02-21 | - |
| 35. Chart Redesign | 2/2 | Complete   | 2026-02-22 | - |
| 36. Home Screen Redesign | v5.0 | 0/? | Not started | - |
| 37. Portfolio Screen Restyle | v5.0 | 0/? | Not started | - |
| 38. History, Config, and Navigation Shell | v5.0 | 0/? | Not started | - |

---
*Roadmap updated: 2026-02-21 after v5.0 roadmap created*

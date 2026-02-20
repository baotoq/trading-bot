# Roadmap: BTC Smart DCA Bot

## Milestones

- ✅ **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 Backtesting Engine** -- Phases 5-8 (shipped 2026-02-13) -- [archive](milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 Web Dashboard** -- Phases 9-12 (shipped 2026-02-14) -- [archive](milestones/v1.2-ROADMAP.md)
- ✅ **v2.0 DDD Foundation** -- Phases 13-19 (shipped 2026-02-20) -- [archive](milestones/v2.0-ROADMAP.md)
- 🚧 **v3.0 Flutter Mobile** -- Phases 20-25 (in progress)

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

### 🚧 v3.0 Flutter Mobile (In Progress)

**Milestone Goal:** Replace Nuxt web dashboard with a native Flutter iOS app with full dashboard feature parity, add push notifications for buy events and health alerts, and remove the Nuxt dashboard from Aspire orchestration.

- [x] **Phase 20: Flutter Project Setup + Core Infrastructure** - Scaffold Flutter project with secure API key storage, Dio HTTP client, and navigation (complete 2026-02-19)
- [ ] **Phase 21: Portfolio + Status Screens** - Home screen with portfolio stats, live price, bot health badge, and countdown timer
- [ ] **Phase 22: Price Chart + Purchase History** - Interactive price chart with purchase markers and scrollable purchase history
- [ ] **Phase 23: Configuration Screen** - View and edit DCA configuration with server validation
- [ ] **Phase 24: Push Notifications** - FCM push delivery for buy events and health alerts, with backend token management
- [ ] **Phase 25: Nuxt Deprecation** - Remove Nuxt dashboard from Aspire orchestration

## Phase Details

### Phase 20: Flutter Project Setup + Core Infrastructure
**Goal**: Users can authenticate the app against the .NET API with their API key injected at build time, and the app launches to a dark-only themed 4-tab navigation with error handling infrastructure
**Depends on**: Nothing (first phase of milestone)
**Requirements**: APP-01, APP-02, APP-03, APP-04, APP-05, APP-06
**Success Criteria** (what must be TRUE):
  1. API base URL and API key are injected at build time via --dart-define and sent as x-api-key header on every Dio request
  2. All subsequent API requests automatically carry the x-api-key header without any user action
  3. When any API call returns 401 or 403, a snackbar shows "Authentication failed" and user stays on current screen
  4. App renders in dark-only mode with Bitcoin orange accent, ignoring iOS system setting
  5. User can pull-to-refresh on any data screen and the API is re-fetched; transient failures show a snackbar with cached data still visible
**Plans**: 2 plans

Plans:
- [x] 20-01-PLAN.md — Flutter project scaffold with dependencies, dark theme, go_router 4-tab navigation, placeholder screens with pull-to-refresh
- [x] 20-02-PLAN.md — Dio HTTP client with build-time config, API key interceptor, typed exceptions, error snackbar/retry widgets

### Phase 21: Portfolio + Status Screens
**Goal**: Users can see their full portfolio position and confirm the bot is alive, with live price and a countdown to the next buy
**Depends on**: Phase 20
**Requirements**: PORT-01, PORT-02, PORT-03, PORT-04, PORT-05
**Success Criteria** (what must be TRUE):
  1. User can see total BTC accumulated, total cost in USD, and unrealized P&L (green/red) on the home screen
  2. Live BTC price refreshes automatically every 30 seconds without user interaction
  3. Bot health status badge (healthy/warning/down) is always visible and reflects the current API health response
  4. A countdown timer shows time remaining until the next scheduled buy, counting down in real time client-side
  5. A last buy detail card shows the most recent purchase's date, price, BTC amount, multiplier used, and drop percentage
**Plans**: 2 plans

Plans:
- [ ] 21-01-PLAN.md — Data layer: extend LiveStatusResponse, Dart DTO models, HomeRepository, Riverpod providers with 30s auto-refresh
- [ ] 21-02-PLAN.md — UI layer: portfolio stats section, health badge, countdown text, last buy card, assemble HomeScreen

### Phase 22: Price Chart + Purchase History
**Goal**: Users can visually explore their DCA performance on a price chart with purchase markers and scroll through the full purchase history
**Depends on**: Phase 21
**Requirements**: CHART-01, CHART-02, CHART-03, CHART-04, CHART-05, CHART-06
**Success Criteria** (what must be TRUE):
  1. User can switch between 7D, 1M, 3M, 6M, 1Y, and All timeframes on the price chart and the chart updates accordingly
  2. Purchase markers appear on the chart colored by multiplier tier (different color per tier) at the correct price and date
  3. A dashed average cost basis line overlays the chart at the user's running average cost
  4. Touching the chart shows a tooltip with the price and date at the touched point
  5. User can scroll through the full purchase history as an infinite list using cursor pagination; new pages load automatically as the user scrolls
  6. User can open a bottom sheet to filter purchase history by date range and multiplier tier
**Plans**: TBD

Plans:
- [ ] 22-01: Price chart screen (fl_chart line chart, 6 timeframes, purchase markers, avg cost line, touch tooltip)
- [ ] 22-02: Purchase history screen (infinite scroll, cursor pagination, card design, bottom sheet filters)

### Phase 23: Configuration Screen
**Goal**: Users can view and edit their live DCA configuration parameters, including multiplier tiers, directly from the app
**Depends on**: Phase 22
**Requirements**: CONF-01, CONF-02, CONF-03, CONF-04
**Success Criteria** (what must be TRUE):
  1. User can view all DCA configuration parameters (base amount, daily schedule, multiplier tiers, bear market boost settings) on a readable config screen
  2. User can tap Edit and modify any numeric parameter using a numeric keyboard, and change the daily buy time via a time picker
  3. User can add, remove, and reorder multiplier tier entries within the edit form before saving
  4. When the server rejects a config change (e.g., invalid tier order, out-of-range value), the validation error appears inline next to the relevant field
**Plans**: TBD

Plans:
- [ ] 23-01: Config view + edit form (numeric fields, time picker, tier list add/remove, PUT /api/config, inline server validation errors)

### Phase 24: Push Notifications
**Goal**: Users receive push notifications on their iPhone when a BTC purchase executes, when a purchase fails, and when the bot has not bought in over 36 hours
**Depends on**: Phase 21 (screens to deep-link into must exist)
**Requirements**: PUSH-01, PUSH-02, PUSH-03, PUSH-04, PUSH-05
**Success Criteria** (what must be TRUE):
  1. User receives a push notification within seconds of a BTC purchase executing, showing the amount, price, and multiplier used
  2. User receives a push notification when the bot misses a buy (no purchase in more than 36 hours)
  3. User receives a push notification when a purchase attempt fails
  4. Tapping any notification opens the app and navigates to the relevant screen (e.g., purchases screen for a buy executed notification)
  5. The backend stores the device FCM token and automatically removes stale tokens when FCM reports them as unregistered
**Plans**: TBD

Plans:
- [ ] 24-01: Backend FCM infrastructure (DeviceToken entity, EF migration, POST /api/devices/register, DELETE /api/devices/{token}, FirebaseAdmin NuGet)
- [ ] 24-02: Backend FcmNotificationService (hooks into PurchaseCompletedHandler, multicast send, stale token cleanup, missed buy alert)
- [ ] 24-03: Flutter FCM integration (Firebase project setup, GoogleService-Info.plist, Xcode entitlements, FcmService, foreground display via flutter_local_notifications, deep-link tap handler)

### Phase 25: Nuxt Deprecation
**Goal**: The Nuxt dashboard is no longer started by Aspire, removing it from the local development orchestration while preserving the code
**Depends on**: Phase 24 (Flutter app confirmed working before removing fallback)
**Requirements**: DEPR-01
**Success Criteria** (what must be TRUE):
  1. Running `dotnet run` in TradingBot.AppHost does not start the Nuxt dashboard container
  2. The TradingBot.Dashboard directory and all its code remain intact and undeleted
**Plans**: TBD

Plans:
- [ ] 25-01: Remove Nuxt resource from AppHost.cs orchestration

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
| 20. Flutter Project Setup + Core Infrastructure | v3.0 | Complete    | 2026-02-19 | 2026-02-19 |
| 21. Portfolio + Status Screens | v3.0 | 0/2 | Not started | - |
| 22. Price Chart + Purchase History | v3.0 | 0/2 | Not started | - |
| 23. Configuration Screen | v3.0 | 0/1 | Not started | - |
| 24. Push Notifications | v3.0 | 0/3 | Not started | - |
| 25. Nuxt Deprecation | v3.0 | 0/1 | Not started | - |

---
*Roadmap updated: 2026-02-20 after 20-02 completion (Phase 20 complete)*

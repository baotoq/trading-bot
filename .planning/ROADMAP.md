# Roadmap: BTC Smart DCA Bot

## Milestones

- **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- **v1.1 Backtesting Engine** -- Phases 5-8 (shipped 2026-02-13) -- [archive](milestones/v1.1-ROADMAP.md)
- **v1.2 Web Dashboard** -- Phases 9-12 (in progress)

## Phases

<details>
<summary>v1.0 Daily BTC Smart DCA (Phases 1-4) -- SHIPPED 2026-02-12</summary>

- [x] Phase 1: Foundation & Hyperliquid Client (3/3 plans) -- completed 2026-02-12
- [x] Phase 2: Core DCA Engine (3/3 plans) -- completed 2026-02-12
- [x] Phase 3: Smart Multipliers (3/3 plans) -- completed 2026-02-12
- [x] Phase 4: Enhanced Notifications & Observability (3/3 plans) -- completed 2026-02-12

</details>

<details>
<summary>v1.1 Backtesting Engine (Phases 5-8) -- SHIPPED 2026-02-13</summary>

- [x] Phase 5: MultiplierCalculator Extraction (1/1 plan) -- completed 2026-02-13
- [x] Phase 6: Backtest Simulation Engine (2/2 plans) -- completed 2026-02-13
- [x] Phase 7: Historical Data Pipeline (2/2 plans) -- completed 2026-02-13
- [x] Phase 8: API Endpoints & Parameter Sweep (2/2 plans) -- completed 2026-02-13

</details>

### v1.2 Web Dashboard (In Progress)

**Milestone Goal:** Visual dashboard for monitoring portfolio, viewing purchase history, running backtests, managing configuration, and tracking live bot status.

- [x] **Phase 9: Infrastructure & Aspire Integration** - Nuxt project setup and orchestration -- completed 2026-02-13
- [x] **Phase 9.1: Migrate Dashboard to Fresh Nuxt Setup** - Move API proxy & auth code to recreated Nuxt project (INSERTED) -- completed 2026-02-13
- [x] **Phase 10: Dashboard Core** - Portfolio overview, purchase history, live status -- completed 2026-02-13
- [ ] **Phase 11: Backtest Visualization** - Equity curves and parameter comparison
- [ ] **Phase 12: Configuration Management** - Editable config with server validation

## Phase Details

### Phase 9: Infrastructure & Aspire Integration

**Goal:** Nuxt 4 frontend project is created, orchestrated via Aspire, and secured with API key authentication

**Depends on:** Nothing (new frontend layer)

**Requirements:** INFR-01, INFR-02, INFR-03, INFR-04

**Success Criteria** (what must be TRUE):
1. User can access Nuxt dev server running on localhost via Aspire orchestration
2. Frontend can call backend API endpoints through proxy without CORS issues
3. API requests include API key authentication and receive 403 if key is missing or invalid
4. Aspire dashboard shows both .NET API and Nuxt frontend as healthy services

**Plans:** 2 plans

Plans:
- [x] 09-01-PLAN.md — Nuxt 4 project creation + Aspire orchestration
- [x] 09-02-PLAN.md — API proxy + API key authentication

### Phase 09.1: Migrate Dashboard to Fresh Nuxt Setup (INSERTED)

**Goal:** Move API proxy & auth code from old dashboard/ to fresh Nuxt 4 TradingBot.Dashboard/ project
**Depends on:** Phase 9
**Plans:** 1 plan

Plans:
- [x] 09.1-01-PLAN.md — Migrate files, install deps, update AppHost path, remove old directory

### Phase 10: Dashboard Core

**Goal:** User can view complete portfolio overview, paginated purchase history, and live bot status

**Depends on:** Phase 9

**Requirements:** PORT-01, PORT-02, PORT-03, PORT-04, PORT-05, HIST-01, HIST-02, HIST-03, HIST-04, LIVE-01, LIVE-02, LIVE-03

**Success Criteria** (what must be TRUE):
1. User can see total BTC accumulated, total cost invested, average cost basis, and unrealized P&L
2. User can see current BTC price updating within 10 seconds
3. User can view paginated purchase history with price, multiplier tier, amount, and BTC quantity per purchase
4. User can sort and filter purchases by date range and multiplier tier
5. User can see purchase timeline as chart overlay on price with multiplier markers
6. User can see bot health status indicator (healthy/error/warning) and next scheduled buy time with countdown
7. User can see connection status indicator showing connected/reconnecting/disconnected state

**Plans:** 3 plans

Plans:
- [x] 10-01-PLAN.md -- Backend dashboard API endpoints (portfolio, purchases, status, chart)
- [x] 10-02-PLAN.md -- Frontend types, composables, npm deps, and server proxy routes
- [x] 10-03-PLAN.md -- Dashboard UI components and main page assembly

### Phase 11: Backtest Visualization

**Goal:** User can run backtests from dashboard and visualize equity curves comparing smart DCA vs fixed DCA

**Depends on:** Phase 10

**Requirements:** BTST-01, BTST-02, BTST-03, BTST-04, BTST-05

**Success Criteria** (what must be TRUE):
1. User can configure backtest parameters (date range, base amount, multiplier tiers) from dashboard form
2. User can trigger backtest execution and see progress indicator while backtest runs
3. User can view equity curve chart comparing smart DCA vs fixed DCA strategies over time
4. User can see backtest metrics table showing cost basis, total BTC, efficiency ratio, and max drawdown
5. User can run parameter sweep and view ranked results sorted by efficiency or return
6. User can compare multiple backtest configurations side-by-side with visual overlays

**Plans:** 4 plans

Plans:
- [ ] 11-01-PLAN.md -- Backend config endpoint, proxy routes, TypeScript types, composable
- [ ] 11-02-PLAN.md -- BacktestForm, BacktestChart, BacktestMetrics, backtest page
- [ ] 11-03-PLAN.md -- SweepForm, SweepResultsTable, tabbed page layout
- [ ] 11-04-PLAN.md -- BacktestComparison with session storage, Add to Compare integration

### Phase 12: Configuration Management

**Goal:** User can view and edit DCA configuration from dashboard with server-side validation

**Depends on:** Phase 11

**Requirements:** CONF-01, CONF-02, CONF-03, CONF-04

**Success Criteria** (what must be TRUE):
1. User can view current DCA configuration including base amount, schedule time, multiplier tier thresholds and values, and bear market settings
2. User can edit base daily amount and schedule time from dashboard form
3. User can edit multiplier tier thresholds and multiplier values from dashboard form
4. Config changes are validated server-side before applying and user sees clear validation errors if invalid
5. User sees confirmation after successful config update and understands whether bot restart is needed

**Plans:** TBD

Plans:
- [ ] 12-01: TBD
- [ ] 12-02: TBD

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
| 11. Backtest Visualization | v1.2 | 0/4 | Not started | - |
| 12. Configuration Management | v1.2 | 0/TBD | Not started | - |

---
*Roadmap updated: 2026-02-13 after Phase 10 completion*

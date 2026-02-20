# Roadmap: BTC Smart DCA Bot

## Milestones

- ✅ **v1.0 Daily BTC Smart DCA** -- Phases 1-4 (shipped 2026-02-12) -- [archive](milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 Backtesting Engine** -- Phases 5-8 (shipped 2026-02-13) -- [archive](milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 Web Dashboard** -- Phases 9-12 (shipped 2026-02-14) -- [archive](milestones/v1.2-ROADMAP.md)
- ✅ **v2.0 DDD Foundation** -- Phases 13-19 (shipped 2026-02-20) -- [archive](milestones/v2.0-ROADMAP.md)
- ✅ **v3.0 Flutter Mobile** -- Phases 20-25.1 (shipped 2026-02-20) -- [archive](milestones/v3.0-ROADMAP.md)
- 📋 **v4.0 Portfolio Tracker** -- Phases 26-32 (planned)

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

### 📋 v4.0 Portfolio Tracker (Planned)

**Milestone Goal:** Multi-asset portfolio tracking (crypto, VN30 ETF, fixed deposits) with live prices, P&L, and multi-currency support (VND/USD) in the Flutter app.

- [x] **Phase 26: Portfolio Domain Foundation** - Domain model, aggregates, EF migration for multi-asset portfolio
- [x] **Phase 27: Price Feed Infrastructure** - Crypto, VN ETF, and USD/VND exchange rate providers with Redis caching
- [x] **Phase 28: Portfolio Backend API** - Read endpoints, write endpoints, DCA auto-import, and historical migration
- [x] **Phase 29: Flutter Portfolio UI** - Complete portfolio feature module in Flutter with currency toggle and staleness indicators
- [x] **Phase 30: Critical Bug Fixes** - Enum mismatch, tooltip formatting, missing asset creation endpoint (completed 2026-02-20)
- [x] **Phase 31: Milestone Verification** - VERIFICATION.md for all phases, traceability closure (completed 2026-02-21)
- [ ] **Phase 32: Tech Debt Cleanup** - Tests, edge case handling, missing Flutter CRUD methods

## Phase Details

### Phase 26: Portfolio Domain Foundation
**Goal**: The database schema and domain model for multi-asset portfolio tracking are in place and tested, ready for all subsequent layers to build on
**Depends on**: Phase 25.1
**Requirements**: PORT-01, PORT-02, PORT-03, PORT-06
**Success Criteria** (what must be TRUE):
  1. `PortfolioAsset` and `AssetTransaction` entities persist and round-trip through EF Core with all fields intact
  2. `FixedDeposit` entity persists with CompoundingFrequency, principal, rate, start date, and maturity date
  3. Fixed deposit accrued value computes correctly for both simple interest and compound (monthly/quarterly/semi-annual/annual) scenarios
  4. Vogen typed IDs (PortfolioAssetId, AssetTransactionId, FixedDepositId) are registered in ConfigureConventions() and pass round-trip EF tests
  5. VndAmount and integer ETF share quantities use correct precision (VND: no decimals, ETF: whole numbers only)
**Plans**: 3 plans

Plans:
- [ ] 26-01-PLAN.md — Domain models: typed IDs, VndAmount, enums, PortfolioAsset/AssetTransaction/FixedDeposit entities
- [ ] 26-02-PLAN.md — InterestCalculator TDD: accrued value calculation for all compounding frequencies
- [ ] 26-03-PLAN.md — EF Core configuration, migration, and integration tests

### Phase 27: Price Feed Infrastructure
**Goal**: Live prices are available for all asset types — crypto from CoinGecko, VN ETFs from VNDirect finfo, and USD/VND rate from open.er-api.com — with Redis caching and graceful degradation
**Depends on**: Phase 26
**Requirements**: PRICE-01, PRICE-02, PRICE-03, PRICE-04
**Research Flag**: NEEDS `/gsd:research-phase` -- VNDirect finfo API JSON response schema is unconfirmed (endpoint timed out during pre-research; field names inferred from vnstock Python source)
**Success Criteria** (what must be TRUE):
  1. BTC and other crypto asset prices are fetched from CoinGecko and cached in Redis with a 5-minute TTL
  2. VN30 ETF prices are fetched from VNDirect finfo API and cached in Redis with a 48-hour TTL; HTTP failure returns last cached value, not an error
  3. USD/VND exchange rate is fetched from open.er-api.com and cached in Redis with a 12-hour TTL
  4. Every price type exposes a last-updated timestamp so consumers can surface staleness to the user
**Plans**: 2 plans

Plans:
- [ ] 27-01-PLAN.md — Shared price feed types (PriceFeedEntry, PriceFeedResult, interfaces) + CoinGecko crypto provider
- [ ] 27-02-PLAN.md — VNDirect ETF provider, exchange rate provider, DI registration + Program.cs wiring

### Phase 28: Portfolio Backend API
**Goal**: All backend endpoints exist for portfolio read, manual write, DCA auto-import, and fixed deposit management — the complete API surface the Flutter app will consume
**Depends on**: Phase 27
**Requirements**: PORT-04, PORT-05
**Success Criteria** (what must be TRUE):
  1. GET /api/portfolio/summary returns total value in both USD and VND, per-asset P&L, and allocation percentages
  2. DCA bot purchases auto-import into the BTC portfolio position idempotently — running the import twice produces no duplicate transactions
  3. All historical DCA bot purchases from the existing Purchases table are migrated into AssetTransactions on first setup
  4. POST /api/portfolio/assets/{id}/transactions accepts manual buy/sell with date, quantity, price, and currency; rejects fractional ETF quantities and future dates
  5. POST/GET/PUT/DELETE /api/portfolio/fixed-deposits endpoints handle the full fixed deposit lifecycle including accrued value calculation
**Plans**: 2 plans

Plans:
- [ ] 28-01-PLAN.md — Domain changes (SourcePurchaseId, FixedDeposit.Update), DCA auto-import handler, historical migration service
- [ ] 28-02-PLAN.md — Portfolio API endpoints (summary, assets, transactions) + fixed deposit CRUD endpoints

### Phase 29: Flutter Portfolio UI
**Goal**: Users can see and manage their full multi-asset portfolio in the Flutter app, with VND/USD toggle, per-asset P&L, allocation chart, and all transaction entry forms
**Depends on**: Phase 28
**Requirements**: DISP-01, DISP-02, DISP-03, DISP-04, DISP-05, DISP-06, DISP-07, DISP-08, DISP-09, DISP-10
**Success Criteria** (what must be TRUE):
  1. User can toggle between VND and USD display for all portfolio values; the preference persists across app restarts
  2. User can see per-asset holdings grouped by type (Crypto / ETF / Fixed Deposit) with current value, unrealized P&L in absolute and percentage terms
  3. User can see an asset allocation pie chart broken down by asset type
  4. User can add manual buy/sell transactions and new fixed deposits via forms in the Flutter app
  5. User can see fixed deposit details including accrued value, days to maturity, and projected maturity amount
  6. User can browse full transaction history across all assets and filter by asset, transaction type, and date range; auto-imported DCA bot transactions show a "Bot" badge and cannot be edited or deleted
  7. VN asset prices show a "price as of [date]" staleness indicator when using cached data; cross-currency values show "converted at today's rate"
**Plans**: 3 plans

Plans:
- [ ] 29-01-PLAN.md — Backend transaction list endpoint + Flutter data layer (models, repository, providers, currency toggle)
- [ ] 29-02-PLAN.md — Portfolio main screen (navigation tab, summary card, donut chart, expandable asset sections, staleness indicators)
- [ ] 29-03-PLAN.md — Sub-screens (add transaction/deposit form, transaction history with filters, fixed deposit detail)

### Phase 30: Critical Bug Fixes
**Goal**: All 3 code bugs identified in the milestone audit are fixed — simple-interest fixed deposits create successfully, donut chart tooltip displays correct currency, and users can create portfolio assets from Flutter
**Depends on**: Phase 29
**Requirements**: PORT-01, PORT-03, DISP-03, DISP-05
**Gap Closure**: Closes 3 integration gaps and 2 broken E2E flows from audit
**Success Criteria** (what must be TRUE):
  1. Flutter sends `'Simple'` (not `'None'`) for CompoundingFrequency — POST /api/portfolio/fixed-deposits succeeds for simple-interest deposits
  2. AllocationDonutChart tooltip displays values in the correct currency format (VND when in VND mode, USD when in USD mode)
  3. POST /api/portfolio/assets endpoint exists and accepts name, ticker, asset type, and native currency; Flutter has a UI to create new portfolio assets
**Plans**: 1 plan

Plans:
- [ ] 30-01-PLAN.md — Fix CompoundingFrequency enum mismatch, donut chart tooltip currency bug, add asset creation endpoint + Flutter UI

### Phase 31: Milestone Verification
**Goal**: All 20 v4.0 requirements are formally verified with VERIFICATION.md files for phases 26-30, SUMMARY frontmatter updated, and REQUIREMENTS.md traceability checkboxes marked complete
**Depends on**: Phase 30
**Requirements**: All 20 (verification closure)
**Gap Closure**: Closes all verification gaps from audit
**Success Criteria** (what must be TRUE):
  1. VERIFICATION.md exists for phases 26, 27, 28, 29, and 30 with per-requirement pass/fail status
  2. All SUMMARY.md files have `requirements-completed` frontmatter listing their satisfied requirements
  3. REQUIREMENTS.md traceability shows all 20 requirements as `[x]` with correct phase assignments
**Plans**: 1 plan

Plans:
- [x] 31-01-PLAN.md — Create VERIFICATION.md for all phases, update SUMMARY frontmatter, close REQUIREMENTS.md traceability

### Phase 32: Tech Debt Cleanup
**Goal**: Key tech debt items from the audit are resolved — price feed and portfolio endpoint tests exist, exchange rate failure is handled gracefully, and Flutter CRUD is complete for fixed deposits
**Depends on**: Phase 30
**Requirements**: None (quality improvement)
**Gap Closure**: Addresses 15 tech debt items from audit
**Success Criteria** (what must be TRUE):
  1. Unit tests exist for CoinGeckoPriceProvider, VNDirectPriceProvider, and OpenErApiProvider
  2. Integration tests exist for portfolio summary, transaction, and fixed deposit endpoints
  3. Exchange rate failure returns last cached value (not 0) for VND conversions
  4. PortfolioRepository has updateFixedDeposit/deleteFixedDeposit methods; Flutter UI has edit/delete for fixed deposits
  5. CoinGecko ID mapping supports dynamic lookup (not hardcoded BTC/ETH only)
**Plans**: 3 plans

Plans:
- [ ] 32-01-PLAN.md — Price feed unit tests + exchange rate graceful degradation
- [ ] 32-02-PLAN.md — Flutter fixed deposit CRUD completion + CoinGecko dynamic ID lookup
- [ ] 32-03-PLAN.md — Integration tests for portfolio and fixed deposit endpoints

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
| 30. Critical Bug Fixes | 1/1 | Complete    | 2026-02-20 | - |
| 31. Milestone Verification | v4.0 | Complete    | 2026-02-20 | 2026-02-21 |
| 32. Tech Debt Cleanup | 1/3 | In Progress|  | - |

---
*Roadmap updated: 2026-02-21 after Phase 31 completion (milestone verification — all 20 v4.0 requirements closed)*

# Project Research Summary

**Project:** BTC Smart DCA Bot — v4.0 Multi-Asset Portfolio Tracker
**Domain:** Personal portfolio tracker (crypto, VN30 ETF, fixed deposits) integrated into existing .NET 10 / Flutter DCA bot
**Researched:** 2026-02-20
**Confidence:** HIGH for crypto/architecture; MEDIUM for VN stock price APIs

## Executive Summary

The v4.0 milestone extends the existing BTC DCA bot into a unified personal portfolio tracker covering crypto, Vietnamese ETFs, and fixed deposits — all displayed in both VND and USD. The recommended approach requires zero new NuGet packages and zero new Flutter packages: all required capabilities (price fetching, currency conversion, chart rendering, number formatting) are covered by extending existing infrastructure with three new HTTP client registrations and one EF Core migration. The architecture stays entirely within `TradingBot.ApiService` by adding a new portfolio domain alongside the existing DCA domain, connected only via domain events — the DCA domain has zero awareness of the portfolio domain.

The critical design constraint is that crypto (BTC/ETH), VN30 ETFs, and fixed deposits are fundamentally different asset classes that must not be unified into a single model. Crypto assets are market-priced in USD with buy/sell transactions. VN30 ETFs are market-priced in VND with end-of-day close prices from an unofficial API. Fixed deposits are not market-priced at all — their value is a deterministic interest calculation. This heterogeneity drives two separate aggregate roots (`PortfolioAsset` for tradeable assets, `FixedDeposit` for term deposits) merged only at the read/DTO layer by `PortfolioCalculationService`.

The primary risk is VN stock price data fragility: there is no official, free, production-grade JSON API for HOSE-listed ETFs. The VNDirect finfo API (reverse-engineered from the vnstock Python community) works without authentication but carries no SLA. The mitigation is mandatory: treat VN prices as best-effort end-of-day data with Redis caching (48-hour TTL), always show a staleness indicator, and degrade gracefully to cached prices rather than erroring. The second major risk is multi-currency P&L correctness — cost basis and current value must be compared in the same native currency, with USD display being a conversion-only concern.

## Key Findings

### Recommended Stack

The stack requires no new dependencies. Existing infrastructure handles all v4.0 needs: the CoinGecko HTTP client is extended for multi-coin current prices; a new typed `HttpClient` registration wraps the VNDirect finfo API for VN ETF prices; a second typed `HttpClient` calls the key-free `open.er-api.com` for USD/VND rates. All three benefit from `Microsoft.Extensions.Http.Resilience` already in the project. Redis provides price caching with type-specific TTLs. Separate aggregate tables (not TPH) are used for portfolio assets and fixed deposits — TPH would introduce nullable columns for FD-specific fields on every asset row, which is the anti-pattern the architecture research explicitly identifies.

**Core technologies:**
- **CoinGecko `/simple/price` extension** — multi-coin current prices in USD; 30 calls/min free tier; already integrated for historical data; extend with a new method on the existing `CoinGeckoClient`
- **VNDirect finfo API (named HttpClient)** — VN30 ETF end-of-day prices in VND; unofficial, no auth required; must use with resilience handler and stale-value fallback; MEDIUM confidence
- **open.er-api.com (typed HttpClient)** — USD/VND exchange rate; key-free; VND confirmed in live response (25,905.86 VND/USD on 2026-02-20); cache 12 hours in Redis
- **EF Core separate tables** — two new aggregate roots with separate DB tables; three new DbSets in `TradingBotDbContext`; no new NuGet package
- **Redis (existing)** — price cache with type-specific TTLs (5-min crypto, 60-min VN ETF, 12-hour exchange rate)
- **`intl` Flutter package (already present)** — `NumberFormat.currency(locale: 'vi_VN', symbol: '₫', decimalDigits: 0)` for VND formatting
- **`fl_chart` Flutter package (already present)** — `PieChartData` for asset allocation chart; `LineChart` for portfolio value over time

### Expected Features

**Must have (P1 — v4.0 launch):**
- Total portfolio value with VND/USD toggle (persisted as Riverpod `StateProvider<Currency>` in `core/providers/`)
- Per-asset holdings view: quantity, current price, total value, P&L (absolute + percent) grouped by asset type
- Unrealized P&L per asset — weighted average cost basis; computed at request time from cached prices, never persisted
- Manual transaction entry for crypto and ETF assets (Buy/Sell) with date, quantity, price per unit, currency
- Fixed deposit entry: principal (VND), annual rate, start date, maturity date, compounding frequency (None/Monthly/Quarterly/SemiAnnual/Annual)
- Auto-import DCA bot purchases into BTC portfolio position — idempotent via `source_reference_id` = `PurchaseId`
- Asset allocation pie chart by asset type (Crypto / ETF / Fixed Deposit) using existing `fl_chart` `PieChartData`
- Auto-fetch crypto prices via CoinGecko (existing integration extended for multi-coin)
- USD/VND exchange rate daily fetch and cache (open.er-api.com, 12h Redis TTL)
- Transaction history list filterable by asset, type, and date range

**Should have (P2 — v4.x):**
- VN30 ETF price auto-fetch (VNDirect finfo API — requires implementation-phase API schema verification)
- Portfolio value chart over time (on-demand computation with Redis cache + invalidation on transaction save)
- Per-asset performance chart (drill-down to individual asset history)
- BTC portfolio quantity reconciliation vs. Hyperliquid spot balance (UI warning on mismatch)
- Fixed deposit maturity alerts via push notification

**Defer (v5+):**
- Manual price entry as ETF price fallback when API is unavailable
- Historical VN ETF price chart (requires separate data ingestion pipeline)
- Additional crypto assets beyond BTC (multi-coin CoinGecko call already architected; coin ID search endpoint needed)
- CSV export for tax advisor use

**Anti-features to exclude:**
- Real-time price streaming via WebSocket (VN market closes at 14:45 ICT; crypto polling every 5 minutes adequate; battery and complexity concern)
- Broker/exchange API sync (Vietnamese brokers require in-person auth registration; out of scope)
- Tax reporting / capital gains calculations (regulatory risk; show P&L clearly for user to provide to tax advisor)
- Portfolio rebalancing suggestions (financial advice, regulatory licensing required)
- FIFO/LIFO cost basis (weighted average is sufficient and far simpler for DCA accumulation style)
- Historical VND/USD rate at transaction time (historical rate APIs require paid tier; display at today's rate with clear label is correct scope)

### Architecture Approach

The portfolio domain sits alongside the existing DCA domain within a single `TradingBot.ApiService`, connected only via the `PurchaseCompletedEvent` domain event already flowing through Dapr pub-sub. A new `PurchaseCompletedPortfolioHandler` consumes this event to auto-import DCA fills — the DCA domain has zero knowledge of the portfolio domain. All price fetching is parallelised via `Task.WhenAll` in `PriceProviderFactory`, with per-provider graceful degradation (each provider returns a Redis-cached stale value on HTTP failure rather than propagating errors). The Flutter side adds a single `currency_provider.dart` Riverpod `StateProvider` in `core/providers/` — a global toggle persisting VND/USD preference across all screens. The API always returns both `valueUsd` and `valueVnd` in every response; currency toggling is pure Flutter display logic with no re-fetch.

**Major components:**
1. **`PortfolioAsset` aggregate root** — owns `AssetTransaction` child entities; maintains weighted average cost basis computed on every `AddTransaction` call; stores `NativeCurrency` ("USD" or "VND") and optional `CoinGeckoId`
2. **`FixedDeposit` aggregate root** — separate table; `AccruedValueVnd(DateOnly asOf)` is a computed method (never persisted), calculated as `PrincipalVnd * (1 + r/n)^(n*t)` or simple interest for non-cumulative; has explicit `CompoundingFrequency` enum field
3. **`PriceProviderFactory`** — dispatches to `ICryptoPriceProvider`, `IVnStockPriceProvider`, `IExchangeRateProvider` in parallel; each provider checks Redis via `PriceCacheService` before making HTTP call
4. **`PortfolioCalculationService`** — pure service combining prices + transactions to compute per-asset P&L in native currency, allocation %, and total value in both USD and VND
5. **`PurchaseCompletedPortfolioHandler`** — idempotent MediatR `INotificationHandler`; upserts BTC `PortfolioAsset`; adds `AssetTransaction` with `ExternalReference = purchaseId.ToString()`; skips dry-run purchases
6. **`currency_provider.dart`** — Riverpod global `StateProvider<Currency>`; currency toggle is pure UI concern; no API re-fetch on toggle

**Build order (hard dependency chain):**
1. Domain models + EF migration (all else depends on this)
2. Price provider infrastructure (independently testable)
3. Portfolio calculation service + read endpoints (unblocks Flutter development)
4. DCA auto-import handler + historical migration (parallelizable with step 3)
5. Manual transaction entry endpoints
6. Flutter portfolio feature module (built last — after API shape is stable)

### Critical Pitfalls

1. **Currency conversion timing corrupts P&L** — Multi-currency P&L is only meaningful when cost basis and current value use the same currency snapshot. Store `cost_native`, `cost_usd`, and `exchange_rate_at_transaction` at write time. Compute P&L in the asset's native currency only; USD display is conversion-only at read time. Never recompute historical cost basis using today's exchange rate. Must be resolved in the data model phase — retrofitting requires re-entering all transaction history.

2. **DCA auto-import duplicates** — Store `source_type` (enum: `DcaBot | Manual`) and `source_reference_id` on every `PortfolioTransaction`. Add a unique index on `(asset_id, source_type, source_reference_id)`. Auto-import uses `INSERT ... ON CONFLICT DO NOTHING`. Must be idempotent before the first import run executes. Lock auto-imported transactions as read-only in the UI (not editable/deletable by the user).

3. **VN price API fragility** — VNDirect finfo API is unofficial with no SLA; breaks silently. Mitigation: Redis cache with 48-hour TTL; "price as of [date]" staleness indicator on all VN asset prices; graceful degradation returning last cached price on HTTP failure; never attempt intraday fetches. Accept end-of-day-only as a product decision, not a limitation to hide. Must be designed into `IVnStockPriceProvider` contract from day one.

4. **Fixed deposit compounding frequency mismatch** — Vietnamese banks use simple interest for non-cumulative FDs and compound interest (monthly/quarterly) for cumulative FDs. Model must have `CompoundingFrequency` enum (`None | Monthly | Quarterly | SemiAnnual | Annual`). Formula differs materially: `None` uses `P * r * (days/365)`, compound uses `P * (1 + r/n)^(n*t)`. Must be modelled correctly at schema design time.

5. **Vogen `ConfigureConventions()` omission** — Every new Vogen `[ValueObject]` ID type (`PortfolioAssetId`, `AssetTransactionId`, `FixedDepositId`) requires explicit registration in `TradingBotDbContext.ConfigureConventions()`. Missing registration causes EF Core to silently use raw `Guid`, producing runtime conversion errors. Write a round-trip EF test for each new entity type — catches this at test time, not in production.

6. **Mixed quantity/precision for multi-currency amounts** — Do not reuse `UsdAmount` for VND values or `Quantity` (8 decimal places) for ETF shares (always whole integers). Introduce `VndAmount` with `HasPrecision(18, 0)` (VND has no subunit in practice) and use `int` for ETF share quantities. Must be resolved at schema design time.

7. **Portfolio chart performance trap** — Chart endpoint without a cache degrades to O(days × assets) query; becomes noticeable after 90 days of history. Solution: compute on demand and cache in Redis with 25-hour TTL; invalidate cache when a transaction with a date >= cached start is saved. Must be designed before building the chart endpoint, not added when performance issues appear.

## Implications for Roadmap

The portfolio tracker has hard dependency ordering driven by the data model: 4 of 7 critical pitfalls must be resolved at schema design time and cannot be retrofitted without requiring users to re-enter financial history. The recommended phase structure flows strictly from that constraint.

### Phase 1: Portfolio Domain Foundation (Data Model + Migration)

**Rationale:** Every other component depends on the database schema. Pitfalls #1, #4, #5, and #6 must be resolved here — they cannot be retrofitted after transaction data has been entered. This is the highest-risk design phase despite being the lowest in external API complexity.

**Delivers:** `PortfolioAsset` and `FixedDeposit` aggregate roots; `AssetTransaction` child entity; Vogen typed IDs (`PortfolioAssetId`, `AssetTransactionId`, `FixedDepositId`); `AssetType` and `TransactionType` enums; `CompoundingFrequency` enum; `VndAmount` value object; `TradingBotDbContext` extensions with `ConfigureConventions()` entries; EF Core migration; round-trip EF tests for all new entities

**Addresses:** Foundation for all P1 features (per-asset holdings, fixed deposit entry, transaction history)

**Avoids:** Pitfall #1 (dual-currency cost basis fields from day one), Pitfall #4 (CompoundingFrequency field required), Pitfall #5 (Vogen convention registration tested), Pitfall #6 (VndAmount and integer ShareQuantity from day one)

**Research flag:** Standard patterns — EF Core separate table aggregates and Vogen typed IDs are identical to existing `Purchase`, `DcaConfiguration` patterns in the codebase. Skip `/gsd:research-phase`.

### Phase 2: Price Feed Infrastructure

**Rationale:** All read features (portfolio overview, P&L display, currency toggle) depend on price providers. Building this as a standalone layer before read endpoints keeps concerns clean and allows the calculation service to be tested against mocked provider interfaces.

**Delivers:** `ICryptoPriceProvider`, `IVnStockPriceProvider`, `IExchangeRateProvider` interfaces; `CoinGeckoPriceProvider` (extends existing `CoinGeckoClient`); `VnStockClient` (new VNDirect finfo named HttpClient with resilience handler); `ExchangeRateClient` (open.er-api.com typed HttpClient); `PriceCacheService` (Redis TTL wrapper); `PriceProviderFactory` with parallel `Task.WhenAll`

**Addresses:** Auto-fetch crypto prices, VND/USD exchange rate, VN30 ETF price fetch (best-effort with graceful degradation)

**Avoids:** Pitfall #3 (VN API fragility — cache-first, staleness metadata, graceful null return on HTTP failure all designed into provider interface contract from day one)

**Research flag:** NEEDS `/gsd:research-phase` for VNDirect finfo API exact JSON response schema. The endpoint timed out during research; field names for close price are inferred from the vnstock Python library source, not confirmed by live request. All other providers are HIGH confidence and need no additional research.

### Phase 3: Portfolio Read API + DCA Auto-Import

**Rationale:** Once the domain model and price providers exist, the calculation service and read endpoints can be built. Auto-import belongs in this phase because it shares the BTC `PortfolioAsset` entity that the read endpoints expose, and historical purchases must be imported before any P&L display is meaningful for BTC.

**Delivers:** `PortfolioCalculationService`; `GET /api/portfolio/summary`, `GET /api/portfolio/assets`, `GET /api/portfolio/assets/{id}`, `GET /api/portfolio/fixed-deposits` endpoints; `PurchaseCompletedPortfolioHandler` (idempotent, skips dry-runs); one-time historical purchase migration importing existing `Purchases` table into `AssetTransactions` using `PurchaseId` as `ExternalReference`

**Addresses:** Total portfolio value (VND/USD toggle), per-asset P&L, allocation %, auto-import DCA bot purchases (all P1 features visible in the UI)

**Avoids:** Pitfall #2 (auto-import idempotency via `source_reference_id` unique constraint designed before first import run)

**Research flag:** Standard patterns — MediatR event handler and minimal API endpoint patterns match existing `DashboardEndpoints` exactly. Skip `/gsd:research-phase`.

### Phase 4: Manual Transaction Entry + Fixed Deposit API

**Rationale:** Manual entry endpoints build on the stable read API. Fixed deposit CRUD is the simplest phase — no price dependencies, pure calculation. This phase completes all P1 backend work and enables full Flutter portfolio feature development.

**Delivers:** `POST /api/portfolio/assets` (create asset), `POST /api/portfolio/assets/{id}/transactions` (manual Buy/Sell), `DELETE /api/portfolio/assets/{id}/transactions/{txId}` (manual only — rejects auto-imported transactions), `POST /api/portfolio/fixed-deposits`, `GET/PUT/DELETE /api/portfolio/fixed-deposits/{id}`; backend validation rejecting fractional ETF share quantities; backend validation rejecting future transaction dates

**Addresses:** Manual transaction entry (crypto/ETF Buy/Sell), fixed deposit entry with accrued value display

**Avoids:** Pitfall #4 (FD compounding formula validated here against both cumulative and non-cumulative scenarios)

**Research flag:** Standard patterns — CRUD endpoints with `ErrorOr` validation follow existing codebase patterns exactly. Skip `/gsd:research-phase`.

### Phase 5: Flutter Portfolio Feature

**Rationale:** Flutter UI is built last — after all backend endpoints are stable — to prevent repeated UI rework from API shape changes. This phase delivers the user-visible milestone.

**Delivers:** `features/portfolio/` module (portfolio screen, asset cards with P&L color coding, allocation pie chart, add transaction bottom sheet, fixed deposit display with maturity countdown); `currency_provider.dart` global Riverpod `StateProvider<Currency>` in `core/providers/`; navigation shell update (Portfolio tab); VND number formatting with thousands separator via `intl`; "price as of [date]" staleness indicator for VN assets; "converted at today's rate" label for cross-currency display

**Addresses:** Total portfolio value with VND/USD toggle, per-asset holdings view, allocation pie chart, transaction history list, fixed deposit display — all P1 features now visible to the user

**Avoids:** Currency toggle as a re-fetch trigger (API always returns both `valueUsd` and `valueVnd`; toggle is pure display logic); VND amounts without thousands separator (UX pitfall)

**Research flag:** Standard patterns — feature folder structure and Riverpod `StateProvider` are identical to existing `features/home/`, `features/chart/` conventions. Skip `/gsd:research-phase`.

### Phase 6: Portfolio Chart (v4.x — Deferred)

**Rationale:** The portfolio value chart is the highest-complexity P2 feature. It requires both the caching strategy (designed upfront to avoid Pitfall #7) and sufficient transaction history to make the chart meaningful. Deferred until P1 features are validated in production.

**Delivers:** `GET /api/portfolio/chart` with on-demand computation and Redis cache (25-hour TTL); cache invalidation triggered when a transaction with a date >= cached chart start is saved; Flutter chart screen using `fl_chart` `LineChart`; "updated X minutes ago" staleness indicator

**Addresses:** Portfolio value chart over time, per-asset performance chart

**Avoids:** Pitfall #7 (chart performance — compute-on-demand with cache-first and transaction-triggered invalidation designed before endpoint is built)

**Research flag:** NEEDS `/gsd:research-phase` for backdated transaction cache invalidation strategy. The edge case of a user entering a past transaction invalidating all chart snapshots from that date forward requires careful design. Standard chart rendering pattern otherwise.

### Phase Ordering Rationale

- **Data model first** — 4 of 7 critical pitfalls must be resolved at schema design time; retroactively adding `exchange_rate_at_transaction`, `CompoundingFrequency`, or `VndAmount` after data has been entered requires user re-entry of financial history
- **Price providers before calculation service** — the `PortfolioCalculationService` depends on provider interfaces; those interfaces must be stable before the service can be built against mocked implementations
- **Read endpoints before write endpoints** — the portfolio overview validates domain model correctness before mutation surface is exposed to users
- **DCA auto-import alongside read API** — both share the BTC `PortfolioAsset` entity; historical import must complete before BTC P&L can be displayed correctly
- **Flutter last** — backend API shape must be stable to avoid repeated UI rework; Flutter has zero dependency risk because it consumes a completed API
- **Portfolio chart deferred** — complexity is HIGH, user value is P2; ship and validate all P1 features before investing in chart complexity

### Research Flags

**Needs `/gsd:research-phase` during planning:**
- **Phase 2 (Price Feed):** VNDirect finfo API exact JSON response schema requires live request verification during implementation. Endpoint timed out during research; close price field names inferred from vnstock Python source.
- **Phase 6 (Portfolio Chart):** Backdated transaction cache invalidation strategy needs careful design to ensure correctness without full recomputation on every chart load.

**Standard patterns (skip `/gsd:research-phase`):**
- **Phase 1 (Domain Model):** EF Core aggregate patterns, Vogen typed IDs — identical conventions to existing `Purchase` and `DcaConfiguration` entities
- **Phase 3 (Read API + Auto-Import):** MediatR `INotificationHandler`, minimal API endpoint groups — exact same pattern as existing `DashboardEndpoints`
- **Phase 4 (Manual Entry + FD API):** CRUD endpoints with `ErrorOr` validation — identical pattern to existing endpoints
- **Phase 5 (Flutter):** Riverpod feature module — identical folder structure to `features/home/`, `features/chart/`

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | No new packages required. CoinGecko and open.er-api.com verified against official docs and live API calls. VNDirect finfo API is MEDIUM due to unofficial, undocumented status. |
| Features | HIGH | P1 feature set well-defined from competitor analysis (Delta, Kubera, CoinGecko Portfolio) and existing codebase API surface. VN ETF auto-fetch flagged MEDIUM — API stability unconfirmed. |
| Architecture | HIGH | All patterns match existing codebase conventions exactly. Code samples in research are valid C# against confirmed project structure. Build order reflects hard dependency analysis. |
| Pitfalls | HIGH | Critical pitfalls drawn from direct codebase inspection (`TradingBotDbContext.cs`, `Purchase.cs`, `UsdAmount.cs`) plus financial domain literature. Phase-to-pitfall mapping is explicit and actionable. |

**Overall confidence:** HIGH for architecture and implementation approach; MEDIUM for VN stock price data reliability (single undocumented API source, no SLA)

### Gaps to Address

- **VNDirect finfo API response schema** — JSON field names for VN ETF close price unconfirmed (endpoint timed out during research). Must verify with a live request at Phase 2 implementation start. Design `IVnStockPriceProvider` to return `null` (not throw) on failure; UI shows "price unavailable" for affected assets. ETF symbol mapping (`E1VFVN30`, `FUESSV30`) confirmed correct from Yahoo Finance cross-reference.

- **VN market hours guard** — VN stock price fetcher should only call VNDirect during HOSE trading hours (09:00–11:30 and 13:00–14:45 ICT, Mon–Fri). Timezone: `Asia/Ho_Chi_Minh` (UTC+7, no DST). Outside these hours, return cached last close price. Implement as a schedule guard in `VnStockClient`. Decision for Phase 2 planning.

- **CoinGecko ID mapping UX** — When a user creates a new crypto asset (non-BTC), they must supply the CoinGecko ID (e.g., `"ethereum"`). Either provide a search endpoint calling CoinGecko `/search`, or document the ID field requirement clearly in the Flutter add-asset form. Decision for Phase 5 planning.

- **BTC quantity reconciliation tolerance** — Portfolio BTC quantity (sum of auto-imported DCA transactions) may differ slightly from Hyperliquid spot balance due to exchange fees and rounding. Define an acceptable tolerance (e.g., 0.00000001 BTC) and surface any discrepancy as a UI info indicator, not an error. Decision for Phase 5 planning.

- **Portfolio snapshot vs. on-demand chart** — Research recommends on-demand computation with Redis cache and transaction-triggered invalidation. This approach handles backdated transaction entry naturally. Confirm the invalidation logic handles the edge case where the user enters a transaction dated months ago (should invalidate the entire chart range from that date forward). Must be confirmed correct before Phase 6 implementation begins.

## Sources

### Primary (HIGH confidence)
- CoinGecko `/simple/price` official docs — multi-coin, VND as supported `vs_currency`, 30 calls/min free tier
- open.er-api.com v6 live response verified 2026-02-20 — VND: 25,905.86 confirmed, no API key required
- EF Core inheritance/table-per-type documentation — separate aggregate table strategy confirmed
- Vogen GitHub (SteveDunn/Vogen) — EF Core value converter pattern; existing codebase usage verified
- Flutter `intl` `NumberFormat.currency` documentation — `vi_VN` locale, `decimalDigits: 0` for VND confirmed
- ExchangeRate-API free tier documentation — no key required; ~1500 req/month; VND in supported currencies
- Existing codebase inspection (`TradingBotDbContext.cs`, `Purchase.cs`, `AggregateRoot.cs`, `DashboardEndpoints.cs`, `PriceDataService.cs`) — HIGH confidence; direct code analysis
- DDD aggregate design (Fowler, Ardalis.Specification patterns) — matches existing codebase conventions exactly

### Secondary (MEDIUM confidence)
- vnstock GitHub (thinh-vu/vnstock) — VNDirect finfo API endpoint pattern confirmed by open-source community; unofficial
- VNDirect finfo-api.vndirect.com.vn v4 — unofficial API; no documentation; endpoint pattern inferred from Python library
- Delta, Kubera, CoinGecko Portfolio, Sharesight feature analysis — P1/P2 feature set and multi-currency display patterns
- Riverpod 3.0 (riverpod.dev) — `StateProvider<Currency>` for global currency toggle is idiomatic; confirmed pattern
- Bryt Software interest accrual methods — compound vs. simple interest formula differences for Vietnamese FD types
- CoinTracking realized vs. unrealized gains methodology — multi-currency P&L correctness approach

### Tertiary (LOW confidence)
- Yahoo Finance E1VFVN30.VN, FUESSV30.VN — ETF ticker symbol format confirmed; unofficial JSON endpoint not selected as primary data source due to scraping risk
- vnstock data disclaimer — explicit "data may be incomplete/inaccurate" warning from official vnstock documentation; confirms fragility assessment

---
*Research completed: 2026-02-20*
*Ready for roadmap: yes*

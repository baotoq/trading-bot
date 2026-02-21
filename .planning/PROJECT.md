# BTC Smart DCA Bot

## What This Is

A personal investment platform: automated BTC DCA on Hyperliquid spot with smart dip-buying multipliers, backtesting engine, and a Flutter iOS app that serves as a unified portfolio tracker across crypto, Vietnamese ETFs, and fixed deposits -- with live prices from CoinGecko and VNDirect, multi-currency P&L (VND/USD), and 103 automated tests.

## Core Value

The bot reliably executes daily BTC spot purchases with smart dip-buying, and the app gives a single view of all investments (crypto, ETF, savings) with real P&L so the user never needs to check multiple platforms.

## Requirements

### Validated

- ✓ Event-driven architecture with MediatR domain events -- v1.0
- ✓ Outbox pattern for reliable message publishing -- v1.0
- ✓ Background service base class (TimeBackgroundService) -- v1.0
- ✓ Serilog structured logging -- v1.0
- ✓ PostgreSQL + EF Core persistence -- v1.0
- ✓ Redis distributed caching -- v1.0
- ✓ Aspire orchestration for local dev -- v1.0
- ✓ Telegram notification infrastructure -- v1.0
- ✓ Hyperliquid spot API integration (EIP-712 signing, prices, balances, orders) -- v1.0
- ✓ Smart DCA engine with configurable base amount and dip multipliers -- v1.0
- ✓ Drop-from-high calculation (30-day high tracking, tier-based multipliers: 1x/1.5x/2x/3x) -- v1.0
- ✓ 200-day MA bear market boost (additive +1.5x below 200 MA) -- v1.0 (updated to additive in v1.1)
- ✓ Configurable daily schedule (time of day for buy execution) -- v1.0
- ✓ Purchase history tracking (amount, price, multiplier used, timestamp) -- v1.0
- ✓ Telegram notifications on each buy (amount, price, multiplier, running totals) -- v1.0
- ✓ Rich Telegram notifications with multiplier reasoning and weekly summaries -- v1.0
- ✓ Health check endpoint and missed purchase detection -- v1.0
- ✓ Dry-run simulation mode -- v1.0
- ✓ Distributed locking via PostgreSQL advisory locks -- v1.0
- ✓ MultiplierCalculator as pure static class (zero dependencies, testable, reusable) -- v1.1
- ✓ Day-by-day backtest simulation (smart DCA vs same-base and match-total fixed DCA) -- v1.1
- ✓ Comprehensive backtest metrics (cost basis, total BTC, max drawdown, tier breakdown, efficiency) -- v1.1
- ✓ CoinGecko historical data pipeline with incremental ingestion and gap detection -- v1.1
- ✓ Parameter sweep with cartesian product, parallel execution, and ranked results -- v1.1
- ✓ Walk-forward validation with 70/30 train/test split and overfitting detection -- v1.1
- ✓ Backtest API endpoints (single backtest, sweep, data ingestion, data status) -- v1.1
- ✓ Nuxt 4 dashboard with Aspire orchestration, API key auth, server-to-server proxy -- v1.2
- ✓ Portfolio overview with live BTC price, stats cards, interactive price chart -- v1.2
- ✓ Purchase history with infinite scroll, cursor pagination, date/tier filtering -- v1.2
- ✓ Backtest visualization with equity curves, parameter sweep, side-by-side comparison -- v1.2
- ✓ DCA configuration management with server-validated forms and cache invalidation -- v1.2
- ✓ Live bot status with health badge, countdown timer, connection indicators -- v1.2

- ✓ Strongly-typed IDs (PurchaseId, IngestionJobId, DcaConfigurationId) via Vogen source generation -- v2.0
- ✓ Value objects for domain primitives (Price, Quantity, Multiplier, UsdAmount, Percentage, Symbol) with built-in validation -- v2.0
- ✓ Rich aggregate roots with factory methods, private setters, behavior methods, and invariant enforcement -- v2.0
- ✓ Domain event dispatch from aggregate roots via SaveChangesInterceptor -- v2.0
- ✓ Domain-to-integration event bridge (domain events automatically produce outbox messages via Dapr) -- v2.0
- ✓ Result pattern with ErrorOr for explicit error handling from domain through endpoint -- v2.0
- ✓ Specification pattern with 7 composable specs (Ardalis.Specification) for reusable query composition -- v2.0
- ✓ AggregateRoot<TId> base class with domain event collection and IAggregateRoot marker -- v2.0

- ✓ Flutter iOS app with full dashboard feature parity (portfolio, charts, history, backtest, config, status) -- v3.0
- ✓ FCM push notifications for purchase executed, failed, and missed events with deep-link navigation -- v3.0
- ✓ Backend device token management with auto-cleanup of stale FCM tokens -- v3.0
- ✓ Nuxt dashboard deprecated from Aspire orchestration (code preserved) -- v3.0
- ✓ Per-channel notification handlers (Telegram + FCM split) -- v3.0

- ✓ Multi-asset portfolio model (crypto, ETF, fixed deposit) with transaction history -- v4.0
- ✓ Auto-fetch crypto prices (CoinGecko), VN ETF prices (VNDirect), USD/VND exchange rate -- v4.0
- ✓ Manual transaction entry (buy/sell for tradeable assets, deposit for savings) -- v4.0
- ✓ Auto-import DCA bot purchases into portfolio + historical migration -- v4.0
- ✓ Portfolio overview: total value (VND/USD toggle), per-asset P&L %, allocation donut chart -- v4.0
- ✓ Fixed deposit tracking with interest rate, maturity date, accrued value, and compound interest -- v4.0
- ✓ USD/VND currency conversion for unified portfolio view with staleness indicators -- v4.0
- ✓ Price feed unit tests, endpoint integration tests (Testcontainers), exchange rate graceful degradation -- v4.0

### Active

## Current Milestone: v5.0 Stunning Mobile UI

**Goal:** Transform the Flutter app from generic Material 3 into a premium glassmorphism design with rich animations, gradient glow charts, and polished data visualization across all 5 tabs.

**Target features:**
- Premium glassmorphism design system (frosted glass cards, blur layers, depth)
- Dashboard overview home screen (hero balance, mini allocation chart, recent activity, quick actions)
- Gradient glow line charts with animated draw-in and premium tooltips
- Essential animations and micro-interactions (page transitions, animated counters, shimmer loading, parallax)
- Typography hierarchy overhaul (system fonts, proper sizing, weights, spacing)
- Full visual redesign of all 5 tabs (Home, Chart, History, Config, Portfolio)

### Out of Scope

- Selling/take-profit logic -- this is accumulation only
- Futures/perps trading -- spot only
- Monthly spending caps -- daily amount + multipliers are the only controls
- Monte Carlo simulation -- DCA is deterministic given price data
- Slippage / fee modeling -- for small spot orders ($10-45/day), fees are <0.1%
- Genetic algorithm / ML optimization -- grid search is sufficient for ~5 DCA parameters
- Backtest result persistence -- compute-on-fly is cheaper; user re-runs as needed
- Multi-user authentication -- single-user bot, API key auth is sufficient
- Real-time order book -- not relevant for DCA spot purchases
- Manual buy/sell buttons -- bot is fully automated
- Generic Repository<T> -- EF Core DbContext is already unit of work
- Event Sourcing -- massive complexity for DCA bot state-based persistence
- Separate domain/persistence models -- over-engineering for this domain size
- Cross-aggregate transactions -- use domain events for eventual consistency
- Real-time price streaming (WebSocket) -- DCA bot buys once daily; 5-min polling sufficient
- Broker/exchange API auto-sync -- Vietnamese brokers require in-person auth registration
- Tax reporting / capital gains -- show P&L clearly for user to provide to tax advisor
- Portfolio rebalancing suggestions -- financial advice requires regulatory licensing
- FIFO/LIFO cost basis -- weighted average sufficient for DCA accumulation style
- Historical VND/USD rate at transaction date -- display at today's rate with label
- Multi-user / family portfolio -- single-user system by design
- Portfolio value chart over time -- deferred to v4.x (CHART-01)
- Per-asset performance chart -- deferred to v4.x (CHART-02)

## Current State

Shipped v4.0 Portfolio Tracker (2026-02-21). Starting v5.0 Stunning Mobile UI.

**Codebase:**
- ~12,000+ lines of C# (backend, 32 phases, 71 plans)
- ~5,500+ lines of Dart (TradingBot.Mobile -- portfolio tracker + DCA dashboard)
- ~4,100 lines of TypeScript/Vue/CSS (TradingBot.Dashboard -- deprecated)
- 103 automated tests (24 MultiplierCalculator, 28 BacktestSimulator, 14 InterestCalculator, 14 PriceFeed, 13 Endpoint integration, 9 Specification, 1 existing)
- Tech stack: .NET 10.0, ASP.NET Core, EF Core, PostgreSQL, Redis, Aspire, MediatR, Serilog, Telegram.Bot, Firebase FCM
- DDD: Vogen 8.0.4, ErrorOr 2.0.1, Ardalis.Specification 9.3.1
- Flutter: Dart 3.11, Riverpod, go_router, fl_chart, Dio, shared_preferences

**API Surface:**
- POST /api/backtest -- single backtest with config overrides
- POST /api/backtest/sweep -- parameter sweep with ranking and walk-forward validation
- POST /api/backtest/data/ingest -- trigger CoinGecko historical data ingestion
- GET /api/backtest/data/status -- check data coverage
- GET /api/backtest/data/ingest/{jobId} -- poll ingestion job progress
- GET /api/backtest/presets/{name} -- inspect sweep presets
- GET /api/dashboard/portfolio -- portfolio stats with live price
- GET /api/dashboard/purchases -- paginated purchase history
- GET /api/dashboard/status -- bot health and next buy time
- GET /api/dashboard/chart -- price chart with purchase markers
- GET /api/dashboard/config -- DCA config for backtest pre-fill
- GET /api/config -- full DCA configuration
- PUT /api/config -- update DCA configuration with validation
- GET /api/config/defaults -- appsettings.json default values
- GET /api/portfolio/summary -- multi-asset portfolio with P&L (USD + VND)
- POST /api/portfolio/assets -- create portfolio asset
- GET /api/portfolio/assets/{id}/transactions -- transaction history with filters
- POST /api/portfolio/assets/{id}/transactions -- add buy/sell transaction
- GET /api/portfolio/fixed-deposits -- list fixed deposits with accrued value
- POST /api/portfolio/fixed-deposits -- create fixed deposit
- PUT /api/portfolio/fixed-deposits/{id} -- update fixed deposit
- DELETE /api/portfolio/fixed-deposits/{id} -- delete fixed deposit

## Constraints

- **Tech Stack**: .NET 10.0 backend + Flutter iOS frontend
- **Exchange**: Hyperliquid spot market only -- no other exchanges (for DCA bot)
- **DCA Direction**: Buy only -- no sell logic (DCA bot remains accumulation only)
- **Portfolio Data**: Manual transaction entry for non-crypto assets, auto-fetch prices where free APIs exist
- **Notifications**: Telegram + FCM -- both required for every purchase
- **Single User**: No multi-user auth -- personal portfolio tracker

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Hyperliquid spot (not perps) | User wants actual BTC accumulation, not leveraged exposure | ✓ Good |
| Smart DCA with drop-from-high tiers | Better average cost than fixed DCA, simple to implement and understand | ✓ Good |
| 200-day MA as bear market indicator | Well-known, reliable signal for macro trend | ✓ Good |
| Configurable schedule + amounts | User wants flexibility to adjust strategy without code changes | ✓ Good |
| PostgreSQL advisory locks (not Dapr) | Dapr lock was stubbed; PostgreSQL advisory locks are real and reliable | ✓ Good |
| IOC orders with 5% slippage | Immediate fill with price protection for spot market buys | ✓ Good |
| Additive bear boost (not multiplicative) | Makes bear market impact more predictable (+1.5 not *1.5) | ✓ Good |
| Stale data policy: use last known | Better than falling back to 1x on transient refresh failures | ✓ Good |
| Graceful degradation to 1.0x | Multiplier failure never prevents DCA purchase | ✓ Good |
| Pure static MultiplierCalculator | Zero dependencies enables reuse in backtest without DI coupling | ✓ Good |
| Direct HttpClient for CoinGecko | Better control than CoinGecko.Net library, matches existing patterns | ✓ Good |
| Bounded Channel queue for ingestion | Single-job enforcement with DropWrite, simple and explicit | ✓ Good |
| BulkExtensions for data import | Efficient bulk upsert with composite key handling | ✓ Good |
| Walk-forward 70/30 split | Industry standard for overfitting detection in parameter optimization | ✓ Good |
| Safety cap on sweep combinations | Prevents runaway computation (default 1000, max 10000) | ✓ Good |
| Nuxt 4 for dashboard (not Blazor/Razor) | User preference, modern Vue ecosystem, SSR + SPA flexibility | ✓ Good |
| Server-to-server auth (Nuxt → .NET) | API key stays on server, browser doesn't need credentials | ✓ Good |
| /proxy/api/** prefix for CORS | Avoids Nuxt /api/** routing conflict, clean proxy pattern | ✓ Good |
| Cursor-based pagination | Avoids drift from offset pagination, efficient for large datasets | ✓ Good |
| vue-chartjs (not raw Chart.js) | Proper Vue lifecycle integration, automatic cleanup on unmount | ✓ Good |
| Session storage for comparison | Survives refresh but not tab close, avoids localStorage quota issues | ✓ Good |
| Singleton DcaConfiguration entity | Single-row constraint via CHECK, JSONB for tiers flexibility | ✓ Good |
| IOptionsMonitor cache invalidation | Immediate config effect after DB update without app restart | ✓ Good |
| Vogen 8.0.4 for typed IDs + value objects | Source-generated, zero runtime overhead, EF Core + STJ converters included | ✓ Good |
| ErrorOr 2.0.1 for Result pattern | Zero allocation, .NET 10 optimized, clean propagation from domain to endpoint | ✓ Good |
| Ardalis.Specification for query encapsulation | Composable specs, server-side SQL evaluation, EF Core integration | ✓ Good |
| Domain events dispatch AFTER SaveChanges | Via interceptor, not before -- prevents event dispatch on failed persistence | ✓ Good |
| Single outbox pipeline for all events | Domain + integration events use same Dapr outbox path, removed dual MediatR/Dapr dispatch | ✓ Good |
| Dead-letter table for failed messages | Automatic move after 3 retries, prevents poison message blocking | ✓ Good |
| AggregateRoot<TId> with IAggregateRoot marker | Interceptor queries ChangeTracker for IAggregateRoot, clean separation | ✓ Good |
| Factory methods throw, behavior methods return ErrorOr | Create() path is startup/infrastructure, behavior is runtime with user input | ✓ Good |
| Nullable Price? in dashboard DTOs | Handles empty DB and unreachable exchange gracefully, no 500 errors | ✓ Good |
| Separate aggregate roots for PortfolioAsset and FixedDeposit | Avoids TPH nullable columns, each aggregate has distinct lifecycle | ✓ Good |
| VndAmount value object with numeric(18,0) | Vietnamese currency has no decimals; prevents fractional VND | ✓ Good |
| Store cost_native + cost_usd + exchange_rate at write time | P&L computed in native currency; avoids historical exchange rate dependency | ✓ Good |
| DCA domain has zero knowledge of portfolio domain | Connected via PurchaseCompletedEvent only; clean bounded context boundary | ✓ Good |
| API returns both valueUsd and valueVnd | Currency toggle is pure Flutter display logic; no extra API calls per toggle | ✓ Good |
| VNDirect dchart-api (not finfo-api) | finfo times out externally; dchart verified live and reliable | ✓ Good |
| PriceFeedEntry uses long for timestamps | Avoids MessagePack DateTimeOffset resolver issues | ✓ Good |
| Stale-while-revalidate for VNDirect | Returns stale immediately, fire-and-forget refresh; ETF prices change rarely | ✓ Good |
| Weighted average cost from buys only | Sells reduce position but don't change avg cost basis; standard DCA P&L | ✓ Good |
| WebApplicationFactory + Testcontainers for integration tests | Real PostgreSQL, in-memory cache, no background services; fast and reliable | ✓ Good |
| Dynamic CoinGecko ID lookup with not-found caching | Well-known dict for 10 common tickers; API search fallback with 1-day negative cache | ✓ Good |

---
*Last updated: 2026-02-21 after v5.0 milestone started*

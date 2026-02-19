# BTC Smart DCA Bot

## What This Is

A recurring buy bot that automatically accumulates BTC on Hyperliquid spot market using a smart DCA strategy with multipliers based on price dips and bear market conditions. Includes a backtesting engine for strategy validation, and a Flutter mobile + web app for portfolio monitoring, backtest visualization, configuration management, and push notifications.

## Core Value

The bot reliably executes daily BTC spot purchases on Hyperliquid with smart dip-buying, so the user accumulates BTC at a better average cost than fixed DCA. The backtesting engine validates this advantage empirically. The mobile app provides real-time visibility, control, and push notifications wherever you are.

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

### Active

<!-- v3.0 Flutter Mobile -->
- [ ] Flutter mobile app (iOS + Web) with full dashboard feature parity
- [ ] Push notifications for buy executions and alerts
- [ ] Deprecate Nuxt dashboard (keep code, remove from Aspire orchestration)

### Out of Scope

- Selling/take-profit logic -- this is accumulation only
- Futures/perps trading -- spot only
- Multi-asset support -- BTC only for now
- Monthly spending caps -- daily amount + multipliers are the only controls
- Monte Carlo simulation -- DCA is deterministic given price data
- Slippage / fee modeling -- for small spot orders ($10-45/day), fees are <0.1%
- Genetic algorithm / ML optimization -- grid search is sufficient for ~5 DCA parameters
- Backtest result persistence -- compute-on-fly is cheaper; user re-runs as needed
- Multi-user authentication -- single-user bot, API key auth is sufficient
- ~~Mobile app~~ -- now building Flutter mobile app (v3.0)
- Real-time order book -- not relevant for DCA spot purchases
- Manual buy/sell buttons -- bot is fully automated
- Generic Repository<T> -- EF Core DbContext is already unit of work
- Event Sourcing -- massive complexity for DCA bot state-based persistence
- Separate domain/persistence models -- over-engineering for this domain size
- Cross-aggregate transactions -- use domain events for eventual consistency

## Current Milestone: v3.0 Flutter Mobile

**Goal:** Replace Nuxt web dashboard with Flutter mobile + web app, add push notifications

**Target features:**
- Flutter app (iOS + Web) with full dashboard parity (portfolio, charts, history, backtest, config, status)
- Push notifications for buy executions and alerts
- Deprecate Nuxt dashboard from Aspire orchestration

## Current State

Shipped v2.0 DDD Foundation (2026-02-20). Starting v3.0 Flutter Mobile.

**Codebase:**
- ~10,000+ lines of C# (backend, 19 phases, 45 plans)
- ~4,100 lines of TypeScript/Vue/CSS (TradingBot.Dashboard — deprecating)
- 62 automated tests (24 MultiplierCalculator, 28 BacktestSimulator, 9 Specification integration, 1 existing)
- Tech stack: .NET 10.0, ASP.NET Core, EF Core, PostgreSQL, Redis, Aspire, MediatR, Serilog, Telegram.Bot
- DDD additions: Vogen 8.0.4, ErrorOr 2.0.1, Ardalis.Specification 9.3.1
- Flutter: Dart 3.11, TradingBot.Mobile/ (fresh init)

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
- PUT /api/config -- update DCA configuration with validation (ErrorOr result mapping)
- GET /api/config/defaults -- appsettings.json default values

**Dashboard Features:**
- Portfolio overview (total BTC, cost, P&L, live price)
- Interactive price chart (6 timeframes, purchase markers, avg cost line)
- Purchase history (infinite scroll, cursor pagination, date/tier filters)
- Backtest visualization (equity curves, metrics, parameter sweep, comparison)
- Configuration management (view/edit, tiers table, server validation)
- Live status (health badge, countdown timer, connection indicator)

## Constraints

- **Tech Stack**: .NET 10.0 backend + Flutter (iOS + Web) frontend -- migrating from Nuxt 4
- **Exchange**: Hyperliquid spot market only -- no other exchanges
- **Asset**: BTC only -- single trading pair
- **Direction**: Buy only -- no sell logic
- **Notifications**: Telegram + Serilog -- both required for every purchase

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

---
*Last updated: 2026-02-20 after v3.0 milestone start*

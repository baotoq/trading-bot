# BTC Smart DCA Bot

## What This Is

A recurring buy bot that automatically accumulates BTC on Hyperliquid spot market using a smart DCA strategy with multipliers based on price dips and bear market conditions. Includes a backtesting engine for simulating strategies against historical price data, parameter sweeps for finding optimal configurations, and walk-forward validation to prevent overfitting.

## Core Value

The bot reliably executes daily BTC spot purchases on Hyperliquid with smart dip-buying, so the user accumulates BTC at a better average cost than fixed DCA. The backtesting engine validates this advantage empirically.

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

### Active

#### Current Milestone: v1.2 Web Dashboard

**Goal:** Visual dashboard for monitoring portfolio, viewing purchase history, running backtests, managing configuration, and tracking live bot status.

**Target features:**
- Portfolio overview (total BTC, cost basis, current value, P&L)
- Purchase history timeline with prices, multipliers, amounts
- Backtest visualization with charts and parameter comparisons
- Config management (edit DCA amount, schedule, multiplier tiers from UI)
- Live status (current BTC price, next buy time, bot health)

### Out of Scope

- Selling/take-profit logic -- this is accumulation only
- Futures/perps trading -- spot only
- Multi-asset support -- BTC only for now
- ~~Web dashboard~~ -- moved to Active for v1.2
- Monthly spending caps -- daily amount + multipliers are the only controls
- Monte Carlo simulation -- DCA is deterministic given price data
- Slippage / fee modeling -- for small spot orders ($10-45/day), fees are <0.1%
- Genetic algorithm / ML optimization -- grid search is sufficient for ~5 DCA parameters
- Backtest result persistence -- compute-on-fly is cheaper; user re-runs as needed

## Current State

Shipped v1.1 Backtesting Engine (2026-02-13).

**Codebase:**
- ~7,100 lines of C# across 8 phases (18 plans)
- 53 automated tests (24 MultiplierCalculator, 28 BacktestSimulator, 1 existing)
- Tech stack: .NET 10.0, ASP.NET Core, EF Core, PostgreSQL, Redis, Aspire, MediatR, Serilog, Telegram.Bot

**API Surface:**
- POST /api/backtest -- single backtest with config overrides
- POST /api/backtest/sweep -- parameter sweep with ranking and walk-forward validation
- POST /api/backtest/data/ingest -- trigger CoinGecko historical data ingestion
- GET /api/backtest/data/status -- check data coverage
- GET /api/backtest/data/ingest/{jobId} -- poll ingestion job progress
- GET /api/backtest/presets/{name} -- inspect sweep presets

## Constraints

- **Tech Stack**: .NET 10.0 backend + Nuxt frontend -- must build on current foundation
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
| Nuxt for dashboard (not Blazor/Razor) | User preference, modern Vue ecosystem, SSR + SPA flexibility | — Pending |

---
*Last updated: 2026-02-13 after v1.2 milestone start*

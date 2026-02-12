# BTC Smart DCA Bot

## What This Is

A recurring buy bot that automatically accumulates BTC on Hyperliquid spot market using a smart DCA strategy. It buys a configurable base amount daily, with multipliers that increase position size during dips — buying more when price drops further from recent highs, and even more aggressively when price is below the 200-day moving average. Rich Telegram notifications explain every buy decision with multiplier reasoning and running totals.

## Core Value

The bot reliably executes daily BTC spot purchases on Hyperliquid with smart dip-buying, so the user accumulates BTC at a better average cost than fixed DCA.

## Requirements

### Validated

- ✓ Event-driven architecture with MediatR domain events — existing
- ✓ Outbox pattern for reliable message publishing — existing
- ✓ Background service base class (TimeBackgroundService) — existing
- ✓ Serilog structured logging — existing
- ✓ PostgreSQL + EF Core persistence — existing
- ✓ Redis distributed caching — existing
- ✓ Aspire orchestration for local dev — existing
- ✓ Telegram notification infrastructure — existing
- ✓ Hyperliquid spot API integration (EIP-712 signing, prices, balances, orders) — v1.0
- ✓ Smart DCA engine with configurable base amount and dip multipliers — v1.0
- ✓ Drop-from-high calculation (30-day high tracking, tier-based multipliers: 1x/1.5x/2x/3x) — v1.0
- ✓ 200-day MA bear market boost (1.5x additional multiplier below 200 MA) — v1.0
- ✓ Configurable daily schedule (time of day for buy execution) — v1.0
- ✓ Purchase history tracking (amount, price, multiplier used, timestamp) — v1.0
- ✓ Telegram notifications on each buy (amount, price, multiplier, running totals) — v1.0
- ✓ Detailed console/file logging of all decisions and executions — v1.0
- ✓ Configuration management (base amount, schedule, multiplier tiers, all adjustable) — v1.0
- ✓ Rich Telegram notifications with multiplier reasoning and weekly summaries — v1.0
- ✓ Health check endpoint and missed purchase detection — v1.0
- ✓ Dry-run simulation mode — v1.0
- ✓ Distributed locking via PostgreSQL advisory locks — v1.0 (replaced Dapr stub)

### Active

(No active requirements — next milestone will define new goals)

### Out of Scope

- Selling/take-profit logic — this is accumulation only
- Futures/perps trading — spot only
- Multi-asset support — BTC only for now
- Web dashboard — logs and Telegram are sufficient
- Backtesting — build the bot first, backtest later if needed
- Monthly spending caps — daily amount + multipliers are the only controls

## Context

- .NET 10.0 codebase with solid infrastructure (MediatR, EF Core, outbox pattern, Aspire)
- v1.0 shipped: 3,590 lines of C# across 4 phases, 11 plans
- BuildingBlocks layer provides base entities, domain events, pub/sub, distributed locks, background services
- Hyperliquid integration uses direct HTTP client with EIP-712 signing via Nethereum
- PostgreSQL for persistence (Purchase + DailyPrice entities), auto-migrations on startup
- 4 background services: DCA scheduler, price data refresh, weekly summary, missed purchase verification
- Telegram notifications for all purchase outcomes with rich formatting

## Constraints

- **Tech Stack**: .NET 10.0, existing BuildingBlocks infrastructure — must build on current foundation
- **Exchange**: Hyperliquid spot market only — no other exchanges
- **Asset**: BTC only — single trading pair
- **Direction**: Buy only — no sell logic
- **Notifications**: Telegram + Serilog — both required for every purchase

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Hyperliquid spot (not perps) | User wants actual BTC accumulation, not leveraged exposure | ✓ Good |
| Smart DCA with drop-from-high tiers | Better average cost than fixed DCA, simple to implement and understand | ✓ Good |
| 200-day MA as bear market indicator | Well-known, reliable signal for macro trend, adds 1.5x boost in downtrends | ✓ Good |
| Configurable schedule + amounts | User wants flexibility to adjust strategy without code changes | ✓ Good |
| No monthly spending cap | Daily amount + multipliers provide sufficient control | ✓ Good |
| PostgreSQL advisory locks (not Dapr) | Dapr lock was stubbed; PostgreSQL advisory locks are real and reliable | ✓ Good |
| IOC orders with 5% slippage | Immediate fill with price protection for spot market buys | ✓ Good |
| Multiplicative multiplier stacking | Dip tier * bear boost with configurable cap (default 4.5x) | ✓ Good |
| Stale data policy: use last known | Better than falling back to 1x on transient refresh failures | ✓ Good |
| Graceful degradation to 1.0x | Multiplier failure never prevents DCA purchase | ✓ Good |
| Dry-run bypasses idempotency | Allows repeated safe testing without interference | ✓ Good |

---
*Last updated: 2026-02-12 after v1.0 milestone*

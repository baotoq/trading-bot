# BTC Smart DCA Bot

## What This Is

A recurring buy bot that automatically accumulates BTC on Hyperliquid spot market using a smart DCA strategy. It buys a configurable base amount daily, with multipliers that increase position size during dips — buying more when price drops further from recent highs, and even more aggressively when price is below the 200-day moving average.

## Core Value

The bot reliably executes daily BTC spot purchases on Hyperliquid with smart dip-buying, so the user accumulates BTC at a better average cost than fixed DCA.

## Requirements

### Validated

- ✓ Event-driven architecture with MediatR domain events — existing
- ✓ Outbox pattern for reliable message publishing — existing
- ✓ Distributed locking via Dapr — existing
- ✓ Background service base class (TimeBackgroundService) — existing
- ✓ Serilog structured logging — existing
- ✓ PostgreSQL + EF Core persistence — existing
- ✓ Redis distributed caching — existing
- ✓ Aspire orchestration for local dev — existing
- ✓ Telegram notification infrastructure — existing

### Active

- [ ] Hyperliquid spot API integration (authentication, market data, order placement)
- [ ] Smart DCA engine with configurable base amount and dip multipliers
- [ ] Drop-from-high calculation (30-day high tracking, tier-based multipliers: 1x/1.5x/2x/3x)
- [ ] 200-day MA bear market boost (1.5x additional multiplier below 200 MA)
- [ ] Configurable daily schedule (time of day for buy execution)
- [ ] Purchase history tracking (amount, price, multiplier used, timestamp)
- [ ] Telegram notifications on each buy (amount, price, multiplier, running totals)
- [ ] Detailed console/file logging of all decisions and executions
- [ ] Configuration management (base amount, schedule, multiplier tiers, all adjustable)

### Out of Scope

- Selling/take-profit logic — this is accumulation only
- Futures/perps trading — spot only
- Multi-asset support — BTC only for now
- Web dashboard — logs and Telegram are sufficient
- Backtesting — build the bot first, backtest later if needed
- Monthly spending caps — daily amount + multipliers are the only controls

## Context

- Existing .NET 10.0 codebase with solid infrastructure (Dapr, MediatR, EF Core, outbox pattern)
- Previous trading logic was removed in a recent "revamp" — clean slate for application layer
- BuildingBlocks layer provides base entities, domain events, pub/sub, distributed locks, background services
- Hyperliquid is a newer exchange — need to research their API (REST + WebSocket)
- User wants Telegram alerts + detailed logs for every buy

## Constraints

- **Tech Stack**: .NET 10.0, existing BuildingBlocks infrastructure — must build on current foundation
- **Exchange**: Hyperliquid spot market only — no other exchanges
- **Asset**: BTC only — single trading pair
- **Direction**: Buy only — no sell logic
- **Notifications**: Telegram + Serilog — both required for every purchase

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Hyperliquid spot (not perps) | User wants actual BTC accumulation, not leveraged exposure | — Pending |
| Smart DCA with drop-from-high tiers | Better average cost than fixed DCA, simple to implement and understand | — Pending |
| 200-day MA as bear market indicator | Well-known, reliable signal for macro trend, adds 1.5x boost in downtrends | — Pending |
| Configurable schedule + amounts | User wants flexibility to adjust strategy without code changes | — Pending |
| No monthly spending cap | Daily amount + multipliers provide sufficient control | — Pending |

---
*Last updated: 2026-02-12 after initialization*

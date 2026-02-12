# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v1.0
**Updated:** 2026-02-12

## Current Phase

**Phase 2: Core DCA Engine** — In Progress

## Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Foundation & Hyperliquid Client | Complete | All plans complete ██████████ 3/3 plans |
| Phase 2: Core DCA Engine | In Progress | Plans 02-01, 02-02 complete ██████░░░░ 2/3 plans |
| Phase 3: Smart Multipliers | Not Started | Blocked by Phase 2 |
| Phase 4: Enhanced Notifications & Observability | Not Started | Blocked by Phase 3 |

## Key Decisions

| Decision | Date | Context |
|----------|------|---------|
| Hyperliquid spot (not Binance) | 2026-02-12 | User's exchange preference |
| Smart DCA with drop-from-high tiers | 2026-02-12 | Better avg cost than fixed DCA |
| 200-day MA bear boost | 2026-02-12 | Well-known macro trend indicator |
| No monthly spending cap | 2026-02-12 | Daily amount + multipliers sufficient |
| Direct HTTP client (no SDK) | 2026-02-12 | No viable .NET SDK for Hyperliquid |
| Nethereum for EIP-712 signing | 2026-02-12 | Most mature .NET Ethereum library |
| IOptionsMonitor for hot-reload config | 2026-02-12 | Enable config changes without restart |
| PurchaseStatus enum for type safety | 2026-02-12 | Avoid string-based status bugs |
| PostgreSQL advisory locks | 2026-02-12 | Replace Dapr stub with real locking |
| EF Core auto-migration on startup | 2026-02-12 | Zero-touch database setup |
| Telegram Markdown v1 (not v2) | 2026-02-12 | Simpler escaping for notifications |
| Error-safe notification handlers | 2026-02-12 | Log errors but never throw |
| Conditional balance display in skip events | 2026-02-12 | Cleaner messages when optional |
| IOC orders with 5% slippage tolerance | 2026-02-12 | Immediate fill with price protection |
| Partial fill threshold at 95% | 2026-02-12 | Distinguish full vs partial fills |
| 5-decimal BTC precision, round DOWN | 2026-02-12 | Avoid exceeding balance |
| Domain events AFTER SaveChangesAsync | 2026-02-12 | Transactional integrity |
| Date-based distributed lock keys | 2026-02-12 | Daily purchase idempotency |
| Fixed 1x multiplier in Phase 2 | 2026-02-12 | Smart multipliers deferred to Phase 3 |

## Known Risks

1. ~~**Distributed lock is stubbed**~~ — ✅ Fixed in 01-01 with PostgreSQL advisory locks
2. **Hyperliquid spot API less documented** — EIP-712 signing needs verification
3. **200-day price history bootstrap** — May need external data source initially

## Session Continuity

**Last session:** 2026-02-12 19:43:09 UTC
**Stopped at:** Completed 02-02-PLAN.md
**Resume file:** None

## Next Action

Continue Phase 2 with Plan 02-03 (Daily Scheduler)

---
*State updated: 2026-02-12*

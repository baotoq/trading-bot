# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v1.0
**Updated:** 2026-02-12

## Current Phase

**Phase 4: Enhanced Notifications & Observability** — Complete (3/3 plans complete)

## Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Foundation & Hyperliquid Client | Complete | All plans complete ██████████ 3/3 plans |
| Phase 2: Core DCA Engine | Complete | All plans complete ██████████ 3/3 plans |
| Phase 3: Smart Multipliers | Complete | All plans complete ██████████ 3/3 plans |
| Phase 4: Enhanced Notifications & Observability | Complete | All plans complete ██████████ 3/3 plans |

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
| 5-minute check interval with 10-minute execution window | 2026-02-12 | Balances responsiveness with retry window |
| No catch-up buys if bot starts after window | 2026-02-12 | Prevents unintended late execution |
| 3 retries with exponential backoff + jitter | 2026-02-12 | 2^n seconds with 0-500ms jitter |
| 4xx errors fail immediately without retry | 2026-02-12 | Client errors won't resolve with retry |
| IServiceScopeFactory for scoped service resolution | 2026-02-12 | Singleton BackgroundService needs scoped DbContext |
| DailyPrice composite key (Date, Symbol) | 2026-02-12 | Time-series data naturally partitioned, no UUIDv7 |
| Non-nullable decimal for multiplier metadata | 2026-02-12 | 0 = "not calculated" for Phase 2 purchases |
| CandleData intermediate type | 2026-02-12 | Separates API deserialization from domain entity |
| OHLCV precision(18,8) | 2026-02-12 | Matches crypto exchange 8-decimal standard |
| Return 0 sentinel for unavailable price data | 2026-02-12 | BTC never 0, unambiguous signal, simpler than nullables |
| Use daily close for 30-day high calculation | 2026-02-12 | Avoid flash spike distortion from intraday wicks |
| Stale data policy: use last known values | 2026-02-12 | Override FR-7: stale data better than no data |
| Bootstrap once on startup, refresh daily 00:05 UTC | 2026-02-12 | Ensures data before first DCA, fresh daily candles |
| 10% tolerance for 200-day SMA gaps | 2026-02-12 | Accept 180+ days for SMA calculation resilience |
| MaxMultiplierCap configurable with default 4.5x | 2026-02-12 | Natural max from tier structure, effectively uncapped but allows safety limits |
| Component-level fallback for price data | 2026-02-12 | 0 price data = 1.0x for that component, other components still calculated |
| Exception-level fallback to 1.0x multiplier | 2026-02-12 | Never let multiplier calculation failure prevent DCA purchase |
| DRY-RUN-{guid} order ID format | 2026-02-12 | Traceability for simulated purchases without colliding with real order IDs |
| Idempotency bypass in dry-run mode | 2026-02-12 | Allow repeated testing without interference, real-mode behavior unchanged |
| IsDryRun defaults to false | 2026-02-12 | Backward compatibility: existing purchases treated as real (non-dry-run) |
| All multiplier metadata in events | 2026-02-12 | PurchaseCompletedEvent carries 6 fields for rich notification formatting |
| Running totals query excludes dry-run purchases | 2026-02-12 | WHERE !IsDryRun filter ensures accurate totals for real spending |
| Natural language multiplier reasoning | 2026-02-12 | Transform multiplier metadata into readable explanations in notifications |
| SIMULATION banner for dry-run notifications | 2026-02-12 | Warning emoji + clear text at top distinguishes simulations from real purchases |
| Weekly summary timing: Sunday 20:00-21:00 UTC | 2026-02-12 | End-of-week reporting cadence with hourly check |
| Missed purchase verification window: target + 40 minutes | 2026-02-12 | Allows 10-min execution window + 30-min grace period |
| Health check threshold: 36 hours | 2026-02-12 | Catches multi-day silent failures while allowing schedule flexibility |
| De-duplication via DateOnly fields | 2026-02-12 | In-memory guard prevents duplicate messages without database overhead |
| Diagnostic reasoning in missed purchase alerts | 2026-02-12 | Queries failed purchases to include specific diagnosis for faster troubleshooting |

## Known Risks

1. ~~**Distributed lock is stubbed**~~ — ✅ Fixed in 01-01 with PostgreSQL advisory locks
2. **Hyperliquid spot API less documented** — EIP-712 signing needs verification
3. **200-day price history bootstrap** — May need external data source initially

## Session Continuity

**Last session:** 2026-02-12 16:10:39 UTC
**Stopped at:** Completed 04-03-PLAN.md (Weekly Summary & Health Monitoring)
**Resume file:** None

## Next Action

Phase 4 complete (3/3 plans). All features from ROADMAP.md phases 1-4 delivered. Ready for Phase 5 or future enhancements.

---
*State updated: 2026-02-12*

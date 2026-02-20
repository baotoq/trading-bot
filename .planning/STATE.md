# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v4.0 Portfolio Tracker
**Updated:** 2026-02-20

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA
**Current focus:** Phase 29 — Flutter Portfolio UI

## Current Position

Phase: 28 of 29 (Portfolio Backend API) — COMPLETE
Plan: 2 of 2 (all executed)
Status: Phase 28 complete
Last activity: 2026-02-20 — Phase 28 complete (2 plans, domain model extensions + auto-import + 9 API endpoints)

Progress: [#######░░░] 75% (v4.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 63 (across v1.0-v4.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (11 plans)
- v4.0: in progress (7 plans so far)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 | Complete |
| v4.0 | 26-29 | 7/TBD | Phase 28 complete |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**v4.0 architecture decisions (from research):**
- Separate aggregate roots for PortfolioAsset and FixedDeposit (not TPH — avoids nullable columns)
- VndAmount value object with HasPrecision(18, 0); integer share quantities for ETF
- store cost_native + cost_usd + exchange_rate_at_transaction at write time (P&L computed in native currency only)
- DCA domain has zero knowledge of portfolio domain — connected via PurchaseCompletedEvent only
- API always returns both valueUsd and valueVnd; currency toggle is pure Flutter display logic
- VN ETF prices: best-effort only, 48h Redis TTL, staleness indicator always shown

**Phase 26 decisions:**
- VndAmount allows zero (non-negative) for zero-fee scenarios
- AssetTransaction.Create is internal to enforce PortfolioAsset aggregate boundary
- InterestCalculator uses 365-day year convention (Vietnamese banking standard)
- Compound interest test tolerance: 500 VND on 10M deposits (double-precision variance from Math.Pow)
- VND Principal: numeric(18,0); Crypto Quantity: numeric(18,8); Fee: numeric(18,2); Rate: numeric(8,6)

**Phase 27 decisions:**
- VNDirect dchart-api used instead of finfo-api (finfo times out externally, dchart verified live)
- PriceFeedEntry uses positional record with long FetchedAtUnixSeconds (avoids MessagePack DateTimeOffset resolver issues)
- VNDirect ETF provider uses stale-while-revalidate (returns stale immediately, fire-and-forget refresh)
- OpenErApi exchange rate uses wait-for-fetch (accuracy matters more for currency conversion)
- CoinGecko API key added as DefaultRequestHeader on HttpClient at DI registration time
- Shared resilience config: 2 retries, 1s exponential backoff, 15s total/8s attempt timeout
- VNDirect close prices multiplied by 1000 to convert from thousands-of-VND to actual VND

**Phase 28 decisions:**
- SourcePurchaseId uses optional parameter (default null) rather than separate overload to keep API surface minimal
- CoinGecko ID mapping via static dictionary (BTC=bitcoin, ETH=ethereum) — extensible for future coins
- P&L uses weighted average cost from buy transactions only (sells reduce position but don't change avg cost basis)
- Fixed deposit accrued/projected values computed per request (no caching — computation is cheap)
- Price feed failures use 0 for that asset, don't fail portfolio summary endpoint
- Historical migration triggers automatically when BTC asset has no bot-imported transactions on first summary call

**v3.0 Flutter conventions carried forward:**
- Dark-only theme, NavigationBar (Material 3) + CupertinoIcons, StatefulShellRoute
- Manual fromJson for DTO models (no json_serializable), intl as explicit dependency
- SliverAppBar with floating+snap, fl_chart with two-LineChartBarData approach
- Explicit isLoadingMore boolean (not copyWithPrevious) for pagination state

### Known Risks

- VNDirect dchart-api is undocumented/unofficial — could change without notice (research valid until 2026-03-20)

### Pending Todos

None.

### Roadmap Evolution

v4.0 roadmap: 4 phases (26-29), 20 requirements, all mapped.

## Session Continuity

Last session: 2026-02-20
Stopped at: Phase 28 complete — all 2 plans executed (domain model extensions + auto-import handler + migration service, portfolio + fixed deposit API endpoints)
Next step: `/gsd:plan-phase 29` (Flutter Portfolio UI)

---
*State updated: 2026-02-20 after Phase 28 completion*

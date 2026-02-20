# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v4.0 Portfolio Tracker
**Updated:** 2026-02-20

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA
**Current focus:** Phase 27 — Price Feeds & Market Data

## Current Position

Phase: 27 of 29 (Price Feeds & Market Data)
Plan: 0 of TBD
Status: Ready to plan
Last activity: 2026-02-20 — Phase 26 complete (3 plans, 7 entities, 14 new tests)

Progress: [##░░░░░░░░] 25% (v4.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 59 (across v1.0-v4.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (11 plans)
- v4.0: in progress (3 plans so far)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 | Complete |
| v4.0 | 26-29 | 3/TBD | Phase 26 complete |

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

**v3.0 Flutter conventions carried forward:**
- Dark-only theme, NavigationBar (Material 3) + CupertinoIcons, StatefulShellRoute
- Manual fromJson for DTO models (no json_serializable), intl as explicit dependency
- SliverAppBar with floating+snap, fl_chart with two-LineChartBarData approach
- Explicit isLoadingMore boolean (not copyWithPrevious) for pagination state

### Known Risks

- Phase 27: VNDirect finfo API JSON schema unconfirmed (endpoint timed out during research). Needs live request verification at Phase 27 planning start. Use `/gsd:research-phase` before planning Phase 27.

### Pending Todos

None.

### Roadmap Evolution

v4.0 roadmap: 4 phases (26-29), 20 requirements, all mapped.

## Session Continuity

Last session: 2026-02-20
Stopped at: Phase 26 complete — all 3 plans executed (domain models, interest calculator, EF Core persistence)
Next step: `/gsd:research-phase 27` (verify VNDirect API before planning)

---
*State updated: 2026-02-20 after Phase 26 completion*

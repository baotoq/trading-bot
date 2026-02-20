# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v4.0 Portfolio Tracker
**Updated:** 2026-02-20

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA
**Current focus:** Phase 26 — Portfolio Domain Foundation

## Current Position

Phase: 26 of 29 (Portfolio Domain Foundation)
Plan: 0 of TBD
Status: Ready to plan
Last activity: 2026-02-20 — v4.0 roadmap created (4 phases, 20 requirements mapped)

Progress: [░░░░░░░░░░] 0% (v4.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 56 (across v1.0-v3.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (11 plans)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 | Complete |
| v4.0 | 26-29 | TBD | Not started |

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
Stopped at: v4.0 roadmap created — ready to plan Phase 26
Next step: `/gsd:plan-phase 26`

---
*State updated: 2026-02-20 after v4.0 roadmap creation*

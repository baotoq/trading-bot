# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v5.0 Stunning Mobile UI
**Updated:** 2026-02-21

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-21)

**Core value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA -- now with a premium glassmorphism UI
**Current focus:** Phase 33 -- Design System Foundation

## Current Position

Phase: 33 of 38 (Design System Foundation)
Plan: — of — (not yet planned)
Status: Ready to plan
Last activity: 2026-02-21 -- v5.0 roadmap created (6 phases, 21 requirements mapped)

Progress: [░░░░░░░░░░] 0% (v5.0)

## Performance Metrics

**Velocity:**
- Total plans completed: 71 (across v1.0-v4.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (12 plans)
- v4.0: 2 days (15 plans)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25.1 | 12 | Complete |
| v4.0 | 26-32 | 15 | Complete |
| v5.0 | 33-38 | TBD | In progress |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

Recent decisions affecting v5.0:
- No BackdropFilter in scrollable lists (History, Portfolio) -- Impeller frame drops confirmed; use opacity-tint + border non-blur GlassCard variant
- GlassTheme as single ThemeExtension -- consolidated before any screen is touched to prevent two-source-of-truth color drift
- profitGreen and lossRed are non-negotiable semantic constants -- must survive token consolidation
- All AnimationControllers in HookConsumerWidget via useAnimationController -- prevents lifecycle leaks across 5 tabs
- _hasAnimated guard pattern -- counters and chart draw-in animate only on initial load, not on every tab revisit
- BackdropFilter on nav bar (GlassBottomNav) deferred to v5.x -- requires physical device performance gate before committing

### Known Risks

- VNDirect dchart-api is undocumented/unofficial -- could change without notice (research valid until 2026-03-20)
- GlassBottomNav BackdropFilter: full-width blur affects every tab transition; fallback is opacity-tint-only nav (safe)
- GlowLineChart data-slice: ChartResponse data model compatibility not yet validated against prices.length * progress approach
- Blur sigma calibration (card: 12, appBar: 16) must be verified on physical iPhone -- iOS Simulator does not accurately represent BackdropFilter GPU cost

### Pending Todos

None.

## Session Continuity

Last session: 2026-02-21
Stopped at: v5.0 roadmap created. Ready to plan Phase 33.
Next step: `/gsd:plan-phase 33`

---
*State updated: 2026-02-21 after v5.0 roadmap created*

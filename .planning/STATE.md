# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v2.0 DDD Foundation
**Updated:** 2026-02-18

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-14)

**Core value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via web dashboard
**Current focus:** Phase 13 -- Strongly-Typed IDs (first phase of v2.0 DDD Foundation)

## Current Position

Phase: 13 of 18 (Strongly-Typed IDs)
Plan: 1 of 2 in current phase (13-01 complete)
Status: In Progress
Last activity: 2026-02-18 -- Completed 13-01 (Vogen typed IDs infrastructure, BaseEntity<TId>, EF Core converters)

Progress: [|||||||||||||||||||||||.........] 65% (31/~48 plans estimated)

## Milestones Shipped

- v1.0 Daily BTC Smart DCA (2026-02-12) -- 4 phases, 11 plans
- v1.1 Backtesting Engine (2026-02-13) -- 4 phases, 7 plans, 53 tests
- v1.2 Web Dashboard (2026-02-14) -- 5 phases, 12 plans

## Performance Metrics

**Velocity:**
- Total plans completed: 31
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: In progress

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-18 | TBD | In progress |

**Plan Execution Metrics:**

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 13-strongly-typed-ids | 01 | 6min | 2 | 6 |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.
Recent for v2.0:
- Vogen 8.0.4 for source-generated value objects and strongly-typed IDs (zero runtime overhead)
- ErrorOr 2.0.1 for Result pattern (zero allocation, .NET 10 optimized)
- Ardalis.Specification.EntityFrameworkCore 9.3.1 for query encapsulation
- Domain events dispatch AFTER SaveChanges via SaveChangesInterceptor (not before -- critical pitfall)
- [Phase 13-strongly-typed-ids]: VogenDefaults uses toPrimitiveCasting/fromPrimitiveCasting (not castOperator) in Vogen 8.0.4
- [Phase 13-strongly-typed-ids]: Vogen assembly-attribute approach does not generate RegisterAllInEfCoreConverters; use per-type Properties<T>().HaveConversion<> in ConfigureConventions
- [Phase 13-strongly-typed-ids]: DailyPriceId excluded: composite key (Date, Symbol) with no Guid column - schema unchanged constraint

### Known Risks

- Domain event dispatch timing: SaveChangesInterceptor.SavedChangesAsync (after commit), NOT SaveChangesAsync override (before commit)
- Value objects need careful EF Core converter registration in ConfigureConventions
- Rich aggregate refactoring touches all entity creation/mutation sites

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-02-18
Stopped at: Completed 13-01-PLAN.md (Vogen typed IDs infrastructure)
Resume file: .planning/phases/13-strongly-typed-ids/13-01-SUMMARY.md
Next step: Execute 13-02-PLAN.md (apply typed IDs to all entities and callers)

---
*State updated: 2026-02-18 after completing 13-01*

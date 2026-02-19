# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v2.0 DDD Foundation
**Updated:** 2026-02-19

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-14)

**Core value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via web dashboard
**Current focus:** Phase 15 -- Rich Aggregate Roots

## Current Position

Phase: 15 of 18 (Rich Aggregate Roots -- Complete)
Plan: 2 of 2 in current phase (both complete)
Status: Phase 15 Complete -- Ready for Phase 16
Last activity: 2026-02-19 -- Completed 15-02 (DcaConfiguration rich aggregate, factory method, behavior methods, domain events, invariant enforcement)

Progress: [||||||||||||||||||||||||||||....] 75% (36/~48 plans estimated)

## Milestones Shipped

- v1.0 Daily BTC Smart DCA (2026-02-12) -- 4 phases, 11 plans
- v1.1 Backtesting Engine (2026-02-13) -- 4 phases, 7 plans, 53 tests
- v1.2 Web Dashboard (2026-02-14) -- 5 phases, 12 plans

## Performance Metrics

**Velocity:**
- Total plans completed: 36
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
| 13-strongly-typed-ids | 02 | 4min | 2 | 13 |
| 14-value-objects | 01 | 5min | 2 | 14 |
| 14-value-objects | 02 | 15min | 2 | 13 |
| 15-rich-aggregate-roots | 01 | 4min | 2 | 9 |
| 15-rich-aggregate-roots | 02 | 2min | 2 | 4 |

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
- [Phase 13-strongly-typed-ids]: DashboardDtos.cs: PurchaseDto.Id stays as Guid (API surface stability) -- implicit Vogen cast from PurchaseId handles LINQ Select projection
- [Phase 13-strongly-typed-ids]: FindAsync replaced with FirstOrDefaultAsync for reliable type-safe LINQ on value-converted EF Core keys
- [Phase 14-value-objects]: Symbol EfCoreValueConverter registered (not skipped) so DailyPrice composite PK key works correctly
- [Phase 14-value-objects]: Multiplier sanity cap 20x (not 10x); operational cap remains MaxMultiplierCap in config
- [Phase 14-value-objects]: High30Day/Ma200Day/RemainingUsdc stay decimal (0 sentinel for data unavailable; value objects reject 0)
- [Phase 14-value-objects]: MultiplierTierData inside jsonb keeps raw decimal (avoid EF Core jsonb/STJ serialization complexity)
- [Phase 14-value-objects]: Conversions.TypeConverter added globally for ASP.NET Core config binding; CultureInfo.InvariantCulture required for tier label formatting
- [Phase 14-value-objects]: DcaOptionsValidator removes positivity checks now enforced by value objects at binding time; DcaExecutionService fallback constructs MultiplierResult directly to avoid 0-sentinel path
- [Phase 15-rich-aggregate-roots]: AggregateRoot<TId> base class with protected AddDomainEvent and ClearDomainEvents; Purchase uses static Create() factory and behavior methods that raise identity-only domain events
- [Phase 15-rich-aggregate-roots]: DcaSchedulerBackgroundService scheduler-level catch blocks log-only for infrastructure failures (no PurchaseFailedEvent): no Purchase aggregate exists at scheduler level to provide PurchaseId
- [Phase 15-rich-aggregate-roots]: DcaConfiguration inherits AggregateRoot<DcaConfigurationId>; fine-grained behavior methods (UpdateDailyAmount/Schedule/Tiers/BearMarket/Settings) each raise DcaConfigurationUpdatedEvent; empty tier list is valid; ConfigurationService retains validator call as defense-in-depth

### Known Risks

- Domain event dispatch timing: SaveChangesInterceptor.SavedChangesAsync (after commit), NOT SaveChangesAsync override (before commit)
- Value objects need careful EF Core converter registration in ConfigureConventions
- Rich aggregate refactoring touches all entity creation/mutation sites

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-02-19
Stopped at: Completed 15-02-PLAN.md
Resume file: .planning/phases/15-rich-aggregate-roots/15-02-SUMMARY.md
Next step: Execute Phase 16 (next phase per ROADMAP)

---
*State updated: 2026-02-19 after 15-02 (DcaConfiguration rich aggregate, factory method, behavior methods, domain events, all 53 tests pass)*

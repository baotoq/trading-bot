# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v2.0 DDD Foundation
**Updated:** 2026-02-19

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-14)

**Core value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via web dashboard
**Current focus:** Phase 18 -- Specification Pattern

## Current Position

Phase: 19 of 19 (Dashboard Nullable Price Fix -- Complete)
Plan: 1 of 1 in current phase (Plan 01 complete -- 19-01 done)
Status: Phase 19 Complete -- All plans done
Last activity: 2026-02-19 -- Completed 19-01 (nullable Price DTOs, null-safe endpoints, TypeScript/Vue null handling)

Progress: [||||||||||||||||||||||||||||||||||||] 90% (43/~48 plans estimated)

## Milestones Shipped

- v1.0 Daily BTC Smart DCA (2026-02-12) -- 4 phases, 11 plans
- v1.1 Backtesting Engine (2026-02-13) -- 4 phases, 7 plans, 53 tests
- v1.2 Web Dashboard (2026-02-14) -- 5 phases, 12 plans

## Performance Metrics

**Velocity:**
- Total plans completed: 41
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
| 16-result-pattern | 01 | 2min | 2 | 4 |
| 16-result-pattern | 02 | 1min | 2 | 2 |
| 17-domain-event-dispatch | 01 | 49min | 2 | 12 |
| 17-domain-event-dispatch | 02 | 4min | 2 | 12 |
| 17-domain-event-dispatch | 03 | 2min | 2 | 2 |
| 18-specification-pattern | 01 | 2min | 2 | 8 |
| 19-dashboard-nullable-price-fix | 01 | 4min | 2 | 5 |

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
- [Phase 16-result-pattern]: ValidateScheduleErrors/ValidateTierErrors return List<Error> shared by Create() (throws) and behavior methods (returns ErrorOr); Create() factory still throws per locked decision
- [Phase 16-result-pattern]: ToHttpResult() handles Updated success as 204 NoContent, all other T values as 200 OK; ConfigurationService callers deferred to Plan 02
- [Phase 16-result-pattern]: ConfigurationService.UpdateAsync returns ErrorOr<Updated>; DcaOptionsValidator failure becomes Error.Validation (not throws); Create() path still throws per locked decision; all behavior method ErrorOr results propagated via IsError check
- [Phase 17-domain-event-dispatch]: Aspire AddNpgsqlDbContext configureDbContextOptions is Action<DbContextOptionsBuilder> (no IServiceProvider); interceptor created before registration and captured by closure
- [Phase 17-domain-event-dispatch]: PurchaseSkippedEvent.SkippedAt renamed to OccurredAt for consistency with all other domain events
- [Phase 17-domain-event-dispatch]: Runtime type serialization via JsonSerializer.Serialize(event, event.GetType(), options) prevents empty JSON when serializing IDomainEvent interface
- [Phase 17-domain-event-dispatch]: MapPubSub uses mediator.Publish(object) with null check -- MediatR runtime dispatch works for both IDomainEvent and IntegrationEvent types; no IntegrationEvent cast needed
- [Phase 17-domain-event-dispatch]: IDomainEventPublisher.PublishDirectAsync calls SaveChangesAsync immediately; appropriate for non-aggregate events without surrounding aggregate transaction
- [Phase 17-domain-event-dispatch]: Dead-letter check at start of ProcessOutboxMessagesAsync (before processing); message moved to dead-letter on 4th pickup cycle after 3 retries exhausted
- [Phase 17-domain-event-dispatch]: All 6 domain events subscribed in PubSubRegistry using fluent .Subscribe<T>() chaining on returned registry value
- [Phase 17-domain-event-dispatch]: DcaExecutionService manual dispatch block (Steps 8-9) removed -- interceptor from Plan 01 handles this automatically during SaveChangesAsync
- [Phase 17-domain-event-dispatch]: PurchaseSkippedEvent now uses IDomainEventPublisher.PublishDirectAsync (same outbox path as aggregate events via Dapr)
- [Phase 17-domain-event-dispatch]: IPublisher (MediatR) fully removed from DcaExecutionService; all event dispatch via single outbox pipeline
- [Phase 18-specification-pattern]: PurchaseFilledStatusSpec has no OrderBy to avoid ordering conflicts when composed with PurchasesOrderedByDateSpec or PurchaseCursorSpec
- [Phase 18-specification-pattern]: PurchaseCursorSpec owns OrderByDescending because cursor comparison inherently assumes descending order
- [Phase 18-specification-pattern]: AsNoTracking lives in ordering specs (PurchasesOrderedByDateSpec, DailyPriceByDateRangeSpec) as always-applied base for read queries
- [Phase 18-specification-pattern]: WithSpecification uses SpecificationEvaluator.Default.GetQuery() on IQueryable<T> (not DbSet<T>) to enable multi-spec chaining
- [Phase 19-dashboard-nullable-price-fix]: TotalCost is decimal (not UsdAmount) in PortfolioResponse DTO -- UsdAmount rejects zero; zero is valid when no purchases exist
- [Phase 19-dashboard-nullable-price-fix]: AverageCostBasis and CurrentPrice are Price? -- null when no purchases or Hyperliquid unreachable; PnL is decimal? null when CurrentPrice unavailable
- [Phase 19-dashboard-nullable-price-fix]: Explicit (decimal) casts before Sum() in endpoints avoid VogenInvalidValueException on empty set

### Known Risks

- Value objects need careful EF Core converter registration in ConfigureConventions
- Rich aggregate refactoring touches all entity creation/mutation sites

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-02-19
Stopped at: Completed 19-01-PLAN.md
Resume file: .planning/phases/19-dashboard-nullable-price-fix/19-01-SUMMARY.md
Next step: Phase 19 complete; v2.0 milestone gap closure done

---
*State updated: 2026-02-19 after 19-01 (nullable Price DTOs, null-safe endpoints, TypeScript/Vue null handling for empty DB/unreachable exchange)*

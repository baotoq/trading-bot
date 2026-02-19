---
phase: 15-rich-aggregate-roots
plan: 02
subsystem: domain
tags: [ddd, aggregate-root, domain-events, dca-configuration, invariants, csharp, dotnet]

# Dependency graph
requires:
  - phase: 15-rich-aggregate-roots/15-01
    provides: AggregateRoot<TId> base class with AddDomainEvent and ClearDomainEvents
  - phase: 14-value-objects
    provides: UsdAmount, Multiplier, Percentage value objects used as DcaConfiguration properties
  - phase: 13-strongly-typed-ids
    provides: DcaConfigurationId strongly-typed ID used in factory and events

provides:
  - DcaConfiguration as rich aggregate root with private constructor, static Create() factory, and fine-grained behavior methods
  - DcaConfigurationCreatedEvent and DcaConfigurationUpdatedEvent identity-only domain events
  - Tier ordering invariant enforcement (ascending, no duplicates, multiplier 0-20)
  - Schedule invariant enforcement (hour 0-23, minute 0-59)
  - ConfigurationService using aggregate API exclusively (no direct property assignment)

affects: [16-specifications, 17-domain-event-interceptor, 18-outbox]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - DcaConfiguration.Create() factory as sole creation entry point (no public constructor)
    - Fine-grained behavior methods that set UpdatedAt and raise DcaConfigurationUpdatedEvent atomically
    - Private static ValidateSchedule/ValidateTiers methods called from Create() and behavior methods
    - Identity-only domain events (DcaConfigurationId only) for configuration lifecycle
    - ConfigurationService calls behavior methods instead of MapFromOptions for update path

key-files:
  created:
    - TradingBot.ApiService/Application/Events/DcaConfigurationCreatedEvent.cs
    - TradingBot.ApiService/Application/Events/DcaConfigurationUpdatedEvent.cs
  modified:
    - TradingBot.ApiService/Models/DcaConfiguration.cs
    - TradingBot.ApiService/Application/Services/ConfigurationService.cs

key-decisions:
  - "DcaConfiguration inherits AggregateRoot<DcaConfigurationId> (not BaseEntity) to gain domain event collection"
  - "Fine-grained behavior methods (UpdateDailyAmount, UpdateSchedule, UpdateTiers, UpdateBearMarket, UpdateSettings) each raise DcaConfigurationUpdatedEvent individually -- no bulk UpdateAll method"
  - "ValidateTiers allows empty/null tiers as valid state (base-only DCA with no multiplier tiers)"
  - "ConfigurationService retains DcaOptionsValidator validation at application boundary as defense-in-depth (not redundant with aggregate invariants)"

patterns-established:
  - "Aggregate.Create() is the only public entry point for DcaConfiguration -- no direct constructor access outside class"
  - "Private ValidateSchedule and ValidateTiers called from both Create() and the corresponding behavior methods to avoid duplication"
  - "UpdatedAt is set inside behavior methods, not by the service layer -- aggregate owns its own timestamp management"
  - "DailyPrice and IngestionJob remain data carriers with public setters (per locked decision -- not all entities need to be aggregates)"

requirements-completed: [DM-03, DM-04]

# Metrics
duration: 2min
completed: 2026-02-19
---

# Phase 15 Plan 02: Rich Aggregate Roots Summary

**DcaConfiguration refactored to DDD aggregate root with private constructor, static Create() factory, five fine-grained behavior methods with invariant enforcement (tier ordering, schedule bounds), and identity-only domain events raised on creation and each mutation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-19T13:29:43Z
- **Completed:** 2026-02-19T13:31:43Z
- **Tasks:** 2
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments

- DcaConfiguration is now a proper DDD aggregate root: AggregateRoot<DcaConfigurationId> inheritance, protected EF Core constructor, private setters on all properties, and static Create() factory
- Fine-grained behavior methods encapsulate all mutation and invariant enforcement: UpdateDailyAmount, UpdateSchedule, UpdateTiers, UpdateBearMarket, UpdateSettings
- Tier ordering invariants enforced at aggregate boundary: ascending drop percentages, no duplicates, multiplier range 0-20
- Schedule invariants enforced: hour 0-23, minute 0-59
- ConfigurationService uses factory method and behavior methods exclusively -- MapFromOptions private method removed
- All 53 existing tests pass without regression

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor DcaConfiguration to rich aggregate root with behavior methods** - `d8a2ea3` (feat)
2. **Task 2: Update ConfigurationService to use aggregate factory and behavior methods** - `477a29c` (feat)

**Plan metadata:** (docs commit, recorded after SUMMARY)

## Files Created/Modified

- `TradingBot.ApiService/Models/DcaConfiguration.cs` - Rich aggregate root: AggregateRoot<DcaConfigurationId> inheritance, private setters, protected EF ctor, Create() factory, behavior methods, ValidateSchedule/ValidateTiers
- `TradingBot.ApiService/Application/Events/DcaConfigurationCreatedEvent.cs` - New identity-only event raised in Create()
- `TradingBot.ApiService/Application/Events/DcaConfigurationUpdatedEvent.cs` - New identity-only event raised in each behavior method
- `TradingBot.ApiService/Application/Services/ConfigurationService.cs` - Uses DcaConfiguration.Create() on create path; five behavior methods on update path; MapFromOptions removed

## Decisions Made

- DcaConfiguration inherits AggregateRoot<DcaConfigurationId> to gain domain event collection -- consistent with Purchase pattern established in Plan 01
- Fine-grained behavior methods chosen over a single UpdateAll method to match the aggregate's semantic intent (each change is a distinct operation)
- Empty tier list is valid (returns early in ValidateTiers) because base-only DCA without multiplier tiers is a legitimate configuration
- ConfigurationService retains DcaOptionsValidator call as defense-in-depth at application boundary -- aggregate invariants protect domain correctness, validator protects API input

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DcaConfiguration aggregate encapsulation complete; behavior methods provide clean API for all mutation
- Both aggregates (Purchase, DcaConfiguration) now follow the same pattern: AggregateRoot base, Create() factory, behavior methods raising identity-only events
- Ready for Phase 16 (Specifications) and Phase 17 (SaveChangesInterceptor auto-dispatch)
- No blockers or concerns

---
*Phase: 15-rich-aggregate-roots*
*Completed: 2026-02-19*

## Self-Check: PASSED

- DcaConfiguration.cs: FOUND
- DcaConfigurationCreatedEvent.cs: FOUND
- DcaConfigurationUpdatedEvent.cs: FOUND
- ConfigurationService.cs: FOUND
- 15-02-SUMMARY.md: FOUND
- Commit d8a2ea3: FOUND
- Commit 477a29c: FOUND

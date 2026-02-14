# Research Summary: DDD Tactical Patterns for .NET 10.0 Trading Bot

**Domain:** DDD Tactical Patterns (Rich Aggregates, Value Objects, Strongly-Typed IDs, Result Pattern, Specification Pattern)
**Researched:** 2026-02-14
**Overall confidence:** HIGH

## Executive Summary

Research completed for adding DDD tactical patterns to the existing .NET 10.0 BTC Smart DCA trading bot. The project already has a solid foundation (.NET 10.0, EF Core 10, PostgreSQL, MediatR for domain events, Dapr for integration events, transactional outbox pattern). This research identifies the minimal stack additions needed to move from an anemic domain model to a rich DDD tactical implementation.

**Key Finding:** Use source generators (Vogen) over reflection-based libraries for zero-overhead value objects and strongly-typed IDs. Leverage existing MediatR + outbox infrastructure for domain events rather than introducing new event libraries. Add only three new packages: Vogen (value objects/IDs), ErrorOr (Result pattern), Ardalis.Specification (query encapsulation).

**Critical Success Factor:** Dispatch domain events AFTER SaveChanges (not before) using SaveChangesInterceptor. This guarantees consistency between aggregate state persistence and event publishing. This is the #1 pitfall that causes rewrites.

**Incremental Migration Recommended:** Start with strongly-typed IDs (quick win, zero runtime cost), add value objects for core primitives (Price, Quantity), introduce Result pattern for error handling, then add specifications for complex queries. Each phase delivers value independently.

## Key Findings

**Stack:** Three new packages only — Vogen 8.0.4 (value objects/IDs via source generation), ErrorOr 2.0.1 (Result pattern, zero allocation), Ardalis.Specification.EntityFrameworkCore 9.3.1 (query encapsulation). NO new event infrastructure needed (use existing MediatR + outbox).

**Architecture:** Enhance existing layered structure with Domain/ValueObjects, Domain/Aggregates (rich behavior), Application/Specifications (query logic), Infrastructure/Data/DomainEventDispatchInterceptor (event timing guarantee). Value objects persist via auto-generated EF Core converters. Domain events dispatch after SaveChanges via interceptor. Application services coordinate aggregates and return ErrorOr<T>.

**Critical pitfall:** Dispatching domain events BEFORE SaveChanges creates inconsistency (events published but aggregate not persisted). Use SaveChangesInterceptor.SavedChangesAsync (after commit) not DbContext.SaveChangesAsync override (before commit). This is the #1 mistake that causes data corruption and requires rewrites.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Strongly-Typed IDs (Quick Win)
- **Addresses:** Type safety, prevents ID mix-ups (PurchaseId vs DailyPriceId vs IngestionJobId)
- **Avoids:** Primitive obsession for entity IDs
- **Why first:** Zero runtime cost (compile-time source generation), no behavioral changes, low risk, immediate value
- **Effort:** Low (1-2 days for all entities)
- **Dependencies:** Install Vogen, create ID value objects, update DbContext.ConfigureConventions, update entities
- **No schema change:** IDs stay as Guid in database, only type changes in C#

### Phase 2: Value Objects for Domain Primitives
- **Addresses:** Primitive obsession for Price, Quantity, Symbol
- **Avoids:** Validation scattered across application layer, invalid domain state
- **Why second:** Builds on Phase 1 (same Vogen infrastructure), encapsulates validation, makes invalid states unrepresentable
- **Effort:** Medium (2-3 days for core primitives)
- **Dependencies:** Vogen already installed from Phase 1, update entities, create EF migration (columns stay decimal/string but validation enforced)
- **Risk:** Medium (validation logic moves to value objects, ensure all creation paths use From() factory)

### Phase 3: Result Pattern for Error Handling
- **Addresses:** Exception-based error handling for domain logic
- **Avoids:** Performance cost of exceptions, poor control flow
- **Why third:** Changes method signatures throughout application, better after IDs and value objects stabilize
- **Effort:** Medium-High (3-5 days to convert domain services, application services, endpoints)
- **Dependencies:** Install ErrorOr, update domain services to return ErrorOr<T>, update endpoints to map Result to HTTP responses
- **Risk:** Medium (breaking change to method signatures, must update all callers)

### Phase 4: Domain Events from Aggregates
- **Addresses:** Tight coupling between aggregates, cross-aggregate coordination
- **Avoids:** Cross-aggregate transactions, synchronous coupling
- **Why fourth:** Requires mature understanding of aggregate boundaries, builds on Phases 1-3
- **Effort:** Medium-High (3-5 days for interceptor, event handlers, aggregate refactoring)
- **Dependencies:** MediatR already present, create SaveChangesInterceptor, update BaseEntity with event collection, create event handlers
- **Risk:** High (event dispatch timing critical, test SaveChanges failure scenarios)
- **Critical:** Use SaveChangesInterceptor.SavedChangesAsync (after commit) NOT override SaveChangesAsync (before commit)

### Phase 5: Specification Pattern for Queries
- **Addresses:** IQueryable leakage into application layer, untestable queries
- **Avoids:** Repeated query logic, hard-to-test data access
- **Why fifth:** Optional, add when query complexity justifies abstraction
- **Effort:** Low-Medium (2-3 days for core specifications)
- **Dependencies:** Install Ardalis.Specification.EntityFrameworkCore, create specifications for complex queries
- **Risk:** Low (specifications translate to same SQL as direct LINQ, no behavioral change)

### Phase 6: Rich Aggregate Roots (Optional - Future Milestone)
- **Addresses:** Complex invariants, aggregate consistency
- **Avoids:** Anemic domain model, business logic in services
- **Why last:** Requires deep domain understanding, builds on all previous phases
- **Effort:** High (varies by aggregate complexity)
- **Risk:** High (fundamental entity design change, requires domain expert validation)
- **Consider:** Only for aggregates with complex invariants (DcaConfig), skip for simple entities (DailyPrice)

## Phase Ordering Rationale

1. **Strongly-Typed IDs first** because zero-risk, immediate value, no behavioral changes
2. **Value Objects second** because builds on same infrastructure (Vogen), encapsulates validation before logic moves to aggregates
3. **Result Pattern third** because changes method signatures (breaking), better after domain primitives stabilize
4. **Domain Events fourth** because requires understanding of aggregate boundaries, highest risk (timing pitfall), builds on mature domain model
5. **Specifications fifth** because optional optimization, no dependencies on other phases
6. **Rich Aggregates last** because most invasive, requires all tactical patterns in place, varies by aggregate complexity

**Dependency Chain:**
```
Strongly-Typed IDs → Value Objects (same Vogen infrastructure)
Value Objects → Result Pattern (validation failures return errors)
Result Pattern → Domain Events (aggregate methods return Result, raise events on success)
Domain Events → Rich Aggregates (aggregates coordinate via events)
```

**No dependencies:**
- Specification Pattern can be added anytime (independent of other phases)

## Research Flags for Phases

**Phase 1 (Strongly-Typed IDs): Low research needs**
- Standard patterns, unlikely to need research
- Main concern: EF Core converter registration (ConfigureConventions)

**Phase 2 (Value Objects): Low research needs**
- Standard patterns, same Vogen infrastructure as Phase 1
- Main concern: Validation logic placement (Validate method, not application service)

**Phase 3 (Result Pattern): Medium research needs**
- Endpoint integration patterns may need research (mapping ErrorOr to HTTP responses)
- Consider FluentResults if hierarchical errors needed (not identified in current research)

**Phase 4 (Domain Events): High research needs**
- Event dispatch timing is critical (SaveChangesInterceptor implementation)
- Test failure scenarios (SaveChanges fails, events should not dispatch)
- Integration with existing outbox pattern (domain events → integration events)

**Phase 5 (Specifications): Low research needs**
- Standard patterns, Ardalis.Specification well-documented
- Main concern: SQL translation (verify IQueryable, not client-side filtering)

**Phase 6 (Rich Aggregates): High research needs**
- Aggregate boundary identification requires domain analysis
- Invariant modeling varies by aggregate (DcaConfig vs Purchase vs DailyPrice)
- Consider domain expert consultation for complex aggregates

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Vogen, ErrorOr, Ardalis.Specification versions verified from NuGet and GitHub. EF Core 10 compatibility confirmed. |
| Features | HIGH | Table stakes (IDs, value objects, Result pattern, events) vs differentiators (specifications, rich aggregates) clearly identified. Anti-features documented. |
| Architecture | HIGH | Layered structure patterns verified from Microsoft docs and community best practices. Integration with existing MediatR/EF Core confirmed. |
| Pitfalls | HIGH | Critical pitfall (event dispatch timing) verified from multiple sources. Mitigation strategies documented with code examples. |

**Confidence Rationale:**
- All package versions verified from official NuGet sources (2026-02-14)
- Vogen EF Core 10 integration verified from official documentation
- ErrorOr .NET 10 compatibility verified (2.0.1 stable, ErrorOr.Core 1.0.1 .NET 10 optimized)
- Ardalis.Specification 9.3.1 supports EF Core 9+ (works with EF Core 10)
- Domain event dispatch patterns verified from Microsoft architecture guidance and multiple community sources
- SaveChangesInterceptor approach verified from EF Core official documentation

**Low Confidence Areas (None Identified):**
- No library compatibility issues identified
- No version conflicts identified
- No missing features identified for DDD tactical patterns

## Gaps to Address

### Areas Where Research Was Inconclusive

1. **ErrorOr vs FluentResults Performance:** No 2026 benchmarks available comparing ErrorOr and FluentResults performance. Both are struct-based and avoid allocations. Recommendation stands (ErrorOr for simplicity, FluentResults for rich error metadata) but actual performance difference not quantified.

2. **EF Core 10 Value Converter Performance:** No specific EF Core 10 benchmarks for Vogen-generated value converters. Based on source generation approach (compile-time, zero runtime overhead), performance should be identical to manual converters, but not verified with data.

3. **Specification Pattern N+1 Query Detection:** Research confirmed specifications translate to IQueryable (server-side), but didn't find tooling recommendations for detecting N+1 queries in specifications. Consider EF Core logging or MiniProfiler integration.

### Topics Needing Phase-Specific Research Later

1. **Phase 4 (Domain Events): Integration Event Conversion**
   - How to convert domain events to integration events for outbox pattern
   - Which domain events should become integration events (all or subset)
   - Event schema versioning if cross-service integration required

2. **Phase 4 (Domain Events): Event Handler Orchestration**
   - If multiple handlers for same event, execution order guarantees
   - Error handling in event handlers (one handler fails, do others still execute)
   - Transactional semantics for handler DB operations

3. **Phase 6 (Rich Aggregates): Aggregate Boundary Analysis**
   - Is Purchase a valid aggregate root or should it be part of DcaStrategy aggregate
   - Is DcaConfig an aggregate or just an entity (depends on invariants)
   - Invariant analysis per aggregate (which business rules require transaction boundaries)

4. **Future: CQRS Read Models**
   - If query performance becomes issue, research read model patterns
   - When to denormalize for query optimization
   - Read model synchronization strategies (domain events vs database views)

### Recommended Next Steps

1. **Immediate (Before Starting Phase 1):**
   - Review existing entities (Purchase, DailyPrice, DcaConfig, IngestionJob)
   - Identify all primitive IDs that should become strongly-typed (PurchaseId, DailyPriceId, etc.)
   - List all domain primitives that need value objects (Price, Quantity, Symbol)

2. **Phase 1 Planning:**
   - Create proof-of-concept with one entity (Purchase) to validate Vogen integration
   - Test EF Core migration (verify Guid columns unchanged, only C# types change)
   - Verify JSON serialization works with dashboard endpoints

3. **Phase 4 Planning (Before Implementation):**
   - Research SaveChangesInterceptor failure scenarios (what happens if handler fails)
   - Design integration event conversion strategy (domain event → outbox message)
   - Test event dispatch timing with unit tests (SaveChanges fails, events not dispatched)

4. **Phase 6 Planning (Before Implementation):**
   - Conduct domain analysis session to identify aggregate boundaries
   - List invariants per entity (which rules require transaction boundaries)
   - Consider domain expert consultation for complex business rules (DCA strategy invariants)

## Decision Points for Roadmap Creator

### Must Decide Before Starting

1. **ErrorOr vs FluentResults:**
   - Recommendation: ErrorOr (simpler, zero-allocation, .NET 10 optimized)
   - Alternative: FluentResults if hierarchical error chains needed
   - Decision needed: Does trading bot need rich error metadata or simple error types sufficient?

2. **Specification Pattern in MVP:**
   - Recommendation: Add in Phase 5 (after core patterns stabilize)
   - Alternative: Skip entirely if queries stay simple
   - Decision needed: Are current queries complex enough to justify specifications?

3. **Rich Aggregates Scope:**
   - Recommendation: Start with DcaConfig (has complex invariants), skip DailyPrice (simple entity)
   - Decision needed: Which aggregates need rich behavior vs staying anemic?

### Can Defer to Phase Planning

1. **Domain Event → Integration Event Mapping:** Decide during Phase 4 which domain events become integration events
2. **Read Model Strategy:** Defer until query performance issues identified
3. **Smart Enums:** Add if workflow state machines needed (OrderStatus, JobStatus)

## Success Criteria for Research Completion

- [x] Domain ecosystem surveyed
- [x] Technology stack recommended with rationale (Vogen, ErrorOr, Ardalis.Specification)
- [x] Feature landscape mapped (table stakes: IDs, value objects, Result pattern, events)
- [x] Architecture patterns documented (layered DDD, SaveChangesInterceptor, event flow)
- [x] Domain pitfalls catalogued (event dispatch timing, cross-aggregate transactions, converter registration)
- [x] Source hierarchy followed (NuGet official, GitHub, Microsoft docs, community articles)
- [x] All findings have confidence levels (HIGH overall, gaps identified)
- [x] Output files created in .planning/research/
  - STACK-ddd-tactical-patterns.md
  - FEATURES-ddd-tactical-patterns.md
  - ARCHITECTURE-ddd-tactical-patterns.md
  - PITFALLS-ddd-tactical-patterns.md
  - SUMMARY-ddd-tactical-patterns.md (this file)
- [x] SUMMARY.md includes roadmap implications (6 phases, ordering rationale, dependencies)
- [x] Structured return ready for orchestrator

## Files Created

| File | Purpose | Key Content |
|------|---------|-------------|
| STACK-ddd-tactical-patterns.md | Technology recommendations | Vogen 8.0.4, ErrorOr 2.0.1, Ardalis.Specification.EntityFrameworkCore 9.3.1. Integration patterns, installation, migration strategy. |
| FEATURES-ddd-tactical-patterns.md | Feature landscape | Table stakes (IDs, value objects, Result pattern, events), differentiators (specifications, rich aggregates), anti-features (generic repository, event sourcing). |
| ARCHITECTURE-ddd-tactical-patterns.md | Architecture patterns | Layered DDD structure, component boundaries, data flow diagrams, patterns to follow (rich aggregates, value objects, domain services, specifications, Result pattern, event dispatch). |
| PITFALLS-ddd-tactical-patterns.md | Domain pitfalls | Critical: event dispatch timing, converter registration, cross-aggregate transactions. Moderate: validation placement, endpoint integration, client-side filtering. Testing recommendations. |
| SUMMARY-ddd-tactical-patterns.md | Executive summary with roadmap implications | 6-phase roadmap, ordering rationale, research flags, confidence assessment, gaps to address, decision points. |

## Roadmap Creator Next Steps

1. **Review research files** to understand DDD tactical patterns domain
2. **Use phase structure** from this summary (6 phases, dependencies documented)
3. **Reference PITFALLS.md** for critical risks (especially Phase 4 event dispatch timing)
4. **Consult STACK.md** for specific library versions and integration code examples
5. **Check FEATURES.md** for table stakes vs differentiators (prioritization guidance)
6. **Use ARCHITECTURE.md** for layered structure and component boundaries

**DO NOT commit research files** — orchestrator will commit after all research threads complete.

---
*Research summary for DDD Tactical Patterns in .NET 10.0 Trading Bot*
*Researched: 2026-02-14*
*Confidence: HIGH*
*Ready for roadmap creation*

# Feature Landscape: DDD Tactical Patterns

**Domain:** DDD Tactical Patterns for .NET 10.0 Trading Bot
**Researched:** 2026-02-14

## Table Stakes

Features users expect from DDD tactical patterns. Missing = domain model feels incomplete or unsafe.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Strongly-Typed IDs** | Type safety, prevents ID mix-ups (PurchaseId vs DailyPriceId) | Low | Vogen source generation makes this trivial. Zero runtime cost. |
| **Value Objects for Domain Primitives** | Encapsulates validation, prevents primitive obsession (Price, Quantity) | Low-Med | Vogen handles generation. Validation logic is domain complexity, not tech complexity. |
| **Immutability** | Value objects and IDs should be immutable | Low | Vogen generates readonly structs by default. C# records also work. |
| **EF Core Value Converters** | Persist value objects to database without manual mapping | Low | Vogen auto-generates. ConfigureConventions for global registration. |
| **JSON Serialization** | API DTOs should serialize value objects correctly | Low | Vogen includes System.Text.Json converters. Enable with Conversions flag. |
| **Result Pattern for Domain Operations** | Domain methods should return success/failure, not throw exceptions | Med | ErrorOr or FluentResults. Changes method signatures throughout application. |
| **Domain Event Raising** | Aggregates should raise events when state changes | Med | Requires event collection on BaseEntity, MediatR handlers. |
| **Domain Event Dispatch After SaveChanges** | Events dispatched only after DB commit (consistency guarantee) | Med | SaveChangesInterceptor to dispatch after successful commit. |

## Differentiators

Features that set a robust DDD implementation apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Specification Pattern** | Encapsulates query logic, testable without database | Med | Ardalis.Specification. Eliminates IQueryable leakage into application layer. |
| **Aggregate Root Enforcement** | Private setters, factory methods, invariant protection | Med-High | Requires rethinking entity design. High value for complex aggregates. |
| **Rich Domain Events** | Events carry domain data, not just IDs | Low-Med | Better than just "PurchaseCreatedEvent(Guid id)". Include relevant aggregate data. |
| **Domain Event Versioning** | Events have version numbers for schema evolution | High | Overkill for most projects. Valuable for event sourcing or cross-service integration. |
| **Outbox Pattern for Integration Events** | Transactional guarantee for cross-service events | High | Already present in trading bot. Keep for integration events, not domain events. |
| **Read Models Separate from Aggregates** | Optimized queries bypass aggregate loading | High | CQRS pattern. Consider for performance-critical reads, not MVP. |
| **Smart Enums** | Type-safe enums with behavior (OrderStatus.CanCancel()) | Low-Med | Thinktecture or Ardalis.SmartEnum. Good for workflow state machines. |
| **Discriminated Unions** | Type-safe alternatives (Either<Error, Success>) | Med | Thinktecture or OneOf. ErrorOr already provides this for error handling. |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Repository abstraction over EF Core** | Adds complexity without value. EF Core is already unit of work. | Use DbContext directly or thin repository for specifications. |
| **Generic Repository<T>** | Forces lowest-common-denominator queries. Hides EF Core features. | Entity-specific repositories or specifications. |
| **Domain Events via Message Bus (Dapr/RabbitMQ)** | Domain events are in-process. Integration events go external. | MediatR for domain events, Dapr for integration events. |
| **Event Sourcing for Aggregates** | Massive complexity. Only needed if you need full audit trail or temporal queries. | Traditional state-based persistence with domain events. |
| **Reflection-based Value Object Libraries** | Runtime overhead, slower than source generators. | Vogen (compile-time source generation). |
| **Manual Value Converters for Every Value Object** | Boilerplate, error-prone, easy to forget. | Vogen auto-generated converters with ConfigureConventions. |
| **Anemic Domain Services** | Logic in services defeats DDD purpose. Aggregates should have behavior. | Move logic into aggregate methods. Services coordinate aggregates. |
| **Cross-Aggregate Transactions** | Violates DDD boundaries. Causes coupling and performance issues. | Use domain events for eventual consistency across aggregates. |

## Feature Dependencies

```
Strongly-Typed IDs → EF Core Value Converters (IDs must persist)
Value Objects → EF Core Value Converters (Value objects must persist)
Value Objects → JSON Serialization (API must serialize/deserialize)
Domain Event Raising → Domain Event Dispatch (events must be processed)
Domain Event Dispatch → SaveChanges Interceptor (dispatch after commit)
Result Pattern → Minimal API Integration (endpoints must handle Result<T>)
Specification Pattern → EF Core IQueryable (specs translate to SQL)
```

## MVP Recommendation

Prioritize (Phase 1-3):
1. **Strongly-Typed IDs** (Vogen) — Quick win, prevents ID mix-ups, zero runtime cost
2. **Value Objects for Core Primitives** (Vogen for Price, Quantity) — Encapsulates validation
3. **Result Pattern for Domain Services** (ErrorOr) — Better error handling than exceptions
4. **Domain Events from Aggregates** (MediatR + SaveChangesInterceptor) — Loose coupling

Defer (Phase 4+):
- **Specification Pattern**: Add when query logic gets complex or you need testability without database
- **Smart Enums**: Add when workflow states need behavior (e.g., OrderStatus transitions)
- **Read Models**: Add when performance requires optimized query paths (CQRS)

Skip Entirely:
- **Event Sourcing**: Not needed for trading bot. State-based persistence is sufficient.
- **Generic Repository**: EF Core DbContext is sufficient. Adds no value.
- **Domain Event Versioning**: Overkill unless you're doing event sourcing or cross-service eventing at scale.

## Implementation Patterns by Aggregate

### Simple Aggregates (e.g., DailyPrice)
- Strongly-typed ID only
- Value objects for primitives (Price)
- Minimal domain events (if any)
- No complex invariants

### Medium Complexity (e.g., Purchase)
- Strongly-typed ID
- Value objects for domain primitives (Price, Quantity, Symbol)
- Domain events (PurchaseExecutedEvent)
- Simple invariants (price > 0, quantity > 0)

### Complex Aggregates (e.g., DcaConfig - if made rich)
- Strongly-typed ID
- Value objects for all primitives
- Multiple domain events (ConfigActivated, TierChanged, ScheduleUpdated)
- Complex invariants (tier thresholds must be ascending, daily amount > 0, schedule valid)
- Factory methods for creation
- Private setters for all state

## Sources

- [Microsoft DDD Microservices Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/) — DDD fundamentals
- [Value Objects in .NET](https://www.milanjovanovic.tech/blog/value-objects-in-dotnet-ddd-fundamentals) — Value object patterns
- [Domain Events with MediatR](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems) — Event patterns
- [Specification Pattern](https://deviq.com/design-patterns/specification-pattern/) — Query encapsulation
- [Aggregate Design Best Practices](https://codeopinion.com/aggregate-root-design-behavior-data/) — Aggregate patterns

---
*Feature landscape for DDD Tactical Patterns*
*Researched: 2026-02-14*

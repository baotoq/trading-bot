# Stack Research: DDD Tactical Patterns in .NET 10.0

**Domain:** DDD Tactical Patterns (Rich Aggregates, Value Objects, Strongly-Typed IDs, Result Pattern, Specification Pattern)
**Researched:** 2026-02-14
**Confidence:** HIGH

## Executive Summary

This stack research focuses ONLY on NEW dependencies needed to add DDD tactical patterns to the existing .NET 10.0 trading bot. The project already has .NET 10.0, EF Core 10, PostgreSQL, MediatR, Dapr, and transactional outbox pattern. We need to add:

1. **Vogen** for value objects and strongly-typed IDs (source generator approach)
2. **ErrorOr** for Result pattern (lightweight, zero allocation, .NET 10 optimized)
3. **Ardalis.Specification.EntityFrameworkCore** for Specification pattern
4. **NO new event infrastructure** (use existing MediatR + outbox pattern)

Key decision: Use source generators (Vogen) over reflection-based libraries for zero-overhead value objects and strongly-typed IDs in .NET 10.

## Recommended Stack

### Core DDD Libraries

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Vogen | 8.0.4 | Value objects and strongly-typed IDs via source generation | Industry standard for .NET 10 value objects. Zero runtime overhead, compile-time safety, built-in EF Core converters. Supports immutability, validation, and serialization out of the box. |
| ErrorOr | 2.0.1 | Result pattern for functional error handling | Lightweight discriminated union with zero allocations. Clean API, supports multiple errors, integrates seamlessly with minimal APIs. Alternative: ErrorOr.Core 1.0.1 (modernized for .NET 10 with fluent API). |
| Ardalis.Specification.EntityFrameworkCore | 9.3.1 | Specification pattern for encapsulating query logic | De facto standard for .NET DDD. Used in Microsoft eShopOnWeb and Clean Architecture templates. Translates specifications to IQueryable for optimal EF Core performance. |

### Supporting Infrastructure (Already Present - NO Action Needed)

| Library | Version | Purpose | Integration Notes |
|---------|---------|---------|-------------------|
| MediatR | 13.1.0 | Domain event dispatch (in-process) | Already configured. Use SaveChangesInterceptor to dispatch domain events after SaveChanges. |
| EF Core 10 | 10.0.0 | ORM with value converter support | Already configured. Vogen generates EF Core value converters automatically. Use ConfigureConventions for global registration. |
| Dapr + Outbox | 1.16.1 | Cross-service event publishing with transactional guarantees | Already configured. Keep existing outbox pattern for integration events. |

## Installation

```bash
# NEW packages for DDD tactical patterns
cd /Users/baotoq/Work/trading-bot/TradingBot.ApiService

# Value objects and strongly-typed IDs
dotnet add package Vogen --version 8.0.4

# Result pattern
dotnet add package ErrorOr --version 2.0.1

# Specification pattern
dotnet add package Ardalis.Specification.EntityFrameworkCore --version 9.3.1
```

## Integration Patterns

### 1. Vogen Value Objects and Strongly-Typed IDs

**For Strongly-Typed IDs (replace raw Guid):**

```csharp
// Domain/ValueObjects/PurchaseId.cs
[ValueObject<Guid>(conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson)]
public readonly partial struct PurchaseId
{
    // Vogen generates everything: equality, parsing, validation, EF converter, JSON converter
}
```

**For Value Objects (domain primitives):**

```csharp
// Domain/ValueObjects/Price.cs
[ValueObject<decimal>(conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson)]
public readonly partial struct Price
{
    private static Validation Validate(decimal value) =>
        value > 0
            ? Validation.Ok
            : Validation.Invalid("Price must be greater than zero");
}
```

**EF Core Configuration (two approaches):**

```csharp
// Approach 1: Auto-register all Vogen converters (recommended)
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.RegisterAllInVogenEfCoreConverters();
}

// Approach 2: Manual registration per entity (if you need fine-grained control)
modelBuilder.Entity<Purchase>()
    .Property(p => p.Id)
    .HasConversion<PurchaseId.EfCoreValueConverter>();
```

**Sources:**
- [Vogen NuGet](https://www.nuget.org/packages/vogen) — Version verification
- [Vogen EF Core Integration](https://stevedunn.github.io/Vogen/efcoreintegrationhowto.html) — Configuration patterns
- [Vogen GitHub](https://github.com/SteveDunn/Vogen) — Source generator implementation

### 2. ErrorOr Result Pattern

**Use in Domain Services:**

```csharp
// Domain/Services/MultiplierCalculator.cs
public static ErrorOr<decimal> CalculateMultiplier(Price currentPrice, Price averagePrice)
{
    if (currentPrice.Value <= 0)
        return Error.Validation("Price.Invalid", "Price must be greater than zero");

    if (averagePrice.Value <= 0)
        return Error.Validation("AveragePrice.Invalid", "Average price must be greater than zero");

    var dropPercentage = ((averagePrice.Value - currentPrice.Value) / averagePrice.Value) * 100;
    var multiplier = CalculateMultiplierFromDrop(dropPercentage);

    return multiplier;
}
```

**Minimal API Integration:**

```csharp
// Endpoints/DashboardEndpoints.cs
app.MapGet("/api/portfolio", async (IPortfolioService service) =>
{
    var result = await service.GetPortfolioAsync();

    return result.Match(
        value => Results.Ok(value),
        errors => Results.BadRequest(errors)
    );
});
```

**ErrorOr Types:**

```csharp
// Built-in error types
Error.Failure()        // Generic failure
Error.Unexpected()     // Unexpected error
Error.Validation()     // Validation failure
Error.Conflict()       // Conflict (e.g., concurrency)
Error.NotFound()       // Entity not found
Error.Unauthorized()   // Authorization failure
Error.Forbidden()      // Permission denied
```

**Sources:**
- [ErrorOr NuGet](https://www.nuget.org/packages/erroror) — Version 2.0.1
- [ErrorOr GitHub](https://github.com/amantinband/error-or) — API documentation
- [Result Pattern in .NET](https://www.milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern) — Implementation patterns

### 3. Ardalis.Specification Pattern

**Define Specifications:**

```csharp
// Application/Specifications/PurchasesByDateRangeSpec.cs
public class PurchasesByDateRangeSpec : Specification<Purchase>
{
    public PurchasesByDateRangeSpec(DateTime startDate, DateTime endDate)
    {
        Query
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
            .OrderByDescending(p => p.CreatedAt)
            .Include(p => p.Asset); // EF Core eager loading
    }
}

// Application/Specifications/ActiveDcaConfigSpec.cs
public class ActiveDcaConfigSpec : Specification<DcaConfig>, ISingleResultSpecification
{
    public ActiveDcaConfigSpec()
    {
        Query.Where(c => c.IsActive);
    }
}
```

**Use with EF Core:**

```csharp
// Infrastructure/Repositories/PurchaseRepository.cs
public async Task<List<Purchase>> GetPurchasesAsync(DateTime startDate, DateTime endDate)
{
    var spec = new PurchasesByDateRangeSpec(startDate, endDate);
    return await _dbContext.Purchases
        .WithSpecification(spec) // Extension from Ardalis.Specification.EntityFrameworkCore
        .ToListAsync();
}

public async Task<DcaConfig?> GetActiveConfigAsync()
{
    var spec = new ActiveDcaConfigSpec();
    return await _dbContext.DcaConfigs
        .WithSpecification(spec)
        .FirstOrDefaultAsync();
}
```

**Composable Specifications:**

```csharp
// Combine specifications
var spec = new PurchasesByDateRangeSpec(startDate, endDate)
    .And(new PurchasesByAssetSpec("BTC"));
```

**Sources:**
- [Ardalis.Specification.EntityFrameworkCore NuGet](https://www.nuget.org/packages/Ardalis.Specification.EntityFrameworkCore) — Version 9.3.1
- [Ardalis.Specification Docs](http://specification.ardalis.com/) — Usage patterns
- [Specification with EF Core](http://specification.ardalis.com/usage/use-specification-dbcontext.html) — DbContext integration
- [Ardalis.Specification GitHub](https://github.com/ardalis/Specification) — Source code and examples

### 4. Domain Events Dispatch (NO NEW PACKAGES)

**Use existing MediatR with SaveChangesInterceptor:**

```csharp
// Infrastructure/Data/DomainEventDispatchInterceptor.cs
public class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DomainEventDispatchInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var domainEntities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}

// Program.cs registration
builder.Services.AddDbContext<TradingBotDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
});

builder.Services.AddScoped<DomainEventDispatchInterceptor>();
```

**Domain Entity with Events:**

```csharp
// Models/BaseEntity.cs (UPDATE EXISTING)
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = NewId.Next().ToGuid();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

**Sources:**
- [Domain Events with EF Core](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) — Design patterns
- [SaveChanges Interceptor for Domain Events](https://mehmetozkaya.medium.com/dispatch-domain-events-w-ef-core-savechangesinterceptor-leads-to-integration-events-7943811f97b1) — Implementation approach
- [Domain Events with MediatR](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems) — MediatR integration

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Vogen | StronglyTypedId | If you ONLY need strongly-typed IDs (no value objects). StronglyTypedId is ID-focused, Vogen is broader (value objects + validation). |
| Vogen | Thinktecture.Runtime.Extensions | If you need discriminated unions or smart enums. Thinktecture offers more advanced features but higher complexity. |
| ErrorOr | FluentResults | If you need hierarchical error chains or elaborate error metadata. FluentResults has richer error modeling but more overhead. |
| Ardalis.Specification | Custom IQueryable extensions | If specifications feel like overkill for your simple queries. But specifications improve testability and reusability. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Reflection-based value object libraries | Runtime overhead, slower than source generators in .NET 10 | Vogen (compile-time source generation) |
| Exception-based error handling for domain logic | Expensive performance cost, poor control flow | ErrorOr or FluentResults (Result pattern) |
| Raw IQueryable in application layer | Leaks EF Core into domain/application, hard to test | Ardalis.Specification (encapsulates queries) |
| Manual EF Core value converters | Boilerplate code, easy to forget, error-prone | Vogen auto-generated converters |
| New domain event infrastructure | Duplicates existing MediatR + outbox pattern | Enhance existing infrastructure with SaveChangesInterceptor |

## Stack Patterns by Variant

**If using rich aggregates with complex invariants:**
- Use Vogen for value objects with validation
- Use ErrorOr for aggregate methods that can fail
- Raise domain events from aggregate methods
- Keep aggregate roots as transaction boundaries

**If using simple entities with minimal logic:**
- Use Vogen for strongly-typed IDs only
- Skip Result pattern (exceptions may suffice)
- Use specifications for query logic
- Domain events optional

**If migrating incrementally from anemic model:**
- Start with strongly-typed IDs (Vogen for entity IDs)
- Add value objects for domain primitives (Price, Quantity, etc.)
- Introduce Result pattern in new domain services
- Migrate queries to specifications gradually
- Add domain events for cross-aggregate communication

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Vogen 8.0.4 | .NET 10.0, EF Core 10 | Source generator works with any .NET version. EF Core converters work with EF Core 8+. |
| ErrorOr 2.0.1 | .NET 10.0 | Zero dependencies. ErrorOr.Core 1.0.1 is .NET 10 optimized variant with fluent API. |
| Ardalis.Specification.EntityFrameworkCore 9.3.1 | EF Core 9+, .NET 10.0 | Version 9.x targets EF Core 9+. Works with .NET 10.0. |
| MediatR 13.1.0 | .NET 10.0 | Already present. No version conflicts. |

**Important:** Vogen is a source generator, not a runtime dependency. It generates code at compile-time, so no runtime overhead or version conflicts.

## Performance Considerations

### Vogen (Value Objects and Strongly-Typed IDs)

- **Compile-time generation:** Zero runtime overhead
- **Struct-based:** Value semantics, stack allocation
- **No reflection:** Unlike reflection-based libraries (ValueOf, etc.)
- **EF Core integration:** Generated value converters translate to efficient SQL

### ErrorOr (Result Pattern)

- **Zero allocations:** Struct-based discriminated union
- **No exceptions:** Avoids expensive exception stack unwinding
- **Pattern matching:** Compile-time safety with C# pattern matching
- **Minimal API friendly:** Direct integration with Results.Ok/BadRequest

### Ardalis.Specification (Specification Pattern)

- **IQueryable translation:** Specifications compile to EF Core queries (not in-memory filtering)
- **Query optimization:** EF Core optimizer applies normal optimizations
- **Index usage:** Properly designed specs use database indexes
- **Eager loading:** Include() support for N+1 prevention

**Benchmark comparison (source generators vs reflection):**
Source generators (like Vogen) provide significantly better performance than reflection-based libraries. Compile-time generation eliminates runtime overhead.

## Migration Strategy

### Phase 1: Strongly-Typed IDs

1. Install Vogen 8.0.4
2. Create strongly-typed ID value objects (PurchaseId, DailyPriceId, etc.)
3. Update DbContext.ConfigureConventions to register Vogen converters
4. Create EF Core migration (ID columns stay as Guid in database, no schema change)
5. Update domain entities to use strongly-typed IDs
6. Update application services and endpoints

### Phase 2: Value Objects for Domain Primitives

1. Identify primitive obsession (decimal for Price, string for Symbol, etc.)
2. Create value objects with Vogen (Price, Quantity, Symbol, etc.)
3. Add validation rules in Validate method
4. Update domain entities to use value objects
5. Create EF Core migration (underlying types may change based on validation)
6. Update application services and endpoints

### Phase 3: Result Pattern for Error Handling

1. Install ErrorOr 2.0.1
2. Update domain services to return ErrorOr<T> instead of throwing exceptions
3. Update application services to handle Result types
4. Update minimal API endpoints to match on Result types
5. Remove try-catch blocks for domain logic (keep for infrastructure failures)

### Phase 4: Specification Pattern for Queries

1. Install Ardalis.Specification.EntityFrameworkCore 9.3.1
2. Identify complex queries with multiple filters/includes
3. Create specification classes in Application/Specifications
4. Update repositories to use WithSpecification extension
5. Move query logic from application services to specifications
6. Write unit tests for specifications (testable without database)

### Phase 5: Domain Events from Aggregates

1. Create DomainEventDispatchInterceptor (SaveChangesInterceptor)
2. Register interceptor in DbContext configuration
3. Update BaseEntity with domain event collection
4. Update aggregate methods to raise domain events
5. Create MediatR handlers for domain events
6. Keep existing outbox pattern for integration events (cross-service communication)

## Sources

**Value Objects and Strongly-Typed IDs:**
- [Vogen NuGet](https://www.nuget.org/packages/vogen) — Latest version 8.0.4
- [Vogen GitHub](https://github.com/SteveDunn/Vogen) — Source generator implementation
- [Vogen EF Core Integration](https://stevedunn.github.io/Vogen/efcoreintegrationhowto.html) — Configuration patterns
- [StronglyTypedId GitHub](https://github.com/andrewlock/StronglyTypedId) — Alternative approach
- [Andrew Lock: Strongly-Typed IDs in EF Core](https://andrewlock.net/strongly-typed-ids-in-ef-core-using-strongly-typed-entity-ids-to-avoid-primitive-obsession-part-4/) — Implementation patterns
- [Value Objects in .NET](https://www.milanjovanovic.tech/blog/value-objects-in-dotnet-ddd-fundamentals) — DDD fundamentals
- [Thinktecture Value Objects](https://www.thinktecture.com/en/net/value-objects-solving-primitive-obsession-in-net/) — Alternative library comparison

**Result Pattern:**
- [ErrorOr NuGet](https://www.nuget.org/packages/erroror) — Version 2.0.1
- [ErrorOr GitHub](https://github.com/amantinband/error-or) — API documentation
- [FluentResults NuGet](https://www.nuget.org/packages/FluentResults/) — Version 4.0.0 (alternative)
- [FluentResults GitHub](https://github.com/altmann/FluentResults) — Alternative implementation
- [Functional Error Handling in .NET](https://www.milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern) — Implementation patterns
- [Result Pattern in C#](https://antondevtips.com/blog/how-to-replace-exceptions-with-result-pattern-in-dotnet) — Best practices

**Specification Pattern:**
- [Ardalis.Specification.EntityFrameworkCore NuGet](https://www.nuget.org/packages/Ardalis.Specification.EntityFrameworkCore) — Version 9.3.1
- [Ardalis.Specification Docs](http://specification.ardalis.com/) — Official documentation
- [Ardalis.Specification GitHub](https://github.com/ardalis/Specification) — Source code
- [Specification with Repository Pattern](http://specification.ardalis.com/usage/use-specification-repository-pattern.html) — Usage patterns
- [Specification with DbContext](http://specification.ardalis.com/usage/use-specification-dbcontext.html) — EF Core integration
- [Getting Started with Specifications](https://blog.nimblepros.com/blogs/getting-started-with-specifications/) — Tutorial

**Domain Events:**
- [Domain Events Design and Implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) — Microsoft architecture guidance
- [SaveChanges Interceptor for Domain Events](https://mehmetozkaya.medium.com/dispatch-domain-events-w-ef-core-savechangesinterceptor-leads-to-integration-events-7943811f97b1) — Implementation approach
- [Domain Events with MediatR](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems) — MediatR integration
- [EF Core Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) — Official documentation
- [Domain-Driven Design with EF Core 8](https://thehonestcoder.com/ddd-ef-core-8/) — DDD patterns with EF Core

**EF Core Value Converters:**
- [EF Core Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions) — Official documentation
- [What's New in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) — EF Core 10 features
- [EF Core Value Converters Best Practices](https://oneuptime.com/blog/post/2026-01-30-ef-core-custom-value-converters/view) — Custom converters

---
*Stack research for: DDD Tactical Patterns in .NET 10.0*
*Researched: 2026-02-14*
*Confidence: HIGH (NuGet and official documentation sources verified)*

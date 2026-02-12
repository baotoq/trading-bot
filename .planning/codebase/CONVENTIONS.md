# Coding Conventions

**Analysis Date:** 2026-02-12

## Domain-Driven Design (DDD)

**Approach:** Follow DDD tactical patterns throughout the domain model layer.

**Strongly-Typed IDs:**
- All entity identifiers must be strongly-typed value objects, not primitive types (`Guid`, `int`, `string`)
- Example: `OrderId`, `StrategyId`, `UserId` instead of raw `Guid`
- Implement as `record struct` for zero-allocation value semantics
- Include EF Core value converters for database mapping

```csharp
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.CreateVersion7());
}
```

**Value Objects:**
- Model domain concepts that are defined by their attributes, not identity
- Examples: `Token` (symbol, chain, decimals), `Price`, `Quantity`, `TradingPair`, `Leverage`
- Implement as `record` or `record struct` for built-in equality
- Encapsulate validation in factory methods or constructors
- Must be immutable

```csharp
public record Token(string Symbol, string Chain, int Decimals)
{
    public static Token Create(string symbol, string chain, int decimals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        return new Token(symbol.ToUpperInvariant(), chain, decimals);
    }
}
```

**Entities:**
- Have a unique strongly-typed identity
- Encapsulate business logic and invariants
- Raise domain events for state changes
- Inherit from `BaseEntity` or `AuditedEntity`

**Aggregates:**
- Define transactional boundaries
- External references use strongly-typed IDs only (no navigation to other aggregates)
- Root entity controls all modifications within the aggregate

## Naming Patterns

**Files:**
- Type names match file names exactly: `OutboxMessage.cs` contains class `OutboxMessage`
- Interface files use I prefix pattern: `IOutboxStore.cs`, `IMessageBroker.cs`, `IEventPublisher.cs`
- Service collection extensions use suffix: `ServiceCollectionExtensions.cs`
- Abstract base classes prefixed descriptively: `BaseEntity.cs`, `AuditedEntity.cs`

**Functions:**
- PascalCase for all public methods: `AddAsync()`, `PublishAsync()`, `ExecuteAsync()`
- Async methods suffixed with `Async`: `PublishAsync()`, `ProcessAsync()`, `GetUnprocessedAsync()`
- Generic method parameter names are descriptive: `AddAsync<T>(T @event, ...)` where T is the event type
- Private methods also PascalCase: `ConfigureOutboxOptions()`, `AddOpenTelemetryExporters()`

**Variables:**
- camelCase for local variables and parameters: `outboxStore`, `logger`, `cancellationToken`, `message`
- CancellationToken parameter consistently named `cancellationToken`
- Private fields use underscore prefix pattern (not commonly used, codebase favors immutable record/init properties)
- Init-only properties common for immutable domain objects: `public Guid Id { get; init; }`

**Types:**
- PascalCase for classes, interfaces, enums, records: `OutboxMessage`, `IOutboxStore`, `ProcessingStatus`
- Interface prefix I required: All public interfaces start with I
- Record types for data transfer: `record IntegrationEvent`, `record OutboxProcessorOptions`
- Abstract classes use `abstract` keyword: `abstract class BaseEntity`, `abstract class AuditedEntity`
- Enums have numeric values aligned: `Pending = 0`, `Processing = 1`, `Published = 2`, `Failed = 3`

## Code Style

**Formatting:**
- EditorConfig file at root: `.editorconfig`
- Indentation: 4 spaces for C# files, 2 spaces for `.csproj` files
- Max line length: 120 characters for C# files
- LF line endings with trailing whitespace trimmed
- No final newline after file end

**Linting:**
- .editorconfig enforces consistent styles
- Compiler nullability enabled: `<Nullable>enable</Nullable>` in all projects
- Implicit usings enabled: `<ImplicitUsings>enable</ImplicitUsings>` in all projects

**Key Editor Config Rules:**
- `csharp_style_namespace_declarations = file_scoped` - Use file-scoped namespaces
- `dotnet_style_readonly_field = true` - Prefer readonly fields
- `csharp_prefer_primary_constructors = true` - Use primary constructors where possible
- `dotnet_style_prefer_auto_properties = true` - Prefer auto properties
- `csharp_style_implicit_object_creation_when_type_is_apparent = true` - Use implicit object creation
- Expression-bodied members for properties and accessors only (not methods or constructors)

## Import Organization

**Order:**
1. System and Microsoft namespaces
2. Third-party library namespaces
3. Project-specific namespaces
4. No sorting within groups

**Path Aliases:**
- None detected; fully qualified namespaces used throughout

**Example from `OutboxEventPublisher.cs`:**
```csharp
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;
```

## Error Handling

**Patterns:**
- Try-catch used in background services and message processors
- Errors logged with contextual data: `logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id)`
- Graceful degradation: Processing continues even if individual message fails
- Retry logic for transient failures: `await outboxStore.IncrementRetryAsync()` with max retry count check
- Max retry count enforced before giving up: `if (message.RetryCount >= 3)` before marking as failed

**Example from `OutboxMessageProcessor.cs`:**
```csharp
try
{
    if (message.RetryCount >= 3)
    {
        logger.LogWarning("Message {MessageId} exceeded max retry count, skipping", message.Id);
        await outboxStore.MarkAsAsync(message.Id, ProcessingStatus.Failed, cancellationToken);
        return;
    }
    // Processing logic
}
catch (Exception ex)
{
    logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
    await outboxStore.IncrementRetryAsync(message.Id, cancellationToken);
}
```

## Logging

**Framework:** Serilog with templated expressions

**Configuration File:** `appsettings.json` defines minimum level and component overrides

**Patterns:**
- Structured logging with named placeholders: `logger.LogInformation("{BackgroundService} started iteration", GetType().Name)`
- Context added via `BeginScope()` with dictionary: `logger.BeginScope(new Dictionary<string, object?> { ["BackgroundService"] = ... })`
- Log levels: Information for normal flow, Warning for retries, Error for failures
- Template includes class name and relevant identifiers

**Example from `TimeBackgroundService.cs`:**
```csharp
logger.BeginScope(new Dictionary<string, object?>()
{
    ["BackgroundService"] = GetType().FullName,
    ["Interval"] = Interval
});
logger.LogInformation("{BackgroundService} is starting with {Interval}", GetType().Name, Interval);
```

## Comments

**When to Comment:**
- XML doc comments on public interface methods (not observed in current codebase examples)
- Inline comments for non-obvious logic (minimal usage observed)
- Code structure is self-documenting through clear naming

**JSDoc/TSDoc:**
- Not applicable (C# project)
- Standard XML doc comment syntax available but not heavily used in this codebase

## Function Design

**Size:**
- Most functions 5-20 lines
- Background service `ExecuteAsync()` uses try-catch-finally with while loop: ~30 lines

**Parameters:**
- Constructor injection preferred for dependencies: `OutboxEventPublisher(IOutboxStore outboxStore, JsonSerializerOptions jsonSerializerOptions)`
- Primary constructors used: `public class DaprMessageBroker(PubSubRegistry registry, DaprClient daprClient)`
- CancellationToken always optional parameter with default: `CancellationToken cancellationToken = default`
- No positional parameters beyond type constraint for generics

**Return Values:**
- Async methods return `Task` or `Task<T>`: `async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)`
- Void returns avoided, use `Task` instead
- `ValueTask` used for simple async patterns: `ValueTask DisposeAsync()` in `LockResponse`

## Module Design

**Exports:**
- Public interfaces at namespace level: `IOutboxStore`, `IMessageBroker`, `IDomainEvent`
- Static extension classes for service registration: `ServiceCollectionExtensions` with `AddDaprPubSub()`, `AddOutboxPublishingWithEfCore()`
- Abstract classes exported for inheritance: `BaseEntity`, `AuditedEntity`, `TimeBackgroundService`

**Barrel Files:**
- Not used; direct namespace imports preferred
- Each file contains single responsibility

## Dependency Injection

**Registration Pattern:**
- `ServiceCollectionExtensions` static classes register dependencies
- Scoped services for request-specific instances: `.AddScoped<IOutboxStore>(...)`
- Singleton for shared instances: `.AddSingleton(new JsonSerializerOptions { ... })`
- HostedService registration: `.AddHostedService<OutboxMessageBackgroundService>()`

**Example from `ServiceCollectionExtensions.cs` in Outbox:**
```csharp
services.AddHostedService<OutboxMessageBackgroundService>();
services.AddScoped<IOutboxStore>(sp => new EfCoreOutboxStore(sp.GetRequiredService<TDbContext>()));
services.AddScoped<IEventPublisher, OutboxEventPublisher>();
services.AddScoped<IOutboxMessageProcessor, OutboxMessageProcessor>();
services.AddSingleton(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
```

## Async/Await Patterns

**Async Methods:**
- All I/O operations are async: database, messaging, HTTP
- Async void avoided (using Task instead)
- `ConfigureAwait(false)` not used (not ASP.NET library code)
- `using` declarations with async disposal: `await using var scope = services.CreateAsyncScope()`

**Example from `OutboxMessageBackgroundService.cs`:**
```csharp
await using var scope = services.CreateAsyncScope();
var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
```

---

*Convention analysis: 2026-02-12*

# Pitfalls Research

**Domain:** Adding DDD Tactical Patterns to Existing .NET Application with EF Core
**Researched:** 2026-02-14
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: EF Core Change Tracking Breaks with Private Setters and Backing Fields

**What goes wrong:**
When refactoring entities to use private setters and backing fields for encapsulation, EF Core's change tracking silently breaks. Properties with private setters work, but collection navigation properties using `IReadOnlyList<T>` with backing fields cause lazy loading to fail—queries return empty lists instead of loading related entities.

**Why it happens:**
EF Core's proxy-based lazy loading requires virtual properties it can override at runtime. When using backing fields with `IReadOnlyList<T>` public properties, EF Core cannot intercept access to the private backing field. The framework sees a property with a private setter as read-write for mapping purposes, but collections require special handling that backing fields break.

**How to avoid:**
1. **For scalar properties**: Private setters are safe—use them freely for encapsulation
2. **For collections**: Choose ONE of these approaches consistently:
   - Use explicit loading via `dbContext.Entry(entity).Collection(e => e.Items).Load()`
   - Use eager loading with `.Include()` in repositories
   - Use `ILazyLoader` injected into entity constructors (doesn't require virtual properties)
   - Keep collections as `virtual ICollection<T>` if lazy loading is essential (sacrifice encapsulation)
3. **Document the choice** in architectural decision records—mixing approaches creates debugging nightmares

**Warning signs:**
- Tests pass with in-memory database but fail with real PostgreSQL
- Collections are null or empty when they shouldn't be
- Related entities exist in database but navigation properties are empty
- No lazy loading errors/warnings in logs (fails silently)

**Phase to address:**
Phase 1: Rich Domain Model - Establish collection loading strategy before implementing aggregates

---

### Pitfall 2: Breaking Database Migrations by Renaming Value Object Properties

**What goes wrong:**
When refactoring primitive properties (like `decimal Price`) into value objects (like `Money Price { get; private set; }`), EF Core generates migrations that DROP the old column and ADD a new one, destroying existing production data. Even worse, if the value object has multiple properties, the migration creates entirely new column structures.

**Why it happens:**
EF Core doesn't understand semantic equivalence—it only sees that `decimal Price` disappeared and a new complex type appeared. Without explicit mapping configuration, the migration scaffolder assumes these are separate entities. The scaffolder warns about "potential data loss" but developers often miss this in migration files.

**How to avoid:**
1. **Before any value object refactoring**:
   - Create a transition migration that renames columns explicitly
   - Use `modelBuilder.Entity<Purchase>().OwnsOne(p => p.Price, pb => pb.Property(m => m.Amount).HasColumnName("Price"))` to map value objects to existing columns
   - For EF Core 8+, use `ComplexProperty` instead of `OwnsOne` for true value objects
2. **Never trust auto-generated migrations**—always inspect the SQL before applying
3. **Use EF Core value converters** for single-property value objects to preserve column structure
4. **Test migrations against a database copy with production-like data** before applying

**Warning signs:**
- Migration file contains `DropColumn` or `AlterColumn` operations
- Migration preview SQL shows column drops
- EF Core warns about "potential data loss" during migration scaffolding
- Column names change from simple names to prefixed names (e.g., `Price` becomes `Price_Amount`)

**Phase to address:**
Phase 2: Value Objects - Test migration path with existing data before implementing

---

### Pitfall 3: Strongly-Typed IDs Break Existing Foreign Keys and Indexes

**What goes wrong:**
Converting `Guid Id` to strongly-typed IDs like `PurchaseId Id` requires custom value converters in EF Core. Without them, migrations generate entirely new columns with different types, breaking foreign key constraints, indexes, and requiring data migration scripts. Existing queries using `.Where(p => p.Id == guid)` fail to compile.

**Why it happens:**
EF Core doesn't automatically know how to convert strongly-typed ID structs/records to database primitives. The default behavior is to treat them as complex types requiring separate columns. Even with value converters configured, ALL code using the entity's ID must be updated simultaneously—partial migration isn't possible.

**How to avoid:**
1. **Use value converters from the start**:
   ```csharp
   modelBuilder.Entity<Purchase>()
       .Property(p => p.Id)
       .HasConversion(
           id => id.Value,
           value => new PurchaseId(value)
       );
   ```
2. **Implement a global `IValueConverterSelector`** to automatically apply converters for all strongly-typed IDs
3. **Use source generators** (like StronglyTypedId or Vogen library) to auto-generate value converters
4. **Create a no-op migration first** to verify the schema doesn't change
5. **Update all query code in same commit** as entity changes

**Warning signs:**
- Compiler errors on queries: `Cannot convert Guid to PurchaseId`
- Migration creates new columns instead of reusing existing ones
- Foreign key constraint errors during migration
- Index definitions change or disappear
- Existing database queries (SQL scripts, views) break

**Phase to address:**
Phase 3: Strongly-Typed IDs - Configure value converters BEFORE changing entity properties

---

### Pitfall 4: Duplicate Domain Event Publication via SaveChanges Interceptor and Outbox

**What goes wrong:**
When implementing domain events with both a SaveChanges interceptor (to collect events) and an outbox pattern (for reliable publishing), events get published TWICE—once immediately after SaveChanges completes and once when the outbox processor runs. This causes duplicate order executions, duplicate notifications, and data inconsistencies.

**Why it happens:**
Two competing approaches for domain events:
1. **Interceptor approach**: Collects events before SaveChanges, publishes after commit
2. **Outbox approach**: Stores events as OutboxMessages during SaveChanges, publishes via background job

Your codebase already has an outbox pattern (`OutboxMessage`, `OutboxMessageProcessor`), so adding an interceptor that also publishes creates a race condition. The interceptor might publish before the outbox processor, causing handlers to run twice with different timing.

**How to avoid:**
1. **Choose ONE dispatch mechanism**:
   - **Option A (Recommended)**: Keep outbox-only—let entities raise events, interceptor converts to `OutboxMessage`, background service publishes
   - **Option B**: Use interceptor-only for immediate consistency, remove outbox (loses reliability)
   - **Never mix both** for the same event types
2. **Clear separation of concerns**:
   - Entities raise domain events (in-memory collection)
   - Interceptor converts domain events to outbox messages (persistence)
   - Outbox processor publishes to MediatR/Dapr (infrastructure)
3. **Add deduplication checks** in event handlers using idempotency keys

**Warning signs:**
- Duplicate entries in outbox table for same domain event
- Event handlers execute twice for single entity change
- Logs show same event published at different timestamps
- Race conditions in integration tests
- Inconsistent event ordering (some handlers see events in wrong order)

**Phase to address:**
Phase 4: Domain Events - Design event dispatch architecture before implementing on entities

---

### Pitfall 5: Oversized Aggregates Cause Performance and Concurrency Issues

**What goes wrong:**
When refactoring anemic entities to rich aggregates, developers naturally group all related entities together (e.g., `Purchase` aggregate with `PriceData`, `Configuration`, `Order` as child entities). This creates aggregates so large that loading them requires expensive JOINs, updates cause frequent optimistic concurrency conflicts, and transactions become slow.

**Why it happens:**
Developers apply the DDD rule "maintain consistency boundaries" too broadly, assuming all entities that interact must be in the same aggregate. The mistake is thinking about technical relationships (foreign keys) rather than business invariants. Not all consistency needs to be immediate—eventual consistency via domain events often works better.

**How to avoid:**
1. **Identify true invariants**: Only group entities that must be transactionally consistent (e.g., `Purchase.Quantity * Purchase.Price must equal Purchase.Cost`)
2. **Keep aggregates small**: In your trading bot, each `Purchase` is likely its own aggregate—it doesn't need to own `DailyPrice` or `DcaConfiguration`
3. **Use eventual consistency for cross-aggregate rules**: If updating `Purchase` should trigger recalculation elsewhere, raise a domain event instead of loading everything into one aggregate
4. **Repository pattern per aggregate root**: One repository per aggregate prevents accidental over-loading
5. **Performance test early**: Load 1000+ aggregates in a tight loop—if it's slow, the aggregate is too big

**Warning signs:**
- Single entity query generates 10+ JOIN statements
- Optimistic concurrency exceptions (`DbUpdateConcurrencyException`) during moderate load
- SaveChanges takes >100ms for simple property updates
- Aggregate root has >5 collection navigation properties
- Loading an aggregate requires `.Include().ThenInclude().ThenInclude()` chains

**Phase to address:**
Phase 1: Rich Domain Model - Define aggregate boundaries based on invariants, not convenience

---

### Pitfall 6: Result Pattern Migration Creates Inconsistent Error Handling

**What goes wrong:**
When gradually migrating from exceptions to Result pattern, you end up with a codebase where some methods return `Result<T>`, others throw exceptions, and others return `null` for errors. Callers don't know which error handling approach to use, leading to swallowed exceptions, unhandled Results, and inconsistent logging.

**Why it happens:**
Incremental migration seems pragmatic ("we'll convert methods as we touch them"), but creates long-lived inconsistency. Library code (EF Core, HttpClient, Dapr) still throws exceptions, so you need exception handling anyway. Mixing patterns means every method call requires checking "does this throw or return Result?"

**How to avoid:**
1. **Define error handling layers**:
   - **Domain layer**: Use Result pattern for business rule violations
   - **Infrastructure layer**: Wrap exceptions in Results using `Result.Try()`
   - **API endpoints**: Convert Results to HTTP responses, let exception middleware handle unexpected errors
2. **Never mix within same layer**: If a domain service uses Results, ALL domain services use Results
3. **Use Result.Try() for gradual migration**:
   ```csharp
   public Result<Purchase> CreatePurchase()
   {
       return Result.Try(() =>
       {
           // Existing code that might throw
           var purchase = new Purchase(...);
           _dbContext.Purchases.Add(purchase);
           _dbContext.SaveChanges(); // might throw
           return purchase;
       });
   }
   ```
4. **Add analyzer rules** to enforce Result return types in domain layer

**Warning signs:**
- Methods with both `Result<T>` return type AND exception documentation
- Try-catch blocks wrapping Result-returning methods
- Results returned but never checked (`.IsSuccess` not called)
- Inconsistent logging (some errors logged in catch blocks, others in Result handlers)
- API endpoints with both `try-catch` and `if (result.IsFailure)` patterns

**Phase to address:**
Phase 5: Result Pattern - Define error handling architecture for all layers before converting methods

---

### Pitfall 7: Specification Pattern Queries Cause N+1 Problems and Memory Issues

**What goes wrong:**
Implementing the Specification pattern with `Func<T, bool>` instead of `Expression<Func<T, bool>>` forces EF Core to load ALL entities into memory before filtering. A seemingly simple query like `repository.Find(new ActivePurchasesSpec())` loads every Purchase from the database, then filters in .NET—causing OutOfMemory exceptions and killing performance.

**Why it happens:**
Developers copy Specification pattern examples from generic DDD tutorials without understanding the EF Core-specific implementation. The signature `Func<T, bool>` compiles fine and even works in unit tests with in-memory collections, but becomes a performance disaster with real databases. The subtle difference between `Func` (delegate executed in .NET) and `Expression` (tree that EF translates to SQL) is easy to miss.

**How to avoid:**
1. **Always use `Expression<Func<T, bool>>`** for specification criteria:
   ```csharp
   public class ActivePurchasesSpec : Specification<Purchase>
   {
       public override Expression<Func<Purchase, bool>> ToExpression()
           => p => p.Status == PurchaseStatus.Filled;
   }
   ```
2. **Include performance flags in specifications**:
   - `.AsNoTracking()` for read-only queries
   - `.AsSplitQuery()` for specs with multiple includes
3. **Test specifications with real database** containing 10k+ rows—in-memory tests won't catch this
4. **Add includes to specification pattern** to handle eager loading within spec
5. **Monitor generated SQL** using EF Core logging during development

**Warning signs:**
- Query logs show `SELECT * FROM Purchases` without WHERE clause
- High memory usage during query execution
- Queries that should be fast (simple WHERE clause) take seconds
- EF Core not logging the WHERE clause in SQL
- Performance difference between development (small DB) and production

**Phase to address:**
Phase 6: Specification Pattern - Enforce Expression usage from first implementation

---

### Pitfall 8: Anemic-to-Rich Migration Breaks Existing Service Layer Logic

**What goes wrong:**
When moving business logic from service classes into rich entities, existing service methods become broken or duplicated. For example, if `DcaExecutionService.CalculateMultiplier()` moves to `Purchase.CalculateMultiplier()`, all existing code calling the service breaks. Worse, if both exist temporarily, they can diverge, creating different results.

**Why it happens:**
Refactoring from anemic to rich is a large-scale architectural change that can't be done atomically. The service layer has dozens of references throughout the codebase—controllers, background jobs, event handlers, tests. During migration, you need both versions to coexist, leading to duplicated logic and uncertainty about which to call.

**How to avoid:**
1. **Strangler Fig pattern**:
   - Add new rich methods on entities
   - Refactor service methods to delegate to entity methods internally
   - Gradually move callers to use entities directly
   - Remove service methods last (after all callers moved)
2. **Start with new features**: Implement new domain operations as rich methods, leave existing anemic code alone
3. **Use feature flags** for risky migrations to enable gradual rollout
4. **Write characterization tests** before refactoring to ensure behavior doesn't change
5. **Never duplicate business logic**—service should either own logic or delegate, not both

**Warning signs:**
- Same business logic in both service and entity
- Unit tests for both service method and entity method testing same behavior
- Bug fixes require updating multiple places
- Uncertainty about which method to call in new code
- Method marked `[Obsolete]` but still widely used

**Phase to address:**
Phase 1: Rich Domain Model - Plan migration strategy before moving any logic

---

### Pitfall 9: Value Object Equality Impacts EF Core Change Tracking Performance

**What goes wrong:**
Creating value objects with complex equality logic (deep comparison of nested objects, collection comparisons) causes EF Core change tracking to perform expensive comparisons on EVERY SaveChanges call. For aggregates with multiple value objects, this can make SaveChanges 10x slower, especially with collections like `List<MultiplierTier>`.

**Why it happens:**
EF Core's default change tracking creates a snapshot of each entity after loading and compares it to current state before SaveChanges. For value objects, EF uses the equality implementation you provide. If `Money.Equals()` does deep comparison or `Address.Equals()` compares all fields, EF calls this for every tracked entity. Developers implement thorough equality without considering performance implications.

**How to avoid:**
1. **Make value objects immutable** (classes or readonly structs)—EF can use reference comparison for immutable types
2. **Use value comparers explicitly** in EF Core configuration:
   ```csharp
   modelBuilder.Entity<Purchase>()
       .Property(p => p.Price)
       .HasConversion(
           m => m.Amount,
           v => new Money(v)
       )
       .Metadata.SetValueComparer(
           new ValueComparer<Money>(
               (m1, m2) => m1.Amount == m2.Amount,  // Fast comparison
               m => m.Amount.GetHashCode(),         // Fast hashing
               m => new Money(m.Amount)             // Fast snapshot
           ));
   ```
3. **Avoid collection value objects** unless absolutely necessary—use owned entities instead
4. **Use `.AsNoTracking()` for read-only queries** to skip change tracking entirely
5. **Profile SaveChanges** with large change sets to catch performance issues early

**Warning signs:**
- SaveChanges takes significantly longer with more tracked entities (should be roughly linear)
- CPU profiling shows time spent in equality comparisons during SaveChanges
- Unexpected behavior: changing a value object doesn't trigger EF Core update
- Value object changes not persisted to database

**Phase to address:**
Phase 2: Value Objects - Configure value comparers during implementation, not as optimization later

---

### Pitfall 10: Repository Abstraction Leaks EF Core Through IQueryable

**What goes wrong:**
Implementing repositories that return `IQueryable<T>` to avoid writing specific query methods seems flexible, but leaks EF Core implementation details throughout the application. Callers add `.Include()`, `.AsNoTracking()`, and other EF-specific methods, making it impossible to switch ORMs and coupling domain layer to infrastructure.

**Why it happens:**
Generic repository pattern with `IQueryable<T> GetAll()` is taught in many tutorials as "flexible" because it avoids N+1 query methods. But this violates repository pattern principles—the repository should expose domain operations (like `GetActivePurchases()`) not query mechanisms. EF Core's `IQueryable` is a leaky abstraction.

**How to avoid:**
1. **Specific repository methods** using domain language:
   ```csharp
   public interface IPurchaseRepository
   {
       Task<Purchase?> GetByIdAsync(PurchaseId id);
       Task<List<Purchase>> GetActivePurchasesAsync();
       Task<List<Purchase>> GetPurchasesInDateRangeAsync(DateOnly from, DateOnly to);
       Task AddAsync(Purchase purchase);
   }
   ```
2. **Use Specification pattern** for complex queries instead of exposing IQueryable
3. **One repository per aggregate root**—generic `IRepository<T>` creates confusion about aggregate boundaries
4. **Encapsulate EF Core features** inside repository implementations, not in callers
5. **Never expose DbContext** outside infrastructure layer

**Warning signs:**
- Domain services calling `.Include()` or `.AsNoTracking()`
- Using EF Core namespaces (`Microsoft.EntityFrameworkCore`) in domain layer
- Repository methods named with generic CRUD terminology instead of domain language
- Large number of parameters on repository methods to handle different query variations
- Callers building complex LINQ expressions against repository results

**Phase to address:**
Phase 7: Repository Pattern - Define aggregate-specific repositories with domain operations

---

### Pitfall 11: Navigation Property Encapsulation Breaks Existing EF Core Queries

**What goes wrong:**
Refactoring navigation properties to use private setters and backing fields (for encapsulation) breaks ALL existing LINQ queries that reference those properties. Queries like `.Where(p => p.LineItems.Any(i => i.Status == "Pending"))` fail to compile or, worse, generate incorrect SQL.

**Why it happens:**
When you change `public List<LineItem> LineItems { get; set; }` to `private readonly List<LineItem> _lineItems` with `public IReadOnlyList<LineItem> LineItems => _lineItems.AsReadOnly()`, EF Core can't translate the `AsReadOnly()` call to SQL. LINQ queries that worked before now either fail at compilation or throw runtime exceptions about unsupported operations.

**How to avoid:**
1. **Use `IReadOnlyCollection<T>` instead of `IReadOnlyList<T>`** for navigation properties (better EF support)
2. **Keep property names unchanged** when refactoring to avoid query breakage
3. **Configure field access in OnModelCreating**:
   ```csharp
   modelBuilder.Entity<Purchase>()
       .Navigation(p => p.LineItems)
       .UsePropertyAccessMode(PropertyAccessMode.Field);
   ```
4. **Test existing queries after refactoring**—don't just check compilation, verify SQL generation
5. **Prefer methods over read-only collections** for aggregate encapsulation when possible

**Warning signs:**
- Runtime exceptions: "The LINQ expression could not be translated"
- Queries compile but generate incorrect SQL
- Navigation properties null when they should be populated
- Performance regression: queries now load everything into memory instead of using SQL
- Tests using in-memory database pass but real database tests fail

**Phase to address:**
Phase 1: Rich Domain Model - Test EF Core query compatibility during encapsulation refactoring

---

### Pitfall 12: Complex Type Mapping in EF Core 8 Has Limited Feature Support

**What goes wrong:**
Using EF Core 8's new `ComplexProperty` feature for value objects seems ideal, but it lacks support for nullable complex types, inheritance, and causes unexpected query issues. Mapping `Money? OptionalPrice` as a complex type throws exceptions, and queries against complex type properties generate inefficient SQL.

**Why it happens:**
EF Core 8 introduced `ComplexProperty` as the proper way to map value objects (replacing the awkward `OwnsOne` workaround), but it's a new feature with incomplete functionality. Documentation promotes it as "the right way" without adequately warning about limitations. The EF Core team is still closing gaps in future releases.

**How to avoid:**
1. **Check current EF Core version limitations** before using complex types (EF Core 9+ improves support)
2. **For nullable value objects**, use value converters instead of complex types
3. **Test query translation** against complex type properties—some operations don't translate to SQL
4. **Keep value objects simple** when using complex types—nested complex types are buggy
5. **Have a fallback plan**: Design value objects so they can be mapped with value converters if complex types fail

**Warning signs:**
- Runtime errors when saving nullable complex types
- Query exceptions: "Could not translate complex type property access"
- Migration fails to generate correct nullable handling
- Unexpected NULL handling behavior (entire complex type null vs. individual properties null)
- Performance issues: queries load more data than needed

**Phase to address:**
Phase 2: Value Objects - Validate EF Core complex type support before committing to approach

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Mixing Result pattern with exceptions in same layer | Faster initial migration | Inconsistent error handling, confusion about which approach to use | Never—choose one per layer |
| Generic `IRepository<T>` instead of specific repositories | Less boilerplate code initially | Leaky abstraction, poor aggregate boundary enforcement | Never in DDD contexts |
| Exposing `IQueryable<T>` from repositories | Avoid writing many query methods | Couples domain to EF Core, makes repository useless | Never—use Specification pattern |
| Using `Func<T, bool>` instead of `Expression<Func<T, bool>>` in specifications | Easier to write and test initially | All data loaded into memory, performance disaster | Only for in-memory collections, never with EF Core |
| Keeping service layer during rich model migration | Both old and new code works simultaneously | Duplicate business logic, maintenance burden | Acceptable during gradual Strangler Fig migration, remove within 2 sprints |
| Using `OwnsOne` instead of `ComplexProperty` for value objects | Works in older EF Core versions | Awkward semantics (treats value as entity with hidden key) | Acceptable if stuck on EF Core 7 or earlier |
| Public setters on value objects for EF Core compatibility | Simpler EF configuration | Breaks immutability, allows invalid state | Never—use private constructors with EF Core |
| Lazy loading with proxies instead of explicit/eager loading | Convenient, less query writing | N+1 problems, hard-to-debug performance issues | Acceptable in read-heavy admin panels, never in high-throughput APIs |
| Storing domain events in entity collections instead of outbox | Simpler initial implementation | Events lost if SaveChanges fails, no cross-service reliability | Acceptable for in-process MediatR only, never for distributed systems |

---

## Integration Gotchas

Common mistakes when integrating DDD patterns with existing infrastructure.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| EF Core + Strongly-Typed IDs | Forgetting to configure value converters, causing new columns in migrations | Configure global `IValueConverterSelector` or use Vogen with `RegisterAllInVogenEfCoreConverters()` |
| EF Core + Value Objects | Using `OwnsOne` which creates hidden keys and weird semantics | Use `ComplexProperty` (EF8+) or value converters for single-property objects |
| EF Core + Encapsulated Collections | Using `IReadOnlyList<T>` breaks lazy loading | Use `IReadOnlyCollection<T>` with `.UsePropertyAccessMode(PropertyAccessMode.Field)` |
| MediatR + Domain Events | Publishing events before SaveChanges, losing events if transaction fails | Use outbox pattern: save events to OutboxMessage table, publish in background job |
| Outbox Pattern + Interceptors | Both interceptor and outbox processor publish same event | Choose one: interceptor converts to OutboxMessage, processor publishes to MediatR/Dapr |
| Repository + Specification | Repository executes specification as `Func<T, bool>` instead of `Expression` | Always use `.Compile()` for in-memory evaluation, Expression tree for database |
| Result Pattern + ASP.NET Core | Returning `Result<T>` directly from endpoints, clients get weird JSON | Convert Results to HTTP responses: `result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error)` |
| PostgreSQL Advisory Locks + Aggregates | Using entity ID as lock key causes cross-aggregate lock contention | Use aggregate-type-specific lock key prefixes (e.g., `Hash("Purchase_" + id)`) |
| Dapr Pub/Sub + Domain Events | Publishing domain events directly to Dapr, no deduplication | Publish through outbox with idempotency keys, Dapr handles message delivery |
| Snapshot Testing + Rich Entities | Entity behavior changes break snapshot tests in unrelated areas | Test behavior (inputs/outputs) not implementation (private fields) |

---

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Value objects with complex equality in change tracking | SaveChanges slows down linearly with number of tracked entities | Use immutable value objects, configure custom `ValueComparer` with fast equality | >100 tracked entities with value objects |
| Lazy loading with proxy pattern | Queries that should take 10ms take seconds in production | Disable lazy loading, use explicit `.Include()` or eager loading | >10 related entities per aggregate |
| Specification pattern loading entire table | All queries fast in dev, slow in production | Always use `Expression<Func<T, bool>>`, test with 10k+ rows | >1,000 rows in table |
| Large aggregates with deep includes | Single aggregate load requires 20+ JOINs | Keep aggregates small, use eventual consistency for cross-aggregate rules | >5 collection navigations per aggregate |
| Repository returning tracked entities for read-only operations | Memory usage grows with query volume | Use `.AsNoTracking()` for all read-only queries | >10,000 queries per hour |
| Domain events stored in entity collections | Memory pressure during bulk operations | Use outbox table for persistence, clear entity events after conversion | >100 domain events per transaction |
| Value converters doing expensive operations | Every property read/write hits converter | Cache converted values, use simple types in database | Called >1,000 times per request |
| Overly generic specifications with runtime composition | Query plans can't be cached, every execution is slow | Use predefined specifications, limit runtime composition | >100 queries per second |

---

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exposing aggregate IDs in public APIs without authorization | Users can guess IDs and access others' data | Use UUIDs (already using UUIDv7), verify ownership in aggregate methods |
| Value objects with validation only in constructor | Invalid data bypasses validation via EF Core setters | Use private setters, configure EF Core to use constructors with Vogen or custom factory |
| Domain events containing sensitive data published to Dapr | Sensitive data logged or sent to untrusted subscribers | Publish event IDs only, subscribers query for details with authorization |
| Storing unencrypted private keys in configuration entities | Keys exposed in database backups and logs | Encrypt sensitive fields, use separate secure storage (already using User Secrets for PrivateKey) |
| Result pattern exposing internal error details to clients | Information disclosure about system internals | Create separate client-facing error messages, log detailed errors internally |
| Repository methods with string-based queries | SQL injection via specification pattern | Always use parameterized `Expression<Func<T, bool>>`, never string interpolation |
| Domain entities serialized directly to API responses | Internal IDs, sensitive fields, navigation properties exposed | Use DTOs for API contracts, map from entities in application layer |
| Temporal entities without audit trail for changes | Compliance violations, no change tracking for investigations | Already using `AuditedEntity` with CreatedAt/UpdatedAt, extend to track who made changes |

---

## UX Pitfalls

Common user experience mistakes in this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Domain validation errors exposed directly to API clients | Technical jargon confuses users ("Invariant violation in PurchaseAggregate") | Map domain errors to user-friendly messages in API layer |
| Result pattern with generic error types | All errors look the same to clients, can't provide specific guidance | Use typed errors: `Result<T, ErrorType>` with distinct error codes |
| Long-running aggregate operations without feedback | API timeout, users don't know if purchase succeeded | Raise domain events for progress tracking, return task IDs for status polling |
| Complex type validation errors without field-level details | Users see "Purchase invalid" without knowing which field | Include property path in validation results: `Result.Failure("Purchase.Price: Must be positive")` |
| Domain events causing side effects in user's request | User waits for async processing (email sending, notifications) | Process domain events asynchronously via outbox, respond immediately |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Value Objects:** EF Core mapping works, but missing value comparers—change tracking broken or slow
- [ ] **Strongly-Typed IDs:** Entities compile, but migrations generate new columns—forgot value converters or Vogen registration
- [ ] **Rich Aggregates:** Business logic in entities, but service layer still has duplicate logic—didn't complete strangler pattern
- [ ] **Domain Events:** Events raised in entities, but both interceptor and outbox publish—duplicate event handling
- [ ] **Repository Pattern:** Interface defined, but returns `IQueryable<T>`—abstraction leaked
- [ ] **Specification Pattern:** Specs compile and tests pass, but uses `Func<T, bool>`—will load all data in production
- [ ] **Result Pattern:** Methods return `Result<T>`, but exception handlers still catch—mixed error handling
- [ ] **Encapsulated Collections:** Collections private, but lazy loading stopped working—forgot to configure field access
- [ ] **Complex Types:** Properties mapped, but nullable complex types throw exceptions—EF Core limitation hit
- [ ] **Aggregate Boundaries:** Each entity is aggregate, but queries use 10 JOINs—aggregates too large
- [ ] **Navigation Properties:** Encapsulated with backing fields, but existing queries broken—didn't test EF translation
- [ ] **Value Object Equality:** Implements `IEquatable<T>`, but SaveChanges slow—didn't configure value comparer

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Data loss from value object migration | HIGH | Restore from backup, write data migration script using old column names, apply before EF migration |
| Duplicate event publication | MEDIUM | Add idempotency keys to event handlers, implement deduplication in outbox processor, replay safe |
| Performance degradation from value object equality | LOW | Add custom `ValueComparer` configuration, no data migration needed, immediate fix |
| Broken lazy loading from encapsulation | LOW | Switch to explicit loading in repositories, or configure `PropertyAccessMode.Field` |
| Strongly-typed ID migration with schema change | HIGH | Rollback migration, configure value converters or Vogen, create no-op migration to verify schema unchanged |
| Oversized aggregates causing contention | MEDIUM | Split aggregate, migrate child entities to new aggregate roots, update repositories |
| IQueryable leak in repositories | MEDIUM | Add specific query methods, mark IQueryable methods `[Obsolete]`, gradually migrate callers |
| Mixed Result/Exception error handling | MEDIUM | Define layer boundaries, convert one layer at a time, use `Result.Try()` for gradual migration |
| Specification using Func instead of Expression | LOW | Change signature to `Expression<Func<T, bool>>`, retest queries, no data changes |
| Complex type limitations in EF Core 8 | LOW | Switch to value converters or `OwnsOne`, adjust mapping configuration, regenerate migration |
| Navigation property query breakage | LOW | Restore property names, configure field access mode, verify SQL generation |
| Anemic logic duplication during migration | LOW | Remove service layer methods, force compile errors, update callers to use entities |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| EF Core change tracking breaks with encapsulation | Phase 1: Rich Domain Model | Run all queries against real database, check lazy loading behavior |
| Value object migrations destroy data | Phase 2: Value Objects | Generate migration, inspect SQL for DROP/ALTER, test against copy of production data |
| Strongly-typed IDs break existing code | Phase 3: Strongly-Typed IDs | Create no-op migration first, verify schema unchanged, all tests pass |
| Duplicate domain event publication | Phase 4: Domain Events | Trace single operation end-to-end, verify event published exactly once |
| Oversized aggregates | Phase 1: Rich Domain Model | Measure SaveChanges performance, count JOINs in queries, test concurrency |
| Result pattern inconsistency | Phase 5: Result Pattern | Review error handling per layer, verify no mixed try-catch + Result code |
| Specification pattern performance | Phase 6: Specification Pattern | Log generated SQL, performance test with 10k+ rows |
| Anemic-to-rich migration breaks services | Phase 1: Rich Domain Model | Run characterization tests before/after, verify behavior unchanged |
| Value object equality performance | Phase 2: Value Objects | Profile SaveChanges with 100+ tracked entities, verify value comparers configured |
| Repository abstraction leak | Phase 7: Repository Pattern | Check for EF Core namespaces in domain layer, verify no IQueryable exposure |
| Navigation property query breakage | Phase 1: Rich Domain Model | Automated tests for all existing LINQ queries, verify SQL generation unchanged |
| Complex type mapping limitations | Phase 2: Value Objects | Test nullable complex types, verify query translation, check migration SQL |

---

## Sources

### EF Core Value Objects & Complex Types
- [Value Object's New Mapping: EF Core 8 ComplexProperty](https://www.codemag.com/Article/2405041/Value-Object%E2%80%99s-New-Mapping-EF-Core-8-ComplexProperty) - MEDIUM confidence
- [Implementing value objects - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects) - HIGH confidence (official docs)
- [EF Core 8 RC1: Complex types as value objects - .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-ef8-rc1/) - HIGH confidence (official blog)
- [Using Complex Types as Value Objects with Entity Framework Core 8.0 | ABP.IO](https://abp.io/community/articles/using-complex-types-as-value-objects-with-entity-framework-core-8.0-fs0ynz6e) - MEDIUM confidence

### Strongly-Typed IDs with EF Core
- [Using strongly-typed entity IDs with EF Core - Andrew Lock](https://andrewlock.net/using-strongly-typed-entity-ids-to-avoid-primitive-obsession-part-3/) - HIGH confidence (well-known expert)
- [Strongly-typed IDs in EF Core (Revisited) - Andrew Lock](https://andrewlock.net/strongly-typed-ids-in-ef-core-using-strongly-typed-entity-ids-to-avoid-primitive-obsession-part-4/) - HIGH confidence
- [Entity Framework Core 7: Strongly Typed Ids Together With Auto-Increment Columns](https://david-masters.medium.com/entity-framework-core-7-strongly-typed-ids-together-with-auto-increment-columns-fd9715e331f3) - MEDIUM confidence

### DDD Aggregates & EF Core
- [Implementing a microservice domain model with .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model) - HIGH confidence (official docs)
- [Modeling Aggregates with DDD and Entity Framework | Kalele](https://kalele.io/modeling-aggregates-with-ddd-and-entity-framework/) - MEDIUM confidence
- [Creating Domain-Driven Design entity classes with Entity Framework Core – The Reformed Programmer](https://www.thereformedprogrammer.net/creating-domain-driven-design-entity-classes-with-entity-framework-core/) - MEDIUM confidence

### Domain Events & Outbox Pattern
- [How To Use EF Core Interceptors - Milan Jovanovic](https://www.milanjovanovic.tech/blog/how-to-use-ef-core-interceptors) - HIGH confidence (well-known expert)
- [How To Use Domain Events To Build Loosely Coupled Systems - Milan Jovanovic](https://www.milanjovanovic.tech/blog/how-to-use-domain-events-to-build-loosely-coupled-systems) - HIGH confidence
- [Implementing the Outbox Pattern - Milan Jovanovic](https://www.milanjovanovic.tech/blog/implementing-the-outbox-pattern) - HIGH confidence
- [Interceptors - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) - HIGH confidence (official docs)

### Result Pattern
- [Replacing Exceptions-as-flow-control with the result pattern - Andrew Lock](https://andrewlock.net/working-with-the-result-pattern-part-1-replacing-exceptions-as-control-flow/) - HIGH confidence
- [Functional Error Handling in .NET With the Result Pattern - Milan Jovanovic](https://www.milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern) - HIGH confidence

### Repository Pattern & Specifications
- [Stop Using Repository Pattern with EF Core in 2026 — Here's Why | Level Up Coding](https://levelup.gitconnected.com/stop-using-repository-pattern-with-ef-core-in-2026-heres-why-8f22168aba3e) - MEDIUM confidence
- [Repository pattern: Common implementation mistakes](https://medium.com/@opflucker/repository-pattern-common-implementation-mistakes-69ae95b63d3c) - MEDIUM confidence
- [7 Clean Ways to Use the Specification Pattern with EF Core 9](https://medium.com/@michaelmaurice410/7-clean-ways-to-use-the-specification-pattern-with-ef-core-9-with-a-tiny-powerful-implementation-f94e1fab45f5) - MEDIUM confidence
- [Top 11 EF Core Mistakes That Kill Performance | ByteCrafted](https://bytecrafted.dev/posts/ef-core/performance-mistakes/) - MEDIUM confidence

### Anemic vs Rich Domain Models
- [Refactoring From an Anemic Domain Model To a Rich Domain Model - Milan Jovanovic](https://www.milanjovanovic.tech/blog/refactoring-from-an-anemic-domain-model-to-a-rich-domain-model) - HIGH confidence
- [Anemic Domain Model - Martin Fowler](https://martinfowler.com/bliki/AnemicDomainModel.html) - HIGH confidence (authoritative source)

### EF Core Change Tracking & Performance
- [Value Comparers - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/value-comparers) - HIGH confidence (official docs)
- [Change Tracking - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/change-tracking/) - HIGH confidence (official docs)
- [Understanding Change Tracking for Better Performance in EF Core](https://antondevtips.com/blog/understanding-change-tracking-for-better-performance-in-ef-core) - MEDIUM confidence

### Navigation Properties & Encapsulation
- [Best practices for providing encapsulation of collection navigation properties with lazy loading · Issue #22752](https://github.com/dotnet/efcore/issues/22752) - HIGH confidence (official EF Core repo discussion)
- [Lazy Loading of Related Data - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/related-data/lazy) - HIGH confidence (official docs)

### Migrations & Breaking Changes
- [Breaking changes in EF Core 9 - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes) - HIGH confidence (official docs)
- [Entity types with constructors - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/constructors) - HIGH confidence (official docs)
- [Migrations Overview - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) - HIGH confidence (official docs)

---

*Pitfalls research for: Adding DDD Tactical Patterns to Existing .NET Application*
*Researched: 2026-02-14*

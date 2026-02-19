# Phase 18: Specification Pattern - Research

**Researched:** 2026-02-19
**Domain:** Ardalis.Specification + EF Core + TestContainers integration testing
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Query coverage:**
- Complex queries only (~7 queries): cursor-paginated purchase history, price chart queries, data range aggregates with dynamic filters
- Simple lookups (FindAsync, single-condition FirstOrDefault) stay as inline LINQ
- Moderate queries (2-3 conditions) stay as inline LINQ even if duplicated -- deduplication is a separate concern
- Aggregate queries (GroupBy + Sum/Count) stay as inline LINQ -- poor fit for spec pattern
- Raw SQL queries (gap detection with generate_series) -- Claude's discretion on whether to wrap in spec
- Purchase history endpoint (5 dynamic filters, cursor pagination) is the primary target and first spec to implement

**Spec granularity:**
- Composable specs: small building-block specs that combine at call sites
- One filter per spec: DateRangeSpec, TierFilterSpec, CursorSpec, StatusFilterSpec, etc. -- maximum composability
- Specs handle filtering and sorting only -- no .Select() projection; callers handle DTO projection on the returned IQueryable
- Spec classes live in `Application/Specifications/` (application layer, not domain)
- Extension method returns IQueryable<T> so callers can chain .Select() projection and .ToListAsync()

**Repository layer:**
- No repository abstraction -- keep DbContext injection as-is
- Add DbSet extension methods (e.g., `.WithSpecification(spec)`) that return IQueryable<T> using Ardalis SpecificationEvaluator
- Reads only: specs for query side; writes stay on DbContext directly (Add, SaveChanges unchanged)
- Tests: integration tests with TestContainers against real PostgreSQL to verify SQL translation

**Pagination & sorting:**
- Pagination stays at endpoint level -- specs don't encapsulate cursor/Take logic
- Default sort orders baked into specs (e.g., purchases always descending by CreatedAt)
- Callers apply cursor comparison and Take to the spec-returned IQueryable

### Claude's Discretion
- Whether to wrap raw SQL gap detection in a spec or leave as-is
- Pagination hasMore indicator pattern (current pageSize+1 or alternative)
- Exact composable spec naming conventions
- Which specific complex queries beyond purchase history get spec treatment in which order

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| QP-01 | Complex queries encapsulated in Specification classes (reusable, testable) | Ardalis.Specification base class `Specification<T>`, `Query.Where()`, `Query.OrderBy()`, `Query.AsNoTracking()` -- all verified |
| QP-02 | Specifications translate to server-side SQL (no client-side evaluation) | `WithSpecification(spec)` returns `IQueryable<T>` -- EF Core translates to SQL. TestContainers integration tests verify actual SQL execution against real Postgres |
| QP-03 | Dashboard queries (purchases, daily prices) use specifications for filtering/pagination | Purchase history endpoint (5 dynamic filters) and price chart queries are primary targets; composable specs chain with `WithSpecification(spec1).WithSpecification(spec2)` |
</phase_requirements>

---

## Summary

Phase 18 replaces complex inline LINQ queries in endpoints and services with composable Ardalis.Specification classes. The library is already designated at milestone level (version 9.3.1). The `WithSpecification(spec)` extension method (from `Ardalis.Specification.EntityFrameworkCore`) is the hook between DbSet and specs -- it returns `IQueryable<T>` allowing callers to chain projection (`.Select()`) and pagination (`.Take()`, `.Where(cursor)`) after spec application.

The composable-spec approach (one filter per spec class) is well-supported by the library: you chain multiple `WithSpecification` calls on the same query builder, each adding a `Where` clause. This is exactly how the purchase history endpoint's 5 dynamic filters map to 5 small spec classes. The `Query` builder inside specs is fluent -- `Query.Where(...).OrderByDescending(...).AsNoTracking()` all chain.

Integration testing with TestContainers (version 4.10.0, released Jan 2026) provides real PostgreSQL verification. The xUnit `IAsyncLifetime` + `IClassFixture<PostgresFixture>` pattern starts a container before tests, runs `dbContext.Database.MigrateAsync()` to apply all migrations, and disposes after. This verifies that specs generate valid server-side SQL (QP-02) -- something in-memory EF Core databases cannot do (e.g., DateOnly comparisons, Vogen value converter behavior).

**Primary recommendation:** Implement composable specs with one filter per class in `Application/Specifications/`, use `WithSpecification(spec)` chaining at call sites (endpoints/services), and validate with TestContainers integration tests.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Ardalis.Specification | 9.3.1 | Base class for all spec types | Locked milestone decision; standard .NET DDD library |
| Ardalis.Specification.EntityFrameworkCore | 9.3.1 | `WithSpecification()` extension method, `SpecificationEvaluator` | Required for EF Core integration; provides DbSet hook |
| Testcontainers.PostgreSql | 4.10.0 | Real PostgreSQL for integration tests | Locked user decision; only way to verify SQL translation |

### Supporting (Test Project Only)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Npgsql.EntityFrameworkCore.PostgreSQL | existing (via Aspire) | DbContext for tests | Already in test project via project reference |
| Microsoft.EntityFrameworkCore (existing) | 10.0.0 | DbContextOptionsBuilder in fixtures | Already present |

### Not Adding
| What | Why Not |
|------|---------|
| Repository abstraction (IRepository<T>) | Locked out -- user explicitly decided against it |
| Generic spec base with pagination built-in | Locked out -- pagination stays at endpoint level |
| Respawn | Useful for data cleanup between tests, but not explicitly requested; TestContainers fresh container per test class is sufficient |

**Installation (main project):**
```bash
dotnet add package Ardalis.Specification --version 9.3.1
dotnet add package Ardalis.Specification.EntityFrameworkCore --version 9.3.1
```

**Installation (test project):**
```bash
dotnet add package Testcontainers.PostgreSql --version 4.10.0
```

---

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.ApiService/
└── Application/
    └── Specifications/         # New folder -- all spec classes here
        ├── Purchases/
        │   ├── PurchaseFilledStatusSpec.cs    # Where(!p.IsDryRun && Filled/PartiallyFilled)
        │   ├── PurchaseDateRangeSpec.cs       # Where(p.ExecutedAt >= start && <= end)
        │   ├── PurchaseTierFilterSpec.cs      # Where(p.MultiplierTier == tier)
        │   ├── PurchaseCursorSpec.cs          # Where(p.ExecutedAt < cursorDate) + OrderByDesc
        │   └── PurchasesOrderedByDateSpec.cs  # OrderByDescending(p.ExecutedAt) + AsNoTracking
        └── DailyPrices/
            ├── DailyPriceBySymbolSpec.cs      # Where(dp.Symbol == symbol)
            └── DailyPriceByDateRangeSpec.cs   # Where(dp.Date >= start) + OrderBy(dp.Date)

tests/TradingBot.ApiService.Tests/
└── Application/
    └── Specifications/         # New folder
        ├── PostgresFixture.cs                 # IAsyncLifetime container fixture
        └── Purchases/
            └── PurchaseSpecsTests.cs          # Integration tests per spec
```

### Pattern 1: Single-Filter Composable Spec
**What:** Each spec class applies exactly one filter condition (plus optionally default sort).
**When to use:** All read-side query filtering in this phase.
**Example:**
```csharp
// Source: https://specification.ardalis.com/features/where.html
// Application/Specifications/Purchases/PurchaseDateRangeSpec.cs
public class PurchaseDateRangeSpec : Specification<Purchase>
{
    public PurchaseDateRangeSpec(DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        Query.Where(p => p.ExecutedAt >= startDateTime && p.ExecutedAt <= endDateTime);
    }
}
```

### Pattern 2: Composable Call Site (Endpoint)
**What:** Caller chains multiple `WithSpecification` calls on a single DbSet, then applies projection.
**When to use:** Any endpoint/service that builds a query from dynamic filters.
**Example (verified from official docs):**
```csharp
// Source: https://specification.ardalis.com/usage/use-specification-dbcontext.html
// Replace current GetPurchaseHistoryAsync inline LINQ block

var query = db.Purchases
    .WithSpecification(new PurchaseFilledStatusSpec())           // base filter always applied
    .WithSpecification(new PurchasesOrderedByDateSpec());        // default sort + AsNoTracking

if (startDate.HasValue)
    query = query.WithSpecification(new PurchaseDateRangeSpec(startDate.Value, endDate ?? DateOnly.MaxValue));

if (!string.IsNullOrEmpty(tier))
    query = query.WithSpecification(new PurchaseTierFilterSpec(tier));

if (!string.IsNullOrEmpty(cursor) && DateTimeOffset.TryParse(cursor, out var cursorDate))
    query = query.WithSpecification(new PurchaseCursorSpec(cursorDate));

var items = await query
    .Take(pageSize + 1)
    .Select(p => new PurchaseDto(...))    // projection at call site, not in spec
    .ToListAsync(ct);
```

### Pattern 3: Default Sort Baked Into Spec
**What:** Spec with no filter parameters but a baked-in `OrderByDescending` -- acts as a "default sort" spec.
**When to use:** When a collection always needs the same ordering (purchases by date desc).
**Example:**
```csharp
// Source: https://specification.ardalis.com/features/orderby.html
public class PurchasesOrderedByDateSpec : Specification<Purchase>
{
    public PurchasesOrderedByDateSpec()
    {
        Query.OrderByDescending(p => p.ExecutedAt)
             .AsNoTracking();
    }
}
```

### Pattern 4: AsNoTracking Inside Spec
**What:** Read-only specs include `.AsNoTracking()` inside the `Query` builder.
**When to use:** All specs in this phase (dashboard endpoints are read-only).
**Note:** Ardalis documentation recommends suffixing with `ReadOnly` when using `AsNoTracking` to signal to callers that entities should not be modified. In our composable pattern where specs are granular building blocks, `AsNoTracking` goes in the "base/ordering" spec for each entity type.

### Pattern 5: TestContainers Integration Test Fixture
**What:** `IAsyncLifetime` class fixture starts PostgreSQL container, runs EF migrations, exposes connection string.
**When to use:** All specification integration tests in this phase.
**Example (verified from TestContainers docs + community patterns):**
```csharp
// Source: https://context7.com/testcontainers/testcontainers-dotnet
// tests/TradingBot.ApiService.Tests/Application/Specifications/PostgresFixture.cs
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithDatabase("tradingbot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public TradingBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(Container.GetConnectionString())
            .Options;
        return new TradingBotDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

// Test class:
[Collection("Postgres")]
public class PurchaseSpecsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PurchaseSpecsTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PurchaseDateRangeSpec_FiltersCorrectly()
    {
        await using var db = _fixture.CreateDbContext();
        // Seed test data...
        var spec = new PurchaseDateRangeSpec(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();
        results.Should().AllSatisfy(p =>
            p.ExecutedAt.Date.Should().BeOnOrAfter(new DateTime(2024, 1, 1)));
    }
}
```

### Anti-Patterns to Avoid

- **Putting `.Select()` projection inside a spec:** The user locked this out. `Select`/`SelectMany` terminate the fluent chain in Ardalis.Specification (returns void, no further chaining). Keep projection at call site.
- **Monolithic per-use-case specs:** Avoid `GetPurchaseHistorySpec(startDate, endDate, tier, cursor, pageSize)` with all 5 parameters. This defeats composability -- use 5 separate specs instead.
- **Calling `.ToListAsync()` inside a spec:** Specs return `IQueryable`, not materialized results. Materialization happens at the call site.
- **Wrapping aggregate queries in specs:** GroupBy + Sum queries don't compose cleanly with Ardalis.Specification. User locked these out -- keep them inline LINQ.
- **Using EF Core InMemory for spec tests:** InMemory doesn't translate expressions the same way as Npgsql; it won't catch Vogen value converter issues or DateOnly comparison issues. TestContainers only.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Apply spec to DbSet | Custom extension method calling `SpecificationEvaluator` manually | `WithSpecification(spec)` from `Ardalis.Specification.EntityFrameworkCore` | Already provided; handles Where, OrderBy, Include, AsNoTracking, Take, Skip, TagWith chaining |
| Evaluate spec in memory | Custom `.Evaluate()` loop | `spec.Evaluate(collection)` built into `Ardalis.Specification` | Already provided; useful for unit testing spec logic without DB |
| PostgreSQL test container lifecycle | Custom Docker SDK setup | `PostgreSqlBuilder` + `IAsyncLifetime` | Handles port binding, health checks, connection string generation |
| Test data cleanup between tests | Truncate table in teardown | Per-test `CreateDbContext()` on fresh migrations or Respawn | Simplest approach: share container per test class, seed/clean per test |

**Key insight:** `Ardalis.Specification.EntityFrameworkCore` provides `WithSpecification(spec)` as a `DbSet<T>` and `IQueryable<T>` extension -- no custom infrastructure needed. The evaluator internally calls `GetQuery(source, spec)` which applies all spec clauses in order.

---

## Common Pitfalls

### Pitfall 1: Value Object Comparisons in LINQ Expressions
**What goes wrong:** Specs containing `Query.Where(p => p.Status == PurchaseStatus.Filled)` work fine, but `Query.Where(dp => dp.Symbol == Symbol.Btc)` may fail or produce wrong SQL if Vogen's EF Core value converter isn't applied.
**Why it happens:** Vogen value objects use `HaveConversion<>` in `ConfigureConventions`. This must be registered in the `TradingBotDbContext` used in tests. Since tests create `TradingBotDbContext` directly with `DbContextOptionsBuilder`, the conventions are applied automatically via the existing `ConfigureConventions` override.
**How to avoid:** Use the real `TradingBotDbContext` in integration tests (not a custom subclass). `CreateDbContext()` in the fixture should use the same constructor.
**Warning signs:** Spec tests pass in-memory but fail against real Postgres; Symbol comparisons returning empty results.

### Pitfall 2: AsNoTracking Placement in Composable Chain
**What goes wrong:** When chaining `WithSpecification(spec1).WithSpecification(spec2)` where `spec1` has `AsNoTracking` and `spec2` does not, the no-tracking behavior still applies (EF Core applies it once to the queryable). But if spec2 re-enables tracking somehow, results are unpredictable.
**Why it happens:** `AsNoTracking()` is applied to the underlying IQueryable by the EvaluatorSelector. Subsequent `WithSpecification` calls do not re-enable tracking.
**How to avoid:** Put `AsNoTracking()` in the "always-applied base spec" for each entity type (e.g., `PurchasesOrderedByDateSpec`). Don't mix tracking and no-tracking specs in the same call chain.
**Warning signs:** Unexpected tracking behavior or EF Core warnings in logs.

### Pitfall 3: Cursor Pagination with Composable Specs
**What goes wrong:** Cursor spec (`WHERE ExecutedAt < @cursor`) and sort spec (`ORDER BY ExecutedAt DESC`) must be consistent. If callers apply cursor filter without the sort, results are undefined.
**Why it happens:** Composable design means callers must know to always pair cursor + sort. The cursor comparison assumes descending order.
**How to avoid:** Combine cursor filter and descending sort in `PurchaseCursorSpec` itself. The sort spec is separate for the non-cursor case, but `PurchaseCursorSpec` should include its own `OrderByDescending` to be self-consistent. Alternatively, document clearly that cursor spec requires sort spec to be applied first.
**Warning signs:** Duplicate items across pages, or cursor returning wrong items.

### Pitfall 4: Spec Classes Not Found in TestContainers Tests
**What goes wrong:** Integration tests can't resolve `WithSpecification` -- `CS1929` or namespace error.
**Why it happens:** `Ardalis.Specification.EntityFrameworkCore` must be referenced in the test project OR the main project (tests have a project reference to the main project). If the main project has it, the test project inherits the extension methods through the project reference.
**How to avoid:** Add `Ardalis.Specification.EntityFrameworkCore` to `TradingBot.ApiService.csproj` (not just test project). Tests reference main project, so extension methods are available.
**Warning signs:** `WithSpecification` not recognized in test files.

### Pitfall 5: EF Core Migrations in TestContainers
**What goes wrong:** `MigrateAsync()` in `InitializeAsync()` fails because the connection string points to a container that isn't fully ready yet.
**Why it happens:** Container reports as started before PostgreSQL is accepting connections.
**How to avoid:** `PostgreSqlBuilder` uses a built-in wait strategy (waits until port is available). `MigrateAsync()` should succeed. If issues arise, add `.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))` to the builder.
**Warning signs:** `NpgsqlException: connection refused` during test fixture setup.

---

## Code Examples

Verified patterns from official sources:

### Spec Definition with All Features
```csharp
// Source: https://specification.ardalis.com/features/where.html + /features/orderby.html + /features/asnotracking.html
// Application/Specifications/Purchases/PurchaseFilledStatusSpec.cs
public class PurchaseFilledStatusSpec : Specification<Purchase>
{
    public PurchaseFilledStatusSpec()
    {
        Query
            .Where(p => !p.IsDryRun &&
                        (p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled))
            .OrderByDescending(p => p.ExecutedAt)
            .AsNoTracking();
    }
}
```

### Chaining Multiple Specs at Call Site
```csharp
// Source: https://specification.ardalis.com/usage/use-specification-dbcontext.html
var query = db.Purchases
    .WithSpecification(new PurchaseFilledStatusSpec());

if (startDate.HasValue)
    query = query.WithSpecification(new PurchaseDateRangeSpec(startDate.Value, endDate.GetValueOrDefault(DateOnly.MaxValue)));

if (!string.IsNullOrEmpty(tier))
    query = query.WithSpecification(new PurchaseTierFilterSpec(tier));

if (!string.IsNullOrEmpty(cursor) && DateTimeOffset.TryParse(cursor, out var cursorDate))
    query = query.WithSpecification(new PurchaseCursorSpec(cursorDate));

var items = await query
    .Take(pageSize + 1)
    .Select(p => new PurchaseDto(p.Id, p.ExecutedAt, p.Price, p.Cost, p.Quantity, p.MultiplierTier ?? "Base", p.Multiplier, p.DropPercentage))
    .ToListAsync(ct);
```

### DailyPrice Chart Spec
```csharp
// Application/Specifications/DailyPrices/DailyPriceByDateRangeSpec.cs
public class DailyPriceByDateRangeSpec : Specification<DailyPrice>
{
    public DailyPriceByDateRangeSpec(Symbol symbol, DateOnly startDate)
    {
        Query
            .Where(dp => dp.Symbol == symbol && dp.Date >= startDate)
            .OrderBy(dp => dp.Date)
            .AsNoTracking();
    }
}
// Usage in GetPriceChartAsync:
var prices = await db.DailyPrices
    .WithSpecification(new DailyPriceByDateRangeSpec(Symbol.Btc, startDate))
    .Select(dp => new PricePointDto(dp.Date.ToString("yyyy-MM-dd"), dp.Close))
    .ToListAsync(ct);
```

### TestContainers Fixture (Full Pattern)
```csharp
// Source: https://context7.com/testcontainers/testcontainers-dotnet
// tests/TradingBot.ApiService.Tests/Application/Specifications/PostgresFixture.cs
using Testcontainers.PostgreSql;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("tradingbot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public TradingBotDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradingBotDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new TradingBotDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

// Test class:
public class PurchaseDateRangeSpecTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public PurchaseDateRangeSpecTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FiltersToDateRange()
    {
        await using var db = _fixture.CreateDbContext();
        // Seed and assert...
        var spec = new PurchaseDateRangeSpec(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();
        results.Should().HaveCountGreaterThan(0);
    }
}
```

### HasMore Pagination Pattern (pageSize + 1 approach)
The existing pattern in `GetPurchaseHistoryAsync` is the standard way:
```csharp
// Fetch one extra item to detect hasMore
var items = await query.Take(pageSize + 1).Select(...).ToListAsync(ct);
var hasMore = items.Count > pageSize;
var resultItems = hasMore ? items.Take(pageSize).ToList() : items;
var nextCursor = hasMore ? resultItems.Last().ExecutedAt.ToString("o") : null;
```
This stays at the endpoint level (locked decision). Specs don't encode `Take` or cursor.

---

## Discretion Recommendations

### Raw SQL Gap Detection Spec
**Decision:** Leave `GapDetectionService.DetectGapsAsync()` as-is (inline raw SQL via `Database.SqlQuery<DateOnly>`).
**Reason:** The `generate_series` PostgreSQL function has no LINQ equivalent and cannot be wrapped in a Specification that translates to SQL. Forcing it into a spec would require raw SQL inside the spec class, which is an anti-pattern for the library. The query is already well-encapsulated in `GapDetectionService`.

### hasMore Indicator Pattern
**Decision:** Keep the existing `pageSize + 1` trick used in `GetPurchaseHistoryAsync`.
**Reason:** It requires one DB round-trip (vs. two with a separate count query). Well-understood pattern. The alternative (count query) adds latency without benefit for cursor pagination.

### Spec Naming Conventions
**Recommendation:** Use entity-based prefixes + filter noun:
- `PurchaseFilledStatusSpec` -- filters to filled/partially-filled non-dry-run purchases (the "always-on" base filter)
- `PurchaseDateRangeSpec(DateOnly start, DateOnly end)` -- date window filter
- `PurchaseTierFilterSpec(string tier)` -- multiplier tier filter
- `PurchaseCursorSpec(DateTimeOffset cursor)` -- cursor filter with built-in descending sort
- `DailyPriceByDateRangeSpec(Symbol symbol, DateOnly startDate)` -- symbol + date range (combined since always used together for this entity)

**Avoid** generic names like `FilterSpec` or redundant `Specification` suffix (class name already makes it clear from the folder).

### Query Ordering in This Phase
1. Purchase history endpoint (`GetPurchaseHistoryAsync`) -- primary target, highest value
2. Price chart endpoint (`GetPriceChartAsync`) -- `DailyPrices` date range + symbol query; `Purchases` chart markers query
3. Portfolio endpoint (`GetPortfolioAsync`) -- filled status filter (shared with above spec)
4. Weekly summary service (`WeeklySummaryService`) -- week date range + filled status (reuses existing specs)
5. Missed purchase verification (`MissedPurchaseVerificationService`) -- today date range + filled status (reuses specs)

Queries 3-5 benefit from spec reuse without new spec classes (they use `PurchaseFilledStatusSpec` + `PurchaseDateRangeSpec` already defined for query 1).

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Repository pattern with IRepository<T> | Direct DbContext + `WithSpecification()` extension | Ardalis.Specification v8+ | No repository overhead; specs compose directly on DbSet |
| Single-result spec via `ISingleResultSpecification` | `SingleResultSpecification<T>` (generic version) | v9.3.x | `ISingleResultSpecification` is obsolete; use generic form |
| Manual `SpecificationEvaluator.Default.GetQuery()` | `WithSpecification(spec)` extension on DbSet | v8+ | Cleaner fluent syntax; same behavior |
| `.NET 6/7` support | `.NET 8, 9, 10` (10 computed compatible) | v9.3.1 (Aug 2025) | Our .NET 10 project is compatible |

**Deprecated/outdated:**
- `ISingleResultSpecification` (non-generic): replaced by `ISingleResultSpecification<T>` and `SingleResultSpecification<T>` base class. Don't use in new specs.
- Repository pattern with Ardalis.Specification: not deprecated but explicitly out of scope per user decision.

---

## Open Questions

1. **Vogen value comparisons in LINQ inside specs**
   - What we know: `TradingBotDbContext.ConfigureConventions` registers Vogen EF Core converters. Tests create `TradingBotDbContext` with the same conventions.
   - What's unclear: Whether Vogen-typed properties (Price, Symbol, Quantity) work correctly as LINQ expression tree predicates in specs translated by Npgsql. The existing inline LINQ already uses these (e.g., `dp.Symbol == Symbol.Btc`), and those work in production.
   - Recommendation: Verify with at least one TestContainers test per Vogen type used in spec predicates. If issues arise, fall back to comparing `.Value` (e.g., `dp.Symbol.Value == "BTC"`).

2. **`PurchaseCursorSpec` ordering conflict with `PurchaseFilledStatusSpec`**
   - What we know: `PurchaseFilledStatusSpec` includes `OrderByDescending(p.ExecutedAt)`. If caller also uses `PurchaseCursorSpec` which wants `OrderByDescending`, Ardalis.Specification may have ordering conflicts.
   - What's unclear: How the library handles multiple `OrderBy` from chained specs (does it append or replace?).
   - Recommendation: Put `OrderByDescending` only in a dedicated ordering spec (or `PurchaseCursorSpec` for the cursor case). `PurchaseFilledStatusSpec` should only filter, not sort. Verify with integration test.

3. **EF Core query logging for SQL verification (QP-02)**
   - What we know: QP-02 requires confirming server-side SQL generation.
   - What's unclear: Whether integration tests will include query log assertions or rely on "test passes against real DB" as sufficient evidence.
   - Recommendation: For QP-02 compliance, configure EF Core logging in the test fixture (`EnableSensitiveDataLogging`, `LogTo(Console.WriteLine)`) and note in test comments that the query succeeds against real Postgres, proving server-side translation.

---

## Sources

### Primary (HIGH confidence)
- `/ardalis/specification` (Context7) -- spec base class, `Query.Where`, `Query.OrderBy`, `Query.AsNoTracking`, `WithSpecification`, chaining, `SpecificationEvaluator`
- `/testcontainers/testcontainers-dotnet` (Context7) -- `PostgreSqlBuilder`, `IAsyncLifetime`, `IClassFixture`, `GetConnectionString`, `MigrateAsync` pattern
- https://specification.ardalis.com/usage/use-specification-dbcontext.html -- `WithSpecification` chaining, IQueryable return
- https://specification.ardalis.com/features/asnotracking.html -- `AsNoTracking` inside spec class
- https://www.nuget.org/packages/Ardalis.Specification.EntityFrameworkCore -- version 9.3.1, .NET 10 computed compatible (verified Aug 2025)
- https://www.nuget.org/packages/Testcontainers.PostgreSql -- version 4.10.0 (verified Jan 2026), .NET 10 compatible

### Secondary (MEDIUM confidence)
- https://ardalis.com/ardalis-specification-v9-release/ -- v9 breaking changes confirmed minimal for existing specs
- https://bakson.dev/2023/08/17/ef-core-and-respawn.html -- TestContainers + EF Core `MigrateAsync` fixture pattern (2023, still valid)

### Tertiary (LOW confidence)
- WebSearch results on AsNoTracking placement -- confirmed by official docs (upgraded to HIGH)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- NuGet versions verified directly, .NET 10 compatibility confirmed
- Architecture (composable pattern): HIGH -- verified via Context7 official docs showing `WithSpecification` chaining returns `IQueryable<T>`
- TestContainers setup: HIGH -- verified via Context7 and official testcontainers.org docs
- Pitfalls (Vogen in LINQ expressions): MEDIUM -- based on existing production behavior; TestContainers tests will confirm
- Ordering conflict between chained specs: LOW -- needs empirical verification

**Research date:** 2026-02-19
**Valid until:** 2026-05-19 (stable libraries, 90-day validity)

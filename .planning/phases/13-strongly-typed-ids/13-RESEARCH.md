# Phase 13: Strongly-Typed IDs - Research

**Researched:** 2026-02-18
**Domain:** Vogen source-generated value objects, EF Core value converters, ASP.NET Core minimal API model binding
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Foreign key scope:**
- All FK properties use the target entity's typed ID (e.g., `Purchase.DcaConfigurationId` becomes type `DcaConfigurationId`)
- Only EF Core-mapped entity properties get typed IDs — infrastructure tables (outbox messages, etc.) keep raw Guid
- Preserve UUIDv7 generation for all typed IDs (time-ordered, index-friendly)
- Service and handler method signatures use typed IDs throughout the call chain (e.g., `GetPurchaseById(PurchaseId id)`)

**API surface typing:**
- Endpoint route parameters bind directly to typed IDs (e.g., `/{id:PurchaseId}`) — requires custom model binder or Vogen integration
- Query parameters also bind to typed IDs — consistent typing across all parameter sources
- JSON serialization is transparent: typed IDs serialize/deserialize as plain GUID strings (dashboard sees no change)
- Dashboard TypeScript types use branded types to mirror backend safety (e.g., `type PurchaseId = string & { __brand: 'PurchaseId' }`)

**Conversion ergonomics:**
- Implicit conversion both directions: Guid → TypedId and TypedId → Guid — minimal ceremony, IDs feel like Guids with extra safety
- Each typed ID has a `.New()` factory method that generates UUIDv7 internally — clean, discoverable creation pattern
- Request/response bodies deserialize plain GUID strings directly into typed IDs

**Rollout strategy:**
- Two-plan approach: Plan 1 = Vogen setup + all ID type definitions + generic `BaseEntity<TId>`. Plan 2 = Apply typed IDs to all entities + update all callers
- Each plan leaves the codebase fully compiling — no broken intermediate state
- EF Core migration created if converter registration requires schema changes (expected: no migration needed, just value converters)
- `BaseEntity` refactored to generic `BaseEntity<TId>` — prepares for Phase 15 `AggregateRoot<TId>` hierarchy

### Claude's Discretion
- Value semantics configuration (equality operators, IComparable, GetHashCode) — configure Vogen appropriately
- EF Core converter registration approach (ConfigureConventions vs per-property)
- Exact Vogen attribute configuration and global settings
- Test helper patterns for creating entities with typed IDs

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| TS-01 | All entity IDs use strongly-typed wrappers (PurchaseId, DailyPriceId, IngestionJobId, DcaConfigurationId) instead of raw Guid | Vogen 8.0.4 `[ValueObject<Guid>]` with `Conversions.EfCoreValueConverter \| Conversions.SystemTextJson` generates compile-safe ID wrappers; `ConfigureConventions` in DbContext registers all converters globally; `TryParse` generated for minimal API route binding |
</phase_requirements>

---

## Summary

Vogen 8.0.4 is a source generator that turns `[ValueObject<Guid>]` declarations into fully-formed strongly-typed ID structs with zero runtime overhead. The generated code includes: equality/hashcode implementations, `From(Guid)` factory, `TryFrom`, `TryParse` (for minimal API binding), `IParsable<T>`, System.Text.Json converter (serializes as plain GUID string), EF Core value converter and comparer, and optionally `FromNewGuid()` via `Customizations.AddFactoryMethodForGuids`. Everything is source-generated — no reflection at runtime.

The two-plan rollout is clean: Plan 1 adds Vogen, defines four ID types, and makes `BaseEntity<TId>` generic. Plan 2 applies typed IDs to entities and updates all call sites. The compiler enforces the rollout correctness: Plan 1 compiles because old entities still compile; Plan 2 fails-fast on any missed call site — the compiler drives completeness.

The one non-obvious requirement is the `.New()` factory method for UUIDv7. Vogen's built-in `Customizations.AddFactoryMethodForGuids` generates `FromNewGuid()` which uses `Guid.NewGuid()` (v4). Since the codebase uses `Guid.CreateVersion7()`, a custom static method must be hand-written on each ID type (one line). This is the only hand-rolled code needed.

**Primary recommendation:** Use `readonly partial struct` for all ID types with `Conversions.EfCoreValueConverter | Conversions.SystemTextJson`, plus `CastOperator = CastOperator.Implicit` globally for both directions. Register EF Core converters via `ConfigureConventions` override in `TradingBotDbContext`. Add a hand-written static `New()` method on each typed ID that calls `Guid.CreateVersion7()`.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Vogen | 8.0.4 | Source-generated strongly-typed value objects | Already decided; zero runtime overhead, generates all required code |

### Supporting (already in project)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.EntityFrameworkCore | 10.0.0 (via Aspire.Npgsql) | Value converter registration | EF Core native `ConfigureConventions` used for global converter registration |
| System.Text.Json | In .NET 10 BCL | JSON serialization of typed IDs as plain GUID strings | Vogen generates STJ converter automatically |

### No Additional Packages Needed

The existing project already has EF Core, STJ, and the .NET 10 BCL (`Guid.CreateVersion7()`). Vogen is the only new package.

**Installation:**
```bash
dotnet add TradingBot.ApiService/TradingBot.ApiService.csproj package Vogen --version 8.0.4
```

---

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── BuildingBlocks/
│   ├── BaseEntity.cs              # Refactor to BaseEntity<TId>
│   └── AuditedEntity.cs           # Unchanged
├── Models/
│   ├── Ids/
│   │   ├── PurchaseId.cs          # NEW: [ValueObject<Guid>] readonly partial struct
│   │   ├── DailyPriceId.cs        # NEW: same pattern
│   │   ├── IngestionJobId.cs      # NEW: same pattern
│   │   └── DcaConfigurationId.cs  # NEW: same pattern
│   ├── Purchase.cs                # Modified: Id becomes PurchaseId
│   ├── DailyPrice.cs              # Modified: composite key (Date+Symbol), no typed ID
│   ├── IngestionJob.cs            # Modified: Id becomes IngestionJobId
│   └── DcaConfiguration.cs        # Modified: Id becomes DcaConfigurationId
```

### Pattern 1: Vogen ID Type Definition

**What:** Source-generated strongly-typed Guid wrapper with UUIDv7 factory, implicit casting both directions, STJ serialization as plain GUID, and EF Core converter.

**When to use:** Every entity ID in the domain (not outbox messages or infrastructure).

```csharp
// Source: Context7 / Vogen official docs + local adaptation for UUIDv7

// Assembly-level defaults (place in a file like VogenGlobalConfig.cs or AssemblyInfo.cs)
[assembly: VogenDefaults(
    underlyingType: typeof(Guid),
    conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson,
    castOperator: CastOperator.Implicit)]  // implicit Guid ↔ TypedId in BOTH directions

// Individual ID type declaration — minimal ceremony
[ValueObject<Guid>]
public readonly partial struct PurchaseId
{
    // Hand-written: generates UUIDv7 (Vogen's FromNewGuid() uses Guid.NewGuid() = v4)
    public static PurchaseId New() => From(Guid.CreateVersion7());
}
```

Vogen generates for each typed ID:
- `From(Guid value)` — wraps an existing Guid
- `TryFrom(Guid, out TypedId)` — safe wrapping
- `TryParse(string, IFormatProvider, out TypedId)` — minimal API route binding
- `IParsable<TypedId>` — query parameter binding
- `EfCoreValueConverter` and `EfCoreValueComparer` inner classes
- System.Text.Json converter that serializes as `"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"` (plain GUID string)
- Equality, `GetHashCode`, `IComparable<T>`, `IEquatable<T>`
- Implicit cast operators (when `CastOperator.Implicit` is set globally)

**Confidence:** HIGH — verified via Context7 docs

### Pattern 2: Generic BaseEntity

**What:** Refactor `BaseEntity` to `BaseEntity<TId>` so typed IDs flow through the entity hierarchy.

**When to use:** All entities using typed IDs. Prepares for Phase 15 `AggregateRoot<TId>`.

```csharp
// BuildingBlocks/BaseEntity.cs — AFTER refactor
namespace TradingBot.ApiService.BuildingBlocks;

public abstract class BaseEntity<TId> : AuditedEntity
{
    public TId Id { get; init; } = default!;
}
```

Entity usage:
```csharp
public class Purchase : BaseEntity<PurchaseId>
{
    // Id is now PurchaseId, set externally via Purchase { Id = PurchaseId.New() }
    // OR: override init in entity if construction needs UUIDv7 timing
}
```

**Note:** `BaseEntity` previously set `Id = Guid.CreateVersion7(CreatedAt)` in a constructor. With typed IDs, this moves to a static factory or caller responsibility. The cleanest pattern is to set it at creation site: `new Purchase { Id = PurchaseId.New() }`. This is consistent with how `DcaConfiguration` and `IngestionJob` already explicitly set their IDs.

**Confidence:** HIGH — straightforward C# generics

### Pattern 3: EF Core Converter Registration via ConfigureConventions

**What:** Single point of registration for all Vogen EF Core converters. Vogen generates an extension method that registers all typed IDs at once.

**When to use:** Override `ConfigureConventions` in `TradingBotDbContext` — fires before `OnModelCreating`.

```csharp
// Infrastructure/Data/TradingBotDbContext.cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);
    // Generated by Vogen — registers EfCoreValueConverter + EfCoreValueComparer
    // for all types decorated with Conversions.EfCoreValueConverter in this assembly
    configurationBuilder.RegisterAllInEfCoreConverters();
}
```

**Important:** The exact method name depends on Vogen's marker class convention. When using the assembly-attribute approach (no marker class), the generated extension name is `RegisterAllInEfCoreConverters`. When using the `[EfCoreConverter<T>]` marker-class pattern, the name is `RegisterAllIn{MarkerClassName}`. For this codebase, the assembly-attribute approach is simpler — no marker class needed since all IDs are in the same project.

**Requires .NET 8+:** The `ConfigureConventions` generated extension is wrapped in `#if NET8_0_OR_GREATER`. The project targets net10.0, so this works.

**Confidence:** HIGH — verified via Context7 Vogen snapshot tests and official integration docs

### Pattern 4: Minimal API Route Binding

**What:** Vogen generates `TryParse(string, IFormatProvider, out TypedId)` and implements `IParsable<TypedId>`. ASP.NET Core minimal APIs use `TryParse` automatically for route and query parameters.

**When to use:** Replace `Guid jobId` parameters in endpoint handlers with `IngestionJobId jobId`.

```csharp
// BEFORE
private static async Task<IResult> GetJobStatusAsync(Guid jobId, ...) { }

// DataEndpoints.cs route registration
group.MapGet("/ingest/{jobId:guid}", GetJobStatusAsync);

// AFTER — typed ID, route constraint changes
private static async Task<IResult> GetJobStatusAsync(IngestionJobId jobId, ...) { }

// Route constraint: {jobId:guid} still works since IngestionJobId is backed by Guid
// ASP.NET core calls IngestionJobId.TryParse(routeValue, ...) automatically
group.MapGet("/ingest/{jobId:guid}", GetJobStatusAsync);
```

**Known Vogen fix:** This was a known compatibility issue fixed in Vogen v4.0.0+. Vogen 8.x generates the correct `TryParse` signature. Confirmed via GitHub issue #559.

**Confidence:** HIGH — verified via Vogen issue #559 and official TryParse generation in Context7 snapshots

### Pattern 5: DailyPrice Special Case

**What:** `DailyPrice` uses a composite primary key `(Date, Symbol)` — not a single Guid. It also extends `AuditedEntity` (not `BaseEntity`). There is no `DailyPriceId` as a typed wrapper.

**Context file says:** `DailyPriceId` is listed in the phase scope. The CONTEXT.md says "all entities use typed IDs." However, `DailyPrice` has no Guid PK — its identity is `(DateOnly Date, string Symbol)`.

**Recommendation (Claude's Discretion):** Create `DailyPriceId` as a nominal type if a composite-key typed ID is wanted. However, composite key typed IDs are significantly more complex (Vogen doesn't generate tuple converters). The simpler interpretation: `DailyPriceId` is not needed because `DailyPrice` has no Guid column to wrap. Confirm with user during planning.

**If a `DailyPriceId` is truly required:** It would be a record wrapping `(DateOnly Date, string Symbol)` — hand-written, not Vogen-generated. OUT OF SCOPE for this phase based on phase boundary ("Guid columns persist, schema unchanged").

**Confidence:** HIGH — based on actual entity code review

### Pattern 6: DcaConfiguration Singleton ID

**What:** `DcaConfiguration` uses a hardcoded Guid `00000000-0000-0000-0000-000000000001` for its singleton pattern. The typed `DcaConfigurationId` must preserve this.

```csharp
[ValueObject<Guid>]
public readonly partial struct DcaConfigurationId
{
    public static DcaConfigurationId New() => From(Guid.CreateVersion7());

    // Singleton sentinel value
    public static readonly DcaConfigurationId Singleton =
        From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}

// In DcaConfiguration entity
public class DcaConfiguration : BaseEntity<DcaConfigurationId>
{
    // Singleton pattern: override default Id
    public DcaConfiguration()
    {
        // Initialization via property init
    }
}

// In ConfigurationService
var entity = new DcaConfiguration
{
    Id = DcaConfigurationId.Singleton
};
```

**Confidence:** HIGH — straightforward adaptation of existing pattern

### Anti-Patterns to Avoid

- **Using `class` instead of `readonly partial struct` for IDs:** Classes add heap allocation. Structs are zero overhead for IDs that are passed by value everywhere.
- **Using `CastOperator.Explicit` only:** The context mandates implicit casting both directions. Explicit-only forces `.Value` everywhere, defeating ergonomics.
- **Relying on `FromNewGuid()` for UUIDv7:** Vogen's built-in generates `Guid.NewGuid()` (v4). Always call `.New()` which wraps `Guid.CreateVersion7()`.
- **Registering converters per-property in `OnModelCreating`:** Four ID types times 4 registrations = verbose and fragile. Use `ConfigureConventions` global registration.
- **Making `IngestionJobQueue` use typed IDs:** `IngestionJobQueue` is an infrastructure channel — it should stay `Channel<Guid>`. The conversion happens at the boundary (endpoint passes `IngestionJobId`, service layer unwraps or passes the typed ID through). The decision: either update queue to `Channel<IngestionJobId>` (cleanest) or keep as `Guid` (infrastructure exception). The context says "service and handler method signatures use typed IDs throughout" — update the queue.
- **Calling `db.IngestionJobs.FindAsync([jobId], ct)` directly:** After typed ID, `FindAsync` takes object array — pass `[jobId.Value]` or rely on implicit conversion depending on EF Core behavior with value-converted keys.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Strongly-typed ID struct boilerplate | Custom struct with equality, hashcode, operators, JSON converter | Vogen `[ValueObject<Guid>]` | Vogen generates 300+ lines of correct, tested code per type |
| EF Core value converter per ID | Custom `ValueConverter<TypedId, Guid>` | Vogen's generated inner class `EfCoreValueConverter` + `ConfigureConventions` | Edge cases in EF Core value comparers (change tracking) are subtle |
| System.Text.Json converter | Custom `JsonConverter<TypedId>` | Vogen `Conversions.SystemTextJson` | Handles both read-as-property-name and write-as-property-name correctly |
| Minimal API TryParse | Manual `TryParse` method | Vogen generates it from `IParsable<T>` implementation | Vogen 4.0+ fixed ASP.NET 8 compatibility |

**Key insight:** The only hand-rolled code needed is the `New()` method (one line) and the `DcaConfigurationId.Singleton` constant. Everything else is Vogen-generated.

---

## Common Pitfalls

### Pitfall 1: EF Core FindAsync with Typed ID Primary Keys

**What goes wrong:** `db.IngestionJobs.FindAsync([jobId], ct)` — `FindAsync` takes `object[]`. With a Vogen struct as the PK type, EF Core may or may not recognize the implicit conversion when searching by key.

**Why it happens:** `FindAsync` uses reflection internally to compare key values. When value converters are registered via `ConfigureConventions`, EF Core knows how to persist/load the type, but `FindAsync`'s key lookup uses the CLR type of the array element for matching.

**How to avoid:** Pass the underlying Guid: `db.IngestionJobs.FindAsync([jobId.Value], ct)` OR use LINQ: `db.IngestionJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)`. The LINQ approach works because EF Core translates `==` through the value converter. Prefer LINQ for clarity.

**Warning signs:** `FindAsync` returns null even when the entity exists. Runtime exception about key type mismatch.

**Confidence:** MEDIUM — based on known EF Core behavior with custom key types; verify during implementation

### Pitfall 2: `IngestionJobQueue` Channel Type Change

**What goes wrong:** `IngestionJobQueue` is `Channel<Guid>`. If service signatures change to use `IngestionJobId` but the queue stays `Guid`, implicit conversion handles the boundary — but it creates an asymmetry where only the queue boundary uses raw Guid.

**Why it happens:** The channel is an infrastructure component; it's tempting to leave it as `Guid`.

**How to avoid:** Change `Channel<Guid>` to `Channel<IngestionJobId>` in Plan 2. Update `TryEnqueue(Guid jobId)` to `TryEnqueue(IngestionJobId jobId)` and `ReadAllAsync()` return to `IAsyncEnumerable<IngestionJobId>`. The background service and endpoint both update accordingly.

**Warning signs:** Implicit conversion at queue boundary — subtle mixing of Guid and typed ID.

### Pitfall 3: DcaConfiguration Singleton Guid Literal in Check Constraint

**What goes wrong:** The DB check constraint `"id = '00000000-0000-0000-0000-000000000001'::uuid"` is a raw SQL string in `OnModelCreating`. This doesn't change with typed IDs (it's a DB-level constraint), but the C# entity default must be set to `DcaConfigurationId.Singleton`.

**Why it happens:** Easy to forget that the singleton default in C# must match the DB constraint.

**How to avoid:** Define `DcaConfigurationId.Singleton` as a static readonly field on the ID type, and use it everywhere (entity initializer, `ConfigurationService`). Never repeat the Guid literal.

**Warning signs:** `ConfigurationService` creates a `DcaConfiguration` with a different ID value, violating the DB constraint at runtime.

### Pitfall 4: `Customizations.AddFactoryMethodForGuids` Generates `FromNewGuid()` with v4 Guid

**What goes wrong:** Using `var id = PurchaseId.FromNewGuid()` creates a v4 UUID (random), not a v7 (time-ordered). The codebase relies on UUIDv7 for index-friendly ordering.

**Why it happens:** Vogen's built-in `FromNewGuid()` calls `Guid.NewGuid()` internally. This is confirmed in the Context7 snapshot: `public static MyVo FromNewGuid() => From(global::System.Guid.NewGuid());`

**How to avoid:** Do NOT use `Customizations.AddFactoryMethodForGuids` in VogenDefaults. Instead, hand-write `New()` on each typed ID:
```csharp
public static PurchaseId New() => From(Guid.CreateVersion7());
```
This is just one line per ID type. The explicit `New()` name is also cleaner than `FromNewGuid()` per the context decision.

**Warning signs:** Created IDs are not time-ordered in the database (check by comparing ID ordering with timestamp ordering).

### Pitfall 5: `BaseEntity` Constructor UUIDv7 Timing

**What goes wrong:** Current `BaseEntity` constructor calls `Guid.CreateVersion7(CreatedAt)` — passing the `AuditedEntity.CreatedAt` timestamp for time-ordering. After making `BaseEntity<TId>`, the constructor can no longer auto-generate the ID because it doesn't know `TId`.

**Why it happens:** Generic base class cannot call `TId.New()` — there's no constraint to enforce a `New()` static factory (C# doesn't support static abstract members on interface constraints at the value type level yet for this pattern).

**How to avoid:** Remove the ID auto-generation from the base class constructor. Each entity must be initialized with its ID at the creation site using `PurchaseId.New()`. This is already the pattern used by `DcaConfiguration` and `IngestionJob` (both explicitly set `Id`). Confirm `Purchase` is always created with an explicit `{ Id = PurchaseId.New() }` in the call site (`DcaExecutionService`).

**Warning signs:** Entity `Id` is uninitialized (default Guid / default typed ID) at time of `SaveChanges`.

### Pitfall 6: Snapshot Test Serialization

**What goes wrong:** Existing snapshot tests in `BacktestSimulatorTests` snapshot `PurchaseLogEntry` data. If `PurchaseLogEntry` starts using `PurchaseId`, the snapshot files will need updating.

**Why it happens:** `PurchaseLogEntry` is a backtest model, not an EF entity — it may not need typed IDs. But if any snapshot-tested data includes an ID field, the serialized form changes.

**How to avoid:** Backtest models (`PurchaseLogEntry`, `BacktestResult`) are NOT EF entities — they should keep raw Guid or avoid IDs entirely. Confirm whether any snapshot-tested data includes ID fields before updating. The CONTEXT.md says "Only EF Core-mapped entity properties get typed IDs" — backtest models are exempt.

**Warning signs:** Snapshot test failures after Plan 2 for non-entity models.

---

## Code Examples

Verified patterns from official Vogen docs and Context7:

### Complete ID Type Definition (Recommended Pattern)

```csharp
// Models/Ids/PurchaseId.cs
// Source: Vogen official docs + codebase-specific adaptation

using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct PurchaseId
{
    // Hand-written: UUIDv7 factory (Vogen's FromNewGuid() uses Guid.NewGuid() = v4)
    public static PurchaseId New() => From(Guid.CreateVersion7());
}
```

### Assembly-Level VogenDefaults

```csharp
// Models/Ids/VogenGlobalConfig.cs (or any .cs file in the assembly)
// Source: Vogen README + Context7

using Vogen;

[assembly: VogenDefaults(
    underlyingType: typeof(Guid),
    conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson,
    castOperator: CastOperator.Implicit)]
    // CastOperator.Implicit = both directions: Guid → TypedId AND TypedId → Guid
    // This satisfies "implicit conversion both directions" constraint
```

**Note on CastOperator:** Setting `castOperator: CastOperator.Implicit` globally means every typed ID can be assigned from a raw `Guid` and compared to a raw `Guid` without explicit cast. This is ergonomic but loses some type-safety at Guid-to-TypedId direction. Given the context decision "feel like Guids with extra safety," this is correct.

### EF Core Registration

```csharp
// Infrastructure/Data/TradingBotDbContext.cs (addition)
// Source: Vogen EF Core integration docs + Context7 snapshots

protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);
    // Vogen generates this extension method for .NET 8+
    // Registers EfCoreValueConverter + EfCoreValueComparer for all Vogen types
    // in this assembly that have Conversions.EfCoreValueConverter
    configurationBuilder.RegisterAllInEfCoreConverters();
}
```

### Generic BaseEntity

```csharp
// BuildingBlocks/BaseEntity.cs — refactored
// NOTE: Keep AuditedEntity unchanged

namespace TradingBot.ApiService.BuildingBlocks;

public abstract class BaseEntity<TId> : AuditedEntity
{
    public TId Id { get; init; } = default!;
    // Removed: automatic Guid.CreateVersion7() — caller must set Id via .New()
}
```

### Entity After Typed ID (Purchase example)

```csharp
// Models/Purchase.cs — after Plan 2
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

public class Purchase : BaseEntity<PurchaseId>
{
    public DateTimeOffset ExecutedAt { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Multiplier { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public bool IsDryRun { get; set; }
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }
    public string? MultiplierTier { get; set; }
    public decimal DropPercentage { get; set; }
    public decimal High30Day { get; set; }
    public decimal Ma200Day { get; set; }
}
```

### Creation Site (DcaExecutionService)

```csharp
// BEFORE
var purchase = new Purchase
{
    ExecutedAt = DateTimeOffset.UtcNow,
    ...
};
// BaseEntity constructor set Id = Guid.CreateVersion7(CreatedAt)

// AFTER
var purchase = new Purchase
{
    Id = PurchaseId.New(),   // explicit UUIDv7
    ExecutedAt = DateTimeOffset.UtcNow,
    ...
};
```

### Minimal API Route Binding (DataEndpoints)

```csharp
// BEFORE
group.MapGet("/ingest/{jobId:guid}", GetJobStatusAsync);
private static async Task<IResult> GetJobStatusAsync(Guid jobId, ...) { }

// AFTER — Vogen generates TryParse, ASP.NET calls it automatically
group.MapGet("/ingest/{jobId:guid}", GetJobStatusAsync);
private static async Task<IResult> GetJobStatusAsync(IngestionJobId jobId, ...) { }
// {jobId:guid} constraint still validates the string is a valid Guid format
// ASP.NET then calls IngestionJobId.TryParse(routeValue, null, out var jobId)
```

### PurchaseDto — No Change to Dashboard Contract

```csharp
// Endpoints/DashboardDtos.cs — PurchaseDto.Id stays Guid in JSON
// Vogen STJ converter serializes PurchaseId → "plain-guid-string"
// Dashboard receives exactly what it received before

// HOWEVER: the C# DTO can optionally use the typed ID for internal type safety:
public record PurchaseDto(
    PurchaseId Id,    // serializes as plain Guid string to JSON
    DateTimeOffset ExecutedAt,
    ...
);
// OR keep as Guid Id — no behavioral change to dashboard
// Recommendation: Keep PurchaseDto.Id as Guid (DTOs are API surface, not domain)
```

### DcaConfigurationId Singleton

```csharp
[ValueObject<Guid>]
public readonly partial struct DcaConfigurationId
{
    public static DcaConfigurationId New() => From(Guid.CreateVersion7());

    // Preserve the singleton pattern used by DcaConfiguration entity
    public static readonly DcaConfigurationId Singleton =
        From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}
```

### FindAsync Replacement Pattern

```csharp
// BEFORE (works with raw Guid key)
var job = await db.IngestionJobs.FindAsync([jobId], ct);

// AFTER (prefer LINQ for typed ID PKs)
var job = await db.IngestionJobs
    .FirstOrDefaultAsync(j => j.Id == jobId, ct);
// EF Core translates == through the registered value converter
```

### DashboardEndpoints PurchaseDto Projection

```csharp
// DashboardEndpoints.cs — .Select() projection
// After Plan 2, p.Id is PurchaseId but PurchaseDto expects Guid
// Option A: Keep PurchaseDto.Id as Guid, use implicit cast
.Select(p => new PurchaseDto(
    Id: p.Id,          // implicit PurchaseId → Guid via CastOperator.Implicit
    ExecutedAt: p.ExecutedAt,
    ...
))
// Option B: Change PurchaseDto.Id to PurchaseId (serializes identically to JSON)
// Recommendation: Option A — DTOs stay as Guid, entities use typed IDs
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hand-written typed ID structs with custom converters | Vogen source generator | Vogen 1.0+ | ~300 lines generated per ID type, zero maintenance |
| Per-property EF converter registration in `OnModelCreating` | `ConfigureConventions.RegisterAllInEfCoreConverters()` | EF Core 6 / .NET 8 | Single registration replaces N per-property calls |
| `Guid.NewGuid()` for entity IDs | `Guid.CreateVersion7()` in .NET 9+ | .NET 9 Preview 7 | Time-ordered IDs, friendly to B-tree indexes |
| `TryParse` compatibility shims for minimal APIs | Vogen 4.0+ auto-generates correct TryParse | Vogen 4.0 | No manual shims needed |

**Deprecated/outdated:**
- Custom `TypeConverter` shims for model binding: Vogen generates `TypeConverter` AND `IParsable<T>` automatically
- `[EfCoreConverter<T>]` marker class in separate project: Not needed here — same project, use assembly attribute approach

---

## Codebase Impact Analysis

Based on code review, here are all the sites that must change in Plan 2:

### Entities (4 files)
- `Models/Purchase.cs` — `BaseEntity` → `BaseEntity<PurchaseId>`, add `Id = PurchaseId.New()` init
- `Models/IngestionJob.cs` — `AuditedEntity` → `BaseEntity<IngestionJobId>`, remove explicit `Id` assignment, use `IngestionJobId.New()`
- `Models/DcaConfiguration.cs` — `AuditedEntity` → `BaseEntity<DcaConfigurationId>`, use `DcaConfigurationId.Singleton`
- `Models/DailyPrice.cs` — **No change**: composite key `(Date, Symbol)`, extends `AuditedEntity` directly, no ID to type

### Events (1 file)
- `Application/Events/PurchaseCompletedEvent.cs` — `Guid PurchaseId` → `PurchaseId PurchaseId`

### Services (3 files)
- `Application/Services/DcaExecutionService.cs` — `purchase.Id` becomes `PurchaseId`, creation site needs `Id = PurchaseId.New()`
- `Application/Services/HistoricalData/DataIngestionService.cs` — `Guid jobId` → `IngestionJobId jobId`, `FindAsync` → `FirstOrDefaultAsync`
- `Application/Services/HistoricalData/IngestionJobQueue.cs` — `Channel<Guid>` → `Channel<IngestionJobId>`

### Infrastructure (2 files)
- `Infrastructure/Data/TradingBotDbContext.cs` — Add `ConfigureConventions` override; `HasKey(e => e.Id)` still works (EF Core knows the type)
- `Infrastructure/Data/DesignTimeDbContextFactory.cs` — No change expected

### Endpoints (2 files)
- `Endpoints/DataEndpoints.cs` — `Guid jobId` → `IngestionJobId jobId`
- `Endpoints/DashboardEndpoints.cs` — `p.Id` projection uses implicit cast to `Guid` for `PurchaseDto`

### Configuration Service (1 file)
- `Application/Services/ConfigurationService.cs` — `Guid.Parse("00000000-...")` → `DcaConfigurationId.Singleton`

### Tests (likely no changes)
- `MultiplierCalculatorTests.cs` — Does not use entity IDs
- `BacktestSimulatorTests.cs` — Uses `PurchaseLogEntry` (backtest model, not entity) — no typed ID needed

---

## Open Questions

1. **`DailyPrice` identity — is `DailyPriceId` truly required?**
   - What we know: `DailyPrice` has a composite key `(DateOnly Date, string Symbol)` — no Guid column exists
   - What's unclear: The context says "DailyPriceId" is in scope. Does the user want a composite-key typed ID or a new surrogate Guid PK?
   - Recommendation: During planning, clarify this. If the DB schema stays unchanged (no new Guid column), `DailyPriceId` cannot be a Vogen Guid wrapper. It either doesn't exist, or it's a hand-written composite key record type. The safest plan: skip `DailyPriceId` (the entity's identity is already captured by its composite key) and document the exception. The phase description says "schema unchanged."

2. **`CastOperator.Implicit` global default — potential footgun?**
   - What we know: Implicit Guid → TypedId means you can accidentally assign any Guid to any typed ID (defeating cross-type safety)
   - What's unclear: Whether the codebase has code paths where mixing Guids from different domains could occur under implicit conversion
   - Recommendation: This is Claude's Discretion per context. Given the codebase currently has only domain-facing Guids (no places where a PurchaseId could be confused with an IngestionJobId in practice), `CastOperator.Implicit` is acceptable and matches the ergonomics goal. An alternative is `CastOperator.Explicit` for Guid→TypedId (prevents accidents) and implicit for TypedId→Guid (allows DTO projection). This would be configured per-type if global default is insufficient.

3. **`IngestionJobQueue` channel type — should it use typed IDs?**
   - What we know: Context says "service and handler method signatures use typed IDs throughout"
   - What's unclear: Whether `IngestionJobQueue` is considered a "service" or "infrastructure channel"
   - Recommendation: Change it to `Channel<IngestionJobId>` — clean break, no Guid at any internal boundary

---

## Sources

### Primary (HIGH confidence)
- `/stevedunn/vogen` (Context7) — VogenDefaults assembly attribute, `AddFactoryMethodForGuids`, `FromNewGuid()`, `TryParse` generation, EF Core value converter snapshots, `RegisterAllInEfCoreConverters` generated method, casting operator configuration
- [Vogen EF Core Integration Docs](https://stevedunn.github.io/Vogen/efcoreintegrationhowto.html) — `ConfigureConventions`, `RegisterAllInVogenEfCoreConverters`, converter registration approaches
- [Vogen FAQ](https://stevedunn.github.io/Vogen/faq.html) — `AddFactoryMethodForGuids` details, struct type selection guidance
- Codebase review of all 4 entities, DbContext, endpoints, services, IngestionJobQueue

### Secondary (MEDIUM confidence)
- [Vogen GitHub #559](https://github.com/SteveDunn/Vogen/issues/559) — ASP.NET 8 minimal API compatibility, TryParse fix in Vogen 4.0+
- [Microsoft Learn: Parameter binding in Minimal API](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0) — TryParse / IParsable binding mechanism
- [NuGet Gallery Vogen 8.0.4](https://www.nuget.org/packages/vogen) — Version confirmation

### Tertiary (LOW confidence — for awareness only)
- [Medium: Standardizing Strongly-Typed IDs UUIDv7 in .NET](https://medium.com/@anderson.buenogod/standardizing-strongly-typed-ids-uuidv7-in-a-clean-hex-net-solution-template-58056b1159fd) — community pattern for UUIDv7 + typed IDs

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Vogen 8.0.4 is already decided; API is verified via Context7
- Architecture: HIGH — BaseEntity<TId> is simple C# generics; patterns are verified against actual entity code
- EF Core registration: HIGH — `ConfigureConventions` approach verified via Context7 and official docs
- Minimal API binding: HIGH — Vogen issue #559 confirms TryParse fix; behavior verified against ASP.NET docs
- Pitfalls: MEDIUM-HIGH — FindAsync behavior with value-converted keys needs runtime verification
- DailyPrice special case: HIGH — composite key is clear from code review, no Guid column exists

**Research date:** 2026-02-18
**Valid until:** 2026-03-18 (Vogen is stable; EF Core 10 / ASP.NET 10 patterns are stable)

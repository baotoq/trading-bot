# Phase 15: Rich Aggregate Roots - Research

**Researched:** 2026-02-19
**Domain:** DDD aggregate roots, EF Core private constructors, static factory methods, domain events collection, behavior methods with invariant enforcement
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Aggregate Boundaries:**
- OutboxMessage stays as infrastructure record -- no DDD ceremony, it's plumbing not domain
- EF Core parameterless constructors: **protected** (not private) -- allows potential inheritance
- Private setters on **aggregate roots only** -- simpler entities like DailyPrice keep public setters if they're data carriers
- Generic `AggregateRoot<TId> : BaseEntity<TId>` -- carries both ID typing and domain event collection in one hierarchy
- Vogen value objects left as-is -- already immutable by design, no need for private setters on top

**Behavior Method Design:**
- Factory methods take **value objects** directly (e.g., `Purchase.Create(Symbol, Price, Quantity, UsdAmount)`) -- caller constructs VOs, factory validates relationships
- Factory methods return **entity directly** (not ErrorOr<T>) -- throws on invalid input; Result pattern comes in Phase 16
- **Fine-grained behavior methods** on DcaConfiguration: `UpdateDailyAmount()`, `UpdateSchedule()`, `UpdateTiers()`, `UpdateBearMarket()` etc. -- each enforces its own invariants
- Aggregate enforces **tier ordering** in `UpdateTiers()` -- ascending drop percentages, no overlaps, sane multiplier ranges

**Domain Event Collection:**
- Both **creation and mutation** raise domain events -- PurchaseCreated, ConfigurationUpdated, TiersUpdated, etc.
- Events carry **identity only** (aggregate ID) -- handlers load the aggregate if they need details
- **Refactor existing events now** to use AggregateRoot.AddDomainEvent() -- clean break, single pattern going forward
- Simple `List<IDomainEvent>` with `AddDomainEvent()` and `ClearDomainEvents()` -- no event versioning or metadata

### Claude's Discretion

- **Aggregate Boundaries:** Evaluate which entities are aggregate roots vs child entities vs data carriers based on which have real business invariants to enforce
- **DcaConfiguration MultiplierTiers:** Whether DcaConfiguration owns MultiplierTiers as child entities or keeps them as JSON column -- evaluate trade-offs based on current schema
- **Dashboard config updates:** Whether dashboard config updates go through aggregate methods or bypass them -- evaluate based on where invariant enforcement matters most

### Deferred Ideas (OUT OF SCOPE)

None -- discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DM-01 | Base entity hierarchy includes AggregateRoot base class with domain event collection | `AggregateRoot<TId>` extends `BaseEntity<TId>`, adds `private List<IDomainEvent> _domainEvents`, exposes `IReadOnlyList<IDomainEvent> DomainEvents`, `AddDomainEvent(IDomainEvent)`, `ClearDomainEvents()` |
| DM-02 | Purchase aggregate enforces invariants (price > 0, quantity > 0, valid symbol) via factory method | `Purchase.Create(Symbol, Price, Quantity, UsdAmount, Multiplier, ...)` -- static factory calls protected constructor, validates cross-field invariants, raises `PurchaseCreated` domain event |
| DM-03 | DcaConfiguration aggregate enforces invariants (tiers ascending, daily amount > 0, valid schedule) via encapsulated behavior methods | Fine-grained methods `UpdateDailyAmount()`, `UpdateSchedule()`, `UpdateTiers()`, `UpdateBearMarket()` each validate their own invariants and raise appropriate domain events |
| DM-04 | Entities use private setters -- state changes only through domain methods | Aggregate roots get `{ get; private set; }` on all properties; data carriers (DailyPrice, IngestionJob) evaluated per invariant ownership |
</phase_requirements>

---

## Summary

Phase 15 is a structural refactor with no new dependencies -- all tools (EF Core, MediatR, Vogen) are already installed. The work is pure C# design: introducing an `AggregateRoot<TId>` base class with domain event collection, then upgrading `Purchase` and `DcaConfiguration` to true aggregates with private constructors, static factory methods, and behavior methods.

The two key EF Core constraints to navigate are: (1) EF Core requires a parameterless constructor to materialize entities from queries -- the decision is `protected` (not `private`) to allow inheritance while blocking casual external construction; and (2) EF Core's backing fields pattern must be used for collection navigation properties like `MultiplierTiers` if they become a proper list. Since `MultiplierTiers` stays as a `jsonb` column (raw decimal data, not a child entity collection), EF Core reads/writes it as a single column -- no backing field ceremony needed there.

The domain events pattern follows the Microsoft eShopOnContainers reference implementation exactly: events accumulate in an in-memory `List<IDomainEvent>` on each aggregate, dispatched AFTER `SaveChanges` (via a `SaveChangesInterceptor` or explicit dispatch). The current codebase uses `IPublisher.Publish()` directly in `DcaExecutionService` -- Phase 15 migrates this to `AddDomainEvent()` on the aggregate; Phase 17 (domain events dispatch) will handle the interceptor wiring.

**Primary recommendation:** Introduce `AggregateRoot<TId>` in `BuildingBlocks/`, refactor `Purchase` first (cleaner invariants), then `DcaConfiguration` (more behavior methods), wire `ConfigurationService` to call aggregate methods instead of property-setting, and update `DcaExecutionService` to use `Purchase.Create()`. Keep `DailyPrice` and `IngestionJob` as data carriers with public setters since they have no cross-field invariants to enforce.

---

## Current Codebase State

### Entity Hierarchy (as of Phase 14)

```
AuditedEntity
  CreatedAt: DateTimeOffset (init)
  UpdatedAt: DateTimeOffset? (set)

BaseEntity<TId> : AuditedEntity
  Id: TId (init)

Purchase : BaseEntity<PurchaseId>        <-- target aggregate
DcaConfiguration : BaseEntity<DcaConfigurationId>  <-- target aggregate
DailyPrice : AuditedEntity               <-- data carrier, composite PK
IngestionJob : BaseEntity<IngestionJobId>  <-- status tracker (evaluate)
```

### What Changes in This Phase

**Add:** `AggregateRoot<TId> : BaseEntity<TId>` with domain event collection
**Modify:** `Purchase` and `DcaConfiguration` inherit from `AggregateRoot<TId>`, get protected constructors and private setters
**Modify:** `DcaExecutionService` uses `Purchase.Create()` factory
**Modify:** `ConfigurationService` calls aggregate behavior methods
**Refactor:** Existing domain events (PurchaseCompletedEvent etc.) get raised via `AddDomainEvent()` instead of direct `IPublisher.Publish()`

**No change:** `DailyPrice`, `IngestionJob`, `OutboxMessage`, EF Core model configuration, DB schema (no migration needed)

---

## Standard Stack

### Core (all already installed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MediatR | installed | `IDomainEvent : INotification`; `IPublisher` dispatches events | Already wired; `IDomainEvent` interface already exists in codebase |
| Entity Framework Core | installed | Backing fields, protected constructors, private setters | Already used throughout |
| Vogen | 8.0.4 | Value objects used in factory method signatures | Already installed, phase 14 complete |

### No New Dependencies

This phase requires zero new NuGet packages. It is pure domain model refactoring within existing infrastructure.

---

## Architecture Patterns

### Pattern 1: AggregateRoot Base Class

The canonical pattern from Microsoft's eShopOnContainers reference (HIGH confidence, official MS docs):

```csharp
// Source: BuildingBlocks/AggregateRoot.cs (new file)
public abstract class AggregateRoot<TId> : BaseEntity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Key decisions reflected:**
- `protected void AddDomainEvent` -- only the aggregate itself raises events (encapsulation)
- `public void ClearDomainEvents` -- infrastructure calls this after dispatching
- `IReadOnlyList<IDomainEvent>` -- read-only external view; no external mutation
- Generic `TId` -- inherits ID typing from `BaseEntity<TId>`

**Why not on `BaseEntity<TId>` directly?**
`DailyPrice` inherits `AuditedEntity` (not `BaseEntity`) and is a data carrier. `IngestionJob` is a status tracker. Putting domain event collection on `BaseEntity` would pollute non-aggregate classes unnecessarily.

---

### Pattern 2: Protected Parameterless Constructor for EF Core

EF Core requires a way to materialize entities from query results. It searches constructors in this order:
1. Parameterized constructor whose parameters match mapped property names (by convention)
2. Parameterless constructor (fallback)

When using static factory methods with private constructors, EF Core needs the parameterless constructor to rehydrate. Decision is `protected` (not `private`) per context decisions (allows inheritance, works with EF Core).

```csharp
// Source: EF Core docs (constructors.md) -- HIGH confidence
public class Purchase : AggregateRoot<PurchaseId>
{
    // For EF Core materialization
    protected Purchase() { }

    // Private constructor -- only factory can call
    private Purchase(
        PurchaseId id,
        Symbol symbol,
        Price price,
        Quantity quantity,
        UsdAmount cost,
        Multiplier multiplier,
        string? multiplierTier,
        Percentage dropPercentage,
        decimal high30Day,
        decimal ma200Day,
        bool isDryRun)
    {
        Id = id;
        Symbol = symbol;
        Price = price;
        Quantity = quantity;
        Cost = cost;
        Multiplier = multiplier;
        MultiplierTier = multiplierTier;
        DropPercentage = dropPercentage;
        High30Day = high30Day;
        Ma200Day = ma200Day;
        IsDryRun = isDryRun;
        ExecutedAt = DateTimeOffset.UtcNow;
        Status = PurchaseStatus.Pending;
    }

    public static Purchase Create(
        Symbol symbol,
        Price price,
        Quantity quantity,
        UsdAmount cost,
        Multiplier multiplier,
        string? multiplierTier,
        Percentage dropPercentage,
        decimal high30Day,
        decimal ma200Day,
        bool isDryRun)
    {
        // Cross-field invariants enforced here if any
        // Individual field validation already done by Vogen VOs
        var purchase = new Purchase(
            PurchaseId.New(), symbol, price, quantity, cost,
            multiplier, multiplierTier, dropPercentage,
            high30Day, ma200Day, isDryRun);

        purchase.AddDomainEvent(new PurchaseCreatedEvent(purchase.Id));
        return purchase;
    }
}
```

**Note on `Id` setter:** Current `BaseEntity<TId>` uses `public TId Id { get; init; }`. This must change to `protected set` or remain `init` -- both work with the protected constructor pattern. `init` works fine since the private constructor sets it.

---

### Pattern 3: Private Setters on Aggregate Properties

EF Core treats private setters as read-write for mapping purposes -- this is fully supported and is the EF Core DDD pattern:

```csharp
// Source: EF Core docs (constructors.md) -- HIGH confidence
public Price Price { get; private set; }
public Quantity Quantity { get; private set; }
public UsdAmount Cost { get; private set; }
public PurchaseStatus Status { get; private set; }
```

EF Core will write directly to the backing field or via the private setter during materialization. No additional EF Core configuration needed.

---

### Pattern 4: Behavior Methods with Invariant Enforcement

DcaConfiguration gets fine-grained behavior methods. Each method:
1. Validates its own inputs
2. Applies the change
3. Raises a specific domain event

```csharp
public class DcaConfiguration : AggregateRoot<DcaConfigurationId>
{
    protected DcaConfiguration() { }

    public static DcaConfiguration Create(
        DcaConfigurationId id,
        UsdAmount baseDailyAmount,
        int dailyBuyHour,
        int dailyBuyMinute,
        int highLookbackDays,
        bool dryRun,
        int bearMarketMaPeriod,
        Multiplier bearBoostFactor,
        Multiplier maxMultiplierCap,
        List<MultiplierTierData> multiplierTiers)
    {
        ValidateTiers(multiplierTiers);
        ValidateSchedule(dailyBuyHour, dailyBuyMinute);

        var config = new DcaConfiguration
        {
            Id = id,
            BaseDailyAmount = baseDailyAmount,
            // ... all properties set
        };

        config.AddDomainEvent(new DcaConfigurationCreatedEvent(config.Id));
        return config;
    }

    public void UpdateDailyAmount(UsdAmount newAmount)
    {
        BaseDailyAmount = newAmount; // Vogen already guarantees > 0
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateSchedule(int hour, int minute)
    {
        if (hour < 0 || hour > 23) throw new ArgumentException("Hour must be 0-23");
        if (minute < 0 || minute > 59) throw new ArgumentException("Minute must be 0-59");
        DailyBuyHour = hour;
        DailyBuyMinute = minute;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateTiers(List<MultiplierTierData> tiers)
    {
        ValidateTiers(tiers); // ascending, no overlaps
        MultiplierTiers = tiers;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new TiersUpdatedEvent(Id));
    }

    private static void ValidateTiers(List<MultiplierTierData> tiers)
    {
        if (!tiers.Any()) return;
        var sorted = tiers.OrderBy(t => t.DropPercentage).ToList();
        if (!tiers.SequenceEqual(sorted))
            throw new ArgumentException("Tiers must be sorted ascending by DropPercentage");
        // Sane multiplier range: 0 < multiplier <= 20 already enforced by Multiplier VO
        // when tiers are converted; raw decimal in MultiplierTierData needs explicit check
        if (tiers.Any(t => t.Multiplier <= 0 || t.Multiplier > 20))
            throw new ArgumentException("Tier multiplier must be between 0 (exclusive) and 20 (inclusive)");
    }
}
```

---

### Pattern 5: Domain Event Dispatch (Phase 15 scope vs Phase 17)

**Phase 15 scope:** Aggregates call `AddDomainEvent()` -- events accumulate in memory.
**Phase 17 scope:** SaveChangesInterceptor dispatches accumulated events after commit.

In Phase 15, the existing explicit `IPublisher.Publish()` calls in `DcaExecutionService` should be refactored to use `Purchase.AddDomainEvent()`. However, since Phase 17 (interceptor) isn't wired yet, Phase 15 can either:

**Option A (recommended):** Keep existing `IPublisher.Publish()` calls in the service layer for now, but ALSO call `AddDomainEvent()` on the aggregate -- events will just accumulate and get cleared without dispatch until Phase 17 adds the interceptor. This makes aggregates correct while avoiding a big-bang migration risk.

**Option B:** Move ALL dispatch to the service layer reading from `purchase.DomainEvents` immediately after save. This is functionally equivalent to current behavior.

**Recommendation:** Option A. The Phase 15 goal is aggregate correctness (factory methods, private setters, behavior methods, events accumulating). Phase 17 will wire the interceptor that actually dispatches them. The existing `IPublisher.Publish()` calls in service layer can remain as a bridge.

---

### Aggregate Classification Decision

Based on codebase analysis:

**`Purchase` → AggregateRoot**
- Has real business invariants: price > 0, quantity >= 0, valid status transitions
- Status transitions (Pending → Filled/Failed/Cancelled) are domain behavior
- Factory method enforces creation invariants

**`DcaConfiguration` → AggregateRoot**
- Has significant domain invariants: tier ordering, schedule validity, amount positivity
- ConfigurationService currently does direct property assignment bypassing invariants
- Fine-grained behavior methods restore invariant enforcement at the model level

**`DailyPrice` → Data Carrier (no change)**
- Simple time-series record: Date + Symbol + OHLCV
- Composite PK means it can't easily use `BaseEntity<TId>` pattern
- No behavioral invariants beyond what Vogen VOs already enforce
- Recommendation: keep public setters; it's bulk-inserted, not mutated

**`IngestionJob` → Data Carrier with State (evaluate)**
- Status field (Pending → Started → Completed/Failed) is a state machine
- Current: `DataIngestionService` sets properties directly
- Assessment: Has status transition logic but it's simple. Status transitions COULD become behavior methods (`Start()`, `Complete(int recordsFetched)`, `Fail(string error)`) -- this would be clean DDD but is borderline for this phase. Given phase focuses on entities with "real business invariants to enforce," IngestionJob is borderline. It does NOT affect `DcaExecutionService` core flow. Recommendation: Make it an aggregate with status behavior methods since status transitions are clearly domain behavior (not just data).

---

## Claude's Discretion: MultiplierTiers Decision

**Current state:** `DcaConfiguration.MultiplierTiers` is `List<MultiplierTierData>` stored as `jsonb`. `MultiplierTierData` is a `record(decimal DropPercentage, decimal Multiplier)`.

**Option A: Keep as JSON column (raw decimal records)**
- No schema change, no migration
- `UpdateTiers()` behavior method validates and replaces the list atomically
- Raw decimal in `MultiplierTierData` -- invariant checking in `ValidateTiers()` uses raw comparison
- Simpler: no EF Core child entity configuration needed

**Option B: Convert to child entities (separate table)**
- Proper DDD: child entities owned by DcaConfiguration aggregate
- Requires new migration (new table)
- More complex EF Core setup (owned collection vs owned table)
- Adds indirection when calling MultiplierCalculator which expects `IReadOnlyList<MultiplierTier>` (from DcaOptions)

**Recommendation: Option A (keep JSON column)**
The jsonb approach was specifically chosen in Phase 14 research for good reasons (research note: "Pitfall 3: jsonb STJ serialization"). Converting to a table adds schema complexity without behavioral benefit. The `MultiplierTierData` record with raw decimals inside jsonb is fine -- `UpdateTiers()` enforces the invariants before persistence. The Vogen value objects aren't needed inside the jsonb blob (Phase 14 decision was explicit: "MultiplierTierData inside jsonb keeps raw decimal").

---

## Claude's Discretion: Dashboard Config Updates

**Current flow:** Dashboard → `ConfigurationEndpoints` → `ConfigurationService.UpdateAsync(DcaOptions)` → validates with `DcaOptionsValidator` → maps properties onto entity → `SaveChanges()`

**Question:** Should `ConfigurationService.UpdateAsync` call aggregate behavior methods or continue direct property mapping?

**Assessment:** If `DcaConfiguration` has behavior methods (`UpdateDailyAmount()`, `UpdateSchedule()`, etc.), but `ConfigurationService` still does `MapFromOptions(entity, options)` (direct property assignment), the aggregate's encapsulation is broken. The whole point of behavior methods is that ALL state changes go through them.

**Recommendation: Yes, call aggregate behavior methods.** `ConfigurationService.UpdateAsync` should call the relevant `UpdateXxx()` methods on the loaded aggregate. The existing `DcaOptionsValidator` validation can remain as an early-exit before loading the entity (fail fast at the application boundary), but the aggregate also validates its own invariants -- this is acceptable defense-in-depth, not redundancy.

The migration path: `ConfigurationService.UpdateAsync` loads the aggregate, then calls `config.UpdateDailyAmount(options.BaseDailyAmount)`, `config.UpdateSchedule(...)`, `config.UpdateTiers(...)`, `config.UpdateBearMarket(...)` in sequence. If any throw (invariant violation), the transaction doesn't reach `SaveChanges`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Domain event dispatch infrastructure | Custom event bus | MediatR (already installed) | Already wired, `IDomainEvent : INotification` already defined |
| EF Core DDD-compatible entities | Complex ORM workarounds | EF Core's built-in private setter + protected constructor support | Native EF Core feature since v1.1 |
| Value object validation in factory | Custom validation framework | Vogen `ValueObjectValidationException` (already thrown on `From()`) | Phase 14 complete; factory receives pre-validated VOs |

---

## Common Pitfalls

### Pitfall 1: Private Constructor Blocks EF Core Materialization

**What goes wrong:** EF Core cannot construct the entity from a query result, throws `InvalidOperationException` at runtime (not compile time).

**Why it happens:** EF Core's materialization finds no accessible constructor.

**How to avoid:** Use `protected` (not `private`) for the parameterless constructor. Per context decision: "EF Core parameterless constructors: protected (not private)."

**Warning signs:** Tests that create entities in-memory work fine; integration tests that load entities from DB fail at runtime.

---

### Pitfall 2: `init` Setters Break Protected Constructor Pattern

**What goes wrong:** `BaseEntity<TId>` currently uses `public TId Id { get; init; }`. A protected constructor in a derived class trying to set `Id` after construction fails because `init` setters can only be called during object initialization syntax.

**Why it happens:** `init` setters are only accessible in object initializers and constructors of the declaring type and derived types -- actually `init` IS accessible from derived constructors in C#. This is NOT a problem. `Id = id;` inside a derived constructor works for `init` properties.

**Verification:** `init` accessors are treated like setters within constructors and `with` expressions. Derived class constructors can set `init` properties of base classes.

**Confidence:** HIGH (C# language specification)

---

### Pitfall 3: `UpdatedAt` Must Be Set on Mutations

**What goes wrong:** Behavior methods mutate state but forget to update `UpdatedAt`, leaving audit trail stale.

**Why it happens:** `UpdatedAt` is on `AuditedEntity` with `public DateTimeOffset? UpdatedAt { get; set; }`. The aggregate's behavior methods must explicitly set it.

**How to avoid:** Each `UpdateXxx()` behavior method ends with `UpdatedAt = DateTimeOffset.UtcNow;`. Alternatively, the `AggregateRoot` base class could override `AddDomainEvent` to auto-set `UpdatedAt`, but this couples timestamps to event raising (fragile). Explicit is better.

---

### Pitfall 4: Domain Events in Tests -- Events Accumulate, Not Dispatched

**What goes wrong:** Unit tests that exercise aggregate factory/behavior methods expect Telegram notifications or other side effects, but those side effects are now decoupled through domain events not yet dispatched.

**Why it happens:** Phase 15 moves to `AddDomainEvent()` pattern; dispatch happens in Phase 17. Until then, events accumulate.

**How to avoid:** Unit tests assert on `aggregate.DomainEvents` directly (assert events were raised, not side effects). Existing integration tests using full `IPublisher` dispatch may break if they relied on the old `publisher.Publish()` call in `DcaExecutionService` -- these need updating when `DcaExecutionService` is refactored to use `Purchase.Create()`.

---

### Pitfall 5: EF Core Cannot Set `private readonly` Fields on Protected Constructor

**What goes wrong:** If the domain events backing field `_domainEvents` is `readonly`, EF Core might try to set it during materialization (it won't, but this is a concern for navigation properties).

**Why it happens:** EF Core materialization uses reflection or compiled expressions to set properties/fields after construction. `readonly` fields can only be set in the constructor.

**How to avoid:** The `_domainEvents` list is NOT an EF Core-mapped property -- it's a pure in-memory collection. EF Core ignores it entirely. Use `readonly` safely: `private readonly List<IDomainEvent> _domainEvents = [];`. EF Core ignores unmapped fields.

---

### Pitfall 6: Vogen `init` Property Conflict in Object Initializer Syntax

**What goes wrong:** When `DcaExecutionService` currently creates `Purchase` via object initializer syntax (`new Purchase { Id = ..., Price = ..., }`) and Phase 15 removes the public parameterless constructor, all existing object-initializer call sites break at compile time.

**Why it happens:** `new Purchase { ... }` requires a publicly accessible parameterless constructor.

**How to avoid:** This is the goal -- compile errors guide the migration. After adding `private` constructor + `static Create()`, the compiler immediately identifies every call site that needs updating. This makes the migration mechanical and safe.

---

### Pitfall 7: ConfigurationService Must Load Entity Before Calling Behavior Methods

**What goes wrong:** `ConfigurationService.UpdateAsync` currently upserts (creates if not exists, updates if exists). The create path does `new DcaConfiguration { Id = DcaConfigurationId.Singleton }` -- this breaks when the public parameterless constructor is removed.

**Why it happens:** Direct object instantiation is no longer available.

**How to avoid:** The create path becomes `DcaConfiguration.Create(DcaConfigurationId.Singleton, ...)` with all required parameters. The update path calls behavior methods on the loaded entity.

---

## Code Examples

### AggregateRoot Base Class (new file)

```csharp
// Source: Microsoft eShopOnContainers pattern, adapted for this codebase
// File: TradingBot.ApiService/BuildingBlocks/AggregateRoot.cs

namespace TradingBot.ApiService.BuildingBlocks;

public abstract class AggregateRoot<TId> : BaseEntity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Purchase Aggregate (refactored)

```csharp
// File: TradingBot.ApiService/Models/Purchase.cs

public class Purchase : AggregateRoot<PurchaseId>
{
    protected Purchase() { } // EF Core

    private Purchase(PurchaseId id, /* all params */)
    {
        Id = id;
        // set all properties
        Status = PurchaseStatus.Pending;
        ExecutedAt = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset ExecutedAt { get; private set; }
    public Symbol Symbol { get; private set; }   // add Symbol as explicit property
    public Price Price { get; private set; }
    public Quantity Quantity { get; private set; }
    public UsdAmount Cost { get; private set; }
    public Multiplier Multiplier { get; private set; }
    public PurchaseStatus Status { get; private set; }
    public bool IsDryRun { get; private set; }
    public string? OrderId { get; private set; }
    public string? RawResponse { get; private set; }
    public string? FailureReason { get; private set; }
    public string? MultiplierTier { get; private set; }
    public Percentage DropPercentage { get; private set; }
    public decimal High30Day { get; private set; }
    public decimal Ma200Day { get; private set; }

    public static Purchase Create(
        Symbol symbol,
        Price price,
        UsdAmount cost,
        Multiplier multiplier,
        string? multiplierTier,
        Percentage dropPercentage,
        decimal high30Day,
        decimal ma200Day,
        bool isDryRun)
    {
        var purchase = new Purchase(
            PurchaseId.New(), symbol, price,
            Quantity.From(0), // placeholder until fill
            cost, multiplier, multiplierTier,
            dropPercentage, high30Day, ma200Day, isDryRun);

        purchase.AddDomainEvent(new PurchaseCreatedEvent(purchase.Id));
        return purchase;
    }

    // Behavior methods for post-creation mutation (fill results)
    public void RecordFill(Quantity quantity, Price avgPrice, UsdAmount actualCost, string orderId)
    {
        Quantity = quantity;
        Price = avgPrice;
        Cost = actualCost;
        OrderId = orderId;
        Status = quantity.Value >= /* original quantity */ * 0.95m
            ? PurchaseStatus.Filled
            : PurchaseStatus.PartiallyFilled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordFailure(string reason, string? rawResponse)
    {
        Status = PurchaseStatus.Failed;
        FailureReason = reason;
        RawResponse = rawResponse;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

**Note:** `Purchase.RecordFill()` needs the originally-requested quantity to compute whether it's Filled vs PartiallyFilled (95% threshold). The factory method should store the intended quantity or pass it to `RecordFill`. This is a design detail for the planner.

### DcaConfiguration Aggregate (refactored)

```csharp
public class DcaConfiguration : AggregateRoot<DcaConfigurationId>
{
    protected DcaConfiguration() { } // EF Core

    public UsdAmount BaseDailyAmount { get; private set; }
    public int DailyBuyHour { get; private set; }
    public int DailyBuyMinute { get; private set; }
    public int HighLookbackDays { get; private set; }
    public bool DryRun { get; private set; }
    public int BearMarketMaPeriod { get; private set; }
    public Multiplier BearBoostFactor { get; private set; }
    public Multiplier MaxMultiplierCap { get; private set; }
    public List<MultiplierTierData> MultiplierTiers { get; private set; } = [];

    public static DcaConfiguration Create(DcaConfigurationId id, UsdAmount baseDailyAmount, ...)
    {
        // validate all, set all
        var config = new DcaConfiguration { ... };
        config.AddDomainEvent(new DcaConfigurationCreatedEvent(config.Id));
        return config;
    }

    public void UpdateDailyAmount(UsdAmount amount)
    {
        BaseDailyAmount = amount; // Vogen ensures > 0
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new DcaConfigurationUpdatedEvent(Id));
    }

    public void UpdateTiers(List<MultiplierTierData> tiers)
    {
        // Validate ascending order, sane multiplier values
        var sorted = tiers.OrderBy(t => t.DropPercentage).ToList();
        if (!tiers.SequenceEqual(sorted))
            throw new ArgumentException("Tiers must be sorted ascending");
        if (tiers.Any(t => t.Multiplier <= 0 || t.Multiplier > 20))
            throw new ArgumentException("Tier multiplier out of range");
        MultiplierTiers = tiers;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new TiersUpdatedEvent(Id));
    }
}
```

### Domain Events (refactored to carry identity only)

```csharp
// New events for Phase 15
public record PurchaseCreatedEvent(PurchaseId PurchaseId) : IDomainEvent;
public record DcaConfigurationCreatedEvent(DcaConfigurationId ConfigId) : IDomainEvent;
public record DcaConfigurationUpdatedEvent(DcaConfigurationId ConfigId) : IDomainEvent;
public record TiersUpdatedEvent(DcaConfigurationId ConfigId) : IDomainEvent;

// Existing events to REFACTOR -- currently raised directly via IPublisher
// PurchaseCompletedEvent -- keep for Phase 15 compat, raised by DcaExecutionService
// reading from purchase.DomainEvents OR keeping existing IPublisher.Publish() call
// (Phase 17 will consolidate)
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| Anemic domain model (public setters, external logic) | Rich aggregate roots (private setters, factory/behavior methods) | Application code cannot put aggregate into invalid state |
| Direct `IPublisher.Publish()` in service | `AddDomainEvent()` + interceptor dispatch | Events raised by domain, dispatched by infrastructure |
| Object initializer construction | Static factory methods | Compile errors guide migration; no invalid construction |

---

## Open Questions

1. **IngestionJob aggregate or data carrier?**
   - What we know: Has status field (Pending/Running/Completed/Failed) with transitions managed in `DataIngestionService` via direct assignment
   - What's unclear: Whether status transitions are "real" business invariants or just infrastructure state
   - Recommendation: Make `IngestionJob` an aggregate root with `Start()`, `Complete(int recordsFetched, int gapsDetected)`, `Fail(string error)` behavior methods. It has clear state transition semantics and the phase requirement DM-04 ("entities use private setters") implies comprehensive coverage.

2. **Symbol property on Purchase -- is it stored in DB?**
   - What we know: Current `Purchase` entity has no `Symbol` column; `DcaExecutionService` hardcodes "BTC/USDC"
   - What's unclear: Should `Purchase.Create()` require a `Symbol` parameter, and does EF Core need a new column?
   - Recommendation: Add `Symbol` as a private-setter property for future-proofing (the factory takes it), but check if it needs a new DB column. If the column doesn't exist, either add it (with migration) or defer. Phase 15 may need a migration if Symbol is added to Purchase.

3. **Quantity in `RecordFill` needs the originally-requested quantity for 95% fill check**
   - What we know: Current code computes `purchase.Status = filledQty >= roundedQuantity * 0.95m` where `roundedQuantity` is a local variable in `DcaExecutionService`
   - What's unclear: How to expose this to `RecordFill()` -- store intended quantity on Purchase, or pass it as a parameter?
   - Recommendation: Store `RequestedQuantity` on Purchase (set in `Create()`), then `RecordFill()` can compute fill status without needing the caller to pass it. Requires adding one column to Purchase table (migration needed).

---

## Sources

### Primary (HIGH confidence)
- Microsoft eShopOnContainers docs: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model -- aggregate root patterns, private setters, behavior methods
- Microsoft seedwork docs: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces -- Entity base class with domain events, `AddDomainEvent`, `RemoveDomainEvent`
- Microsoft domain events docs: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation -- deferred event collection, `SaveChanges` dispatch
- EF Core constructors docs (Context7, /dotnet/entityframework.docs): private setters, parameterized constructors, protected constructors for DDD entities
- EF Core backing fields docs (Context7, /dotnet/entityframework.docs): `[BackingField]`, `HasField()`, `PropertyAccessMode`

### Secondary (MEDIUM confidence)
- Milan Jovanović blog (verified patterns): https://www.milanjovanovic.tech/blog/how-to-use-ef-core-interceptors -- `PublishDomainEventsInterceptor` with `SavedChangesAsync` pattern
- The Reformed Programmer (verified patterns): https://www.thereformedprogrammer.net/creating-domain-driven-design-entity-classes-with-entity-framework-core/ -- private constructors, backing field collections, access method patterns

### Tertiary (LOW confidence)
- None -- all major claims verified against official documentation

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all tools already installed and in use
- Architecture (AggregateRoot base class): HIGH -- direct from MS eShopOnContainers reference implementation
- EF Core private/protected constructor behavior: HIGH -- from official EF Core docs via Context7
- Domain event collection pattern: HIGH -- from official MS microservices docs
- Aggregate boundary decisions (IngestionJob): MEDIUM -- reasonable assessment, but IngestionJob classification involves judgment
- Symbol column addition: MEDIUM -- depends on whether planner decides to add it

**Research date:** 2026-02-19
**Valid until:** 2026-04-19 (stable domain -- EF Core DDD patterns are not fast-moving)

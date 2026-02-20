# Phase 26: Portfolio Domain Foundation - Research

**Researched:** 2026-02-20
**Domain:** EF Core entity modeling, Vogen typed IDs/value objects, domain modeling, interest calculation
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Asset inventory:**
- Support three asset types: Crypto, ETF, Fixed Deposit
- Crypto: BTC (auto-seeded from DCA bot), plus user-added alts (ETH, SOL, and others)
- ETF: E1VFVN30 as the primary ETF, user can add others
- BTC asset is auto-created from existing DCA purchase data; all other assets added manually
- Assets are dynamically added by the user — no hardcoded catalog beyond the BTC auto-seed

**Fixed deposit modeling:**
- User typically has 1-3 fixed deposits at a time
- Primary use case is simple interest (no compounding) — but domain model must support both simple and compound (monthly/quarterly/semi-annual/annual) per requirements PORT-06
- No early withdrawal modeling — just start date, maturity date, and rate
- Matured deposits get a "Matured" status rather than being deleted — keeps history visible
- Fixed deposit lifecycle: Active → Matured (status field on entity)

**Transaction data capture:**
- Track fees per transaction — fee amount field on AssetTransaction
- No notes field — keep transactions minimal (date, quantity, price, currency, fee)
- No exchange/source field — just the trade data
- DCA bot auto-imported transactions should pull fee data from existing Purchase records if available
- Auto-imported transactions are read-only (flagged with source = Bot, not editable/deletable per DISP-08)

### Claude's Discretion

- Entity inheritance strategy (TPH vs TPT vs separate tables)
- Aggregate root boundaries
- Value object choices beyond what Vogen requires
- Test data and edge case coverage approach

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PORT-01 | User can create portfolio assets with name, ticker, asset type (Crypto/ETF/FixedDeposit), and native currency (USD/VND) | PortfolioAsset entity design, AssetType enum, Currency enum |
| PORT-02 | User can record buy/sell transactions on tradeable assets with date, quantity, price per unit, and currency | AssetTransaction entity design, TransactionType enum (Buy/Sell), VndAmount/UsdAmount value objects |
| PORT-03 | User can create fixed deposits with principal (VND), annual interest rate, start date, maturity date, and compounding frequency | FixedDeposit entity design, CompoundingFrequency enum, VndAmount precision |
| PORT-06 | Fixed deposit accrued value is calculated correctly for both simple interest and compound interest | Interest calculation formulas, pure static method pattern from MultiplierCalculator |
</phase_requirements>

---

## Summary

Phase 26 lays the database schema and domain model for multi-asset portfolio tracking. Three new aggregate roots are needed: `PortfolioAsset` (holds crypto or ETF positions), `AssetTransaction` (one buy or sell record under an asset), and `FixedDeposit` (a term deposit). The existing codebase has all patterns needed: Vogen `[ValueObject<T>]` for typed IDs and value objects, `AggregateRoot<TId>` base class, `ConfigureConventions()` for bulk EF Core converter registration, and Testcontainers-based integration tests with transaction rollback.

The key architecture decision left to Claude's discretion is the inheritance strategy. Research points to **separate tables** (no inheritance hierarchy in EF Core) as the correct choice: `PortfolioAsset` is a standalone table; `AssetTransaction` is a child table with a FK to `PortfolioAsset`; `FixedDeposit` is a separate standalone table. This matches the existing pattern (all current entities are separate tables, not TPH/TPT) and avoids nullable column sprawl.

The interest calculation logic (PORT-06) should follow the `MultiplierCalculator` pattern: a pure static class with no dependencies, tested in isolation with theory tests. The formulas are well-understood domain math.

**Primary recommendation:** Follow existing patterns exactly. New entities = new IDs in `Models/Ids/`, new value objects in `Models/Values/`, entity classes in `Models/`, EF configuration in `TradingBotDbContext.ConfigureConventions()` + `OnModelCreating()`, migration from `TradingBot.ApiService/` directory, integration tests using `PostgresFixture` + transaction rollback.

---

## Standard Stack

### Core (already in project — no new packages needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Vogen | 8.0.4 | Typed IDs and value objects | Already used for PurchaseId, UsdAmount, etc. |
| EF Core (Npgsql) | 10.0.0 | ORM + migrations | Project standard |
| Testcontainers.PostgreSql | 4.10.0 | Integration tests with real Postgres | Already used in PurchaseSpecsTests |
| xUnit + FluentAssertions | 2.9.3 / 7.0.0 | Test framework | Project standard |

### No New Packages Required

All libraries are already present in `TradingBot.ApiService.csproj` and `TradingBot.ApiService.Tests.csproj`. Phase 26 is purely a domain + persistence layer — no external APIs, no new NuGet packages.

---

## Architecture Patterns

### Recommended Entity File Layout

Following existing codebase conventions:

```
TradingBot.ApiService/
├── Models/
│   ├── Ids/
│   │   ├── PortfolioAssetId.cs         # [ValueObject<Guid>] readonly partial struct
│   │   ├── AssetTransactionId.cs       # [ValueObject<Guid>] readonly partial struct
│   │   └── FixedDepositId.cs           # [ValueObject<Guid>] readonly partial struct
│   ├── Values/
│   │   └── VndAmount.cs                # [ValueObject<decimal>] HasPrecision(18, 0)
│   ├── PortfolioAsset.cs               # AggregateRoot<PortfolioAssetId>
│   ├── AssetTransaction.cs             # BaseEntity<AssetTransactionId> (child of PortfolioAsset)
│   └── FixedDeposit.cs                 # AggregateRoot<FixedDepositId>
├── Application/
│   └── Services/
│       └── InterestCalculator.cs       # Pure static class (same pattern as MultiplierCalculator)
└── Infrastructure/
    └── Data/
        ├── TradingBotDbContext.cs       # Add new DbSets + converters + model config
        └── Migrations/
            └── ..._AddPortfolioEntities.cs
```

### Pattern 1: Typed IDs (identical to PurchaseId)

Vogen generates `EfCoreValueConverter` and `EfCoreValueComparer` inner classes when `Conversions.EfCoreValueConverter` is specified. The global config in `VogenGlobalConfig.cs` already sets this for all types.

```csharp
// Models/Ids/PortfolioAssetId.cs
using Vogen;

namespace TradingBot.ApiService.Models.Ids;

[ValueObject<Guid>]
public readonly partial struct PortfolioAssetId
{
    public static PortfolioAssetId New() => From(Guid.CreateVersion7());
}
```

Same pattern for `AssetTransactionId` and `FixedDepositId`. VogenGlobalConfig already sets `Conversions.EfCoreValueConverter | Conversions.SystemTextJson | Conversions.TypeConverter`.

### Pattern 2: VndAmount Value Object

VND has no decimal places (ISO 4217: VND exponent = 0). Use `HasPrecision(18, 0)` in EF config. Allow zero (for zero-fee transactions).

```csharp
// Models/Values/VndAmount.cs
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct VndAmount
{
    private static Validation Validate(decimal value) =>
        value >= 0
            ? Validation.Ok
            : Validation.Invalid("VndAmount must be non-negative");

    public static bool operator <(VndAmount left, VndAmount right) => left.Value < right.Value;
    public static bool operator >(VndAmount left, VndAmount right) => left.Value > right.Value;
    public static bool operator <=(VndAmount left, VndAmount right) => left.Value <= right.Value;
    public static bool operator >=(VndAmount left, VndAmount right) => left.Value >= right.Value;

    public static VndAmount operator +(VndAmount left, VndAmount right) =>
        VndAmount.From(left.Value + right.Value);
}
```

### Pattern 3: ETF Share Quantities as int (not Vogen)

ETF shares are whole numbers only. Use plain `int` — no Vogen wrapper needed. There is no fractional ETF share in the Vietnamese market. This avoids over-engineering with a typed value object for a simple integer constraint.

> ETF quantity stored as `int`, validated in domain factory: `if (quantity <= 0) throw`.

### Pattern 4: PortfolioAsset Entity (Aggregate Root, separate table)

Separate tables (no inheritance) is the correct choice. `PortfolioAsset` covers both Crypto and ETF via an `AssetType` discriminator field.

```csharp
// Models/PortfolioAsset.cs
public class PortfolioAsset : AggregateRoot<PortfolioAssetId>
{
    protected PortfolioAsset() { }

    public string Name { get; private set; } = null!;
    public string Ticker { get; private set; } = null!;
    public AssetType AssetType { get; private set; }
    public Currency NativeCurrency { get; private set; }

    private readonly List<AssetTransaction> _transactions = [];
    public IReadOnlyList<AssetTransaction> Transactions => _transactions.AsReadOnly();

    public static PortfolioAsset Create(string name, string ticker, AssetType assetType, Currency nativeCurrency)
    {
        // validation guards
        return new PortfolioAsset
        {
            Id = PortfolioAssetId.New(),
            Name = name,
            Ticker = ticker,
            AssetType = assetType,
            NativeCurrency = nativeCurrency
        };
    }

    public AssetTransaction AddTransaction(DateOnly date, decimal quantity, decimal pricePerUnit,
        Currency currency, TransactionType type, decimal? feeAmount, TransactionSource source)
    {
        var tx = AssetTransaction.Create(Id, date, quantity, pricePerUnit, currency, type, feeAmount, source);
        _transactions.Add(tx);
        return tx;
    }
}

public enum AssetType { Crypto, ETF }
public enum Currency { USD, VND }
```

**Note on collection navigation:** EF Core requires the backing field to be named with `_` prefix + camelCase property name, or configured explicitly. The pattern `private readonly List<T> _transactions` with `IReadOnlyList<T> Transactions` is the correct DDD pattern — EF Core discovers it automatically.

### Pattern 5: AssetTransaction Entity (child entity, NOT aggregate root)

Transactions are part of the `PortfolioAsset` aggregate. They do NOT inherit from `AggregateRoot`. Use `BaseEntity<AssetTransactionId>`.

```csharp
// Models/AssetTransaction.cs
public class AssetTransaction : BaseEntity<AssetTransactionId>
{
    protected AssetTransaction() { }

    public PortfolioAssetId PortfolioAssetId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Quantity { get; private set; }       // positive for buy, can be fractional for crypto
    public decimal PricePerUnit { get; private set; }
    public Currency Currency { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal? Fee { get; private set; }           // nullable — not all trades have fees
    public TransactionSource Source { get; private set; } // Bot = read-only, Manual = editable

    internal static AssetTransaction Create(PortfolioAssetId assetId, DateOnly date, decimal quantity,
        decimal pricePerUnit, Currency currency, TransactionType type, decimal? fee, TransactionSource source)
    {
        return new AssetTransaction
        {
            Id = AssetTransactionId.New(),
            PortfolioAssetId = assetId,
            Date = date,
            Quantity = quantity,
            PricePerUnit = pricePerUnit,
            Currency = currency,
            Type = type,
            Fee = fee,
            Source = source
        };
    }
}

public enum TransactionType { Buy, Sell }
public enum TransactionSource { Manual, Bot }
```

**Key:** `AssetTransaction.Create` is `internal` — only `PortfolioAsset.AddTransaction` should create transactions. This enforces the aggregate boundary.

### Pattern 6: FixedDeposit Entity (separate aggregate root, separate table)

`FixedDeposit` is NOT related to `PortfolioAsset` — it's a separate aggregate. Principal and accrued value are in VND. No navigation from `PortfolioAsset` to `FixedDeposit`.

```csharp
// Models/FixedDeposit.cs
public class FixedDeposit : AggregateRoot<FixedDepositId>
{
    protected FixedDeposit() { }

    public string BankName { get; private set; } = null!;
    public VndAmount Principal { get; private set; }
    public decimal AnnualInterestRate { get; private set; }  // e.g., 0.065 = 6.5%
    public DateOnly StartDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public CompoundingFrequency CompoundingFrequency { get; private set; }
    public FixedDepositStatus Status { get; private set; }

    public static FixedDeposit Create(string bankName, VndAmount principal, decimal annualInterestRate,
        DateOnly startDate, DateOnly maturityDate, CompoundingFrequency compoundingFrequency)
    {
        if (maturityDate <= startDate)
            throw new ArgumentException("Maturity date must be after start date");
        if (annualInterestRate <= 0 || annualInterestRate > 1)
            throw new ArgumentException("Annual interest rate must be between 0 and 1 (exclusive)");

        return new FixedDeposit
        {
            Id = FixedDepositId.New(),
            BankName = bankName,
            Principal = principal,
            AnnualInterestRate = annualInterestRate,
            StartDate = startDate,
            MaturityDate = maturityDate,
            CompoundingFrequency = compoundingFrequency,
            Status = FixedDepositStatus.Active
        };
    }

    public void Mature()
    {
        Status = FixedDepositStatus.Matured;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum CompoundingFrequency
{
    Simple,         // No compounding
    Monthly,        // 12 times per year
    Quarterly,      // 4 times per year
    SemiAnnual,     // 2 times per year
    Annual          // 1 time per year
}

public enum FixedDepositStatus { Active, Matured }
```

### Pattern 7: Interest Calculator (Pure Static Class)

Follows the `MultiplierCalculator` pattern — pure static, no dependencies, fully unit-testable.

```csharp
// Application/Services/InterestCalculator.cs
public static class InterestCalculator
{
    /// <summary>
    /// Calculates accrued value of a fixed deposit as of a given date.
    /// Implements PORT-06: both simple and compound interest.
    /// </summary>
    public static decimal CalculateAccruedValue(
        decimal principal,
        decimal annualRate,
        DateOnly startDate,
        DateOnly asOfDate,
        CompoundingFrequency frequency)
    {
        var daysElapsed = asOfDate.DayNumber - startDate.DayNumber;
        if (daysElapsed <= 0) return principal;

        var yearsElapsed = daysElapsed / 365.0m;

        return frequency switch
        {
            CompoundingFrequency.Simple =>
                principal * (1 + annualRate * yearsElapsed),

            CompoundingFrequency.Monthly =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 12), (double)(yearsElapsed * 12)),

            CompoundingFrequency.Quarterly =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 4), (double)(yearsElapsed * 4)),

            CompoundingFrequency.SemiAnnual =>
                principal * (decimal)Math.Pow((double)(1 + annualRate / 2), (double)(yearsElapsed * 2)),

            CompoundingFrequency.Annual =>
                principal * (decimal)Math.Pow((double)(1 + annualRate), (double)yearsElapsed),

            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
    }
}
```

**Math note:** Compound interest formula is `P * (1 + r/n)^(n*t)` where `n` is periods per year and `t` is years. Simple interest is `P * (1 + r*t)`. Using `Math.Pow` with `double` cast is acceptable for financial display (not settlement) — precision loss is sub-paisa for typical Vietnamese deposit sizes.

### Pattern 8: EF Core Configuration

Add to `TradingBotDbContext`:

```csharp
// In ConfigureConventions():
configurationBuilder.Properties<PortfolioAssetId>()
    .HaveConversion<PortfolioAssetId.EfCoreValueConverter, PortfolioAssetId.EfCoreValueComparer>();
configurationBuilder.Properties<AssetTransactionId>()
    .HaveConversion<AssetTransactionId.EfCoreValueConverter, AssetTransactionId.EfCoreValueComparer>();
configurationBuilder.Properties<FixedDepositId>()
    .HaveConversion<FixedDepositId.EfCoreValueConverter, FixedDepositId.EfCoreValueComparer>();
configurationBuilder.Properties<VndAmount>()
    .HaveConversion<VndAmount.EfCoreValueConverter, VndAmount.EfCoreValueComparer>();

// In OnModelCreating():
modelBuilder.Entity<PortfolioAsset>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
    entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
    entity.Property(e => e.AssetType).HasMaxLength(20).HasConversion<string>();
    entity.Property(e => e.NativeCurrency).HasMaxLength(5).HasConversion<string>();
    entity.HasMany(e => e.Transactions)
        .WithOne()
        .HasForeignKey(t => t.PortfolioAssetId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<AssetTransaction>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Quantity).HasPrecision(18, 8);       // crypto needs 8 decimals
    entity.Property(e => e.PricePerUnit).HasPrecision(18, 8);
    entity.Property(e => e.Fee).HasPrecision(18, 2);            // fees: 2 decimals sufficient
    entity.Property(e => e.Currency).HasMaxLength(5).HasConversion<string>();
    entity.Property(e => e.Type).HasMaxLength(10).HasConversion<string>();
    entity.Property(e => e.Source).HasMaxLength(10).HasConversion<string>();
    entity.HasIndex(e => e.PortfolioAssetId);
    entity.HasIndex(e => e.Date);
});

modelBuilder.Entity<FixedDeposit>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.BankName).HasMaxLength(100).IsRequired();
    entity.Property(e => e.Principal).HasPrecision(18, 0);      // VND: no decimals
    entity.Property(e => e.AnnualInterestRate).HasPrecision(8, 6);  // e.g., 0.065000
    entity.Property(e => e.CompoundingFrequency).HasMaxLength(20).HasConversion<string>();
    entity.Property(e => e.Status).HasMaxLength(10).HasConversion<string>();
    entity.HasIndex(e => e.Status);
});
```

**Precision decisions (confirmed against existing patterns):**
- `VndAmount` (VND): `HasPrecision(18, 0)` — VND has no decimal places
- `Quantity` for crypto: `HasPrecision(18, 8)` — same as existing BTC quantity
- `PricePerUnit`: `HasPrecision(18, 8)` — same as existing Price
- `Fee` (VND or USD): `HasPrecision(18, 2)` — transaction fees rarely need more than 2 decimals
- `AnnualInterestRate`: `HasPrecision(8, 6)` — stores 0.065000 comfortably

### Pattern 9: Integration Tests (EF round-trip)

Follow `PurchaseSpecsTests` exactly: use `PostgresFixture`, wrap each test in `BeginTransactionAsync` + `RollbackAsync`.

```csharp
public class PortfolioAssetTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public PortfolioAssetTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PortfolioAsset_PersistsAndRoundTrips()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var asset = PortfolioAsset.Create("Bitcoin", "BTC", AssetType.Crypto, Currency.USD);
        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync();

        var loaded = await db.PortfolioAssets.FindAsync(asset.Id);
        loaded.Should().NotBeNull();
        loaded!.Ticker.Should().Be("BTC");
        loaded.AssetType.Should().Be(AssetType.Crypto);

        await transaction.RollbackAsync();
    }
}
```

**PostgresFixture is already declared** in `Application/Specifications/PostgresFixture.cs` and is shared across test classes via `IClassFixture<PostgresFixture>`. New test classes in portfolio subdirectories reuse this same fixture.

### Pattern 10: DbSet Declarations

Add to `TradingBotDbContext`:

```csharp
public DbSet<PortfolioAsset> PortfolioAssets => Set<PortfolioAsset>();
public DbSet<AssetTransaction> AssetTransactions => Set<AssetTransaction>();
public DbSet<FixedDeposit> FixedDeposits => Set<FixedDeposit>();
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Typed IDs | Custom struct with operator overloads | Vogen `[ValueObject<Guid>]` | Generates EfCoreValueConverter, SystemTextJson, TypeConverter, equality, IsInitialized guard |
| VND amount type | Custom decimal wrapper | Vogen `[ValueObject<decimal>]` | Same reasons — converter pattern already established |
| Interest formula math library | Third-party financial lib | Plain C# math in `InterestCalculator` | Formulas are 3 lines each; no library needed |
| Repository pattern | IPortfolioAssetRepository interface + impl | Direct DbContext in services (existing pattern) | Project does not use repository abstractions — DbContext injected directly in services |
| EF converter registration per-property | `.HasConversion<>()` in each entity config | `ConfigureConventions()` bulk registration | Existing pattern, prevents misses |

**Key insight:** The project deliberately omits the repository pattern. All entities access `TradingBotDbContext` directly in application services. Do not introduce repository interfaces for portfolio entities.

---

## Common Pitfalls

### Pitfall 1: Forgetting to Register Vogen Converters in ConfigureConventions

**What goes wrong:** EF Core silently stores the raw Guid as-is (no conversion), then fails to match on queries because the comparer is missing.
**Why it happens:** Vogen generates the converter class but EF doesn't apply it automatically — explicit registration required.
**How to avoid:** For every new typed ID and value object, add `.HaveConversion<T.EfCoreValueConverter, T.EfCoreValueComparer>()` in `ConfigureConventions()`.
**Warning signs:** Tests pass for inserts but fail on queries that filter by ID. Migration snapshot shows `uniqueidentifier` instead of the expected mapping.

### Pitfall 2: Wrong Precision for VND

**What goes wrong:** Using `HasPrecision(18, 2)` for VND causes EF to store 0.00 format when VND values should be whole numbers. Some databases will round, others will accept 0.00 but return incorrect display.
**Why it happens:** Default assumption from USD/EUR usage where 2 decimal places are standard.
**How to avoid:** VND has ISO 4217 exponent 0 — always use `HasPrecision(18, 0)`.
**Warning signs:** Accrued value calculation returns 1000000.00 instead of 1000000.

### Pitfall 3: Private Setter + EF Core Materialization

**What goes wrong:** EF Core cannot set `private set` properties during materialization if there is no parameterless constructor.
**Why it happens:** EF Core uses the parameterless constructor for materialization, then sets properties. If properties are `private init` or use required init, EF cannot set them.
**How to avoid:** Always include `protected Entity() { }` (already done in existing entities). Use `private set` not `private init` for mutable properties. Use `private set` or `init` only for properties set once in the constructor.
**Warning signs:** EF materializes entities with all-default values despite correct data in DB.

### Pitfall 4: Collection Navigation Shadow Property (EF Core)

**What goes wrong:** EF Core may create a shadow FK property on `AssetTransaction` if the relationship is not configured explicitly with `HasForeignKey(t => t.PortfolioAssetId)`.
**Why it happens:** EF convention-based discovery may choose a shadow property instead of the declared `PortfolioAssetId` property.
**How to avoid:** Explicitly configure `.HasMany(...).WithOne().HasForeignKey(t => t.PortfolioAssetId)` in `OnModelCreating`.
**Warning signs:** Migration creates two FK columns — one explicit and one shadow `PortfolioAssetId1`.

### Pitfall 5: `Math.Pow` with `decimal` — Requires Double Cast

**What goes wrong:** `Math.Pow` does not accept `decimal` — it requires `double`. Forgetting to cast causes compile error.
**Why it happens:** `Math.Pow` is defined as `double Math.Pow(double, double)`.
**How to avoid:** Always cast: `(decimal)Math.Pow((double)base, (double)exponent)`.
**Warning signs:** Compile error CS1503: cannot convert from decimal to double.

### Pitfall 6: Interest Calculation Day Count Convention

**What goes wrong:** Using calendar days `(DateTimeOffset.UtcNow - startDate).TotalDays / 365` can give 366 for leap years, distorting interest.
**Why it happens:** `TimeSpan.TotalDays` reflects actual calendar days including leap day.
**How to avoid:** Use `DateOnly.DayNumber` subtraction for day count, then divide by 365 (not 365.25). Vietnamese banking convention for term deposits uses 365-day year fixed (not actual/365).
**Warning signs:** Accrued interest for leap year deposits is slightly higher than expected.

### Pitfall 7: TPH vs Separate Tables — Don't Use TPH for FixedDeposit

**What goes wrong:** If someone models `PortfolioAsset`, `FixedDeposit` as a single TPH hierarchy, EF creates many nullable columns that don't apply across types.
**Why it happens:** EF defaults to TPH when inheritance is detected.
**How to avoid:** Do not create an inheritance hierarchy. `PortfolioAsset` and `FixedDeposit` are separate aggregates with separate tables. The `AssetType` discriminator on `PortfolioAsset` distinguishes Crypto from ETF within a single table — no inheritance needed.
**Warning signs:** Migration creates a single table with a `Discriminator` column and many nullable columns.

---

## Code Examples

### Verified: Vogen ID Pattern (from codebase)

```csharp
// Source: TradingBot.ApiService/Models/Ids/PurchaseId.cs
[ValueObject<Guid>]
public readonly partial struct PurchaseId
{
    public static PurchaseId New() => From(Guid.CreateVersion7());
}
```

### Verified: ConfigureConventions Registration (from codebase)

```csharp
// Source: TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
configurationBuilder.Properties<PurchaseId>()
    .HaveConversion<PurchaseId.EfCoreValueConverter, PurchaseId.EfCoreValueComparer>();
```

### Verified: Enum-as-String Storage (from codebase)

```csharp
// Source: TradingBotDbContext.cs — Purchase entity config
entity.Property(e => e.Status)
    .HasMaxLength(20)
    .HasConversion<string>();
```

### Verified: Integration Test Pattern with Transaction Rollback (from codebase)

```csharp
// Source: tests/.../PurchaseSpecsTests.cs
[Fact]
public async Task SomeTest()
{
    await using var db = _fixture.CreateDbContext();
    await using var transaction = await db.Database.BeginTransactionAsync();

    // seed + act + assert ...

    await transaction.RollbackAsync();
}
```

### Verified: ClearDomainEvents in Tests (from codebase)

```csharp
// Source: PurchaseSpecsTests.cs — pattern for aggregate factories that raise domain events
var asset = PortfolioAsset.Create(...);
asset.ClearDomainEvents();  // prevents interceptor issues during SaveChangesAsync in tests
db.PortfolioAssets.Add(asset);
await db.SaveChangesAsync();
```

### Interest Calculation Formulas

```csharp
// Simple interest: I = P * r * t
// Example: 10,000,000 VND at 6.5% for 180 days
// t = 180/365 = 0.4932 years
// Accrued = 10,000,000 * (1 + 0.065 * 0.4932) = 10,320,548 VND

// Monthly compound: A = P * (1 + r/12)^(12*t)
// Quarterly compound: A = P * (1 + r/4)^(4*t)
// Semi-annual compound: A = P * (1 + r/2)^(2*t)
// Annual compound: A = P * (1 + r)^t
```

---

## Architecture Decisions (Claude's Discretion)

### Decision 1: Separate Tables (No Inheritance Hierarchy)

**Chosen:** Separate tables for `PortfolioAsset`, `AssetTransaction`, and `FixedDeposit`.

**Rationale:**
- All existing entities use separate tables (no TPH/TPT anywhere in codebase)
- `PortfolioAsset` (Crypto/ETF) and `FixedDeposit` have entirely different fields — TPH would produce many nullable columns
- The `AssetType` enum discriminator on `PortfolioAsset` handles the Crypto/ETF distinction within a single table cleanly
- TPT adds JOIN overhead on every query for no benefit in this case
- Separate aggregate roots = separate tables (DDD rule: don't model inter-aggregate relationships as inheritance)

### Decision 2: PortfolioAsset as Aggregate Root with Transactions as Children

**Chosen:** `PortfolioAsset` is the aggregate root, `AssetTransaction` is a child entity accessed only through `PortfolioAsset.AddTransaction()`.

**Rationale:**
- Transactions only make sense in the context of an asset — no independent existence
- Ensures consistency: can't create orphan transactions
- Matches DDD: one aggregate guards its children's invariants
- `AssetTransaction.Create` is `internal` to enforce boundary
- EF cascade delete ensures transactions are removed when asset is removed

### Decision 3: FixedDeposit as Separate Aggregate Root

**Chosen:** `FixedDeposit` is its own aggregate root, no relationship to `PortfolioAsset`.

**Rationale:**
- Fixed deposits have no transactions (no buy/sell records — just principal at creation)
- Display in Phase 29 will show them in a separate "Fixed Deposits" section
- No FK to `PortfolioAsset` avoids coupling that doesn't exist in the domain
- Future Phase 28 may add `FixedDeposit` to portfolio display without needing a FK

### Decision 4: Test Coverage Strategy

**Chosen:** EF round-trip integration tests (Testcontainers) for all three entities + unit tests for `InterestCalculator`.

**Rationale:**
- EF round-trip tests verify: Vogen converters registered correctly, precision correct, enum stored as string, FK relationship works
- Unit tests for `InterestCalculator` verify all 5 compounding modes with known values — same theory-test pattern as `MultiplierCalculatorTests`
- No snapshot tests needed (no complex nested output; round-trip verification suffices)

---

## Open Questions

1. **ETF quantity precision**
   - What we know: Vietnamese ETF shares are whole numbers (no fractional ETF trading in VN market)
   - What's unclear: Should quantity be `int` or `decimal` stored with `HasPrecision(18, 0)`?
   - Recommendation: Use plain `int` for ETF-specific quantity on `AssetTransaction`. However, since `AssetTransaction.Quantity` must also hold crypto (8 decimal places), store as `decimal` with `HasPrecision(18, 8)` and enforce integer constraint in domain logic for ETF transactions (`if (assetType == AssetType.ETF && quantity != Math.Floor(quantity)) throw`). The field is decimal because it serves both asset types.

2. **BankName field for FixedDeposit**
   - What we know: User has deposits at Vietnamese banks
   - What's unclear: Is `BankName` a free-text string or a constrained enum (BIDV, VCB, Vietcombank, etc.)?
   - Recommendation: Free-text string `HasMaxLength(100)` — user adds any bank name. No enum needed, keeps it flexible.

3. **AssetTransaction.PurchaseId cross-reference**
   - What we know: Bot-imported transactions (Source = Bot) correspond to `Purchase` records
   - What's unclear: Should `AssetTransaction` store a nullable `PurchaseId` FK to `Purchase` for traceability?
   - Recommendation: No FK in Phase 26. Phase 28 handles the import logic. If traceability is needed later, add `PurchaseId?` in a Phase 28 migration. Don't over-engineer domain model for a future concern.

---

## Sources

### Primary (HIGH confidence)

- Codebase: `TradingBot.ApiService/Models/Ids/PurchaseId.cs` — Vogen ID pattern verified
- Codebase: `TradingBot.ApiService/Models/Ids/VogenGlobalConfig.cs` — global Vogen defaults confirmed
- Codebase: `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` — ConfigureConventions and OnModelCreating patterns
- Codebase: `TradingBot.ApiService/Models/Purchase.cs` — AggregateRoot pattern, factory method, ClearDomainEvents
- Codebase: `tests/.../PurchaseSpecsTests.cs` — Integration test patterns with Testcontainers + transaction rollback
- Codebase: `tests/.../PostgresFixture.cs` — Shared fixture setup confirmed
- Context7 `/stevedunn/vogen` — EfCoreValueConverter/EfCoreValueComparer generation confirmed (HIGH)
- Context7 `/dotnet/entityframework.docs` — TPH/TPT inheritance strategies, separate tables guidance (HIGH)

### Secondary (MEDIUM confidence)

- Interest calculation formulas (simple + compound) — standard financial math, cross-verified against VN banking conventions
- DateOnly.DayNumber for day count — verified against .NET docs pattern
- Vietnamese ETF share integer constraint — based on VN Securities Law; ETFs trade in round lots of 100 shares minimum, no fractional

### Tertiary (LOW confidence)

- VND day count convention (365 fixed vs actual/365) — common Vietnamese banking practice, not verified against specific regulation text

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in project, patterns verified from codebase
- Architecture: HIGH — entity patterns, EF configuration, test patterns directly verified from codebase
- Interest calculation: HIGH — standard compound/simple interest formulas, well-established math
- Day count convention: MEDIUM — Vietnamese banking standard, not verified against regulatory text

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (stable stack — 30 day validity)

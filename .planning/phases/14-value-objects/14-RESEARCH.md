# Phase 14: Value Objects - Research

**Researched:** 2026-02-18
**Domain:** Vogen source-generated value objects for domain primitives (decimal, string), EF Core value converters, arithmetic/comparison operator patterns
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Which fields to wrap:**
- Core 5 from roadmap: Price, Quantity, Multiplier, UsdAmount, Symbol
- Include extras where frequently used: Percentage (for drop thresholds in multiplier tiers), and any other primitives Claude identifies as high-usage in domain logic
- Price and UsdAmount are **distinct types** — Price = market price per unit, UsdAmount = dollar amount to spend/spent. Cannot accidentally interchange them.
- Value objects used in configuration too — DcaOptions uses value objects (DailyAmount as UsdAmount, tier thresholds as Percentage) for full type safety through config binding
- Symbol type: Claude's discretion on whether string wrapper or enum-like, based on codebase usage

**Validation rules:**
- Price: strictly positive (> 0), reject zero and negative
- UsdAmount: strictly positive (> 0), reject zero and negative
- Quantity: Claude's discretion on zero allowance based on entity usage patterns
- Multiplier: must be > 0, cap at reasonable max (e.g., 10x) to catch config typos
- Percentage: stored as 0-1 format (0.05 = 5%). Multiply by 100 for display.
- Symbol: validation rules at Claude's discretion based on usage
- Fail mode: throw ValueObjectValidationException on invalid input (Vogen default). ErrorOr wrapping deferred to Phase 16.

**Arithmetic operations:**
- Add arithmetic operators to value objects for domain-expressive code
- Cross-type operators enforced: UsdAmount / Price = Quantity, UsdAmount * Multiplier = UsdAmount, etc.
- Full comparison operators (>, <, >=, <=) on all numeric value objects — essential for DCA threshold logic
- IComparable support on all numeric types

**API boundary behavior:**
- JSON serialization as raw primitives (Price: 50000.50, Symbol: "BTC") — no wrapping objects. Dashboard API unchanged.
- DTOs use value objects directly (not raw decimal/string) — Vogen JSON converters handle serialization transparently
- Tests compare value objects directly: `result.Price.Should().Be(Price.From(100))` — domain-expressive assertions
- Dashboard TypeScript: add branded types for value objects (Price, Quantity, etc.) matching Phase 13 typed ID pattern

### Claude's Discretion
- Exact set of "extra" value objects beyond the core 5 (based on codebase frequency analysis)
- Symbol implementation approach (string wrapper vs constrained)
- Zero-quantity validity (based on entity usage)
- Exact multiplier upper bound value
- Decimal precision handling for each type
- EF Core converter registration approach (continuing Phase 13 ConfigureConventions pattern)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| TS-02 | Domain primitives use value objects with validation (Price, Quantity, Multiplier, UsdAmount, Symbol) | Vogen 8.0.4 `[ValueObject<decimal>]` / `[ValueObject<string>]` with `private static Validation Validate(T value)` method generates compile-safe wrappers with `ValueObjectValidationException` on invalid input |
| TS-03 | Value objects persist via auto-generated EF Core converters registered in ConfigureConventions | Phase 13 established the `Properties<T>().HaveConversion<T.EfCoreValueConverter, T.EfCoreValueComparer>()` pattern in `ConfigureConventions`; new value object types require additional registrations in the same override |
| TS-04 | Value objects serialize/deserialize correctly in all API endpoints (JSON round-trip) | Vogen `Conversions.SystemTextJson` generates a `JsonConverter<T>` that reads/writes raw primitive values; decimal VOs serialize as JSON numbers, string VOs as JSON strings — no API surface changes needed |
</phase_requirements>

---

## Summary

Phase 14 follows Phase 13's established Vogen pattern, but with a critical difference: value objects wrap domain-semantic primitives (`decimal`, `string`) rather than entity IDs (`Guid`). The same Vogen source generator infrastructure (already installed at 8.0.4) applies. The `VogenGlobalConfig.cs` assembly attribute must be updated because the existing defaults specify `underlyingType: typeof(Guid)` — the new value objects will specify their underlying type per-type via `[ValueObject<decimal>]` / `[ValueObject<string>]`, overriding the Guid default.

The most important implementation finding: Vogen generates `CompareTo` (IComparable) and `==`/`!=` operators automatically for decimal value objects. It does NOT generate `<`, `>`, `<=`, `>=` operators. These must be hand-written inside each numeric value object's `partial struct` body (one-line each). Cross-type operators (`UsdAmount / Price = Quantity`, `UsdAmount * Multiplier = UsdAmount`) are also hand-written — Vogen does not generate these at all. This is by design: Vogen's philosophy discourages arithmetic operators in favor of named domain methods, but the context decisions explicitly require them.

The EF Core registration pattern from Phase 13 continues exactly: `configurationBuilder.Properties<Price>().HaveConversion<Price.EfCoreValueConverter, Price.EfCoreValueComparer>()` in `ConfigureConventions`. The `DcaOptions` configuration binding — replacing raw `decimal` fields with value objects — requires special attention: ASP.NET Core's `IOptions<T>` binding uses `TypeConverter` to bind from configuration strings. Vogen generates a `TypeConverter` for decimal/string types when `Conversions` includes the appropriate flag, but the default `Conversions.EfCoreValueConverter | Conversions.SystemTextJson` may not include `TypeConverter`. This must be explicitly verified and addressed.

**Primary recommendation:** Define value object types in `Models/Values/` mirroring the `Models/Ids/` pattern from Phase 13. Per-type `[ValueObject<decimal>]` attributes override the global `Guid` default. Hand-write comparison operators and cross-type arithmetic. Register EF Core converters in `ConfigureConventions`. Apply value objects to entities first (Plan 1), then to `DcaOptions` configuration and service signatures (Plan 2).

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Vogen | 8.0.4 | Source-generated value objects for decimal/string domain primitives | Already installed in Phase 13; zero runtime overhead, generates all required converters |

### Supporting (already in project)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.EntityFrameworkCore | 10.0.0 | Value converter registration for decimal/string typed columns | Continuation of Phase 13 `ConfigureConventions` pattern |
| System.Text.Json | .NET 10 BCL | JSON serialization of value objects as plain numbers/strings | Vogen generates `JsonConverter<T>` automatically via `Conversions.SystemTextJson` |

### No Additional Packages Needed

Vogen 8.0.4 is already installed. No new packages are required for this phase.

**Installation:** N/A — Vogen already in `TradingBot.ApiService.csproj`.

---

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── Models/
│   ├── Ids/                        # Phase 13: Guid-based typed IDs (unchanged)
│   │   ├── VogenGlobalConfig.cs    # Assembly defaults: Guid underlying, implicit cast
│   │   ├── PurchaseId.cs
│   │   ├── IngestionJobId.cs
│   │   └── DcaConfigurationId.cs
│   ├── Values/                     # Phase 14: domain primitive value objects (NEW)
│   │   ├── Price.cs                # [ValueObject<decimal>] — market price per BTC
│   │   ├── UsdAmount.cs            # [ValueObject<decimal>] — dollar amount to spend
│   │   ├── Quantity.cs             # [ValueObject<decimal>] — BTC quantity
│   │   ├── Multiplier.cs           # [ValueObject<decimal>] — DCA multiplier
│   │   ├── Percentage.cs           # [ValueObject<decimal>] — drop threshold (0-1 format)
│   │   └── Symbol.cs               # [ValueObject<string>]  — trading pair/asset symbol
│   ├── Purchase.cs                 # UPDATED: decimal fields → value objects
│   ├── DcaConfiguration.cs         # UPDATED: decimal fields → value objects
│   └── ...
├── Configuration/
│   └── DcaOptions.cs               # UPDATED: decimal → value objects in Plan 2
```

### Pattern 1: Numeric Value Object Definition

**What:** Source-generated decimal wrapper with validation, `==`/`!=`, `CompareTo`, and hand-written comparison/arithmetic operators.

**When to use:** All domain numeric primitives that have validation rules (Price, UsdAmount, Quantity, Multiplier, Percentage).

```csharp
// Models/Values/Price.cs
// Source: Vogen README + codebase adaptation
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct Price
{
    // Validation: strictly positive (> 0)
    private static Validation Validate(decimal value) =>
        value > 0
            ? Validation.Ok
            : Validation.Invalid("Price must be strictly positive");

    // Comparison operators — NOT generated by Vogen, must be hand-written
    public static bool operator <(Price left, Price right) => left.Value < right.Value;
    public static bool operator >(Price left, Price right) => left.Value > right.Value;
    public static bool operator <=(Price left, Price right) => left.Value <= right.Value;
    public static bool operator >=(Price left, Price right) => left.Value >= right.Value;

    // Cross-type domain math: $100 / $50,000 per BTC = 0.002 BTC
    public static Quantity operator /(UsdAmount amount, Price price) =>
        Quantity.From(amount.Value / price.Value);
}
```

**Generated by Vogen (no hand-writing needed):**
- `From(decimal value)` — wraps decimal, calls Validate, throws `ValueObjectValidationException` if invalid
- `TryFrom(decimal, out Price)` — safe creation
- `==`, `!=` (both value-to-value and value-to-decimal)
- `CompareTo(Price other)`, `CompareTo(object)` — IComparable implementation
- `EfCoreValueConverter`, `EfCoreValueComparer` inner classes
- `JsonConverter` that serializes as plain decimal number
- `TypeConverter` for configuration binding (requires `Conversions.TypeConverter` flag — see Pitfall 4)
- `TryParse(string, ...)` — IParsable implementation for minimal API binding

**NOT generated by Vogen:**
- `<`, `>`, `<=`, `>=` operators (must be hand-written — confirmed via Context7 snapshot inspection)
- Cross-type arithmetic (`UsdAmount / Price = Quantity`) — hand-written
- Arithmetic between same types (`UsdAmount + UsdAmount = UsdAmount`) — hand-written if needed

**Confidence:** HIGH — verified via Context7 Vogen snapshots showing only `CompareTo` and `==`/`!=` generated

### Pattern 2: VogenGlobalConfig — Existing Assembly Defaults Apply for Guid Only

**What:** The existing `VogenGlobalConfig.cs` sets `underlyingType: typeof(Guid)` globally. Per-type `[ValueObject<decimal>]` attributes override the underlying type. The global Guid default does NOT conflict with decimal value objects.

**Key insight:** When you write `[ValueObject<decimal>]`, Vogen uses `decimal` as the underlying type for that specific type, regardless of the global default. The global `VogenGlobalConfig.cs` remains unchanged.

```csharp
// Models/Ids/VogenGlobalConfig.cs — UNCHANGED from Phase 13
// (For reference only)
[assembly: VogenDefaults(
    underlyingType: typeof(Guid),         // default for unspecified VOs — Ids use this
    conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson,
    toPrimitiveCasting: CastOperator.Implicit,
    fromPrimitiveCasting: CastOperator.Implicit)]

// Models/Values/Price.cs — explicit underlying type overrides global Guid default
[ValueObject<decimal>]  // underlying = decimal (overrides global Guid)
public readonly partial struct Price { ... }
```

**Confidence:** HIGH — Vogen docs confirm per-type `[ValueObject<T>]` overrides assembly `VogenDefaults`

### Pattern 3: EF Core Registration in ConfigureConventions

**What:** Continuation of Phase 13's `Properties<T>().HaveConversion<>()` approach. Each new value object type adds one registration to the same `ConfigureConventions` override in `TradingBotDbContext`.

**When to use:** Every value object type that persists via EF Core (Price, UsdAmount, Quantity, Multiplier, Percentage). Symbol maps to `string` — no converter needed for EF (the column stays string type).

```csharp
// Infrastructure/Data/TradingBotDbContext.cs — addition to ConfigureConventions
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // Phase 13 (typed IDs — unchanged)
    configurationBuilder.Properties<PurchaseId>()
        .HaveConversion<PurchaseId.EfCoreValueConverter, PurchaseId.EfCoreValueComparer>();
    configurationBuilder.Properties<IngestionJobId>()
        .HaveConversion<IngestionJobId.EfCoreValueConverter, IngestionJobId.EfCoreValueComparer>();
    configurationBuilder.Properties<DcaConfigurationId>()
        .HaveConversion<DcaConfigurationId.EfCoreValueConverter, DcaConfigurationId.EfCoreValueComparer>();

    // Phase 14 (value objects — NEW)
    configurationBuilder.Properties<Price>()
        .HaveConversion<Price.EfCoreValueConverter, Price.EfCoreValueComparer>();
    configurationBuilder.Properties<UsdAmount>()
        .HaveConversion<UsdAmount.EfCoreValueConverter, UsdAmount.EfCoreValueComparer>();
    configurationBuilder.Properties<Quantity>()
        .HaveConversion<Quantity.EfCoreValueConverter, Quantity.EfCoreValueComparer>();
    configurationBuilder.Properties<Multiplier>()
        .HaveConversion<Multiplier.EfCoreValueConverter, Multiplier.EfCoreValueComparer>();
    configurationBuilder.Properties<Percentage>()
        .HaveConversion<Percentage.EfCoreValueConverter, Percentage.EfCoreValueComparer>();
    // Symbol is string-based — column stays string/text, no converter needed
    // unless explicit string converters are desired (Claude's Discretion)
}
```

**Why Symbol may not need a converter:** `Symbol` wraps `string`. EF Core stores the column as TEXT. Since the underlying type is already string, EF can use the string column directly via the value converter, but storing `Symbol.Value` (a string) is semantically equivalent. Register it if you want EF to use the typed `Symbol` throughout queries; skip if Symbol never persists to a column (currently `DailyPrice.Symbol` is a raw string in the composite key). See Pitfall 5.

**Confidence:** HIGH — direct continuation of Phase 13 verified pattern

### Pattern 4: String Value Object for Symbol

**What:** `Symbol` wraps `string`. The codebase uses `"BTC"`, `"BTC/USDC"` as raw strings throughout. The question is whether Symbol is a constrained enum-like type or a flexible string wrapper.

**Codebase usage analysis:**
- `DailyPrice.Symbol` — persisted column, values: `"BTC"` (asset name)
- `PriceDataService.Get30DayHighAsync(string symbol, ...)` — parameter
- `PriceDataService.Get200DaySmaAsync(string symbol, ...)` — parameter
- `HyperliquidClient.GetSpotPriceAsync("BTC/USDC", ...)` — spot trading pair format
- `HyperliquidClient.GetCandlesAsync(symbol, ...)` — uses shorter `"BTC"` format
- `DcaOptions` — no Symbol field (uses hardcoded `"BTC"` in service code)

**Recommendation (Claude's Discretion):** String wrapper approach is more flexible and safer than an enum, since the codebase uses two formats (`"BTC"` for historical data and `"BTC/USDC"` for Hyperliquid). Validate: non-empty, max length 20 (matching DB constraint), uppercase.

```csharp
// Models/Values/Symbol.cs
[ValueObject<string>]
public readonly partial struct Symbol
{
    private static Validation Validate(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20
            ? Validation.Ok
            : Validation.Invalid("Symbol must be non-empty and at most 20 characters");

    // Well-known constants for compile-time safety
    public static readonly Symbol Btc = From("BTC");
    public static readonly Symbol BtcUsdc = From("BTC/USDC");
}
```

Note: No `<`, `>` operators for `Symbol` — comparison on strings has no domain meaning here.

**Confidence:** HIGH — based on actual codebase string usage review

### Pattern 5: Cross-Type Arithmetic Operators

**What:** Operators that return a different value object type, encoding domain math rules.

**When to use:** In the `partial struct` body of the LEFT operand type (or the type that "returns" the result — common C# convention for cross-type operators).

```csharp
// In Price.cs (left operand in the division)
// "UsdAmount / Price = Quantity"
public static Quantity operator /(UsdAmount amount, Price price) =>
    Quantity.From(amount.Value / price.Value);

// In UsdAmount.cs
// "UsdAmount * Multiplier = UsdAmount"
public static UsdAmount operator *(UsdAmount amount, Multiplier multiplier) =>
    UsdAmount.From(amount.Value * multiplier.Value);

// "UsdAmount + UsdAmount = UsdAmount"  (accumulation in backtest/portfolio)
public static UsdAmount operator +(UsdAmount left, UsdAmount right) =>
    UsdAmount.From(left.Value + right.Value);

// In Quantity.cs
// "Quantity * Price = UsdAmount"  (calculating cost of purchase)
public static UsdAmount operator *(Quantity quantity, Price price) =>
    UsdAmount.From(quantity.Value * price.Value);
```

**C# operator placement rule:** The operator must be defined in one of the two operand types. For `UsdAmount / Price = Quantity`, it can go in either `UsdAmount` or `Price`. Convention: place it where it reads most naturally in domain language.

**Confidence:** HIGH — standard C# operator placement, no Vogen-specific behavior

### Pattern 6: Codebase Impact — Where Decimals Become Value Objects

Based on code review, the following fields change type in entities and related code:

**Entity fields (`Purchase.cs`):**
- `Price` (`decimal`) → `Price`
- `Quantity` (`decimal`) → `Quantity`
- `Cost` (`decimal`) → `UsdAmount`
- `Multiplier` (`decimal`) → `Multiplier`
- `DropPercentage` (`decimal`) → `Percentage`
- `High30Day` (`decimal`) → `Price` (30-day high is still a price)
- `Ma200Day` (`decimal`) → `Price` (200-day MA is still a price)

**Entity fields (`DcaConfiguration.cs`):**
- `BaseDailyAmount` (`decimal`) → `UsdAmount`
- `BearBoostFactor` (`decimal`) → `Multiplier`
- `MaxMultiplierCap` (`decimal`) → `Multiplier`
- `MultiplierTiers` — stored as `jsonb`; `MultiplierTierData.DropPercentage` → `Percentage`, `MultiplierTierData.Multiplier` → `Multiplier`

**Entity fields (`DailyPrice.cs`):**
- `Open`, `High`, `Low`, `Close` (`decimal`) → `Price`
- `Volume` (`decimal`) — stays `decimal` (no domain semantics beyond "number of units")
- `Symbol` (`string`) → `Symbol` (only if Symbol EF converter registered)

**Configuration (`DcaOptions.cs`) — Plan 2:**
- `BaseDailyAmount` (`decimal`) → `UsdAmount`
- `BearBoostFactor` (`decimal`) → `Multiplier`
- `MaxMultiplierCap` (`decimal`) → `Multiplier`
- `MultiplierTier.DropPercentage` (`decimal`) → `Percentage`
- `MultiplierTier.Multiplier` (`decimal`) → `Multiplier`

**Events (`PurchaseCompletedEvent.cs`):**
- `BtcAmount` (`decimal`) → `Quantity`
- `Price` (`decimal`) → `Price`
- `UsdSpent` (`decimal`) → `UsdAmount`
- `RemainingUsdc` (`decimal`) → `UsdAmount`
- `CurrentBtcBalance` (`decimal`) → `Quantity`
- `Multiplier` (`decimal`) → `Multiplier`
- `DropPercentage` (`decimal`) → `Percentage`
- `High30Day` (`decimal`) → `Price`
- `Ma200Day` (`decimal`) → `Price`

**MultiplierCalculator.cs and MultiplierResult record:**
- `MultiplierCalculator.Calculate()` parameters: `currentPrice` → `Price`, `baseAmount` → `UsdAmount`, `high30Day` → `Price`, `ma200Day` → `Price`, bearBoostFactor → `Multiplier`, maxCap → `Multiplier`
- `MultiplierResult` record: all decimal fields typed accordingly

**BacktestSimulator.cs / Backtest models:**
- `DailyPriceData` record: `Open`, `High`, `Low`, `Close` → `Price`; `Volume` stays decimal
- `PurchaseLogEntry`: all price/amount/quantity fields typed accordingly
- `BacktestConfig`: `BaseDailyAmount` → `UsdAmount`, multiplier fields → value objects

**Endpoints / DTOs:**
- `PurchaseDto`: `price`, `cost`, `quantity`, `multiplier`, `dropPercentage` fields typed
- JSON serialization unchanged (Vogen STJ serializes as plain numbers)

**Dashboard TypeScript (following Phase 13 pattern):**
- Add branded types for `Price`, `UsdAmount`, `Quantity`, `Multiplier`, `Percentage`, `Symbol` in `dashboard.ts`
- `PurchaseDto` interface fields typed to branded types

### Anti-Patterns to Avoid

- **Putting `[ValueObject<decimal>]` in VogenGlobalConfig with a second `[assembly: VogenDefaults]`:** Only one `[assembly: VogenDefaults]` attribute per assembly. New value objects use `[ValueObject<decimal>]` per-type — no global change needed.
- **Adding comparison operators via the underlying decimal directly:** Don't write `operator <(decimal left, Price right)`. Cross-primitive comparison is already provided via the implicit cast `toPrimitiveCasting: CastOperator.Implicit`. Write typed-to-typed operators only.
- **Applying value objects to `PurchaseLogEntry` backtest model without considering snapshot tests:** `PurchaseLogEntry` is snapshot-tested via Snapper. If fields change type, snapshot files need manual update. Plan for snapshot refresh.
- **Forgetting the `Conversions.TypeConverter` flag for `DcaOptions` binding:** ASP.NET Core configuration binding uses `TypeConverter`, not `JsonConverter`. Without it, `UsdAmount` won't bind from `"10.0"` in appsettings.json.
- **Making `DailyPrice.Symbol` a `Symbol` value object without updating the composite key:** `DailyPrice` uses `(Date, Symbol)` as a composite PK. If `Symbol` becomes a Vogen type, the `HasKey(e => new { e.Date, e.Symbol })` configuration in `OnModelCreating` must register a converter for the key. EF Core key converters via `ConfigureConventions` handle this automatically — the `Properties<Symbol>().HaveConversion<>()` registration covers PK columns too.
- **Forgetting `MultiplierTierData` inside the `jsonb` column:** `DcaConfiguration.MultiplierTiers` is a `List<MultiplierTierData>` stored as `jsonb`. `MultiplierTierData` is a record with `decimal` fields. These fields do NOT go through EF Core value converters (they're inside JSON, not individual columns). The record stays with raw `decimal` or uses value objects that System.Text.Json can serialize — the Vogen STJ converter handles this. Test this carefully.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Decimal value object boilerplate | Custom struct with validation, equality, IComparable, JSON converter, EF converter | Vogen `[ValueObject<decimal>]` | ~300 lines generated per type; edge cases in EF comparers, STJ read/write, initialization guard |
| String value object | Custom class with validation, equality, TypeConverter | Vogen `[ValueObject<string>]` | Handles null guards, case sensitivity, Equals/GetHashCode correctly |
| EF Core converter per decimal column | Custom `ValueConverter<Price, decimal>` | Vogen's generated `EfCoreValueConverter` inner class + `ConfigureConventions` | Same pattern as Phase 13 typed IDs |
| JSON converter per value object | Custom `JsonConverter<Price>` | Vogen `Conversions.SystemTextJson` | Handles both number and string serialization contexts |

**Key insight:** The only hand-rolled code per value object is: (1) the `Validate` method (2-3 lines), (2) comparison operators `<`, `>`, `<=`, `>=` (4 one-liners), and (3) cross-type arithmetic operators (1-2 lines each). Everything else is source-generated.

---

## Common Pitfalls

### Pitfall 1: Vogen Does NOT Generate `<`, `>`, `<=`, `>=` Operators

**What goes wrong:** Writing `if (dropPercentage >= tier.DropPercentage)` compiles only if `>=` is defined. After converting both to value objects, the compiler will error with "Operator '>=' cannot be applied to operands of type 'Percentage' and 'Percentage'".

**Why it happens:** All Context7 snapshots of Vogen-generated decimal structs show only `CompareTo` and `==`/`!=`. The `<`, `>`, `<=`, `>=` operators are not in the generated output. This was discussed in GitHub issue #241 (merged PR #652 which adds `<`/`>` for numeric-like underlying types), but the snapshots in the codebase at v8.0.4 do not show them being generated consistently. Treat as absent.

**How to avoid:** Hand-write all four comparison operators in each numeric value object's partial struct body. Pattern:
```csharp
public static bool operator <(Price left, Price right) => left.Value < right.Value;
public static bool operator >(Price left, Price right) => left.Value > right.Value;
public static bool operator <=(Price left, Price right) => left.Value <= right.Value;
public static bool operator >=(Price left, Price right) => left.Value >= right.Value;
```
Total: 4 one-liners per numeric type. Six numeric types (Price, UsdAmount, Quantity, Multiplier, Percentage) = 24 lines total.

**Warning signs:** Compiler errors CS0019 "Operator '>' cannot be applied" after applying value objects to `MultiplierCalculator.Calculate`.

**Confidence:** HIGH — confirmed from Context7 snapshot analysis; all v8.0 snapshots show only `CompareTo` and `==`/`!=`

### Pitfall 2: DcaOptions Configuration Binding Requires TypeConverter Conversion

**What goes wrong:** `DcaOptions.BaseDailyAmount` changes from `decimal` to `UsdAmount`. ASP.NET Core reads `"BaseDailyAmount": 10.0` from appsettings.json and binds it to the options class. For this to work, .NET needs to convert a `decimal` (or string) to `UsdAmount`. It uses `TypeConverter` (not `JsonConverter`) for configuration binding.

**Why it happens:** `IOptions<T>` binding uses `System.ComponentModel.TypeConverter`. Vogen generates a `TypeConverter` only when `Conversions.TypeConverter` is included. The current global default is `Conversions.EfCoreValueConverter | Conversions.SystemTextJson` — no `TypeConverter`.

**How to avoid:** Add `Conversions.TypeConverter` to the value objects used in DcaOptions. Either:
- Option A: Add to per-type `[ValueObject<decimal>]` for affected types: `[ValueObject<decimal>(conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson | Conversions.TypeConverter)]`
- Option B: Update `VogenGlobalConfig.cs` to add `TypeConverter` globally. But this applies to Guid typed IDs too — should be fine since TypeConverter for Guids is standard.

**Recommendation:** Option A (per-type) is safer. Add TypeConverter only to the value objects that will be used in DcaOptions: UsdAmount, Multiplier, Percentage.

**Warning signs:** `InvalidOperationException` at startup when loading configuration: "Failed to bind configuration to type 'DcaOptions'" or similar.

**Confidence:** HIGH — this is a well-known .NET configuration binding constraint. TypeConverter is required for non-primitive types in configuration.

### Pitfall 3: MultiplierTierData Inside jsonb Column

**What goes wrong:** `DcaConfiguration.MultiplierTiers` is stored as `jsonb` (PostgreSQL). The C# type is `List<MultiplierTierData>`. `MultiplierTierData` is `record(decimal DropPercentage, decimal Multiplier)`. If these fields change to `Percentage` and `Multiplier` value objects, EF Core needs to serialize the list to JSON. EF Core uses STJ for `jsonb` columns — so Vogen's STJ converter applies. However, STJ will serialize `Percentage.Value` (a decimal) correctly if the implicit cast or the JsonConverter is registered.

**Why it happens:** STJ doesn't automatically know how to serialize Vogen value objects unless a `JsonConverter<T>` is registered in the serialization options. EF Core's jsonb serialization uses its own STJ settings, which may not include Vogen's generated converters.

**How to avoid:** Keep `MultiplierTierData` using raw `decimal` fields for now. The record is a "DTO inside jsonb" — not a domain entity itself. Converting it to value objects is a secondary concern that adds complexity without safety benefit (the data comes from config validation anyway). Scope decision: leave `MultiplierTierData` as-is with raw `decimal`.

**Warning signs:** EF Core throwing serialization errors on `DcaConfiguration` CRUD operations. jsonb column storing `null` or incorrect values.

**Confidence:** MEDIUM — this behavior depends on EF Core's jsonb serialization STJ configuration. Verify during implementation.

### Pitfall 4: Snapshot Tests Will Break for BacktestSimulator

**What goes wrong:** `BacktestSimulatorTests.cs` uses Snapper for golden-file snapshot comparison. `PurchaseLogEntry` contains many decimal fields (`Price`, `SmartAmountUsd`, `SmartBtcBought`, etc.). If these change to value objects, Snapper's snapshot files (stored as JSON) will need to be updated because the serialized form changes.

**Why it happens:** Snapper compares the serialized JSON representation. `Price.From(50000)` serializes differently than `50000m` in Snapper's output unless Snapper uses the STJ JsonConverter — which it may not.

**How to avoid:**
- Check whether Snapper respects registered `JsonConverter<T>` from the global STJ options. If Vogen's converters are globally registered, `Price` serializes as `50000` (a number) in JSON, matching the old snapshot format.
- If Snapper does not use global STJ options, the snapshot files need regeneration after applying value objects to backtest models.
- Plan: Apply value objects to entities first (Plan 1). Apply to backtest models (Plan 2). After Plan 2, run `dotnet test` and use Snapper's update flag to regenerate snapshot files: `UPDATE_SNAPSHOTS=true dotnet test`.

**Warning signs:** Snapshot test failures in `BacktestSimulatorTests` after applying value objects to `PurchaseLogEntry` or `DailyPriceData`.

**Confidence:** MEDIUM — depends on Snapper's STJ configuration. Needs verification.

### Pitfall 5: DailyPrice.Symbol in Composite Key

**What goes wrong:** `DailyPrice` uses composite key `(DateOnly Date, string Symbol)`. If `Symbol` becomes a `Symbol` value object, EF Core needs to know how to compare and convert the Symbol column in the key.

**Why it happens:** EF Core requires value converters to be registered for types used in primary/composite keys. The `Properties<Symbol>().HaveConversion<>()` registration in `ConfigureConventions` should handle this, but composite key usage may have edge cases.

**How to avoid:** Apply `Symbol` value object to `DailyPrice.Symbol` after verifying that the composite key query (`HasKey(e => new { e.Date, e.Symbol })`) still works correctly. No schema migration is needed since the underlying column type stays `text`/`varchar`. Test via integration or by running the app and verifying historical data queries work.

**Alternative:** Keep `DailyPrice.Symbol` as raw `string` and only apply `Symbol` to service layer parameters. This is pragmatic but inconsistent with full type safety.

**Confidence:** MEDIUM — EF Core composite key with value converters is supported but less commonly used; test explicitly.

### Pitfall 6: EF Core HasPrecision Annotations Still Required

**What goes wrong:** `OnModelCreating` currently sets `HasPrecision(18, 8)` on `Purchase.Price`, `Purchase.Quantity`, etc. After value objects, these properties still need precision annotations because the underlying DB column type doesn't change.

**Why it happens:** `HasPrecision` is a column-level annotation, not a type-level annotation. Vogen doesn't carry precision information. The annotations in `OnModelCreating` must stay.

**How to avoid:** Keep all `entity.Property(e => e.Price).HasPrecision(18, 8)` annotations in `OnModelCreating`. The property type changes from `decimal` to `Price`, but EF Core will still accept `entity.Property(e => e.Price)` — it just uses the registered value converter to know the underlying column type.

**Confidence:** HIGH — EF Core precision annotations are independent of value converters.

### Pitfall 7: toPrimitiveCasting: Implicit May Cause Accidental Precision Bypass

**What goes wrong:** With implicit `toPrimitiveCasting`, writing `decimal d = price` silently unwraps the Price into a raw decimal. Existing code that does `purchase.Price * multiplier` (decimal arithmetic) will still compile — it implicitly unwraps Price to decimal, multiplies by decimal Multiplier, returning decimal. This silently bypasses value object arithmetic and returns a raw decimal.

**Why it happens:** The global `VogenGlobalConfig.cs` sets `toPrimitiveCasting: CastOperator.Implicit`. This was correct for Guid typed IDs (ergonomics). For numeric value objects, it means any arithmetic with a raw decimal "just works" — which can prevent the compiler from catching type-incorrect arithmetic.

**How to avoid:** For numeric value objects, consider using `toPrimitiveCasting: CastOperator.Explicit` per-type by specifying it in `[ValueObject<decimal>(toPrimitiveCasting: CastOperator.Explicit)]`. This forces `.Value` access, making arithmetic intentions explicit. However, this conflicts with the global default and makes projections more verbose.

**Recommendation (Claude's Discretion):** Keep implicit to-primitive casting for value objects (consistent with Phase 13 IDs). Accept that `decimal d = somePrice` works silently. The primary value is in catching type errors across domain boundaries (passing UsdAmount where Price is expected), not in preventing decimal arithmetic.

**Confidence:** MEDIUM — trade-off between ergonomics and strictness. Follow user's preference for implicit casting established in Phase 13.

---

## Code Examples

Verified patterns from official sources and codebase analysis:

### Complete Numeric Value Object (Price)

```csharp
// Models/Values/Price.cs
// Source: Vogen README Validate pattern + hand-written operators

using Vogen;
using TradingBot.ApiService.Models.Values; // for Quantity, UsdAmount cross-type

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct Price
{
    private static Validation Validate(decimal value) =>
        value > 0
            ? Validation.Ok
            : Validation.Invalid("Price must be strictly positive (> 0)");

    // Comparison operators (NOT generated by Vogen)
    public static bool operator <(Price left, Price right) => left.Value < right.Value;
    public static bool operator >(Price left, Price right) => left.Value > right.Value;
    public static bool operator <=(Price left, Price right) => left.Value <= right.Value;
    public static bool operator >=(Price left, Price right) => left.Value >= right.Value;
}
```

### Complete Numeric Value Object (UsdAmount) with Cross-Type Arithmetic

```csharp
// Models/Values/UsdAmount.cs
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct UsdAmount
{
    private static Validation Validate(decimal value) =>
        value > 0
            ? Validation.Ok
            : Validation.Invalid("UsdAmount must be strictly positive (> 0)");

    // Comparison operators
    public static bool operator <(UsdAmount left, UsdAmount right) => left.Value < right.Value;
    public static bool operator >(UsdAmount left, UsdAmount right) => left.Value > right.Value;
    public static bool operator <=(UsdAmount left, UsdAmount right) => left.Value <= right.Value;
    public static bool operator >=(UsdAmount left, UsdAmount right) => left.Value >= right.Value;

    // Arithmetic: UsdAmount + UsdAmount = UsdAmount (accumulation in portfolio)
    public static UsdAmount operator +(UsdAmount left, UsdAmount right) =>
        UsdAmount.From(left.Value + right.Value);

    // Cross-type: UsdAmount * Multiplier = UsdAmount (applying DCA boost)
    public static UsdAmount operator *(UsdAmount amount, Multiplier multiplier) =>
        UsdAmount.From(amount.Value * multiplier.Value);

    // Cross-type: UsdAmount / Price = Quantity (how much BTC can I buy?)
    public static Quantity operator /(UsdAmount amount, Price price) =>
        Quantity.From(amount.Value / price.Value);
}
```

### Percentage Value Object (0-1 format)

```csharp
// Models/Values/Percentage.cs
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct Percentage
{
    // 0-1 format: 0.05 = 5%. Display: value * 100
    private static Validation Validate(decimal value) =>
        value >= 0 && value <= 1
            ? Validation.Ok
            : Validation.Invalid("Percentage must be between 0 and 1 (e.g. 0.05 for 5%)");

    // Comparison operators
    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;
    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;
    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;
    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;
}
```

### Multiplier with Upper Bound Validation

```csharp
// Models/Values/Multiplier.cs
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct Multiplier
{
    // > 0, cap at 10x to catch config typos (per user decision "e.g. 10x")
    private const decimal MaxReasonableMultiplier = 10m;

    private static Validation Validate(decimal value) =>
        value > 0 && value <= MaxReasonableMultiplier
            ? Validation.Ok
            : Validation.Invalid($"Multiplier must be between 0 (exclusive) and {MaxReasonableMultiplier} (inclusive)");

    // Comparison operators
    public static bool operator <(Multiplier left, Multiplier right) => left.Value < right.Value;
    public static bool operator >(Multiplier left, Multiplier right) => left.Value > right.Value;
    public static bool operator <=(Multiplier left, Multiplier right) => left.Value <= right.Value;
    public static bool operator >=(Multiplier left, Multiplier right) => left.Value >= right.Value;
}
```

### Quantity with Zero Consideration

```csharp
// Models/Values/Quantity.cs
// Zero-allowance decision: DcaExecutionService creates Purchase with Quantity = 0 initially,
// then updates after fill. Zero is valid in pending state.
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<decimal>]
public readonly partial struct Quantity
{
    // Zero allowed: Purchase records start at 0 before fill
    private static Validation Validate(decimal value) =>
        value >= 0
            ? Validation.Ok
            : Validation.Invalid("Quantity must be zero or positive");

    // Comparison operators
    public static bool operator <(Quantity left, Quantity right) => left.Value < right.Value;
    public static bool operator >(Quantity left, Quantity right) => left.Value > right.Value;
    public static bool operator <=(Quantity left, Quantity right) => left.Value <= right.Value;
    public static bool operator >=(Quantity left, Quantity right) => left.Value >= right.Value;

    // Cross-type: Quantity * Price = UsdAmount (cost of purchase)
    public static UsdAmount operator *(Quantity quantity, Price price) =>
        UsdAmount.From(quantity.Value * price.Value);

    // Quantity + Quantity = Quantity (accumulation)
    public static Quantity operator +(Quantity left, Quantity right) =>
        Quantity.From(left.Value + right.Value);
}
```

### Symbol (String Wrapper)

```csharp
// Models/Values/Symbol.cs
using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<string>]
public readonly partial struct Symbol
{
    private static Validation Validate(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20
            ? Validation.Ok
            : Validation.Invalid("Symbol must be non-empty and at most 20 characters");

    // Well-known domain constants
    public static readonly Symbol Btc = From("BTC");
    public static readonly Symbol BtcUsdc = From("BTC/USDC");
}
```

### Updated Purchase Entity

```csharp
// Models/Purchase.cs — after Phase 14
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class Purchase : BaseEntity<PurchaseId>
{
    public DateTimeOffset ExecutedAt { get; set; }
    public Price Price { get; set; }         // was decimal
    public Quantity Quantity { get; set; }   // was decimal
    public UsdAmount Cost { get; set; }      // was decimal
    public Multiplier Multiplier { get; set; }  // was decimal
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public bool IsDryRun { get; set; }
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }

    // Multiplier metadata
    public string? MultiplierTier { get; set; }
    public Percentage DropPercentage { get; set; }  // was decimal
    public Price High30Day { get; set; }            // was decimal
    public Price Ma200Day { get; set; }             // was decimal
}
```

### DcaOptions After Value Objects (Plan 2)

```csharp
// Configuration/DcaOptions.cs — after Phase 14 Plan 2
public class DcaOptions
{
    public UsdAmount BaseDailyAmount { get; set; }   // was decimal
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int HighLookbackDays { get; set; } = 30;
    public int BearMarketMaPeriod { get; set; } = 200;
    public Multiplier BearBoostFactor { get; set; }  // was decimal
    public Multiplier MaxMultiplierCap { get; set; } // was decimal
    public bool DryRun { get; set; } = false;
    public List<MultiplierTier> MultiplierTiers { get; set; } = [];
}

public class MultiplierTier
{
    public Percentage DropPercentage { get; set; }   // was decimal
    public Multiplier Multiplier { get; set; }        // was decimal
}
```

**Important:** `UsdAmount`, `Multiplier`, `Percentage` must include `Conversions.TypeConverter` for this to bind from appsettings.json. Either add it per-type or update global config.

### MultiplierCalculator.Calculate Updated Signature

```csharp
// Application/Services/MultiplierCalculator.cs — after Phase 14
public static MultiplierResult Calculate(
    Price currentPrice,
    UsdAmount baseAmount,
    Price high30Day,
    Price ma200Day,
    IReadOnlyList<MultiplierTier> tiers,
    Multiplier bearBoostFactor,
    Multiplier maxCap)
{
    // drop% calculation now uses value object arithmetic
    Percentage dropPercentage = high30Day > Price.From(0)
        ? Percentage.From((high30Day.Value - currentPrice.Value) / high30Day.Value)
        : Percentage.From(0);

    // tier matching: dropPercentage >= tier.DropPercentage uses hand-written >= on Percentage
    ...
}
```

### Dashboard TypeScript Branded Types (dashboard.ts additions)

```typescript
// app/types/dashboard.ts — additions following Phase 13 pattern
export type Price = number & { readonly __brand: 'Price' }
export type UsdAmount = number & { readonly __brand: 'UsdAmount' }
export type Quantity = number & { readonly __brand: 'Quantity' }
export type Multiplier = number & { readonly __brand: 'Multiplier' }
export type Percentage = number & { readonly __brand: 'Percentage' }
export type Symbol = string & { readonly __brand: 'Symbol' }

export interface PurchaseDto {
  id: PurchaseId        // from Phase 13
  executedAt: string
  price: Price          // was number
  cost: UsdAmount       // was number
  quantity: Quantity    // was number
  multiplierTier: string
  multiplier: Multiplier  // was number
  dropPercentage: Percentage  // was number
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Raw `decimal` fields with manual validation in `DcaOptionsValidator` | Vogen value objects with built-in validation | Phase 14 | `DcaOptionsValidator` can remove redundant bounds checks (value objects already validate at construction) |
| `decimal Price`, `decimal Quantity`, `decimal Cost` in Purchase entity | `Price Price`, `Quantity Quantity`, `UsdAmount Cost` | Phase 14 | Compile-time prevention of `cost / quantity` (giving price) when `cost / price` (giving quantity) was intended |
| Validator checks `options.BaseDailyAmount <= 0` | `UsdAmount.From()` throws if <= 0 | Phase 14 | Validation at construction, not at startup validation only |

**Deprecated/outdated after Phase 14:**
- `DcaOptionsValidator` bounds checks for `BaseDailyAmount > 0`, `BearBoostFactor > 0`, `MaxMultiplierCap > 0`: these become redundant when value objects validate at construction. The validator can focus on cross-field logic (ordering of tiers, etc.).
- Manual null/zero guards like `if (high30Day > 0)` in `MultiplierCalculator`: becomes `if (high30Day > Price.From(0))` which is semantically identical but uses typed comparison.

---

## Open Questions

1. **Zero-quantity for Quantity: allow or reject?**
   - What we know: `DcaExecutionService` creates `Purchase { Quantity = 0 }` initially (before fill). `PurchaseCompletedHandler` computes `totalBtc = purchases.Sum(p => p.Quantity)` — summing zeros is fine.
   - What's unclear: Should a `Purchase` ever be persisted with `Quantity = 0`? Is it semantically valid?
   - Recommendation: Allow zero (`Quantity >= 0`). A pending purchase with zero quantity before fill is a valid intermediate state. Reject negative only.

2. **Multiplier upper bound: 10x?**
   - What we know: `MaxMultiplierCap = 4.5` in appsettings.json. The validation is "catch config typos."
   - What's unclear: Is 10x the right cap? A legitimate cap of 9.5x would fail with `MaxMultiplierCap = Multiplier.From(9.5m)` if cap is 10. But what if someone intentionally sets a 15x cap?
   - Recommendation: Use 20x as the "sanity cap" for Multiplier. The actual operational cap is `MaxMultiplierCap` in config — the Vogen validation just prevents ridiculous values. 20x is sufficiently high to allow any realistic DCA strategy while catching `100` or `1000` typos.

3. **Should `DailyPrice.Symbol` become `Symbol`?**
   - What we know: `DailyPrice` uses composite PK `(Date, Symbol)`. EF Core supports value converters on PK columns via `ConfigureConventions`. The DB column stays `text`.
   - What's unclear: Whether EF Core handles `Symbol` in composite key correctly with `HasKey(e => new { e.Date, e.Symbol })`. This pattern is less tested.
   - Recommendation: Apply `Symbol` to `DailyPrice.Symbol` but verify with a test that historical data queries work. If it breaks, fall back to keeping `DailyPrice.Symbol` as raw `string` (use `Symbol` in service parameters only).

4. **Should `PurchaseCompletedEvent` fields change to value objects?**
   - What we know: `PurchaseCompletedEvent` is a domain event serialized for outbox/Dapr pub-sub via MessagePack. Changing field types affects serialization.
   - What's unclear: Whether MessagePack handles Vogen value objects (it uses custom formatters, not STJ). Vogen doesn't generate MessagePack formatters.
   - Recommendation: Scope events to value objects IF MessagePack serialization is not used for this event. Check `PurchaseCompletedEvent` serialization path: it's published via `OutboxMessage` which serializes the event via JSON (not MessagePack). Redis cache uses MessagePack but events don't go to Redis. So STJ serialization of the event should work fine with Vogen's generated JsonConverter.

5. **Backtest models (`PurchaseLogEntry`, `DailyPriceData`) — apply or defer?**
   - What we know: These are snapshot-tested. `MultiplierCalculator.Calculate` is called by both `DcaExecutionService` (real) and `BacktestSimulator` (simulation).
   - What's unclear: If `MultiplierCalculator.Calculate` changes signature to value objects, both callers must update. `BacktestSimulator` uses raw decimals throughout its internal `DayData` record and `PurchaseLogEntry`.
   - Recommendation: Apply value objects to `MultiplierCalculator.Calculate` public signature. Allow `BacktestSimulator` to use `.Value` to extract decimals for its internal arithmetic (snapshot tests stay stable). Or apply fully and refresh snapshots in Plan 2. Depends on scope preference.

---

## Codebase Impact Summary

### Plan 1: Value Object Definitions + Entity Application
- Define `Price`, `UsdAmount`, `Quantity`, `Multiplier`, `Percentage`, `Symbol` in `Models/Values/`
- Register EF Core converters in `ConfigureConventions`
- Apply to entity fields: `Purchase`, `DcaConfiguration`, `DailyPrice`
- Update `PurchaseCompletedEvent` and related handlers
- Add TypeScript branded types to `dashboard.ts`
- No migration needed (column types stay the same: `numeric(18,8)`, `text`, etc.)

### Plan 2: Apply to Services, Config, and Backtest
- Apply to `DcaOptions` (requires `Conversions.TypeConverter`)
- Apply to `MultiplierCalculator.Calculate` signature and `MultiplierResult`
- Apply to `BacktestSimulator` / `PurchaseLogEntry` / `DailyPriceData` (snapshot refresh)
- Apply to service parameters: `PriceDataService`, `DcaExecutionService`
- Update `DcaOptionsValidator` to remove redundant validation that value objects now provide
- Verify snapshot tests pass (update snapshots if needed)

---

## Sources

### Primary (HIGH confidence)
- `/stevedunn/vogen` (Context7) — decimal value object generated code (snap-v8.0 snapshots), Validate method signature, CompareTo generation, operator == and != generation, TypeConverter description, STJ converter generation
- Phase 13 VERIFICATION.md — confirmed `toPrimitiveCasting`/`fromPrimitiveCasting` (not `castOperator`), `Properties<T>().HaveConversion<>()` approach, Phase 13 actual state
- Codebase code review — all entity fields, services, configuration, events, dashboard TypeScript types, tests

### Secondary (MEDIUM confidence)
- [Vogen FAQ](https://stevedunn.github.io/Vogen/faq.html) — TypeConverter for configuration binding guidance
- [Vogen Casting Docs](https://stevedunn.github.io/Vogen/casting.html) — toPrimitiveCasting / fromPrimitiveCasting confirmed as distinct params in 8.0.4
- [Vogen README](https://github.com/SteveDunn/Vogen/blob/main/README.md) — `private static Validation Validate(T value)` method signature confirmed
- [GitHub Issue #241](https://github.com/SteveDunn/Vogen/issues/241) — `<`/`>` operators issue and PR #652 merge, but v8 snapshots still show only CompareTo; treat as absent to be safe
- [GitHub Discussion #687](https://github.com/SteveDunn/Vogen/discussions/687) — comparison operator philosophy (CompareTo vs >/< operators)

### Tertiary (LOW confidence — for awareness)
- WebSearch results confirming `private static Validation Validate(T value)` pattern across multiple Vogen versions

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Vogen 8.0.4 already in project; decimal/string value object pattern verified via Context7
- Architecture: HIGH — direct extension of Phase 13 pattern; project structure clear from code review
- EF Core registration: HIGH — continuation of Phase 13 verified approach
- Comparison operators: HIGH — confirmed NOT generated by Vogen v8 snapshots; hand-write required
- TypeConverter for config binding: HIGH — well-known .NET constraint; verified in docs
- Snapshot test impact: MEDIUM — depends on Snapper/STJ integration not directly verified
- Cross-type arithmetic: HIGH — standard C# operator placement, no Vogen specifics
- DailyPrice composite key: MEDIUM — less commonly tested pattern

**Research date:** 2026-02-18
**Valid until:** 2026-03-18 (Vogen 8.0.4 stable; EF Core 10 patterns stable)

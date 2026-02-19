# Phase 5: MultiplierCalculator Extraction - Research

**Researched:** 2026-02-13
**Domain:** C# static class extraction, pure functions, unit testing
**Confidence:** HIGH

## Summary

This phase extracts multiplier calculation logic from `DcaExecutionService.CalculateMultiplierAsync()` into a pure, testable static class. The extraction is straightforward: convert the async method (lines 270-354) to a synchronous static method by removing `await` calls and accepting pre-computed values (high30Day, ma200Day) as parameters instead of fetching them.

The existing `MultiplierResult` record (lines 360-367) already provides the right output shape and should be moved alongside the calculator. The codebase uses xUnit, FluentAssertions, and NSubstitute for testing, with file-scoped namespaces and primary constructors throughout.

**Primary recommendation:** Create `MultiplierCalculator` as a static class in `Application/Services/` namespace, use record for result type, test with xUnit Theory + InlineData for tier boundaries and Snapper for golden snapshot regression.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Calculator output shape:**
- Return a rich result object, not just a multiplier number
- Result includes: multiplier value, matched tier name (always present — "Base" for 1.0x days), whether bear market detected, bear boost amount applied, drop percentage from 30-day high, and final spend amount
- Calculator takes baseAmount as an input parameter and computes the final spend amount (baseAmount * multiplier)
- When no tier triggers (normal day), return multiplier = 1.0 with tier = "Base" — always a valid result, never null tier

**Bear market detection:**
- MultiplierCalculator handles bear market detection internally — pass in ma200Day price, calculator determines bear status by comparing currentPrice < ma200Day
- Result explicitly includes both isBearMarket flag AND the bearBoostApplied amount for full simulation transparency
- If MA200 data is unavailable (null/zero), treat as non-bear market — no boost applied, conservative default
- Max cap applies AFTER bear boost: finalMultiplier = min(tierMultiplier + bearBoost, maxCap). Cap always wins.

**Test scenario coverage:**
- Use both golden snapshot (capture current production behavior) AND hand-calculated expected values
- Test at exact tier boundaries (>= vs > verification) AND mid-tier values for comprehensive coverage
- Unit tests only for MultiplierCalculator — no integration test for DcaExecutionService delegation

### Claude's Discretion

- Bear boost + tier combination coverage: Claude determines the right balance of exhaustive vs representative test cases
- Result object naming and structure (record vs class)
- Namespace and file placement within the project

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 10.0 | Runtime and language features | Current LTS, project baseline |
| xUnit | 2.9.3 | Test framework | Already in use, industry standard for .NET testing |
| FluentAssertions | 7.0.0 | Assertion library | Already in use, provides readable assertions |
| NSubstitute | 5.3.0 | Mocking library | Already in use, not needed for pure functions but available |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Snapper | 2.4.1 | Snapshot testing | For golden master regression tests |
| Xunit.Combinatorial | 1.6.24 | Combinatorial test data | If exhaustive tier+bear combinations needed |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Static class | Extension methods | Extension methods provide better IntelliSense but require a type to extend; static utility class is clearer for pure calculation |
| Record | Class | Records provide value equality and immutability by default, perfect for result objects; classes require manual equality implementation |
| Snapper | Verify | Both support snapshot testing; Snapper is simpler (no file organization conventions), Verify has richer diff tools |

**Installation:**

```bash
# Already installed
# xUnit, FluentAssertions, NSubstitute in TradingBot.ApiService.Tests.csproj

# Add for snapshot testing
dotnet add tests/TradingBot.ApiService.Tests package Snapper
```

## Architecture Patterns

### Recommended Project Structure

```
TradingBot.ApiService/
├── Application/
│   └── Services/
│       ├── MultiplierCalculator.cs      # NEW: Pure static calculation
│       ├── DcaExecutionService.cs       # MODIFIED: Delegate to calculator
│       └── IPriceDataService.cs
tests/TradingBot.ApiService.Tests/
├── Application/
│   └── Services/
│       └── MultiplierCalculatorTests.cs # NEW: Unit tests
```

### Pattern 1: Pure Static Calculator

**What:** Static class with pure functions - no state, no async, no DI, deterministic outputs.

**When to use:** For calculations that don't need I/O, database access, or mutable state. Perfect for reusable logic across live DCA and backtesting.

**Example:**

```csharp
// Source: Extracted from DcaExecutionService.CalculateMultiplierAsync()
namespace TradingBot.ApiService.Application.Services;

public static class MultiplierCalculator
{
    public static MultiplierResult Calculate(
        decimal currentPrice,
        decimal baseAmount,
        decimal high30Day,
        decimal ma200Day,
        IReadOnlyList<MultiplierTier> tiers,
        decimal bearBoostFactor,
        decimal maxCap)
    {
        // Calculation logic here (pure, synchronous)
        // Returns rich result object with all metadata
    }
}

public record MultiplierResult(
    decimal Multiplier,
    string Tier,              // "Base", ">= 5%", ">= 10%", etc.
    bool IsBearMarket,
    decimal BearBoostApplied, // 0 or bearBoostFactor
    decimal DropPercentage,
    decimal High30Day,
    decimal Ma200Day,
    decimal FinalAmount);     // baseAmount * Multiplier
```

**Note:** Current `MultiplierResult` record in `DcaExecutionService.cs` needs modification to match user requirements:
- Add `IsBearMarket` bool
- Add `BearBoostApplied` decimal
- Add `FinalAmount` decimal
- Rename `TotalMultiplier` to `Multiplier`
- Remove `DipMultiplier` and `BearMultiplier` (internal calculation details, not needed in result)
- Change `Tier` from "None" / "Error (fallback)" to "Base" for 1.0x days

### Pattern 2: Refactoring Async to Sync

**What:** Convert async method with external dependencies to pure sync method with pre-computed inputs.

**When to use:** When extracting calculation logic from a service that fetches data.

**Before (DcaExecutionService):**

```csharp
private async Task<MultiplierResult> CalculateMultiplierAsync(
    decimal currentPrice, DcaOptions options, CancellationToken ct)
{
    var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
    var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);
    // ... calculation logic
}
```

**After (MultiplierCalculator + DcaExecutionService):**

```csharp
// MultiplierCalculator (pure, sync)
public static MultiplierResult Calculate(
    decimal currentPrice,
    decimal baseAmount,
    decimal high30Day,
    decimal ma200Day,
    IReadOnlyList<MultiplierTier> tiers,
    decimal bearBoostFactor,
    decimal maxCap)
{
    // ... calculation logic (no await, no async)
}

// DcaExecutionService (orchestration)
private async Task<MultiplierResult> CalculateMultiplierAsync(
    decimal currentPrice, DcaOptions options, CancellationToken ct)
{
    var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
    var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

    return MultiplierCalculator.Calculate(
        currentPrice,
        options.BaseDailyAmount,
        high30Day,
        ma200Day,
        options.MultiplierTiers,
        options.BearBoostFactor,
        options.MaxMultiplierCap);
}
```

### Pattern 3: xUnit Theory with InlineData for Boundary Testing

**What:** Parameterized tests using `[Theory]` and `[InlineData]` attributes to test exact tier boundaries.

**When to use:** When testing conditional logic with specific thresholds (e.g., `dropPercent >= 5`, `dropPercent >= 10`).

**Example:**

```csharp
// Test tier boundaries: >= 5%, >= 10%, >= 20%
[Theory]
[InlineData(4.99, 1.0, "Base")]      // Just below 5% threshold
[InlineData(5.00, 1.5, ">= 5%")]     // Exact 5% boundary
[InlineData(5.01, 1.5, ">= 5%")]     // Just above 5%
[InlineData(9.99, 1.5, ">= 5%")]     // Just below 10%
[InlineData(10.00, 2.0, ">= 10%")]   // Exact 10% boundary
[InlineData(19.99, 2.0, ">= 10%")]   // Just below 20%
[InlineData(20.00, 3.0, ">= 20%")]   // Exact 20% boundary
[InlineData(25.00, 3.0, ">= 20%")]   // Above 20%
public void Calculate_TierBoundaries_MatchesExpectedMultiplier(
    decimal dropPercent, decimal expectedMultiplier, string expectedTier)
{
    // Arrange: high30Day = 100000, currentPrice = high30Day * (1 - dropPercent/100)
    var currentPrice = 100000m * (1m - dropPercent / 100m);

    // Act
    var result = MultiplierCalculator.Calculate(
        currentPrice: currentPrice,
        baseAmount: 10m,
        high30Day: 100000m,
        ma200Day: 100000m, // Above price, no bear boost
        tiers: DefaultTiers,
        bearBoostFactor: 1.5m,
        maxCap: 4.5m);

    // Assert
    result.Multiplier.Should().Be(expectedMultiplier);
    result.Tier.Should().Be(expectedTier);
}
```

### Pattern 4: Golden Snapshot Testing with Snapper

**What:** Capture current production behavior as baseline, fail tests if output changes unexpectedly.

**When to use:** For regression testing during refactoring — ensures new calculator produces identical results to old code.

**Example:**

```csharp
[Fact]
public void Calculate_ProductionScenarios_MatchesGoldenSnapshot()
{
    // Arrange: Real production scenarios from appsettings.json
    var scenarios = new[]
    {
        new { Price = 95000m, High = 100000m, MA200 = 80000m, Name = "5% drop, bull market" },
        new { Price = 85000m, High = 100000m, MA200 = 90000m, Name = "15% drop, bear market" },
        new { Price = 75000m, High = 100000m, MA200 = 90000m, Name = "25% drop, bear market" },
    };

    var results = scenarios.Select(s => new
    {
        s.Name,
        Result = MultiplierCalculator.Calculate(
            currentPrice: s.Price,
            baseAmount: 10m,
            high30Day: s.High,
            ma200Day: s.MA200,
            tiers: DefaultTiers,
            bearBoostFactor: 1.5m,
            maxCap: 4.5m)
    }).ToList();

    // Assert: Snapshot matches previous run
    results.ShouldMatchSnapshot();
}
```

**Note:** Snapper stores snapshots in `__snapshots__/` folder next to test file. First run creates baseline, subsequent runs compare against it.

### Anti-Patterns to Avoid

- **Async static methods:** MultiplierCalculator should be 100% synchronous. No async/await, no Task returns. Async defeats the purpose of a pure, reusable calculator.
- **Accessing external state:** No DI, no database calls, no logging inside the calculator. All inputs via parameters, all outputs via return value.
- **Mutating input parameters:** `IReadOnlyList<MultiplierTier>` ensures tiers can't be modified. Don't convert to mutable List internally.
- **Null tier in result:** Always return a valid tier name. Use "Base" for normal days, never null or empty string.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Snapshot testing | Custom JSON diffing | Snapper or Verify | Handles serialization, file management, diff reporting, and approval workflow |
| Parameterized tests | Manual test duplication | xUnit Theory + InlineData | Declarative, readable, generates separate test cases automatically |
| Decimal comparisons | `==` operator | FluentAssertions `Should().Be()` | Decimal is precise, but FluentAssertions provides better error messages |
| Value equality for records | Manual `Equals()` | C# record type | Built-in value equality, ToString(), deconstruction, with-expressions |

**Key insight:** C# records eliminate 50+ lines of boilerplate for result objects (Equals, GetHashCode, ToString). Static classes with pure functions are trivially testable without mocks. Don't reinvent these wheels.

## Common Pitfalls

### Pitfall 1: Forgetting to Handle MA200 = 0 Edge Case

**What goes wrong:** Division by zero or NullReferenceException when MA200 data is unavailable.

**Why it happens:** Current code checks `if (ma200Day > 0)` before using it, but easy to forget during extraction.

**How to avoid:** Explicitly handle the "unavailable data" case per user requirement: treat as non-bear market (no boost).

**Warning signs:**
- Tests fail with divide-by-zero
- Passing 0 for ma200Day doesn't return expected result

**Code pattern:**

```csharp
bool isBearMarket = ma200Day > 0 && currentPrice < ma200Day;
decimal bearBoostApplied = isBearMarket ? bearBoostFactor : 0m;
```

### Pitfall 2: Multiplicative Bear Boost Instead of Additive

**What goes wrong:** User requirement says "max cap applies AFTER bear boost: finalMultiplier = min(tierMultiplier + bearBoost, maxCap)".

**Why it happens:** Current code uses `var totalMultiplier = dipMultiplier * bearMultiplier` (multiplicative, lines 324).

**How to avoid:** Verify with user if this is intentional change or if current code should be preserved. Current code multiplies (1.5 tier * 1.5 bear = 2.25x), but context says "tierMultiplier + bearBoost".

**Warning signs:**
- Golden snapshot tests fail after extraction
- User requirement contradicts existing code behavior

**Resolution:** CLARIFY with user during plan review. Context may have typo, or this is an intentional behavior change.

### Pitfall 3: Tier Matching Logic Off-by-One

**What goes wrong:** Using `>` instead of `>=` or vice versa for tier thresholds.

**Why it happens:** Current code uses `dropPercent >= t.DropPercentage` (line 292), which is correct for "drop of 5% or more triggers 1.5x". Easy to flip during refactoring.

**How to avoid:** Test exact boundaries (5.00%, 10.00%, 20.00%) with InlineData to verify `>=` behavior.

**Warning signs:**
- Boundary tests fail
- Drop of exactly 5% doesn't match expected tier

**Code pattern:**

```csharp
// Correct: >= for "drop of 5% or more"
var matchedTier = tiers
    .OrderByDescending(t => t.DropPercentage)
    .FirstOrDefault(t => dropPercent >= t.DropPercentage);
```

### Pitfall 4: "None" vs "Base" Tier Naming

**What goes wrong:** Current code returns `Tier = "None"` for normal days (line 282). User requirement says use `"Base"` instead.

**Why it happens:** Code was written before naming convention decision.

**How to avoid:** Change tier name from "None" to "Base" during extraction, verify with unit test.

**Warning signs:**
- User requirement test expects "Base" but gets "None"
- Golden snapshot may preserve old behavior

**Code pattern:**

```csharp
// OLD: tier = "None"
// NEW:
string tier = "Base";
```

### Pitfall 5: Golden Snapshot Matches Wrong Behavior

**What goes wrong:** Snapshot captures current buggy behavior, then extraction "passes" but preserves the bug.

**Why it happens:** Golden master assumes current code is correct, but bugs exist.

**How to avoid:** Use BOTH snapshot (regression check) AND hand-calculated assertions (correctness check). If they disagree, investigate before approving snapshot.

**Warning signs:**
- Snapshot test passes but InlineData boundary test fails
- Multiplier math doesn't match manual calculation

**Resolution:** Hand-calculated tests are source of truth for correctness. Update snapshot only after verifying math.

## Code Examples

Verified patterns from existing codebase:

### Extract Method - Preserve Logging in Service

```csharp
// Source: DcaExecutionService.cs lines 270-354

// BEFORE: All logic in service
private async Task<MultiplierResult> CalculateMultiplierAsync(
    decimal currentPrice, DcaOptions options, CancellationToken ct)
{
    try
    {
        var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
        var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

        // ... 70 lines of calculation logic with logging ...

        logger.LogInformation("Multiplier: dip={DipMult}x ...", ...);
        return new MultiplierResult(...);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Multiplier calculation failed, falling back to 1.0x");
        return new MultiplierResult(...); // Fallback
    }
}

// AFTER: Pure calculation extracted, orchestration stays in service
private async Task<MultiplierResult> CalculateMultiplierAsync(
    decimal currentPrice, DcaOptions options, CancellationToken ct)
{
    try
    {
        var high30Day = await priceDataService.Get30DayHighAsync("BTC", ct);
        var ma200Day = await priceDataService.Get200DaySmaAsync("BTC", ct);

        var result = MultiplierCalculator.Calculate(
            currentPrice,
            options.BaseDailyAmount,
            high30Day,
            ma200Day,
            options.MultiplierTiers,
            options.BearBoostFactor,
            options.MaxMultiplierCap);

        logger.LogInformation(
            "Multiplier: tier={Tier}, drop={Drop:F2}%, bear={IsBear}, total={Total:F2}x",
            result.Tier, result.DropPercentage, result.IsBearMarket, result.Multiplier);

        return result;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Multiplier calculation failed, falling back to 1.0x");

        return MultiplierCalculator.Calculate(
            currentPrice: currentPrice,
            baseAmount: options.BaseDailyAmount,
            high30Day: 0m,     // Treat as unavailable
            ma200Day: 0m,      // Treat as unavailable
            tiers: [],         // Empty = no tier match
            bearBoostFactor: options.BearBoostFactor,
            maxCap: options.MaxMultiplierCap);
    }
}
```

**Key point:** Logging, try-catch, async data fetching stay in service. Pure calculation moves to static class.

### Test Class Organization

```csharp
// Source: Project conventions + xUnit best practices

namespace TradingBot.ApiService.Tests.Application.Services;

public class MultiplierCalculatorTests
{
    // Common test data
    private static readonly List<MultiplierTier> DefaultTiers =
    [
        new() { DropPercentage = 5, Multiplier = 1.5m },
        new() { DropPercentage = 10, Multiplier = 2.0m },
        new() { DropPercentage = 20, Multiplier = 3.0m }
    ];

    [Theory]
    [InlineData(...)]
    public void Calculate_TierBoundaries_MatchesExpectedMultiplier(...) { }

    [Theory]
    [InlineData(...)]
    public void Calculate_BearMarketScenarios_AppliesBoostCorrectly(...) { }

    [Fact]
    public void Calculate_MaxCapExceeded_ClampsToMaxCap() { }

    [Fact]
    public void Calculate_NoDataAvailable_ReturnsBaseMultiplier() { }

    [Fact]
    public void Calculate_ProductionScenarios_MatchesGoldenSnapshot() { }
}
```

**Pattern:** Group related test scenarios with Theory, use Fact for single-case tests, descriptive method names follow `MethodUnderTest_Scenario_ExpectedOutcome`.

### FluentAssertions for Decimal Assertions

```csharp
// Source: FluentAssertions best practices

// Simple equality (decimal is precise, no tolerance needed)
result.Multiplier.Should().Be(1.5m);
result.FinalAmount.Should().Be(15.0m); // 10 * 1.5

// Multiple assertions on object
result.Should().NotBeNull();
result.Tier.Should().Be(">= 5%");
result.IsBearMarket.Should().BeFalse();
result.BearBoostApplied.Should().Be(0m);

// Record equality (value-based)
var expected = new MultiplierResult(
    Multiplier: 1.5m,
    Tier: ">= 5%",
    IsBearMarket: false,
    BearBoostApplied: 0m,
    DropPercentage: 5.0m,
    High30Day: 100000m,
    Ma200Day: 100000m,
    FinalAmount: 15.0m);

result.Should().BeEquivalentTo(expected);
```

**Note:** Decimal uses fixed-point arithmetic, so exact equality is safe. No `BeApproximately()` needed unlike float/double.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Async methods everywhere | Pure sync for calculations | .NET 5+ | Easier to test, reusable in non-async contexts (backtest loops) |
| Class for result objects | Record types | C# 9 (2020) | Built-in value equality, immutability, with-expressions, ToString() |
| Manual test data loops | Theory + InlineData | xUnit 2.0+ | Declarative parameterized tests, better readability |
| JSON snapshot comparison | Snapper / Verify libraries | 2018+ | Automatic snapshot management, approval workflow |

**Deprecated/outdated:**
- `ValueTuple` for results (C# 7): Records are superior (named properties, better IntelliSense)
- xUnit `ClassData` for simple scenarios: InlineData is more readable for primitives

## Open Questions

1. **Multiplicative vs Additive Bear Boost**
   - What we know: Current code multiplies (dipMultiplier * bearMultiplier), CONTEXT.md says add (tierMultiplier + bearBoost)
   - What's unclear: Is this an intentional behavior change or a typo in context?
   - Recommendation: Preserve current multiplicative behavior in extraction (golden snapshot will verify), flag for user review during plan approval. If user wants additive, it's a one-line change in the calculator.

2. **Error Handling Strategy**
   - What we know: Current code has try-catch with fallback to 1.0x, but pure function can't catch exceptions during data fetch (that's in the service layer)
   - What's unclear: Should calculator throw on invalid inputs (negative price, negative baseAmount) or return error result?
   - Recommendation: Calculator should assume valid inputs (service validates before calling), add guard clauses with `ArgumentOutOfRangeException` for contract enforcement. Service handles try-catch for data fetching.

3. **Tier Naming Format**
   - What we know: Context says "Base" for normal days, current code uses "None", existing logs show ">= 5%" format
   - What's unclear: Should error fallback tier be "Base" or "Error" or something else?
   - Recommendation: Use "Base" for normal days (no tier matched), preserve "Error (fallback)" for exception cases (0 high30Day, 0 ma200Day). Service controls which code path executes.

## Sources

### Primary (HIGH confidence)

- [Static Class Design - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/static-class) - Framework design guidelines for static classes
- [Records - C# reference - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) - Official record type documentation
- [Best practices for writing unit tests - .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) - Official .NET testing best practices
- [Refactor into pure functions - LINQ to XML - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/linq/refactor-pure-functions) - Pure function refactoring guidance
- Codebase analysis: `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Application/Services/DcaExecutionService.cs` (lines 270-367)
- Codebase analysis: `/Users/baotoq/Work/trading-bot/tests/TradingBot.ApiService.Tests/TradingBot.ApiService.Tests.csproj` - Existing test stack

### Secondary (MEDIUM confidence)

- [Snapshot Testing in C# - Production Ready](https://www.production-ready.de/2025/12/01/snapshot-testing-in-csharp-en.html) - Snapshot testing patterns
- [GitHub - theramis/Snapper](https://github.com/theramis/Snapper) - Snapper snapshot testing library
- [Creating parameterised tests in xUnit - Andrew Lock](https://andrewlock.net/creating-parameterised-tests-in-xunit-with-inlinedata-classdata-and-memberdata/) - xUnit parameterized testing patterns
- [Numeric types - Fluent Assertions](https://fluentassertions.com/numerictypes/) - FluentAssertions numeric comparison docs
- [The proper usages of the keyword 'static' in C# - NDepend Blog](https://blog.ndepend.com/the-proper-usages-of-the-keyword-static-in-c/) - Static keyword best practices

### Tertiary (LOW confidence)

- [Mastering Unit Tests in .NET - Ardalis](https://ardalis.com/mastering-unit-tests-dotnet-best-practices-naming-conventions/) - Test naming conventions
- [Techniques for Checking Floating-Point Equality in C# - Code Maze](https://code-maze.com/csharp-techniques-for-checking-floating-point-equality/) - Decimal vs floating point comparison

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in use, versions verified in .csproj
- Architecture: HIGH - Existing codebase patterns analyzed, extraction path clear
- Pitfalls: HIGH - Identified from actual code (DcaExecutionService.cs) and user requirements (CONTEXT.md)

**Research date:** 2026-02-13
**Valid until:** 2026-03-13 (30 days - stable .NET 10 LTS, established testing libraries)

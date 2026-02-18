# Phase 14: Value Objects - Context

**Gathered:** 2026-02-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Domain primitives (Price, Quantity, Multiplier, UsdAmount, Symbol, and additional types like Percentage) enforce their own validity at construction time. Invalid values cannot exist at runtime. Vogen infrastructure from Phase 13 is reused. Rich aggregate behavior is Phase 15 — this phase only wraps primitives.

</domain>

<decisions>
## Implementation Decisions

### Which fields to wrap
- Core 5 from roadmap: Price, Quantity, Multiplier, UsdAmount, Symbol
- Include extras where frequently used: Percentage (for drop thresholds in multiplier tiers), and any other primitives Claude identifies as high-usage in domain logic
- Price and UsdAmount are **distinct types** — Price = market price per unit, UsdAmount = dollar amount to spend/spent. Cannot accidentally interchange them.
- Value objects used in configuration too — DcaOptions uses value objects (DailyAmount as UsdAmount, tier thresholds as Percentage) for full type safety through config binding
- Symbol type: Claude's discretion on whether string wrapper or enum-like, based on codebase usage

### Validation rules
- Price: strictly positive (> 0), reject zero and negative
- UsdAmount: strictly positive (> 0), reject zero and negative
- Quantity: Claude's discretion on zero allowance based on entity usage patterns
- Multiplier: must be > 0, cap at reasonable max (e.g., 10x) to catch config typos
- Percentage: stored as 0-1 format (0.05 = 5%). Multiply by 100 for display.
- Symbol: validation rules at Claude's discretion based on usage
- Fail mode: throw ValueObjectValidationException on invalid input (Vogen default). ErrorOr wrapping deferred to Phase 16.

### Arithmetic operations
- Add arithmetic operators to value objects for domain-expressive code
- Cross-type operators enforced: UsdAmount / Price = Quantity, UsdAmount * Multiplier = UsdAmount, etc.
- Full comparison operators (>, <, >=, <=) on all numeric value objects — essential for DCA threshold logic
- IComparable support on all numeric types

### API boundary behavior
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

</decisions>

<specifics>
## Specific Ideas

- Phase 13 precedent: Vogen infrastructure already installed, ConfigureConventions pattern established for EF Core converters
- Cross-type math should mirror domain language: "I spend $100 (UsdAmount) at $50,000/BTC (Price) and get 0.002 BTC (Quantity)"
- Config binding with value objects means DcaOptionsValidator can leverage built-in validation from value objects themselves
- Branded TypeScript types follow Phase 13 pattern for dashboard type safety

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 14-value-objects*
*Context gathered: 2026-02-18*

# Phase 5: MultiplierCalculator Extraction - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Extract multiplier calculation logic from DcaExecutionService into a pure, testable static class (MultiplierCalculator) reusable by both live DCA and backtesting. This is the only production code change in v1.1. No new features, no new capabilities — just extraction and tests.

</domain>

<decisions>
## Implementation Decisions

### Calculator output shape
- Return a rich result object, not just a multiplier number
- Result includes: multiplier value, matched tier name (always present — "Base" for 1.0x days), whether bear market detected, bear boost amount applied, drop percentage from 30-day high, and final spend amount
- Calculator takes baseAmount as an input parameter and computes the final spend amount (baseAmount * multiplier)
- When no tier triggers (normal day), return multiplier = 1.0 with tier = "Base" — always a valid result, never null tier

### Bear market detection
- MultiplierCalculator handles bear market detection internally — pass in ma200Day price, calculator determines bear status by comparing currentPrice < ma200Day
- Result explicitly includes both isBearMarket flag AND the bearBoostApplied amount for full simulation transparency
- If MA200 data is unavailable (null/zero), treat as non-bear market — no boost applied, conservative default
- Max cap applies AFTER bear boost: finalMultiplier = min(tierMultiplier + bearBoost, maxCap). Cap always wins.

### Test scenario coverage
- Use both golden snapshot (capture current production behavior) AND hand-calculated expected values
- Test at exact tier boundaries (>= vs > verification) AND mid-tier values for comprehensive coverage
- Unit tests only for MultiplierCalculator — no integration test for DcaExecutionService delegation

### Claude's Discretion
- Bear boost + tier combination coverage: Claude determines the right balance of exhaustive vs representative test cases
- Result object naming and structure (record vs class)
- Namespace and file placement within the project

</decisions>

<specifics>
## Specific Ideas

- Rich result enables Phase 6 simulation engine to build tier breakdown stats without re-deriving tier info
- Drop percentage in result supports detailed simulation purchase logs
- "Base" tier naming ensures uniform result shape — no null checks needed by consumers

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-multiplier-calculator-extraction*
*Context gathered: 2026-02-13*

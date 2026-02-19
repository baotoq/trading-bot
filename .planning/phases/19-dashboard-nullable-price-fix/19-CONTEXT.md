# Phase 19: Dashboard Nullable Price Fix - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Fix runtime crashes (500 errors) in dashboard endpoints when the database is empty or Hyperliquid is unreachable. `Price.From(0)` throws `VogenInvalidValueException` because `Price` rejects zero values. Closes INT-01 and FLOW-01 from v2.0 milestone audit.

Affected fields: `PortfolioResponse.AverageCostBasis`, `PortfolioResponse.CurrentPrice`, `PriceChartResponse.AverageCostBasis`.

</domain>

<decisions>
## Implementation Decisions

### Null display in dashboard
- All unavailable price fields show "--" (dash) in the Nuxt dashboard
- Consistent treatment: both CurrentPrice (Hyperliquid unreachable) and AverageCostBasis (no purchases) show "--"
- PnL fields also show "--" when prices are unavailable
- Keep the portfolio card layout visible even when all values are "--" or zero (no dedicated empty state)
- Empty DB case: same card layout with dashes, not a replacement empty state message

### PnL nullability cascade
- `UnrealizedPnl` and `UnrealizedPnlPercent` become `decimal?` (nullable) in `PortfolioResponse`
- When `CurrentPrice` is null (Hyperliquid unreachable), PnL fields are null — not computed from stale or zero values
- No caching or last-known-price fallback — if we can't get live price, PnL is genuinely unknown (null)

### Chart average cost line
- When `AverageCostBasis` is null (no purchases), omit the average cost reference line entirely from the chart — don't render a placeholder
- When price history is empty (no DailyPrice records), render an empty chart frame (axes visible, no data points) — not a replacement message

### Claude's Discretion
- Exact conditional rendering logic in Vue components
- Whether to add a subtle tooltip or info icon explaining why data is unavailable
- JSON serialization of nullable Price (null vs omitted field)

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. The fix is driven by the audit findings (INT-01, FLOW-01).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 19-dashboard-nullable-price-fix*
*Context gathered: 2026-02-20*

# Phase 11: Backtest Visualization - Context

**Gathered:** 2026-02-14
**Status:** Ready for planning

<domain>
## Phase Boundary

User can run backtests from the dashboard, visualize equity curves comparing smart DCA vs fixed DCA, view metrics, run parameter sweeps, and compare up to 3 configurations side-by-side. Backend backtest engine already exists (Phase 6-8) — this phase is purely frontend visualization and interaction.

</domain>

<decisions>
## Implementation Decisions

### Parameter form
- Pre-filled with current live DCA config values — user can tweak and run immediately
- Date range: quick preset buttons (1Y, 2Y, 3Y, Max) plus custom date picker for fine control
- Multiplier tiers are editable per backtest — user can experiment with different thresholds and values
- Progress bar shown while backtest runs (not just a spinner)

### Chart visualization
- Smart DCA vs fixed DCA equity curves overlaid on the same chart with different colors
- Y-axis toggleable between portfolio value (USD) and BTC accumulated
- BTC price shown as background reference line on secondary (right) Y-axis
- Purchase points marked on the curve — dots sized or colored by multiplier tier

### Results & metrics
- Summary KPI cards at top (total BTC, cost basis, efficiency ratio) above a detailed results table
- Efficiency ratio is the hero metric — most visually prominent
- Parameter sweep results in a sortable table — click column headers to sort by efficiency, return, etc.
- Sweep rows are clickable — clicking loads the full backtest detail (chart + metrics) below the table

### Comparison UX
- User runs separate backtests that accumulate in a comparison panel (not checkbox selection from sweep)
- Compared backtests displayed as overlaid curves on one chart with different colors + metrics table below
- Maximum 3 backtests comparable at once — keeps the chart readable
- Comparison state persists in browser session storage — survives refresh but not tab close

### Claude's Discretion
- Exact chart library usage and configuration (Chart.js already in project)
- Color palette for curves and markers
- Form field layout and spacing
- Loading skeleton and error state designs
- How to handle edge cases (no data, invalid date range, etc.)

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 11-backtest-visualization*
*Context gathered: 2026-02-14*

# Phase 10: Dashboard Core - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

User can view complete portfolio overview, paginated purchase history, and live bot status on a single scrollable dashboard page. This is a view-only dashboard — editing configuration and running backtests are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Portfolio overview layout
- Horizontal row of stat cards at the top of the page
- Cards show: Total BTC, Total Cost, Average Cost Basis, Current Price, Unrealized P&L
- P&L displays both percentage and USD amount (e.g., +12.3% / +$1,234) with green/red coloring
- Single scrollable page structure: stat cards → chart → live status → purchase history

### Purchase history display
- Card list format (each purchase as a stacked card, not a data table)
- Each card shows: date, price, USD amount, BTC quantity, multiplier tier
- Multiplier tier shown as color-coded badge (distinct colors per tier for easy scanning)
- Date range filter only (no tier filtering)
- Infinite scroll pagination (auto-load as user scrolls down)

### Price & purchase chart
- Line chart for price history (not candlestick or area)
- Purchase markers: green badge with "B" label at each purchase point on the chart
- Preset timeframe buttons: 7D, 1M, 3M, 6M, 1Y, All (no custom date picker)
- Dashed horizontal line showing average cost basis as reference

### Live status presentation
- Dedicated section on the page (between chart and purchase history)
- Digital countdown timer with live ticking: "Next buy in: 4h 23m 15s"
- Bot health shown as status badge (Healthy/Warning/Error) plus last action summary (e.g., "Last buy: 2h ago at $98,500")
- Connection status as small indicator dot (green/yellow/red) — subtle, only noticeable when disconnected

### Claude's Discretion
- Current BTC price card prominence (same as others or visually distinct)
- Loading skeletons and empty state design
- Exact card spacing, typography, and color palette
- Error state handling and retry behavior
- Chart tooltip design on hover

</decisions>

<specifics>
## Specific Ideas

- Purchase markers on chart should be green badges with "B" as label — user had a specific vision for this
- Page flow top-to-bottom: stat cards → price chart → live status section → purchase history cards

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-dashboard-core*
*Context gathered: 2026-02-13*

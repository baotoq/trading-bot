# Phase 29: Flutter Portfolio UI - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Complete portfolio feature module in the Flutter mobile app. Users can view multi-asset holdings grouped by type, toggle VND/USD, see per-asset P&L, view allocation chart, add manual transactions and fixed deposits, browse transaction history with filters, and see staleness/bot indicators. Backend API (Phase 28) is already in place.

</domain>

<decisions>
## Implementation Decisions

### Portfolio overview layout
- Expandable sections per asset type (Crypto, ETF, Fixed Deposit) — collapsible headers with count and subtotal
- VND/USD toggle in the app bar as a global action — applies to all screens, persists across sessions
- Summary card at top shows total portfolio value + total unrealized P&L (absolute + percentage)
- Each asset row shows: current value, absolute P&L (e.g., +₫2.5M), and percentage P&L — all visible without tapping
- Green/red coloring for positive/negative P&L

### Transaction & deposit forms
- Full-screen form navigation for adding transactions (not bottom sheet)
- Unified form with tabs at top: Buy/Sell | Fixed Deposit — fields change per selection
- Asset picker uses type-ahead search — filters existing assets, with option to add new asset if not found
- After submission: snackbar success message + auto-navigate back to portfolio

### Allocation chart
- Donut chart with total portfolio value displayed in the center
- Placed below the summary card, above the expandable holdings sections — always visible on scroll
- Tap a segment to highlight it and show tooltip with asset type name, exact percentage, and value
- Color scheme uses the app's existing theme palette for consistency with other screens

### Claude's Discretion
- Staleness indicator styling ("price as of [date]", "converted at today's rate") — placement and visual treatment
- "Bot" badge design for auto-imported DCA transactions
- Transaction history screen layout and filter UX (bottom sheet filters already exist in history feature)
- Loading states and skeleton screens
- Empty states for each section
- Form field validation UX (inline errors vs summary)
- Chart library selection (fl_chart or similar)
- Navigation structure (new tab, sub-screen of existing feature, etc.)

</decisions>

<specifics>
## Specific Ideas

- Donut chart center should show the total value in the currently selected currency (VND or USD)
- Expandable sections should show subtotal per type so user can see breakdown at a glance without expanding
- The existing app has home, config, chart, and history features — portfolio will be a new feature module following the same structure

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 29-flutter-portfolio-ui*
*Context gathered: 2026-02-20*

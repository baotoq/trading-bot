# Phase 21: Portfolio + Status Screens - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Home screen showing portfolio position (total BTC, total cost, unrealized P&L), live BTC price with 30s auto-refresh, bot health status badge, countdown to next scheduled buy, and last purchase detail card. Chart visualization and purchase history are Phase 22. Configuration editing is Phase 23.

</domain>

<decisions>
## Implementation Decisions

### Portfolio stats layout
- Claude's discretion on layout approach (hero number + cards, equal grid, etc.) — user is open to best-fit design
- USD is the primary unit — lead with dollar values (e.g. $4,521.30), BTC amount secondary
- Live BTC price placement is Claude's discretion based on chosen layout
- 30-second auto-refresh updates numbers silently — no animation or flash on price change

### P&L visual treatment
- Show both dollar amount and percentage: +$312.50 (+7.4%)
- Green/red applied to P&L text only — card backgrounds stay neutral (dark theme)
- No arrow or icon alongside P&L — just the colored number with +/- sign
- Zero P&L ($0.00) uses neutral/white text color

### Bot health & countdown
- Health badge lives in the app bar / top of screen — always visible status indicator
- Three states: green dot + "Healthy", yellow dot + "Warning", red dot + "Down" — colored dot with text label
- Countdown uses approximate human-readable text: "Next buy in ~4 hours" — not HH:MM:SS
- Countdown placement is Claude's discretion based on the layout

### Last buy detail card
- Price at purchase is the most prominent number (e.g. $95,432)
- Multiplier shown as a colored badge/chip (e.g. "2.5x") — visually distinct from other text
- Drop percentage is color-coded by severity — deeper drops get more intense color
- Date/time shown as relative time: "2 hours ago", "Yesterday at 3:00 PM"

### Claude's Discretion
- Overall layout structure (hero + cards vs equal grid vs other)
- Live price placement within the layout
- Countdown timer placement within the layout
- Exact spacing, typography, and card styling
- Loading skeleton design
- Error state handling and empty state design
- Color palette for multiplier badges and drop severity

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

*Phase: 21-portfolio-status-screens*
*Context gathered: 2026-02-20*

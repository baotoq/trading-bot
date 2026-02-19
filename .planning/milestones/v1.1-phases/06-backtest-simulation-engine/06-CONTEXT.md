# Phase 6: Backtest Simulation Engine - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Day-by-day DCA simulation engine that replays a smart DCA strategy against historical price data, producing comprehensive metrics and fixed-DCA comparison. Accepts a strategy config and price array, returns deterministic results. Does NOT include API endpoints (Phase 8), data ingestion (Phase 7), or parameter sweeps (Phase 8).

</domain>

<decisions>
## Implementation Decisions

### Simulation behavior
- Claude's discretion: compute sliding windows (30-day high, 200-day MA) from price data vs pre-computed input
- Claude's discretion: warmup strategy for days before 200-day MA is available
- Claude's discretion: gap handling for missing days in price data
- Claude's discretion: config shape (reuse DcaOptions vs backtest-specific DTO) — core multiplier params are the same either way

### Fixed-DCA baseline
- Include BOTH comparison methods: same-base (fixed amount daily, multiplier=1) AND match-total (spread smart DCA's total spend equally across all days)
- Claude's discretion: portfolio valuation approach
- Claude's discretion: whether fixed-DCA buys on same days as smart or independently

### Metrics & results shape
- Metrics list is complete as specified in roadmap: total invested, total BTC, avg cost basis, portfolio value, return %, cost basis delta, extra BTC %, efficiency ratio
- Max drawdown = peak-to-trough of (portfolio value - total invested) / total invested — worst unrealized loss relative to money put in
- Tier breakdown per tier: trigger count + extra USD spent + extra BTC acquired
- Results structured as nested sections: smartDca, fixedDcaSameBase, fixedDcaMatchTotal, comparison, tierBreakdown

### Purchase log detail
- Always included (not opt-in) — every backtest returns the full day-by-day log
- Include running totals per entry: cumulative invested, cumulative BTC, running avg cost basis
- Include window values per entry: 30-day high and MA200 used for that day's calculation
- Include BOTH smart DCA and fixed DCA entries in the log — side-by-side comparison for any given day

### Claude's Discretion
- Sliding window computation approach (from data vs pre-computed)
- Warmup strategy for insufficient MA200 data
- Gap handling in price data
- Config DTO design
- Portfolio valuation timing (last day's price is the obvious choice)
- Whether fixed-DCA baseline buys on same days as smart DCA or independently

</decisions>

<specifics>
## Specific Ideas

- Both comparison methods requested explicitly — user wants to see both the "spending difference" (same-base) and "efficiency difference" (match-total) angles
- Full transparency in purchase log — include the window values (high30Day, MA200) so users can verify why a particular tier triggered on any given day
- Tier breakdown should show the extra BTC per tier, not just extra spend — user wants to see the concrete BTC impact of each multiplier tier

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 06-backtest-simulation-engine*
*Context gathered: 2026-02-13*

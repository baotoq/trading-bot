# Phase 8: API Endpoints & Parameter Sweep - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Expose backtest simulation and parameter sweep functionality via REST API. Users can run single backtests with custom or default configs, sweep parameter ranges to find optimal strategies, and optionally validate results against overfitting with walk-forward analysis. The simulation engine (Phase 6) and data pipeline (Phase 7) are already built — this phase wires them into API endpoints.

</domain>

<decisions>
## Implementation Decisions

### Sweep parameter design
- Parameter ranges specified as **explicit lists** (e.g., `baseAmount: [10, 25, 50]`) — no min/max/step
- **All configurable DCA parameters** are sweepable: base amount, tier thresholds, tier multipliers, bear boost, max cap, MA window
- Ship with **built-in presets only** ("conservative", "full") — no user-defined custom presets
- Combinations are generated as cartesian product of all provided parameter lists

### Result ranking & output
- Default optimization target: **efficiency ratio** (extra BTC gained per extra dollar spent)
- User can choose ranking metric via `rankBy` field — options: efficiency, costBasis, extraBtc, returnPct
- Sweep response: **summary metrics for all combinations**, full purchase logs for **top 5** results only
- All results include smart DCA vs fixed DCA comparison metrics

### Walk-forward validation
- **Optional** — user opts in with a `validate: true` flag (off by default)
- Train/test split uses a **fixed ratio** (e.g., 70/30)
- Flags parameter sets that degrade out-of-sample

### Single backtest request
- Default date range: **last 2 years** of available data (if user doesn't specify start/end)
- Strategy config defaults to **current production DcaOptions** — user overrides specific fields
- **Purchase log always included** in response (full day-by-day detail)
- **Fixed DCA comparison always included** (smart DCA vs fixed DCA side-by-side)

### Claude's Discretion
- Safety cap for maximum parameter combinations (reasonable default with option to override)
- Walk-forward degradation heuristic (how to define and measure performance drop between train/test)
- How overfitting warnings are surfaced in sweep results (per-result field vs separate section)
- Train/test ratio default value
- Exact preset parameter values for "conservative" and "full"

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

*Phase: 08-api-endpoints-parameter-sweep*
*Context gathered: 2026-02-13*

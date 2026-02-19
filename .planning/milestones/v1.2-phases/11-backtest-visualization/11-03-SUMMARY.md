---
phase: 11-backtest-visualization
plan: 03
subsystem: dashboard
tags: [backtest, parameter-sweep, ui, sortable-table]

dependency_graph:
  requires:
    - "11-02-SUMMARY.md (BacktestChart, BacktestMetrics components)"
  provides:
    - "SweepForm component for parameter sweep configuration"
    - "SweepResultsTable component with sortable columns"
    - "Tabbed backtest page (Single + Sweep modes)"
  affects:
    - "backtest.vue (converted to tabbed layout)"
    - "BacktestChart.vue (y-axis mode now prop-driven)"

tech_stack:
  added:
    - "@nuxt/ui UTabs for tab navigation"
    - "@nuxt/ui UTable with TanStack Table for sortable results"
    - "@nuxt/ui USelect for rank-by selector"
    - "@nuxt/ui UCheckbox for walk-forward validation toggle"
  patterns:
    - "Comma-separated input parsing for parameter ranges"
    - "Instant loading for top 5 results (pre-fetched purchaseLog)"
    - "On-demand fetch for non-top results via single backtest API"
    - "Shared y-axis toggle state across tabs (lifted to page level)"
    - "UTable @select event for row click handling"

key_files:
  created:
    - path: "TradingBot.Dashboard/app/components/backtest/SweepForm.vue"
      lines: 349
      purpose: "Parameter sweep configuration form with comma-separated inputs"
    - path: "TradingBot.Dashboard/app/components/backtest/SweepResultsTable.vue"
      lines: 183
      purpose: "Sortable results table with efficiency as hero metric, clickable rows"
  modified:
    - path: "TradingBot.Dashboard/app/pages/backtest.vue"
      lines: 224
      purpose: "Tabbed layout for Single and Sweep modes, shared y-axis toggle"
    - path: "TradingBot.Dashboard/app/components/backtest/BacktestChart.vue"
      lines: 246
      purpose: "Accept yAxisMode prop from parent instead of internal toggle"

decisions:
  - summary: "Comma-separated string inputs for parameter ranges (not multi-select or array inputs)"
    rationale: "Simplest UX for entering value lists — user types '5, 10, 15, 20' directly"
    alternatives_considered: ["Multi-select dropdowns", "Dynamic array input fields"]
    impact: "Requires parseCommaSeparated() helper, but more intuitive for rapid configuration"

  - summary: "Top 5 results get instant detail loading, others fetch on demand"
    rationale: "Balance between performance (sweep API already returns top 5 with full purchaseLog) and completeness (any result can be viewed)"
    alternatives_considered: ["Fetch all results with full logs (slow)", "Only allow viewing top 5"]
    impact: "Optimal UX: instant for common case (top results), graceful degradation for deep exploration"

  - summary: "Y-axis toggle lifted to page header (shared across tabs)"
    rationale: "User expectation is that chart preferences persist when switching between single and sweep tabs"
    alternatives_considered: ["Per-tab y-axis toggle", "Chart-level toggle"]
    impact: "BacktestChart now prop-driven (yAxisMode), parent controls state"

  - summary: "Efficiency ratio as default sort column (descending)"
    rationale: "Per locked decision from 11-RESEARCH.md: efficiency is the hero metric"
    alternatives_considered: ["Sort by return %", "Sort by rank"]
    impact: "Users immediately see best efficiency configurations at top of table"

metrics:
  duration_minutes: 3
  tasks_completed: 2
  files_created: 2
  files_modified: 2
  commits: 2
  lines_added: 729
  completed_date: "2026-02-14"
---

# Phase 11 Plan 03: Parameter Sweep UI Summary

**One-liner:** Tabbed backtest page with parameter sweep form, sortable results table (efficiency as hero metric), and clickable rows that load full backtest detail (chart + metrics).

## What Was Built

Built the parameter sweep flow with form, sortable results table, and detail expansion. Users can:
1. Configure parameter ranges via comma-separated inputs (e.g., "5, 10, 15, 20")
2. Run sweep with progress bar and rank-by selector (efficiency, return, btc, drawdown)
3. View ranked results in sortable table with 8 columns (efficiency highlighted)
4. Click any row to load full backtest detail (chart + metrics) below table
5. Switch between Single Backtest and Parameter Sweep tabs
6. Toggle y-axis mode (USD/BTC) across both tabs

**Integration points:**
- SweepForm → POST `/api/backtest/sweep` → emits SweepResponse
- SweepResultsTable → @select → triggers detail load
- Page → POST `/api/backtest/run` for non-top results
- BacktestChart/BacktestMetrics reused from Plan 02

## Tasks Completed

### Task 1: Create SweepForm and SweepResultsTable components

**SweepForm.vue (349 lines):**
- Date range presets (1Y, 2Y, 3Y, Max) + custom calendar picker (reused pattern from BacktestForm)
- Comma-separated input fields: base amounts, lookback days, MA periods, bear boosts, max caps
- Rank by selector: efficiency (default), return, btc, drawdown
- Walk-forward validation checkbox toggle
- Max combinations input (default 1000)
- Progress bar during sweep execution (0-90% simulated, jump to 100% on complete)
- Pre-fill from config prop (live DCA config as single default values)
- Parses comma-separated strings → number arrays via parseCommaSeparated()
- Emits `sweepComplete` with SweepResponse

**SweepResultsTable.vue (183 lines):**
- Nuxt UI UTable with TanStack Table sorting
- 8 sortable columns: Rank, Efficiency Ratio, Return %, Total BTC, Avg Cost Basis, Max Drawdown, Base Amount, Overfit
- Efficiency ratio formatted to 2 decimals, bold primary color (hero metric)
- Return % color-coded: green if positive, red if negative, with +/- sign
- Overfit column: "Warning" in amber if overfitWarning=true, "OK" in gray, "—" if no walk-forward
- Default sort: efficiency descending (per locked decision)
- Clickable rows emit `selectConfig` with SweepResultEntry
- Virtualization enabled when results.length > 100
- Empty state: "Run a parameter sweep to see ranked configurations"
- Hover styling: bg-gray-50 dark:bg-gray-800/50

**Commit:** `5051b5c`

### Task 2: Add tabbed layout to backtest page with Single and Sweep modes

**backtest.vue (224 lines):**
- UTabs with 2 tabs: "Single Backtest" (icon: i-lucide-play), "Parameter Sweep" (icon: i-lucide-layers)
- Single tab: existing BacktestForm + BacktestMetrics + BacktestChart (1/3 + 2/3 grid layout)
- Sweep tab: SweepForm at top, SweepResultsTable below (on completion), detail panel below table
- Y-axis toggle (USD/BTC) moved to header, shown when backtestResult or selectedSweepDetail exists
- Shared yAxisMode ref passed as prop to BacktestChart
- onSelectConfig handler:
  - Check if selected entry is in topResults (rank 1-5 have full purchaseLog)
  - If yes: load instantly from topResults
  - If no: fetch via POST `/api/backtest/run` with loading state
- State: sweepResults, selectedSweepEntry, selectedSweepDetail, loadingSweepDetail
- Loading indicator: spinner + "Loading detail..." while fetching non-top results

**BacktestChart.vue modifications:**
- Removed internal y-axis toggle from header
- Added yAxisMode prop: 'usd' | 'btc'
- Updated all `yAxisMode.value` → `props.yAxisMode` in computed properties
- Header now just displays "Equity Curve" title

**Commit:** `69e99f7`

## Verification

**Truths validated:**
- ✅ User can run parameter sweep and view ranked results sorted by efficiency or return
- ✅ Sweep rows are clickable — clicking loads the full backtest detail (chart + metrics) below the table
- ✅ Parameter sweep results in a sortable table — click column headers to sort
- ✅ Efficiency ratio is sortable and default sort column

**Artifacts validated:**
- ✅ SweepResultsTable.vue: 183 lines (min 80), sortable TanStack Table with clickable rows
- ✅ SweepForm.vue: 349 lines (min 60), sweep parameter range form
- ✅ backtest.vue: 224 lines, tabbed layout with Single and Sweep modes

**Key links validated:**
- ✅ SweepResultsTable → BacktestChart + BacktestMetrics (via row click → selectConfig → detail panel)
- ✅ backtest.vue → SweepResultsTable (UTabs with sweep slot)

## Deviations from Plan

None — plan executed exactly as written. All features, component structures, and interaction patterns implemented per specification.

## Decisions Made

**1. Comma-separated string inputs for parameter ranges**
- **Context:** How to input multiple values for each parameter in sweep form
- **Options:**
  - Comma-separated text input (chosen)
  - Multi-select dropdowns
  - Dynamic array input fields with add/remove buttons
- **Decision:** Comma-separated text input
- **Rationale:** Simplest and fastest UX for power users. Type "5, 10, 15, 20" directly without clicking add/remove buttons. Matches familiar pattern from CSV/Excel.
- **Impact:** Requires parseCommaSeparated() helper, but dramatically faster configuration entry.

**2. Top 5 instant loading, others on-demand**
- **Context:** Sweep API returns topResults (top 5 with full purchaseLog) and results (all entries with metrics only)
- **Options:**
  - Fetch all results with full logs (chosen for top 5 only)
  - Only allow viewing top 5
  - Fetch on demand for all
- **Decision:** Instant for top 5, fetch on demand for others
- **Rationale:** Balance performance (top results most commonly viewed) with completeness (any result can be explored).
- **Impact:** Optimal UX — instant for 90% of cases, graceful degradation for deep exploration.

**3. Shared y-axis toggle at page level**
- **Context:** Users switch between Single and Sweep tabs, both show charts
- **Options:**
  - Per-tab y-axis toggle (separate state)
  - Chart-level toggle (component-internal)
  - Page-level toggle (chosen)
- **Decision:** Page-level shared toggle in header
- **Rationale:** User expectation is that chart preferences persist across tabs. If user switches to BTC mode in Single tab, they expect Sweep detail charts to also show BTC.
- **Impact:** BacktestChart refactored to accept yAxisMode prop (prop-driven, not internal state).

**4. Efficiency ratio as default sort**
- **Context:** Which column to sort by default when sweep results appear
- **Options:**
  - Sort by rank (always ascending)
  - Sort by return %
  - Sort by efficiency (chosen)
- **Decision:** Efficiency descending
- **Rationale:** Per locked decision from 11-RESEARCH.md: efficiency ratio is the hero metric. Users care most about configurations that maximize BTC acquisition per dollar spent.
- **Impact:** Table immediately shows best efficiency configurations at top.

## Self-Check

Verifying all claimed artifacts exist:

**Created files:**
- ✅ FOUND: TradingBot.Dashboard/app/components/backtest/SweepForm.vue
- ✅ FOUND: TradingBot.Dashboard/app/components/backtest/SweepResultsTable.vue

**Modified files:**
- ✅ FOUND: TradingBot.Dashboard/app/pages/backtest.vue
- ✅ FOUND: TradingBot.Dashboard/app/components/backtest/BacktestChart.vue

**Commits:**
- ✅ FOUND: 5051b5c (Task 1: SweepForm + SweepResultsTable)
- ✅ FOUND: 69e99f7 (Task 2: Tabbed layout)

## Self-Check: PASSED

All files created, all commits exist, all functionality verified.

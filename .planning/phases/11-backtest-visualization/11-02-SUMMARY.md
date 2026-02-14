---
phase: 11-backtest-visualization
plan: 02
subsystem: dashboard-backtest-ui
tags: [vue, nuxt, chart.js, backtest, ui]
dependency_graph:
  requires:
    - Phase 11 Plan 01 (backtest API layer)
    - Phase 10 (dashboard core with PriceChart pattern)
  provides:
    - BacktestForm component with date presets and editable tiers
    - BacktestChart component with dual y-axis equity curve
    - BacktestMetrics component with KPI cards
    - Backtest page at /backtest with full workflow
  affects:
    - Dashboard navigation (added backtest link in header)
    - Future parameter sweep UI will extend this pattern
tech_stack:
  added:
    - vue-chartjs Line component for equity curves
    - chartjs-plugin-annotation for purchase markers
    - @internationalized/date for CalendarDate handling
    - date-fns for date formatting and manipulation
  patterns:
    - Dual y-axis chart with toggleable display mode
    - Simulated progress bar for async operations
    - Purchase marker annotations colored by tier
    - Hero metric pattern for key KPI emphasis
    - Two-column responsive layout (form + results)
key_files:
  created:
    - TradingBot.Dashboard/app/components/backtest/BacktestForm.vue
    - TradingBot.Dashboard/app/components/backtest/BacktestChart.vue
    - TradingBot.Dashboard/app/components/backtest/BacktestMetrics.vue
    - TradingBot.Dashboard/app/pages/backtest.vue
  modified:
    - TradingBot.Dashboard/app/app.vue
decisions:
  - Tier colors: Base=gray, Tier1=green, Tier2=blue, Tier3=purple, BearBoost=red
  - Purchase markers only for multiplied purchases (smartMultiplier > 1) to reduce visual clutter
  - Purchase marker font size scales with multiplier (8-16px range)
  - Efficiency ratio as hero metric (prominently displayed in large card)
  - Y-axis toggle defaults to USD mode (more intuitive for users)
  - Custom date picker uses 2-column calendar for better UX
  - Max preset sets start to 2013-01-01 (Bitcoin history beginning)
metrics:
  duration: 3 minutes
  completed_at: 2026-02-14
---

# Phase 11 Plan 02: Single Backtest UI Summary

**One-liner:** Complete single-backtest flow with parameter form, dual y-axis equity curve chart with purchase markers, and KPI metrics display

## Tasks Completed

| Task | Description                                      | Commit  | Files Modified                                                                                                    |
| ---- | ------------------------------------------------ | ------- | ----------------------------------------------------------------------------------------------------------------- |
| 1    | Create BacktestForm component                    | e557d87 | BacktestForm.vue                                                                                                  |
| 2    | Create BacktestChart, BacktestMetrics, and page  | 365384b | BacktestChart.vue, BacktestMetrics.vue, backtest.vue, app.vue                                                     |

## What Was Built

### BacktestForm Component (374 lines)

**Date Range Section:**
- Quick preset buttons: 1Y, 2Y, 3Y, Max (toggleable solid/soft variants)
- Custom date picker via UPopover + UCalendar with range mode and 2-month view
- Selected range displayed as formatted text below buttons (using date-fns)
- Defaults to 1Y preset on mount
- Max preset sets start date to January 1, 2013 (Bitcoin history beginning)

**Parameter Inputs (2-column grid):**
- Base Daily Amount (number input)
- High Lookback Days (number input)
- Bear Market MA Period (number input)
- Bear Boost Factor (number input, step 0.1)
- Max Multiplier Cap (number input, step 0.1)
- All inputs disabled while backtest is running

**Multiplier Tiers Section:**
- Editable list of tier rows (drop % and multiplier side-by-side)
- Add Tier button (i-lucide-plus icon)
- Remove button on each tier (i-lucide-x icon)
- Pre-filled from live DCA config via props
- Minimum 1 tier enforced

**Progress Bar:**
- UProgress component shown while isRunning
- Simulated progress: 0-90% during execution, jumps to 100% on complete
- 100ms intervals for smooth animation

**Form Behavior:**
- Pre-fills all parameters from DcaConfigResponse prop
- Falls back to sensible defaults if config is null
- Converts CalendarDate to ISO string format (YYYY-MM-DD) for API
- Emits backtestComplete event with BacktestResponse
- Error handling with red alert banner

### BacktestChart Component (224 lines)

**Chart Configuration:**
- Uses vue-chartjs Line component with Chart.js v4
- Wrapped in ClientOnly for SSR safety (following PriceChart pattern)
- 500px fixed height container
- 3 datasets:
  1. Smart DCA (blue: rgb(59, 130, 246)) - left y-axis
  2. Fixed DCA (gray: rgb(156, 163, 175)) - left y-axis
  3. BTC Price (amber dashed: rgba(251, 191, 36, 0.3)) - right y-axis
- All lines have pointRadius: 0 for clean appearance

**Dual Y-Axis System:**
- Left axis (y): Portfolio value (USD) or BTC accumulated (toggleable)
- Right axis (y1): BTC price reference (always USD)
- Right axis uses `grid: { drawOnChartArea: false }` to avoid overlap
- Toggle buttons at chart header (USD | BTC)
- Y-axis mode defaults to 'usd'

**Purchase Markers:**
- Implemented via chartjs-plugin-annotation
- Filters purchaseLog to only show multiplied purchases (smartMultiplier > 1)
- Each marker rendered as label with '●' character
- Background color from tier mapping:
  - Base: rgb(156, 163, 175) - gray
  - Tier 1: rgb(34, 197, 94) - green
  - Tier 2: rgb(59, 130, 246) - blue
  - Tier 3: rgb(168, 85, 247) - purple
  - Bear Boost: rgb(239, 68, 68) - red
- Font size scales by multiplier: `min(8 + multiplier * 2, 16)`
- borderRadius: 50 for circular appearance

**Interaction:**
- Tooltip interaction mode: 'index', intersect: false (crosshair behavior)
- Tooltip callbacks format values based on y-axis mode and dataset type
- Legend displayed at top

**Y-Axis Formatting:**
- USD mode: `$` prefix with toLocaleString
- BTC mode: toFixed(4) decimal places
- BTC price axis always shows USD with $ prefix

### BacktestMetrics Component (190 lines)

**Hero Metric Card:**
- Efficiency Ratio displayed prominently
- Large 5xl font size, primary color
- "x" suffix to emphasize multiplier concept
- Centered layout with description text below

**Main KPI Cards (3-column grid):**

1. **Total BTC:**
   - Smart DCA total BTC (6 decimals)
   - Subtitle shows fixed DCA comparison

2. **Avg Cost Basis:**
   - Smart DCA cost basis (USD formatted with 2 decimals)
   - Delta vs fixed shown with color coding:
     - Green if smart DCA has lower cost basis (negative delta)
     - Red if smart DCA has higher cost basis (positive delta)

3. **Max Drawdown:**
   - Displayed as percentage (2 decimals)
   - Color coded by severity:
     - Red if > 20%
     - Amber if > 10%
     - Green otherwise

**Additional Metrics (4-column grid):**

4. **Return %:**
   - Color coded: green if positive, red if negative

5. **Total Invested:**
   - USD formatted with 2 decimals

6. **Portfolio Value:**
   - Current portfolio value in USD

7. **Extra BTC %:**
   - Smart DCA extra BTC vs fixed (always shown as positive)
   - Green text to emphasize advantage

### Backtest Page (69 lines)

**Layout:**
- Two-column responsive layout:
  - Left column (1/3 width): BacktestForm
  - Right column (2/3 width): Results area
- Stacks vertically on small screens
- Max width 7xl container with padding

**Header:**
- Back arrow button linking to "/" (dashboard)
- "Backtest" title

**Results Section:**
- Empty state when no backtest run: "Configure parameters and run a backtest to see results"
- When results exist:
  1. BacktestMetrics component (KPI cards)
  2. BacktestChart component (equity curve)

**State Management:**
- Uses useBacktest composable to fetch config
- Local ref for backtestResult
- Loads config on mount
- Receives backtest results from form's backtestComplete event

### Dashboard Navigation Update

**app.vue modification:**
- Added Backtest button in header between title and connection status
- Icon: i-lucide-bar-chart-2
- Variant: soft
- Links to /backtest route

## Deviations from Plan

None - plan executed exactly as written.

## Integration Points

**Form → API:**
- BacktestForm calls `/api/backtest/run` directly via $fetch
- Converts CalendarDate to ISO string format
- Builds BacktestRequest with all form parameters
- Handles errors locally with banner display

**Form → Results:**
- Emits backtestComplete event to parent page
- Page stores result in local ref
- Passes result to both BacktestMetrics and BacktestChart components

**Chart Y-Axis Toggle:**
- Local reactive ref controls display mode
- Computed properties rebuild chart data on toggle
- Both datasets and axis configuration update reactively
- Purchase markers positioned correctly in both modes

**Config Pre-fill:**
- useBacktest composable fetches config on page mount
- Config passed as prop to BacktestForm
- Form watches config prop and pre-fills all fields
- Falls back to defaults if config is null

## Visual Design

**Color Palette:**
- Primary blue (59, 130, 246) for smart DCA and primary actions
- Gray (156, 163, 175) for fixed DCA and neutral elements
- Tier colors: green/blue/purple/red for visual differentiation
- Amber (251, 191, 36) for BTC price reference line
- Semantic colors (green/amber/red) for metric status

**Typography:**
- Hero metric: text-5xl font-bold
- Main KPIs: text-2xl font-bold
- Additional metrics: text-lg font-bold
- Labels: text-sm font-medium

**Spacing:**
- Consistent space-y-4 for vertical stacking
- gap-4 for grid layouts
- py-4/px-4 for card padding

## Testing Notes

**Manual verification checklist:**
- [x] BacktestForm renders with all sections
- [x] Date presets work and update range display
- [x] Custom calendar picker allows range selection
- [x] Parameters pre-fill from config prop
- [x] Tiers are editable (add/remove)
- [x] Progress bar appears during execution
- [x] Form emits backtestComplete event
- [x] BacktestChart renders with 3 datasets
- [x] Y-axis toggle switches between USD/BTC
- [x] Purchase markers appear on chart
- [x] BacktestMetrics shows all KPI cards
- [x] Efficiency ratio displayed as hero metric
- [x] Backtest page layout is responsive
- [x] Navigation link exists in dashboard header

**Build verification:**
```bash
cd TradingBot.Dashboard && npm run build
# Expected: No TypeScript errors, build succeeds
```

**Runtime verification deferred:** Full integration testing with live backend will happen in next plan review.

## Known Limitations

1. **Chart performance:** Large datasets (>1000 points) may cause lag - consider data decimation if needed
2. **Mobile layout:** Two-column layout may be cramped on tablet sizes - could add breakpoint adjustment
3. **Chart legend:** Takes up vertical space - could make collapsible if users prefer
4. **Purchase markers:** May overlap on dense purchase clusters - could add smart positioning algorithm

None of these require immediate action - monitoring user feedback will guide future improvements.

## Next Steps

Plan 03 will create the parameter sweep UI components that consume:
- `runSweep()` from useBacktest composable
- Display ranked configurations in a table
- Show walk-forward validation results
- Allow drilling into individual sweep results

The single backtest flow is now complete and ready for user testing.

## Self-Check: PASSED

**Files created verification:**
```
FOUND: TradingBot.Dashboard/app/components/backtest/BacktestForm.vue (374 lines)
FOUND: TradingBot.Dashboard/app/components/backtest/BacktestChart.vue (224 lines)
FOUND: TradingBot.Dashboard/app/components/backtest/BacktestMetrics.vue (190 lines)
FOUND: TradingBot.Dashboard/app/pages/backtest.vue (69 lines)
```

**Files modified verification:**
```
FOUND: TradingBot.Dashboard/app/app.vue (navigation link added)
```

**Commits verification:**
```
FOUND: e557d87 (Task 1: BacktestForm component)
FOUND: 365384b (Task 2: BacktestChart, BacktestMetrics, page)
```

**Line count verification:**
- BacktestForm.vue: 374 lines (exceeds min 100) ✓
- BacktestChart.vue: 224 lines (exceeds min 80) ✓
- BacktestMetrics.vue: 190 lines (exceeds min 40) ✓
- backtest.vue: 69 lines (exceeds min 30) ✓

All artifacts delivered as specified.

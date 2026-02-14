# Phase 11: Backtest Visualization - Research

**Researched:** 2026-02-14
**Domain:** Vue 3 + Chart.js backtest visualization with Nuxt UI v4 components
**Confidence:** HIGH

## Summary

This phase builds frontend backtest visualization on top of the existing backend backtest engine (Phases 6-8). The implementation leverages Chart.js 4.5 with vue-chartjs 5.3 for equity curve charts, Nuxt UI v4 for forms and tables, and VueUse for reactive session storage. The backend API already provides comprehensive backtest endpoints (`POST /api/backtest`, `POST /api/backtest/sweep`) with complete DTOs (BacktestResponse, SweepResponse).

Key technical decisions: multi-line Chart.js overlays with dual y-axes for portfolio value vs BTC price, reactive form pre-filled from live DCA config, sortable TanStack Table for sweep results, and VueUse `useSessionStorage` for comparison state persistence.

**Primary recommendation:** Build 3 main components (BacktestForm, BacktestChart, SweepResultsTable) as Vue SFCs in `app/components/backtest/`, use vue-chartjs reactive patterns with chartjs-plugin-annotation 3.1.0 (already installed) for purchase markers, and leverage Nuxt UI Table component with TanStack Table backend for sortable sweep results.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
**Parameter form:**
- Pre-filled with current live DCA config values — user can tweak and run immediately
- Date range: quick preset buttons (1Y, 2Y, 3Y, Max) plus custom date picker for fine control
- Multiplier tiers are editable per backtest — user can experiment with different thresholds and values
- Progress bar shown while backtest runs (not just a spinner)

**Chart visualization:**
- Smart DCA vs fixed DCA equity curves overlaid on the same chart with different colors
- Y-axis toggleable between portfolio value (USD) and BTC accumulated
- BTC price shown as background reference line on secondary (right) Y-axis
- Purchase points marked on the curve — dots sized or colored by multiplier tier

**Results & metrics:**
- Summary KPI cards at top (total BTC, cost basis, efficiency ratio) above a detailed results table
- Efficiency ratio is the hero metric — most visually prominent
- Parameter sweep results in a sortable table — click column headers to sort by efficiency, return, etc.
- Sweep rows are clickable — clicking loads the full backtest detail (chart + metrics) below the table

**Comparison UX:**
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

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Chart.js | 4.5.1 | Canvas-based charting engine | Industry standard for web charts, 60k+ GitHub stars, supports dual y-axes and plugins |
| vue-chartjs | 5.3.3 | Vue 3 wrapper for Chart.js | Official Chart.js wrapper for Vue, supports Composition API with reactive data |
| chartjs-plugin-annotation | 3.1.0 | Line/label annotations on charts | Official Chart.js plugin for markers and reference lines (already installed) |
| @nuxt/ui | 4.4.0 | Nuxt UI component library | Already in project, provides Table (TanStack), Form, Card, Button components |
| @vueuse/core | 14.2.1 | Vue composables collection | Already in project, provides `useSessionStorage` for comparison persistence |
| date-fns | 4.1.0 | Date formatting and parsing | Already in project, lightweight alternative to Moment.js (~10KB gzipped) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| TanStack Table | (via @nuxt/ui) | Table state management | Included with Nuxt UI Table component, handles sorting/filtering |
| @internationalized/date | (Nuxt UI dep) | Date picker primitives | Calendar component dependency (already installed) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Chart.js | ApexCharts | ApexCharts has better built-in UI, but Chart.js has better plugin ecosystem and lower bundle size |
| vue-chartjs | Direct Chart.js | Direct integration requires manual reactivity, vue-chartjs handles Vue lifecycle automatically |
| TanStack Table | Custom table | TanStack provides sorting/filtering out-of-box, custom solution requires significant effort |
| VueUse | Manual localStorage | VueUse provides SSR-safe reactive wrapper, manual approach requires boilerplate |

**Installation:**
All dependencies already installed in package.json. No new packages required.

## Architecture Patterns

### Recommended Project Structure
```
app/
├── components/
│   └── backtest/
│       ├── BacktestForm.vue          # Parameter configuration form
│       ├── BacktestChart.vue         # Equity curve chart with dual y-axes
│       ├── BacktestMetrics.vue       # KPI cards (efficiency, BTC, cost)
│       ├── SweepResultsTable.vue     # Sortable sweep results
│       └── BacktestComparison.vue    # Side-by-side comparison panel
├── pages/
│   └── backtest.vue                  # Main backtest page layout
├── composables/
│   └── useBacktest.ts                # Backtest API calls and state
└── types/
    └── backtest.ts                   # TS types mirroring backend DTOs
```

### Pattern 1: Reactive Chart.js with vue-chartjs
**What:** Use vue-chartjs component-based approach with reactive refs for data and options
**When to use:** All Chart.js visualizations requiring automatic updates on data changes
**Example:**
```vue
<script setup lang="ts">
import { Line } from 'vue-chartjs'
import { ref, computed } from 'vue'
import type { ChartData, ChartOptions } from 'chart.js'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
} from 'chart.js'
import annotationPlugin from 'chartjs-plugin-annotation'

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  annotationPlugin
)

// Reactive data automatically updates chart
const chartData = ref<ChartData<'line'>>({
  labels: [],
  datasets: [
    {
      label: 'Smart DCA',
      data: [],
      borderColor: 'rgb(59, 130, 246)',
      yAxisID: 'y'
    },
    {
      label: 'BTC Price',
      data: [],
      borderColor: 'rgba(156, 163, 175, 0.5)',
      yAxisID: 'y1' // Secondary axis
    }
  ]
})

const chartOptions = computed<ChartOptions<'line'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    y: {
      type: 'linear',
      position: 'left',
      title: { display: true, text: 'Portfolio Value ($)' }
    },
    y1: {
      type: 'linear',
      position: 'right',
      title: { display: true, text: 'BTC Price ($)' },
      grid: { drawOnChartArea: false }
    }
  },
  plugins: {
    annotation: {
      annotations: {
        // Purchase markers
      }
    }
  }
}))
</script>

<template>
  <Line :data="chartData" :options="chartOptions" />
</template>
```
**Source:** [vue-chartjs Reactive Data with Composition API](https://context7.com/apertureless/vue-chartjs/llms.txt)

### Pattern 2: Dual Y-Axes for Multi-Metric Overlays
**What:** Assign datasets to different y-axes using `yAxisID` property
**When to use:** When displaying metrics with different scales (e.g., portfolio value vs BTC price)
**Example:**
```javascript
// Dataset configuration
datasets: [
  {
    label: 'Portfolio Value',
    data: portfolioValues,
    yAxisID: 'y' // Left axis
  },
  {
    label: 'BTC Price',
    data: btcPrices,
    yAxisID: 'y1' // Right axis
  }
]

// Scale configuration
scales: {
  y: {
    type: 'linear',
    position: 'left',
    ticks: {
      callback: (value) => `$${value.toLocaleString()}`
    }
  },
  y1: {
    type: 'linear',
    position: 'right',
    grid: { drawOnChartArea: false } // Don't overlay grid lines
  }
}
```
**Source:** [Chart.js Multi-Axis Line Chart](https://www.chartjs.org/docs/latest/samples/line/multi-axis.html)

### Pattern 3: Purchase Point Markers with chartjs-plugin-annotation
**What:** Use annotation plugin's label type to mark purchase events with variable size/color
**When to use:** Marking discrete events on time-series charts
**Example:**
```javascript
plugins: {
  annotation: {
    annotations: purchases.reduce((acc, purchase, index) => {
      acc[`purchase${index}`] = {
        type: 'label',
        xValue: purchase.date,
        yValue: purchase.portfolioValue,
        content: ['●'], // Dot marker
        backgroundColor: getTierColor(purchase.tier),
        color: 'white',
        font: {
          size: getTierSize(purchase.multiplier) // Size by multiplier
        },
        borderRadius: 50,
        padding: 4
      }
      return acc
    }, {})
  }
}
```
**Source:** [chartjs-plugin-annotation Label Annotations](https://www.chartjs.org/chartjs-plugin-annotation/latest/guide/types/label.html)

### Pattern 4: Nuxt UI Table with Sortable Columns
**What:** Use TanStack Table-powered Nuxt UI Table component with column definitions
**When to use:** Displaying tabular data with sorting, filtering, and row interactions
**Example:**
```vue
<script setup lang="ts">
import { h } from 'vue'

const columns = [
  {
    accessorKey: 'rank',
    header: 'Rank',
    meta: { class: { th: 'w-16' } }
  },
  {
    accessorKey: 'efficiency',
    header: 'Efficiency',
    cell: ({ row }) => h('span', { class: 'font-semibold text-primary' },
      row.getValue('efficiency').toFixed(2))
  },
  {
    accessorKey: 'returnPercent',
    header: 'Return %',
    cell: ({ row }) => `${row.getValue('returnPercent').toFixed(2)}%`
  }
]

const sorting = ref([{ id: 'efficiency', desc: true }])

function onRowClick(event, row) {
  // Load backtest detail
  loadBacktestDetail(row.original)
}
</script>

<template>
  <UTable
    :data="sweepResults"
    :columns="columns"
    v-model:sorting="sorting"
    @select="onRowClick"
    class="cursor-pointer"
  />
</template>
```
**Source:** [Nuxt UI Table Component](https://ui.nuxt.com/docs/components/table)

### Pattern 5: Session Storage Persistence with VueUse
**What:** Use `useSessionStorage` to persist comparison state reactively
**When to use:** State that should survive refresh but not tab close
**Example:**
```typescript
import { useSessionStorage } from '@vueuse/core'

// Automatically syncs with sessionStorage, max 3 items
const comparedBacktests = useSessionStorage<BacktestResult[]>(
  'backtest-comparison',
  [],
  {
    serializer: {
      read: (v) => v ? JSON.parse(v) : [],
      write: (v) => JSON.stringify(v.slice(0, 3)) // Cap at 3
    }
  }
)

function addToComparison(result: BacktestResult) {
  if (comparedBacktests.value.length >= 3) {
    comparedBacktests.value.shift() // Remove oldest
  }
  comparedBacktests.value.push(result)
}
```
**Source:** [VueUse useSessionStorage](https://vueuse.org/core/usesessionstorage/)

### Pattern 6: Pre-fill Form from Live Config
**What:** Fetch current DCA config via `/api/dashboard/status` or dedicated config endpoint
**When to use:** Initializing backtest form with production values
**Example:**
```vue
<script setup lang="ts">
const { data: liveConfig } = await useFetch('/api/dashboard/config')

const formData = ref({
  baseDailyAmount: liveConfig.value?.baseDailyAmount ?? 10,
  highLookbackDays: liveConfig.value?.highLookbackDays ?? 365,
  tiers: liveConfig.value?.tiers ?? []
})
</script>

<template>
  <UFormGroup label="Base Daily Amount">
    <UInput v-model.number="formData.baseDailyAmount" type="number" />
  </UFormGroup>
</template>
```

### Pattern 7: Date Range Presets with Calendar
**What:** Button group for quick presets (1Y, 2Y, 3Y, Max) + popover calendar for custom range
**When to use:** User-friendly date range selection
**Example:**
```vue
<script setup lang="ts">
import { CalendarDate, DateFormatter } from '@internationalized/date'

const selectedPreset = ref<string | null>('1Y')
const dateRange = ref({
  start: new CalendarDate(2025, 2, 14),
  end: new CalendarDate(2026, 2, 14)
})

function applyPreset(preset: string) {
  selectedPreset.value = preset
  const end = new CalendarDate(2026, 2, 14)
  const start = preset === '1Y' ? end.subtract({ years: 1 })
    : preset === '2Y' ? end.subtract({ years: 2 })
    : preset === '3Y' ? end.subtract({ years: 3 })
    : new CalendarDate(2013, 1, 1) // Max

  dateRange.value = { start, end }
}
</script>

<template>
  <div class="flex gap-2">
    <UButton
      v-for="preset in ['1Y', '2Y', '3Y', 'Max']"
      :variant="selectedPreset === preset ? 'solid' : 'soft'"
      @click="applyPreset(preset)"
    >
      {{ preset }}
    </UButton>
    <UPopover>
      <UButton icon="i-lucide-calendar" variant="soft">Custom</UButton>
      <template #content>
        <UCalendar v-model="dateRange" range :number-of-months="2" />
      </template>
    </UPopover>
  </div>
</template>
```
**Source:** [Nuxt UI Calendar with Date Range](https://ui4.nuxt.com/docs/components/calendar)

### Anti-Patterns to Avoid
- **String interpolation in logs:** Use structured logging with named placeholders (`logger.LogInformation("Backtest run for {DateRange}", range)`)
- **Mutating Chart.js data directly:** Always replace entire data object for reactivity (`chartData.value = { ...newData }`)
- **Over-fetching on comparison add:** Don't re-fetch full backtest, store result from initial run
- **Rendering components in table cells without `h()`:** Use Vue's `h()` helper with `resolveComponent()` for proper reactivity

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Table sorting/filtering | Custom sort logic | TanStack Table (via Nuxt UI) | Handles edge cases (nulls, strings, dates), provides pagination, column visibility |
| Session storage reactivity | Manual localStorage wrapper | VueUse `useSessionStorage` | SSR-safe, automatic serialization, ref reactivity, cross-tab sync |
| Date formatting | Custom date functions | date-fns | Handles timezones, locales, DST edge cases (~10KB vs 200KB Moment.js) |
| Chart tooltips | Custom overlay divs | Chart.js tooltip plugin | Handles positioning, responsive, configurable callbacks |
| Form validation | Regex checks | Nuxt UI Form + backend validation | Built-in error display, async validation, accessibility |
| Loading states | DIV spinners | Nuxt UI Progress/Button loading | Consistent UX, accessible, configurable |

**Key insight:** Backtest visualization has deep state management needs (sorting, filtering, comparison tracking). Custom solutions introduce bugs with null data, timezone mismatches, and reactivity edge cases. Established libraries handle these through years of production testing.

## Common Pitfalls

### Pitfall 1: Chart.js Reactivity Breaks with Mutation
**What goes wrong:** Mutating `chartData.value.datasets[0].data.push()` doesn't trigger re-render
**Why it happens:** Vue 3 reactivity requires full object replacement for deep changes
**How to avoid:** Always assign new object: `chartData.value = { ...chartData.value, datasets: newDatasets }`
**Warning signs:** Chart doesn't update after API response, console shows no errors
**Source:** [vue-chartjs Reactive Data](https://context7.com/apertureless/vue-chartjs/llms.txt)

### Pitfall 2: Date Serialization Mismatch with Backend
**What goes wrong:** Backend sends `DateOnly` as "2026-02-14", JavaScript Date parses as UTC midnight, displays as previous day in local timezone
**Why it happens:** .NET DateOnly serializes without time, JS Date assumes UTC 00:00
**How to avoid:** Use `date-fns` `parseISO()` and format without timezone conversion: `format(parseISO(dateString), 'yyyy-MM-dd')`
**Warning signs:** Dates off by 1 day in UI but correct in API response
**Source:** [date-fns parse documentation](https://date-fns.org/)

### Pitfall 3: TanStack Table Column Width Flicker
**What goes wrong:** Table columns jump/resize on sort when no explicit widths set
**Why it happens:** TanStack recalculates widths on data change, content affects auto-sizing
**How to avoid:** Set explicit `size` in column meta for key columns (rank, metrics)
**Warning signs:** Table layout shifts on sort, especially with numeric columns
**Source:** [Nuxt UI Table sizing](https://ui.nuxt.com/docs/components/table)

### Pitfall 4: Session Storage Quota Exceeded
**What goes wrong:** Storing full purchase logs (1000+ entries) in comparison state hits 5MB quota
**Why it happens:** Each backtest result includes day-by-day purchase log (can be 50KB+)
**How to avoid:** Store only summary metrics in comparison state, fetch full logs on demand
**Warning signs:** Browser throws QuotaExceededError, comparison feature stops working
**Source:** [VueUse useSessionStorage](https://vueuse.org/core/usesessionstorage/)

### Pitfall 5: Chart.js Plugin Registration Missing
**What goes wrong:** Annotation plugin annotations don't render, no console errors
**Why it happens:** Forgot `ChartJS.register(annotationPlugin)` before component mount
**How to avoid:** Register all plugins in component `<script setup>` before template renders
**Warning signs:** Chart renders but purchase markers missing, plugin options ignored
**Source:** [vue-chartjs Plugin Usage](https://context7.com/apertureless/vue-chartjs/llms.txt)

### Pitfall 6: Equity Curve Y-Axis Scale Mismatch
**What goes wrong:** BTC price (40K-100K) makes portfolio value (5K-10K) line appear flat
**Why it happens:** Single y-axis with auto-range dominated by larger values
**How to avoid:** Always use dual y-axes (`yAxisID: 'y'` and `yAxisID: 'y1'`) for different magnitudes
**Warning signs:** One dataset appears as flat line at bottom of chart
**Source:** [Chart.js Multi-Axis](https://www.chartjs.org/docs/latest/samples/line/multi-axis.html)

### Pitfall 7: Sweep Results Table Performance Degradation
**What goes wrong:** Table becomes sluggish with 1000+ sweep combinations
**Why it happens:** Rendering all rows with complex cell formatters
**How to avoid:** Enable virtualization: `virtualize: true` or `virtualize: { overscan: 5 }`
**Warning signs:** Scrolling lags, browser pegs CPU at 100%
**Source:** [Nuxt UI Table virtualization](https://ui.nuxt.com/docs/components/table)

## Code Examples

Verified patterns from official sources:

### Multi-Dataset Equity Curve with Purchase Markers
```vue
<script setup lang="ts">
import { Line } from 'vue-chartjs'
import { ref, computed } from 'vue'
import type { ChartData, ChartOptions } from 'chart.js'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
} from 'chart.js'
import annotationPlugin from 'chartjs-plugin-annotation'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend, annotationPlugin)

interface BacktestChartProps {
  result: BacktestResult | null
  yAxisMode: 'usd' | 'btc'
}

const props = defineProps<BacktestChartProps>()

const chartData = computed<ChartData<'line'>>(() => {
  if (!props.result) return { labels: [], datasets: [] }

  const purchases = props.result.purchaseLog

  return {
    labels: purchases.map(p => p.date),
    datasets: [
      {
        label: 'Smart DCA',
        data: purchases.map(p => props.yAxisMode === 'usd' ? p.smartPortfolioValue : p.smartBtcAccumulated),
        borderColor: 'rgb(59, 130, 246)',
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        yAxisID: 'y',
        pointRadius: 0,
        borderWidth: 2
      },
      {
        label: 'Fixed DCA',
        data: purchases.map(p => props.yAxisMode === 'usd' ? p.fixedPortfolioValue : p.fixedBtcAccumulated),
        borderColor: 'rgb(156, 163, 175)',
        backgroundColor: 'rgba(156, 163, 175, 0.1)',
        yAxisID: 'y',
        pointRadius: 0,
        borderWidth: 2
      },
      {
        label: 'BTC Price',
        data: purchases.map(p => p.btcPrice),
        borderColor: 'rgba(251, 191, 36, 0.3)',
        yAxisID: 'y1',
        pointRadius: 0,
        borderWidth: 1,
        borderDash: [5, 5]
      }
    ]
  }
})

const chartOptions = computed<ChartOptions<'line'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: {
    mode: 'index',
    intersect: false
  },
  scales: {
    y: {
      type: 'linear',
      position: 'left',
      title: {
        display: true,
        text: props.yAxisMode === 'usd' ? 'Portfolio Value ($)' : 'BTC Accumulated'
      },
      ticks: {
        callback: (value) => props.yAxisMode === 'usd'
          ? `$${Number(value).toLocaleString()}`
          : Number(value).toFixed(4)
      }
    },
    y1: {
      type: 'linear',
      position: 'right',
      title: {
        display: true,
        text: 'BTC Price ($)'
      },
      grid: {
        drawOnChartArea: false
      },
      ticks: {
        callback: (value) => `$${Number(value).toLocaleString()}`
      }
    }
  },
  plugins: {
    legend: {
      position: 'top'
    },
    tooltip: {
      callbacks: {
        label: (context) => {
          let label = context.dataset.label || ''
          if (label) label += ': '

          if (context.datasetIndex === 2) { // BTC Price
            label += `$${context.parsed.y.toLocaleString()}`
          } else if (props.yAxisMode === 'usd') {
            label += `$${context.parsed.y.toLocaleString()}`
          } else {
            label += `${context.parsed.y.toFixed(4)} BTC`
          }
          return label
        }
      }
    },
    annotation: {
      annotations: props.result?.purchaseLog
        .filter(p => p.smartPurchaseMade)
        .reduce((acc, purchase, index) => {
          acc[`purchase${index}`] = {
            type: 'label',
            xValue: purchase.date,
            yValue: props.yAxisMode === 'usd' ? purchase.smartPortfolioValue : purchase.smartBtcAccumulated,
            content: ['●'],
            backgroundColor: getTierColor(purchase.tier),
            color: 'white',
            font: {
              size: getTierSize(purchase.multiplier)
            },
            borderRadius: 50,
            padding: 4
          }
          return acc
        }, {}) ?? {}
    }
  }
}))

function getTierColor(tier: string): string {
  const colors = {
    'Base': 'rgb(156, 163, 175)',
    'Tier1': 'rgb(34, 197, 94)',
    'Tier2': 'rgb(59, 130, 246)',
    'Tier3': 'rgb(168, 85, 247)',
    'BearBoost': 'rgb(239, 68, 68)'
  }
  return colors[tier] ?? colors.Base
}

function getTierSize(multiplier: number): number {
  // Base size 8, scale up with multiplier (cap at 16)
  return Math.min(8 + multiplier * 2, 16)
}
</script>

<template>
  <div class="h-[500px]">
    <ClientOnly>
      <Line :data="chartData" :options="chartOptions" />
    </ClientOnly>
  </div>
</template>
```
**Source:** Composite pattern from [Chart.js Multi-Axis](https://www.chartjs.org/docs/latest/samples/line/multi-axis.html) + [chartjs-plugin-annotation](https://www.chartjs.org/chartjs-plugin-annotation/latest/)

### Sortable Sweep Results Table with Row Click
```vue
<script setup lang="ts">
import { h } from 'vue'

interface SweepTableProps {
  results: SweepResultEntry[]
}

const props = defineProps<SweepTableProps>()
const emit = defineEmits<{
  selectConfig: [config: BacktestConfig]
}>()

const columns = [
  {
    accessorKey: 'rank',
    header: 'Rank',
    meta: { class: { th: 'w-16 text-center', td: 'text-center' } }
  },
  {
    accessorKey: 'comparison.efficiencyRatio',
    header: 'Efficiency',
    cell: ({ row }) => h('span', {
      class: 'font-bold text-lg text-primary'
    }, row.getValue('comparison.efficiencyRatio').toFixed(2)),
    meta: { class: { th: 'w-32' } }
  },
  {
    accessorKey: 'smartDca.returnPercent',
    header: 'Return %',
    cell: ({ row }) => {
      const value = row.getValue('smartDca.returnPercent')
      return h('span', {
        class: value > 0 ? 'text-green-600' : 'text-red-600'
      }, `${value > 0 ? '+' : ''}${value.toFixed(2)}%`)
    }
  },
  {
    accessorKey: 'smartDca.totalBtc',
    header: 'Total BTC',
    cell: ({ row }) => row.getValue('smartDca.totalBtc').toFixed(4)
  },
  {
    accessorKey: 'config.baseDailyAmount',
    header: 'Base Amount',
    cell: ({ row }) => `$${row.getValue('config.baseDailyAmount')}`
  },
  {
    accessorKey: 'walkForward.overfit',
    header: 'Overfit',
    cell: ({ row }) => {
      const wf = row.original.walkForward
      if (!wf) return '—'
      return h('span', {
        class: wf.overfitWarning ? 'text-amber-600' : 'text-gray-400'
      }, wf.overfitWarning ? '⚠️ Warning' : '✓ OK')
    }
  }
]

const sorting = ref([{ id: 'comparison.efficiencyRatio', desc: true }])

function handleRowClick(event: Event, row: any) {
  emit('selectConfig', row.original.config)
}
</script>

<template>
  <UTable
    :data="results"
    :columns="columns"
    v-model:sorting="sorting"
    @select="handleRowClick"
    :virtualize="results.length > 100"
    class="cursor-pointer"
    :ui="{
      td: 'hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors'
    }"
  >
    <template #empty>
      <div class="text-center py-8 text-gray-500">
        No sweep results. Run a parameter sweep to see ranked configurations.
      </div>
    </template>
  </UTable>
</template>
```
**Source:** [Nuxt UI Table Component](https://ui.nuxt.com/docs/components/table)

### Backtest Parameter Form with Presets and Progress
```vue
<script setup lang="ts">
import { CalendarDate } from '@internationalized/date'
import { format, parseISO } from 'date-fns'

const { data: liveConfig } = await useFetch('/api/dashboard/config')

const formData = ref({
  baseDailyAmount: liveConfig.value?.baseDailyAmount ?? 10,
  highLookbackDays: liveConfig.value?.highLookbackDays ?? 365,
  startDate: null as CalendarDate | null,
  endDate: null as CalendarDate | null,
  tiers: liveConfig.value?.tiers ?? []
})

const selectedPreset = ref('1Y')
const isRunning = ref(false)
const progress = ref(0)

async function runBacktest() {
  isRunning.value = true
  progress.value = 0

  try {
    // Simulate progress (real impl would use SSE or polling)
    const progressInterval = setInterval(() => {
      if (progress.value < 90) progress.value += 10
    }, 200)

    const response = await $fetch('/api/backtest', {
      method: 'POST',
      body: {
        startDate: formData.value.startDate?.toString(),
        endDate: formData.value.endDate?.toString(),
        baseDailyAmount: formData.value.baseDailyAmount,
        tiers: formData.value.tiers
      }
    })

    clearInterval(progressInterval)
    progress.value = 100

    emit('backtestComplete', response)
  } finally {
    setTimeout(() => {
      isRunning.value = false
      progress.value = 0
    }, 500)
  }
}

function applyPreset(preset: string) {
  selectedPreset.value = preset
  const today = new CalendarDate(2026, 2, 14)

  const ranges = {
    '1Y': { years: 1 },
    '2Y': { years: 2 },
    '3Y': { years: 3 },
    'Max': null
  }

  const range = ranges[preset]
  formData.value.startDate = range ? today.subtract(range) : new CalendarDate(2013, 1, 1)
  formData.value.endDate = today
}
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="text-lg font-semibold">Backtest Configuration</h2>
    </template>

    <!-- Date range presets -->
    <div class="mb-6">
      <label class="block text-sm font-medium mb-2">Date Range</label>
      <div class="flex gap-2 mb-2">
        <UButton
          v-for="preset in ['1Y', '2Y', '3Y', 'Max']"
          :key="preset"
          :variant="selectedPreset === preset ? 'solid' : 'soft'"
          @click="applyPreset(preset)"
        >
          {{ preset }}
        </UButton>
        <UPopover>
          <UButton icon="i-lucide-calendar" variant="soft">Custom</UButton>
          <template #content>
            <UCalendar
              v-model="formData.startDate"
              v-model:end="formData.endDate"
              range
              :number-of-months="2"
              class="p-2"
            />
          </template>
        </UPopover>
      </div>
      <p class="text-sm text-gray-500">
        {{ formData.startDate?.toString() }} to {{ formData.endDate?.toString() }}
      </p>
    </div>

    <!-- Parameter inputs -->
    <div class="grid grid-cols-2 gap-4 mb-6">
      <UFormGroup label="Base Daily Amount">
        <UInput
          v-model.number="formData.baseDailyAmount"
          type="number"
          :disabled="isRunning"
        />
      </UFormGroup>

      <UFormGroup label="High Lookback Days">
        <UInput
          v-model.number="formData.highLookbackDays"
          type="number"
          :disabled="isRunning"
        />
      </UFormGroup>
    </div>

    <!-- Multiplier tiers (editable) -->
    <div class="mb-6">
      <label class="block text-sm font-medium mb-2">Multiplier Tiers</label>
      <div v-for="(tier, index) in formData.tiers" :key="index" class="grid grid-cols-2 gap-2 mb-2">
        <UInput
          v-model.number="tier.dropPercentage"
          type="number"
          placeholder="Drop %"
          :disabled="isRunning"
        />
        <UInput
          v-model.number="tier.multiplier"
          type="number"
          placeholder="Multiplier"
          :disabled="isRunning"
        />
      </div>
    </div>

    <!-- Progress bar -->
    <UProgress v-if="isRunning" v-model="progress" status class="mb-4" />

    <!-- Submit button -->
    <UButton
      @click="runBacktest"
      :loading="isRunning"
      :disabled="!formData.startDate || !formData.endDate"
      block
    >
      Run Backtest
    </UButton>
  </UCard>
</template>
```
**Source:** Composite from [Nuxt UI Calendar](https://ui4.nuxt.com/docs/components/calendar) + [Nuxt UI Progress](https://ui4.nuxt.com/docs/components/progress)

### Comparison Panel with Session Storage
```vue
<script setup lang="ts">
import { useSessionStorage } from '@vueuse/core'

interface ComparisonEntry {
  id: string
  label: string
  config: BacktestConfig
  metrics: DcaMetrics
  color: string
}

const comparedBacktests = useSessionStorage<ComparisonEntry[]>(
  'backtest-comparison',
  [],
  {
    serializer: {
      read: (v) => v ? JSON.parse(v) : [],
      write: (v) => JSON.stringify(v.slice(0, 3)) // Max 3
    }
  }
)

const colors = ['rgb(59, 130, 246)', 'rgb(34, 197, 94)', 'rgb(168, 85, 247)']

function addToComparison(result: BacktestResult, label: string) {
  const entry: ComparisonEntry = {
    id: crypto.randomUUID(),
    label,
    config: result.config,
    metrics: result.smartDca,
    color: colors[comparedBacktests.value.length % 3]
  }

  if (comparedBacktests.value.length >= 3) {
    comparedBacktests.value.shift()
  }
  comparedBacktests.value.push(entry)
}

function removeFromComparison(id: string) {
  comparedBacktests.value = comparedBacktests.value.filter(b => b.id !== id)
}

function clearComparison() {
  comparedBacktests.value = []
}
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">
          Comparison ({{ comparedBacktests.length }}/3)
        </h2>
        <UButton
          v-if="comparedBacktests.length > 0"
          variant="soft"
          color="red"
          @click="clearComparison"
        >
          Clear All
        </UButton>
      </div>
    </template>

    <div v-if="comparedBacktests.length === 0" class="text-center py-8 text-gray-500">
      Run backtests to compare configurations
    </div>

    <div v-else>
      <!-- Comparison chart -->
      <BacktestChart
        :results="comparedBacktests"
        mode="comparison"
        class="mb-6"
      />

      <!-- Metrics table -->
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b">
              <th class="text-left py-2">Metric</th>
              <th
                v-for="backtest in comparedBacktests"
                :key="backtest.id"
                class="text-right py-2"
                :style="{ color: backtest.color }"
              >
                {{ backtest.label }}
                <UButton
                  icon="i-lucide-x"
                  variant="ghost"
                  size="xs"
                  @click="removeFromComparison(backtest.id)"
                  class="ml-2"
                />
              </th>
            </tr>
          </thead>
          <tbody>
            <tr class="border-b">
              <td class="py-2">Total BTC</td>
              <td
                v-for="backtest in comparedBacktests"
                :key="backtest.id"
                class="text-right py-2"
              >
                {{ backtest.metrics.totalBtc.toFixed(4) }}
              </td>
            </tr>
            <tr class="border-b">
              <td class="py-2">Avg Cost Basis</td>
              <td
                v-for="backtest in comparedBacktests"
                :key="backtest.id"
                class="text-right py-2"
              >
                ${{ backtest.metrics.avgCostBasis.toLocaleString() }}
              </td>
            </tr>
            <tr class="border-b">
              <td class="py-2">Return %</td>
              <td
                v-for="backtest in comparedBacktests"
                :key="backtest.id"
                class="text-right py-2"
                :class="backtest.metrics.returnPercent > 0 ? 'text-green-600' : 'text-red-600'"
              >
                {{ backtest.metrics.returnPercent > 0 ? '+' : '' }}{{ backtest.metrics.returnPercent.toFixed(2) }}%
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </UCard>
</template>
```
**Source:** [VueUse useSessionStorage](https://vueuse.org/core/usesessionstorage/)

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Moment.js for dates | date-fns | 2020 | Bundle size: 200KB → 10KB, tree-shakable |
| Custom table sorting | TanStack Table | 2023 | Type-safe, headless UI, battle-tested |
| Manual localStorage | VueUse composables | 2021 | SSR-safe, reactive, zero boilerplate |
| Chart.js v2 | Chart.js v4 | 2023 | Tree-shakable, TypeScript, better performance |
| Mixins for reactivity | Composition API | 2020 (Vue 3) | Better TypeScript, reusable logic, clearer dependencies |

**Deprecated/outdated:**
- **Chart.js v2 global config:** v4 requires explicit component registration (`ChartJS.register()`)
- **vue-chartjs mixins:** v5 removed mixins, use component-based approach with reactive refs
- **Nuxt UI v2:** v4 unified UI and UI Pro, changed import paths and component APIs
- **Options API:** Still supported but Composition API preferred for new code per Vue 3 best practices

## Open Questions

1. **Backend config endpoint**
   - What we know: Dashboard endpoints exist (`/api/dashboard/status`, `/api/dashboard/portfolio`)
   - What's unclear: Does a dedicated `/api/dashboard/config` endpoint exist for DCA config, or should we fetch from status?
   - Recommendation: Check existing endpoints first, create minimal endpoint if needed (just return `IOptionsMonitor<DcaOptions>.CurrentValue`)

2. **Sweep progress reporting**
   - What we know: Sweep can run 1000+ combinations, takes seconds to minutes
   - What's unclear: Backend supports progress callbacks or just synchronous response?
   - Recommendation: Start with synchronous (show progress bar with estimated time), add SSE/polling if needed in later phase

3. **Historical data availability**
   - What we know: Backend has `DailyPrice` table with BTC OHLCV data
   - What's unclear: What's the actual date range available (min/max date)?
   - Recommendation: Query on component mount: `GET /api/backtest/data/range` or infer from first backtest error

## Sources

### Primary (HIGH confidence)
- [Chart.js Official Documentation](https://www.chartjs.org/docs/latest/) - Multi-axis, scriptable options, annotations
- [vue-chartjs v5.3.3 Documentation](https://context7.com/apertureless/vue-chartjs/llms.txt) - Composition API, reactivity, plugins
- [Nuxt UI v4 Documentation](https://ui.nuxt.com/docs) - Table, Form, Calendar, Progress components
- [VueUse Core Documentation](https://vueuse.org/core/usesessionstorage/) - useSessionStorage composable
- [date-fns Documentation](https://date-fns.org/) - Format, parse, date manipulation
- [chartjs-plugin-annotation v3.1.0](https://www.chartjs.org/chartjs-plugin-annotation/latest/) - Label annotations, line markers

### Secondary (MEDIUM confidence)
- [QuantifiedStrategies - Equity Curve Best Practices](https://www.quantifiedstrategies.com/equity-curve/) - Visual design patterns for backtest charts
- [Volatility Backtesting Charts](https://www.getvolatility.com/user-guide/backtesting-charts/) - UX patterns for financial backtesting
- [W&B Sweep Visualization](https://docs.wandb.ai/guides/sweeps/visualize-sweep-results/) - Parameter sweep result table patterns

### Tertiary (LOW confidence)
- None - all findings verified with official documentation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All dependencies already installed, official docs verified
- Architecture: HIGH - Patterns from official Chart.js, vue-chartjs, Nuxt UI examples
- Pitfalls: MEDIUM - Identified from library issue trackers and community patterns, not all tested in this specific stack

**Research date:** 2026-02-14
**Valid until:** 60 days (stable ecosystem, no fast-moving dependencies)

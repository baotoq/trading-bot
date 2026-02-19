# Phase 10: Dashboard Core - Research

**Researched:** 2026-02-13
**Domain:** Vue 3 / Nuxt 4 dashboard development with real-time data, charting, and infinite scroll
**Confidence:** HIGH

## Summary

Phase 10 implements a view-only dashboard displaying portfolio metrics, price charts with purchase markers, live bot status, and paginated purchase history. The user has made specific layout and UI decisions (horizontal stat cards, card-based purchase list, line chart with green "B" markers, live countdown timer). This research identifies the standard Nuxt 4 / Vue 3 stack, proven patterns for real-time updates, charting libraries suited to the decisions, and performance optimizations for infinite scroll pagination.

The recommended stack centers on Nuxt UI for components, Chart.js with annotation plugin for purchase markers on price charts, VueUse composables for countdown timers and infinite scroll via Intersection Observer, and cursor-based pagination in EF Core for efficient data fetching. Key pitfalls include hydration errors from SSR, Chart.js memory leaks without proper cleanup, and N+1 queries when loading purchase history with related data.

**Primary recommendation:** Use Nuxt UI Card/Badge components for stats and purchase cards, Chart.js with chartjs-plugin-annotation for purchase markers and average cost basis line, VueUse useInfiniteScroll and useInterval for infinite scroll and countdown timer, and implement cursor-based pagination with AsNoTracking queries in .NET backend to avoid N+1 issues and optimize for large datasets.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Portfolio overview layout:**
- Horizontal row of stat cards at the top of the page
- Cards show: Total BTC, Total Cost, Average Cost Basis, Current Price, Unrealized P&L
- P&L displays both percentage and USD amount (e.g., +12.3% / +$1,234) with green/red coloring
- Single scrollable page structure: stat cards → chart → live status → purchase history

**Purchase history display:**
- Card list format (each purchase as a stacked card, not a data table)
- Each card shows: date, price, USD amount, BTC quantity, multiplier tier
- Multiplier tier shown as color-coded badge (distinct colors per tier for easy scanning)
- Date range filter only (no tier filtering)
- Infinite scroll pagination (auto-load as user scrolls down)

**Price & purchase chart:**
- Line chart for price history (not candlestick or area)
- Purchase markers: green badge with "B" label at each purchase point on the chart
- Preset timeframe buttons: 7D, 1M, 3M, 6M, 1Y, All (no custom date picker)
- Dashed horizontal line showing average cost basis as reference

**Live status presentation:**
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

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope

</user_constraints>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Nuxt UI | ^4.4.0 | Component library (Card, Badge, Skeleton) | Already installed, provides Tailwind-based components optimized for Nuxt SSR, includes dashboard-specific components like DashboardCard |
| Chart.js | ^4.x | Line chart rendering | Most popular JS charting library (71.4k stars), lightweight, highly performant, extensive ecosystem |
| chartjs-plugin-annotation | ^3.x | Chart markers and reference lines | Official Chart.js plugin for drawing annotations, supports custom markers and dashed horizontal lines (average cost basis) |
| VueUse | Latest | Composables (useInfiniteScroll, useInterval) | Standard Vue 3 composable library, provides performant Intersection Observer-based infinite scroll and reactive interval timers |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| vue-chartjs | ^5.x | Vue 3 wrapper for Chart.js | Simplifies Chart.js integration with Vue lifecycle, handles chart cleanup automatically |
| @vuepic/vue-datepicker | Latest | Date range picker | If date range filter needs calendar UI (user specified date range filter, component provides range selection with SSR support) |
| date-fns | Latest | Date formatting and manipulation | Lightweight alternative to Moment.js, tree-shakeable, used for countdown timer calculations and date display |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Chart.js | ApexCharts | ApexCharts has beautiful animations but larger bundle size; Chart.js is lighter and sufficient for line charts with markers |
| Chart.js | ECharts | ECharts handles complex charts well but overkill for simple line chart with markers; Chart.js is simpler and faster for this use case |
| vue-chartjs | Direct Chart.js | vue-chartjs handles Vue lifecycle (destroy on unmount) automatically; direct Chart.js requires manual cleanup but gives more control |
| VueUse useInfiniteScroll | vue3-infinite-scroll | VueUse is already recommended in Vue ecosystem, provides more composables for other needs (useInterval for countdown timer) |

**Installation:**
```bash
# Frontend dependencies
cd TradingBot.Dashboard
npm install chart.js vue-chartjs chartjs-plugin-annotation @vueuse/core date-fns
npm install --save-dev @types/chart.js

# Optional: date picker if calendar UI is needed
npm install @vuepic/vue-datepicker
```

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.Dashboard/
├── components/
│   ├── dashboard/
│   │   ├── StatCard.vue           # Reusable stat card component
│   │   ├── PortfolioStats.vue     # Horizontal row of stat cards
│   │   ├── PriceChart.vue         # Line chart with markers
│   │   ├── LiveStatus.vue         # Bot health, countdown timer, connection status
│   │   └── PurchaseHistoryCard.vue # Single purchase card
│   └── common/
│       ├── LoadingSkeleton.vue    # Skeleton for loading states
│       └── EmptyState.vue         # Empty state placeholder
├── composables/
│   ├── useDashboard.ts            # Fetch portfolio/status data
│   ├── usePurchaseHistory.ts     # Infinite scroll purchase pagination
│   ├── useCountdownTimer.ts      # Next buy countdown logic
│   └── usePriceChart.ts          # Chart.js setup and cleanup
├── pages/
│   └── index.vue                  # Main dashboard page
└── types/
    └── dashboard.ts               # TypeScript types for API responses
```

### Pattern 1: Composable-Based Data Fetching

**What:** Encapsulate data fetching logic into composables using `useFetch` with proper SSR support

**When to use:** All API calls to backend, allows reusability and clean separation of concerns

**Example:**
```typescript
// composables/useDashboard.ts
export const useDashboard = () => {
  const { data: portfolio, pending, error, refresh } = useFetch('/api/portfolio', {
    // Lazy: true prevents blocking navigation
    lazy: true,
    // Server: false runs only on client for real-time data
    server: false,
    // Polling interval for live updates (10 seconds)
    watch: false
  })

  // Manual polling with useInterval
  const { pause, resume } = useIntervalFn(refresh, 10000)

  return { portfolio, pending, error, refresh, pause, resume }
}
```

### Pattern 2: Infinite Scroll with Intersection Observer

**What:** Use VueUse `useInfiniteScroll` composable for auto-loading purchase history

**When to use:** Purchase history pagination (user decision: infinite scroll)

**Example:**
```typescript
// composables/usePurchaseHistory.ts
import { useInfiniteScroll } from '@vueuse/core'

export const usePurchaseHistory = () => {
  const purchases = ref<Purchase[]>([])
  const cursor = ref<string | null>(null)
  const hasMore = ref(true)
  const loading = ref(false)

  const loadMore = async () => {
    if (!hasMore.value || loading.value) return
    loading.value = true

    const { data } = await $fetch('/api/purchases', {
      query: { cursor: cursor.value, pageSize: 20 }
    })

    purchases.value.push(...data.items)
    cursor.value = data.nextCursor
    hasMore.value = data.hasMore
    loading.value = false
  }

  // Trigger when scrolling near bottom
  const el = ref<HTMLElement>()
  useInfiniteScroll(el, loadMore, { distance: 100 })

  return { purchases, loading, hasMore, el }
}
```

### Pattern 3: Chart.js with Lifecycle Cleanup

**What:** Wrap Chart.js in composable with proper cleanup to avoid memory leaks

**When to use:** PriceChart component (line chart with purchase markers)

**Example:**
```typescript
// composables/usePriceChart.ts
import { Chart } from 'chart.js/auto'
import annotationPlugin from 'chartjs-plugin-annotation'

Chart.register(annotationPlugin)

export const usePriceChart = (canvasRef: Ref<HTMLCanvasElement | null>) => {
  let chartInstance: Chart | null = null

  const createChart = (priceData: PricePoint[], purchases: Purchase[], avgCostBasis: number) => {
    if (!canvasRef.value) return

    // Destroy existing chart to prevent memory leak
    if (chartInstance) {
      chartInstance.destroy()
    }

    chartInstance = new Chart(canvasRef.value, {
      type: 'line',
      data: {
        labels: priceData.map(p => p.date),
        datasets: [{
          label: 'BTC Price',
          data: priceData.map(p => p.price),
          borderColor: 'rgb(75, 192, 192)',
          tension: 0.1
        }]
      },
      options: {
        plugins: {
          annotation: {
            annotations: {
              // Average cost basis line (dashed horizontal)
              avgLine: {
                type: 'line',
                yMin: avgCostBasis,
                yMax: avgCostBasis,
                borderColor: 'rgba(255, 99, 132, 0.5)',
                borderDash: [6, 6],
                borderWidth: 2
              },
              // Purchase markers (green "B" badges)
              ...purchases.reduce((acc, purchase, idx) => {
                acc[`purchase${idx}`] = {
                  type: 'label',
                  xValue: purchase.date,
                  yValue: purchase.price,
                  content: ['B'],
                  backgroundColor: 'rgb(34, 197, 94)',
                  color: 'white',
                  font: { size: 12, weight: 'bold' },
                  borderRadius: 4,
                  padding: 4
                }
                return acc
              }, {})
            }
          }
        }
      }
    })
  }

  // Cleanup on unmount
  onUnmounted(() => {
    if (chartInstance) {
      chartInstance.destroy()
      chartInstance = null
    }
  })

  return { createChart }
}
```

### Pattern 4: Live Countdown Timer

**What:** Use VueUse `useInterval` with reactive countdown state

**When to use:** Live status section (next buy countdown)

**Example:**
```typescript
// composables/useCountdownTimer.ts
import { useInterval } from '@vueuse/core'
import { differenceInSeconds, formatDuration } from 'date-fns'

export const useCountdownTimer = (targetTime: Ref<Date | null>) => {
  const remaining = ref('')

  const updateCountdown = () => {
    if (!targetTime.value) {
      remaining.value = 'N/A'
      return
    }

    const seconds = differenceInSeconds(targetTime.value, new Date())
    if (seconds <= 0) {
      remaining.value = 'Now'
      return
    }

    const hours = Math.floor(seconds / 3600)
    const minutes = Math.floor((seconds % 3600) / 60)
    const secs = seconds % 60
    remaining.value = `${hours}h ${minutes}m ${secs}s`
  }

  // Update every second
  const { pause, resume } = useInterval(updateCountdown, 1000, { immediate: true })

  return { remaining, pause, resume }
}
```

### Pattern 5: Cursor-Based Pagination (Backend)

**What:** Use cursor-based pagination instead of offset for efficient large dataset queries

**When to use:** Purchase history endpoint (better performance than SKIP/TAKE on large datasets)

**Example:**
```csharp
// TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
public record PurchaseHistoryRequest(
    string? Cursor,
    int PageSize = 20,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
);

public record PurchaseHistoryResponse(
    List<PurchaseDto> Items,
    string? NextCursor,
    bool HasMore
);

private static async Task<IResult> GetPurchaseHistoryAsync(
    [AsParameters] PurchaseHistoryRequest request,
    TradingBotDbContext db,
    CancellationToken ct)
{
    var query = db.Purchases
        .AsNoTracking() // Read-only optimization
        .Where(p => !p.IsDryRun && (p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled));

    // Date range filter
    if (request.StartDate.HasValue)
        query = query.Where(p => p.ExecutedAt >= request.StartDate.Value.ToDateTime(TimeOnly.MinValue));
    if (request.EndDate.HasValue)
        query = query.Where(p => p.ExecutedAt <= request.EndDate.Value.ToDateTime(TimeOnly.MaxValue));

    // Cursor-based pagination
    if (!string.IsNullOrEmpty(request.Cursor))
    {
        var cursorDate = DateTimeOffset.Parse(request.Cursor);
        query = query.Where(p => p.ExecutedAt < cursorDate);
    }

    var items = await query
        .OrderByDescending(p => p.ExecutedAt)
        .Take(request.PageSize + 1) // Fetch one extra to check if more exists
        .ToListAsync(ct);

    var hasMore = items.Count > request.PageSize;
    if (hasMore) items.RemoveAt(items.Count - 1);

    var nextCursor = hasMore ? items.Last().ExecutedAt.ToString("o") : null;

    var dtos = items.Select(p => new PurchaseDto(
        p.Id,
        p.ExecutedAt,
        p.Price,
        p.Cost,
        p.Quantity,
        p.MultiplierTier ?? "Base"
    )).ToList();

    return Results.Ok(new PurchaseHistoryResponse(dtos, nextCursor, hasMore));
}
```

### Anti-Patterns to Avoid

- **Loading all purchases at once:** Always paginate — fetching 1000+ purchases kills performance and wastes bandwidth
- **Offset pagination for large datasets:** `Skip(page * pageSize).Take(pageSize)` forces database to process all skipped rows; use cursor-based instead
- **Polling without cleanup:** Always pause polling (useInterval) when component unmounts or user navigates away
- **Direct entity exposure from API:** Use DTOs to decouple domain models from API contracts, prevent overexposure of sensitive fields
- **Chart.js without destroy:** Always call `chart.destroy()` on unmount to prevent memory leaks from event listeners
- **SSR-incompatible code in setup:** Use `onMounted` or `<ClientOnly>` for browser-specific APIs (Chart.js, Intersection Observer)

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Infinite scroll detection | Custom scroll event listeners with throttling | VueUse `useInfiniteScroll` | Handles Intersection Observer setup, cleanup, edge cases (rapidly scrolling, multiple triggers), and performance optimizations automatically |
| Countdown timer | `setInterval` with manual state management | VueUse `useInterval` or `useCountdown` | Handles cleanup, pause/resume, reactive state, and avoids time drift issues with setInterval inaccuracy over long periods |
| Chart annotations | Custom canvas drawing logic | chartjs-plugin-annotation | Official plugin handles positioning, scaling, responsiveness, and edge cases (data outside chart bounds) that custom code misses |
| Date range filtering | Custom date input components | `@vuepic/vue-datepicker` or Nuxt UI InputDate | Handles timezone edge cases, locale formatting, accessibility (keyboard nav), and SSR compatibility |
| Loading skeletons | Manual placeholder divs | Nuxt UI Skeleton component | Provides consistent pulse animation, accessibility, and matches design system automatically |

**Key insight:** Dashboard UX depends on smooth real-time updates and infinite scroll. Custom implementations often miss edge cases (unmount cleanup, scroll event spam, time drift) that mature libraries handle. Use VueUse for client-side interactivity, Chart.js ecosystem for charting, and EF Core cursor pagination for backend efficiency.

## Common Pitfalls

### Pitfall 1: Hydration Mismatches from Real-Time Data

**What goes wrong:** Initial SSR render shows stale data, client hydration updates with fresh data, Vue detects mismatch and throws hydration error, forcing full client-side re-render

**Why it happens:** Real-time data (current price, countdown timer) changes between server render time and client hydration time

**How to avoid:** Use `server: false` in useFetch for real-time data, or wrap in `<ClientOnly>` component to skip SSR entirely

**Warning signs:** Console errors like "Hydration node mismatch", flickering content on page load, client data overwriting server HTML

**Example:**
```vue
<!-- Bad: SSR renders stale price, client hydrates with fresh price -->
<div>{{ currentPrice }}</div>

<!-- Good: Client-only rendering for real-time data -->
<ClientOnly>
  <div>{{ currentPrice }}</div>
  <template #fallback>
    <div>Loading price...</div>
  </template>
</ClientOnly>
```

### Pitfall 2: Chart.js Memory Leaks

**What goes wrong:** Chart.js instances persist after component unmounts, event listeners stay attached to canvas, memory usage grows on navigation between pages

**Why it happens:** Chart.js doesn't auto-cleanup, developer forgets to call `.destroy()` on chart instance in `onUnmounted`

**How to avoid:** Always call `chart.destroy()` in onUnmounted lifecycle hook, or use vue-chartjs wrapper which handles cleanup automatically

**Warning signs:** Browser memory usage grows on repeated page visits, DevTools heap snapshots show detached Chart instances, canvas event listeners accumulate

**Example:**
```typescript
// Bad: Chart instance leaks
const createChart = () => {
  const chart = new Chart(ctx, config) // Leaks on unmount
}

// Good: Cleanup on unmount
let chartInstance: Chart | null = null
const createChart = () => {
  if (chartInstance) chartInstance.destroy() // Destroy old chart
  chartInstance = new Chart(ctx, config)
}
onUnmounted(() => {
  if (chartInstance) chartInstance.destroy() // Cleanup
})
```

### Pitfall 3: N+1 Queries in Purchase History

**What goes wrong:** Loading 20 purchases triggers 1 query for purchases + 20 queries for related data (if includes), exponentially worse with 100+ purchases

**Why it happens:** EF Core's `Include()` can cause separate queries per entity if not used correctly, or eager loading isn't specified

**How to avoid:** Use `AsNoTracking()` for read-only queries, avoid unnecessary `Include()` (purchase entity has all needed fields), use projection if only subset of fields needed

**Warning signs:** Database profiler shows dozens of SELECT queries for one endpoint call, response time grows linearly with page size, WARN logs for multiple queries

**Example:**
```csharp
// Bad: Potential N+1 if Purchase has navigations
var purchases = await db.Purchases.ToListAsync();

// Good: AsNoTracking for read-only, no includes needed
var purchases = await db.Purchases
    .AsNoTracking()
    .OrderByDescending(p => p.ExecutedAt)
    .Take(20)
    .ToListAsync();
```

### Pitfall 4: Infinite Scroll Triggering Multiple Times

**What goes wrong:** User scrolls quickly to bottom, Intersection Observer triggers loadMore multiple times before first request completes, duplicate data fetched, UI shows duplicates

**Why it happens:** No loading guard in loadMore function, Intersection Observer callback fires on every intersection event

**How to avoid:** Guard with `loading` flag at start of loadMore, check `hasMore` flag before fetching, disable observer while loading

**Warning signs:** Duplicate purchase cards appear, multiple network requests for same cursor in DevTools, flickering during scroll

**Example:**
```typescript
// Bad: No loading guard
const loadMore = async () => {
  const data = await fetchPurchases() // Triggers multiple times
}

// Good: Loading guard + hasMore check
const loading = ref(false)
const hasMore = ref(true)
const loadMore = async () => {
  if (loading.value || !hasMore.value) return // Guard
  loading.value = true
  try {
    const data = await fetchPurchases()
    hasMore.value = data.hasMore
  } finally {
    loading.value = false // Always reset
  }
}
```

### Pitfall 5: Stale Countdown Timer After Navigation

**What goes wrong:** User navigates away from dashboard, countdown timer interval keeps running in background, causes memory leak and unnecessary CPU usage

**Why it happens:** `setInterval` or `useInterval` not paused/cleared on unmount

**How to avoid:** Always pause interval in `onUnmounted`, VueUse composables return `pause()` function for cleanup

**Warning signs:** Browser performance degrades over time, DevTools shows timers running for unmounted components, CPU usage higher than expected

**Example:**
```typescript
// Bad: Interval leaks
onMounted(() => {
  setInterval(updateCountdown, 1000) // Leaks
})

// Good: Cleanup interval
const { pause } = useInterval(updateCountdown, 1000)
onUnmounted(() => pause()) // Stop interval
```

### Pitfall 6: Timezone Confusion with DateTimeOffset

**What goes wrong:** Backend returns DateTimeOffset in UTC, frontend displays in local time without clarifying timezone, user sees confusing purchase times

**Why it happens:** JavaScript Date automatically converts to local timezone, no explicit formatting to show timezone

**How to avoid:** Use date-fns to format with explicit timezone display, or convert all times to UTC for consistency, show timezone indicator in UI

**Warning signs:** Purchase times off by hours depending on user timezone, confusion about "next buy" countdown

**Example:**
```typescript
// Bad: Ambiguous timezone display
const formatted = new Date(purchase.executedAt).toLocaleString()

// Good: Explicit UTC formatting
import { formatInTimeZone } from 'date-fns-tz'
const formatted = formatInTimeZone(purchase.executedAt, 'UTC', 'yyyy-MM-dd HH:mm:ss zzz')
// Displays: 2026-02-13 14:30:00 UTC
```

## Code Examples

Verified patterns from official sources:

### Nuxt UI Card Component with Stats

```vue
<!-- Source: https://ui.nuxt.com/docs/components/card -->
<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h3 class="text-base font-semibold">Total BTC</h3>
        <UBadge color="primary" variant="subtle">Live</UBadge>
      </div>
    </template>

    <div class="space-y-2">
      <p class="text-3xl font-bold">{{ btcAmount.toFixed(8) }}</p>
      <p class="text-sm text-gray-500">{{ usdValue }} USD</p>
    </div>
  </UCard>
</template>
```

### VueUse Infinite Scroll

```typescript
// Source: https://vueuse.org/core/useinfinitescroll/
import { useInfiniteScroll } from '@vueuse/core'

const el = ref<HTMLElement>()
const data = ref([])

useInfiniteScroll(
  el,
  async () => {
    // Load more items
    const response = await fetch('/api/purchases')
    data.value.push(...response.items)
  },
  { distance: 10 } // Trigger 10px before bottom
)
```

### Chart.js Line Chart with Annotation

```typescript
// Source: https://www.chartjs.org/chartjs-plugin-annotation/latest/guide/
import { Chart } from 'chart.js/auto'
import annotationPlugin from 'chartjs-plugin-annotation'

Chart.register(annotationPlugin)

const chart = new Chart(ctx, {
  type: 'line',
  data: { /* ... */ },
  options: {
    plugins: {
      annotation: {
        annotations: {
          line1: {
            type: 'line',
            yMin: 95000,
            yMax: 95000,
            borderColor: 'rgb(255, 99, 132)',
            borderWidth: 2,
            borderDash: [6, 6], // Dashed line
            label: {
              content: 'Average Cost Basis',
              enabled: true
            }
          }
        }
      }
    }
  }
})
```

### EF Core Cursor Pagination

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying
var query = db.Purchases
    .AsNoTracking()
    .OrderByDescending(p => p.ExecutedAt);

if (!string.IsNullOrEmpty(cursor))
{
    var cursorDate = DateTimeOffset.Parse(cursor);
    query = query.Where(p => p.ExecutedAt < cursorDate);
}

var items = await query.Take(pageSize + 1).ToListAsync();
var hasMore = items.Count > pageSize;
if (hasMore) items.RemoveAt(items.Count - 1);

var nextCursor = hasMore ? items.Last().ExecutedAt.ToString("o") : null;
```

### Nuxt useFetch with Polling

```typescript
// Source: https://nuxt.com/docs/api/composables/use-fetch
const { data, refresh } = useFetch('/api/portfolio', {
  lazy: true,
  server: false // Client-only for real-time data
})

// Poll every 10 seconds
const { pause, resume } = useIntervalFn(refresh, 10000)

onUnmounted(() => pause()) // Stop polling on unmount
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Moment.js for dates | date-fns or native Intl | 2020+ | date-fns is 10x smaller (2kb vs 20kb), tree-shakeable, no need for heavy library for simple formatting |
| Scroll event listeners | Intersection Observer API | 2019+ | Intersection Observer offloads work to browser, async, no scroll event spam, better performance |
| Offset pagination (SKIP/TAKE) | Cursor-based pagination | 2021+ | Cursor pagination avoids O(n) database scan for SKIP, scales to millions of rows, consistent with data changes |
| setInterval for timers | requestAnimationFrame or VueUse composables | 2020+ | RAF is frame-synced and pauses when tab inactive, VueUse handles cleanup and reactive state automatically |
| vue-chartjs 3.x | vue-chartjs 5.x with Chart.js 4 | 2023 | Chart.js 4 has tree-shaking support, better TypeScript types, performance improvements, new features |
| Nuxt 3 useFetch blocking navigation | useFetch with lazy: true | 2023 (Nuxt 3.4+) | lazy: true allows data to load after navigation, improves perceived performance, better UX for slow endpoints |

**Deprecated/outdated:**
- **Moment.js:** Deprecated, replaced by date-fns or native Intl.DateTimeFormat
- **Chart.js 2.x:** Use Chart.js 4.x for modern features and tree-shaking
- **Manual scroll event throttling:** Use Intersection Observer API instead
- **EF Core Include() everywhere:** Use AsNoTracking() for read-only, projections (Select) for subset of fields

## Open Questions

1. **Real-time price updates: WebSocket or polling?**
   - What we know: Backend doesn't have WebSocket endpoint yet, polling is simpler to implement
   - What's unclear: Is 10-second polling acceptable for "live" price, or do we need sub-second updates?
   - Recommendation: Start with 10-second polling (`useIntervalFn` with 10000ms), can upgrade to WebSocket in later phase if needed

2. **Purchase marker click behavior**
   - What we know: User wants green "B" badges on chart at purchase points
   - What's unclear: Should clicking a marker show purchase details in tooltip/modal, or just static markers?
   - Recommendation: Start with static markers, add click interaction only if user requests it (keeps scope small)

3. **Date range filter: calendar UI or preset buttons?**
   - What we know: User specified "date range filter only" but didn't specify UI
   - What's unclear: Preset buttons (Last 7 days, Last 30 days) or calendar picker?
   - Recommendation: Start with preset buttons (simpler), add calendar picker if user needs custom ranges

4. **Portfolio stats refresh strategy**
   - What we know: Stats need to be "live" but unclear if every stat updates at same rate
   - What's unclear: Should Total BTC update on every purchase (event-driven), or polling (every 10s)?
   - Recommendation: Polling for simplicity (consistent with price updates), event-driven adds complexity for minimal benefit

## Sources

### Primary (HIGH confidence)

- [Nuxt UI Components Documentation](https://ui.nuxt.com/docs/components) - Card, Badge, Skeleton, InputDate components
- [Chart.js Documentation](https://www.chartjs.org/docs/latest/charts/line.html) - Line chart configuration
- [chartjs-plugin-annotation](https://www.chartjs.org/chartjs-plugin-annotation/latest/guide/) - Annotation types and configuration
- [VueUse Documentation](https://vueuse.org/core/useinfinitescroll/) - useInfiniteScroll, useInterval composables
- [Microsoft Learn - EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) - AsNoTracking, cursor pagination
- [Nuxt Data Fetching](https://nuxt.com/docs/api/composables/use-fetch) - useFetch with lazy and server options
- [Vue.js Performance Guide](https://vuejs.org/guide/best-practices/performance) - Reactivity overhead and optimization

### Secondary (MEDIUM confidence)

- [Best Chart Libraries for Vue Projects in 2026](https://weavelinx.com/best-chart-libraries-for-vue-projects-in-2026/) - Chart.js vs ApexCharts vs ECharts comparison
- [Vue 3 Infinite Scroll Best Practices](https://learnvue.co/articles/vue-infinite-scrolling) - Intersection Observer implementation
- [.NET Minimal API Pagination Best Practices](https://code-maze.com/paging-aspnet-core-webapi/) - Cursor vs offset pagination
- [Nuxt 3 Real-Time Updates Guide](https://krutiepatel.com/blog/30-real-time-with-nuxt-3-a-guide-to-websocket-integration) - Polling vs WebSocket vs SSE
- [Fixing Hydration Errors in Nuxt](https://masteringnuxt.com/blog/fixing-hydration-errors-in-nuxt-a-practical-guide) - SSR hydration mismatch solutions
- [EF Core N+1 Queries Problem](https://programmingpulse.vercel.app/blog/solving-the-n1-problem-in-entity-framework-core) - Include pitfalls

### Tertiary (LOW confidence)

- Community discussions on Chart.js memory leaks - GitHub issues confirm .destroy() is required
- Vue 3 countdown timer libraries - Multiple approaches exist, VueUse is most consistent with ecosystem

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Nuxt UI already installed, Chart.js is industry standard, VueUse is Vue ecosystem standard
- Architecture: HIGH - Patterns verified from official docs (Nuxt, Vue, Chart.js, EF Core), matches project conventions
- Pitfalls: HIGH - Hydration, N+1 queries, memory leaks documented in official sources and GitHub issues

**Research date:** 2026-02-13
**Valid until:** 2026-03-15 (30 days - stack is stable, no major updates expected)

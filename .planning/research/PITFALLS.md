# Domain Pitfalls: Web Dashboard for Trading Bot

**Domain:** Web dashboard for DCA trading bot
**Researched:** 2026-02-13

## Critical Pitfalls

Mistakes that cause rewrites, security breaches, or major UX issues.

### Pitfall 1: Building Config Editing Before Proving Dashboard Value
**What goes wrong:** Phase 1 includes editable configuration (PUT /api/config, hot-reload logic, form validation). User edits DCA amount, tiers, or schedule from UI. Backend needs to reload appsettings.json or invalidate IOptionsMonitor cache. This is 3-5 days of work (backend endpoint + validation + hot-reload + testing).

**Why it happens:** "Users want to edit config from UI" sounds like a table stakes feature. But research shows **users primarily need transparency first** (see portfolio, verify purchases), not management (edit settings).

**Consequences:** Delays MVP by ~5 days. Adds backend complexity (config file watching, IOptionsMonitor invalidation). Risk of bugs (partial config updates, validation bypass). Users already edit appsettings.json successfully today.

**Prevention:**
- Phase 1: Read-only config view. Display current `DcaOptions` from GET /api/config.
- Phase 4 (later): If users request editing frequently, add PUT /api/config with validation + restart prompt.
- Mitigation: "Edit config" button links to appsettings.json with instructions.

**Detection:** If planning Phase 1 includes "config form" or "PUT endpoint," stop and defer to Phase 4.

### Pitfall 2: Storing API Keys in Frontend Code or Git
**What goes wrong:** Hardcoding API key in Nuxt source (`const API_KEY = "abc123"`) or committing `.env` to git. API key is exposed in browser source, git history, or deployed client bundle.

**Why it happens:** Convenience during development. Forgetting to add `.env` to `.gitignore`. Not understanding that Nuxt `.env` is build-time (still baked into bundle).

**Consequences:** **Security breach**. Anyone with browser DevTools can extract API key and access backend. If key is in git history, rotating doesn't help (historical commits still exposed).

**Prevention:**
- Use `.env.local` for secrets (gitignored by default in Nuxt).
- API key auth in HTTP header: `X-API-Key: <key>`.
- Backend validates key, returns 403 if missing/wrong.
- Never use query params (`/api/data?key=abc`) -- logged in access logs.
- Phase 4: Upgrade to JWT with HTTP-only cookie for session management.

**Example (.env.local, gitignored):**
```bash
NUXT_PUBLIC_API_KEY=your-secret-key-here
```

**Detection:** Run `git log --all -S "api" --` to search for API key in git history. Check browser Network tab for exposed keys.

### Pitfall 3: N+1 Query Problem in Portfolio Aggregation
**What goes wrong:** Backend loads purchase summary, then queries each purchase's details in separate DB calls. 100 purchases = 100+ queries, 2-5 second response time.

**Why it happens:** Using ORM `.Include()` incorrectly or looping over purchases to calculate totals.

**Consequences:** Slow API responses (>1s), poor UX, database connection exhaustion at scale.

**Prevention:**
- Use aggregation query with `GroupBy().Select()`:
```csharp
var summary = await db.Purchases
    .Where(p => p.Status == PurchaseStatus.Filled)
    .GroupBy(p => 1)  // Single group
    .Select(g => new {
        TotalBtc = g.Sum(p => p.Quantity),
        TotalCost = g.Sum(p => p.Cost),
        Count = g.Count()
    })
    .FirstOrDefaultAsync();
```
- Single query, ~10ms for 1000 purchases.

**Detection:** Enable EF Core query logging (`Microsoft.EntityFrameworkCore.Database.Command: Information`). Check for >3 SELECT queries per API call.

### Pitfall 4: Polling Live Price Too Aggressively
**What goes wrong:** Frontend polls GET /api/price every 100ms (10 req/s). Backend hammers Hyperliquid API. Rate limited, temp ban, or degraded performance.

**Why it happens:** "Real-time" sounds like it needs sub-second updates. BTC price changes are visible at 1s intervals.

**Consequences:** Hyperliquid API rate limit exceeded. Backend errors propagate to frontend. User sees stale prices or error states.

**Prevention:**
- Poll at 5-10 second intervals: `useIntervalFn(() => refresh(), 5000)` (VueUse).
- Use SignalR/SSE for true push (Phase 4).
- Cache price on backend for 5s: `[ResponseCache(Duration = 5)]` or IMemoryCache.

**Example (frontend):**
```typescript
const { data: price, refresh } = useFetch('/api/price', { server: false })
const { pause, resume } = useIntervalFn(refresh, 5000)  // 5s polling
onUnmounted(() => pause())
```

**Detection:** Check browser Network tab. If >1 request per second to `/api/price`, reduce interval.

### Pitfall 5: Not Implementing Pagination for Purchase History
**What goes wrong:** GET /api/purchases returns ALL purchases (1000+ records) in single response. 500KB+ JSON, 3-5s load time, browser hangs rendering 1000 table rows.

**Why it happens:** "Pagination is complex, just load all data" works for 10 records but breaks at scale.

**Consequences:** Slow page loads, browser memory pressure, poor UX. User abandons dashboard.

**Prevention:**
- Server-side pagination with offset/limit:
```csharp
var items = await db.Purchases
    .OrderByDescending(p => p.ExecutedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```
- Frontend pagination controls (Nuxt UI `<UPagination>`).
- Page size: 50-100 records.
- Add index: `CREATE INDEX idx_purchase_executed ON Purchase(ExecutedAt DESC)`.

**Detection:** Load /purchases page. If >1s load time or >100KB response, add pagination.

## Moderate Pitfalls

### Pitfall 6: No Error States in UI
**What goes wrong:** API call fails (network error, 500 backend error). Frontend shows loading spinner forever or blank screen.

**Why it happens:** useFetch error handling not implemented. No retry logic or fallback UI.

**Prevention:**
- Show error state: `if (error.value) return <ErrorMessage />`.
- Retry button: `<button @click="refresh()">Retry</button>`.
- Toast notification for transient errors.

**Example:**
```vue
<template>
  <div v-if="pending">Loading...</div>
  <div v-else-if="error">
    <p>Failed to load portfolio: {{ error.message }}</p>
    <button @click="refresh()">Retry</button>
  </div>
  <PortfolioCard v-else :data="data" />
</template>
```

### Pitfall 7: Chart Rendering Performance with Large Datasets
**What goes wrong:** Equity curve backtest over 4 years = 1,460 daily points. Rendering all points in SVG chart freezes browser for 2-3 seconds.

**Why it happens:** Using SVG-based chart library (D3, ApexCharts) for large timeseries. SVG renders every point as DOM element.

**Prevention:**
- Use canvas-based library: **lightweight-charts** (2kb, TradingView, 60fps).
- Downsample if >1000 points: show every Nth point (e.g., weekly instead of daily for 4-year view).
- Lazy-load charts: only render when tab/section is visible.

### Pitfall 8: Not Handling Timezone Confusion
**What goes wrong:** Backend stores timestamps in UTC. Frontend displays them in user's local time without indication. User sees "Purchase at 2:00 AM" when they scheduled 10:00 AM UTC.

**Why it happens:** `new Date()` auto-converts to local time. Not showing timezone.

**Prevention:**
- Backend: Always store/return `DateTimeOffset` with UTC (`executedAt: "2026-02-13T10:00:00Z"`).
- Frontend: Use dayjs with timezone plugin or format with `Intl.DateTimeFormat`.
- Display: "Feb 13, 2026 10:00 AM UTC" or "Feb 13, 2026 3:00 AM PST".

**Example:**
```typescript
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'
dayjs.extend(utc)
dayjs.extend(timezone)

const formatted = dayjs(purchase.executedAt).tz(dayjs.tz.guess()).format('MMM D, YYYY h:mm A z')
// "Feb 13, 2026 3:00 AM PST"
```

### Pitfall 9: CORS Misconfiguration (Wildcard Origins)
**What goes wrong:** Backend allows `Access-Control-Allow-Origin: *` in production. Any website can call your API from browser JavaScript.

**Why it happens:** Copy-paste from StackOverflow. "Works in dev" becomes "left in prod."

**Consequences:** **Security risk**. Malicious site can steal user data if they visit while logged into your dashboard.

**Prevention:**
- Dev: `WithOrigins("http://localhost:3000")`.
- Prod: `WithOrigins("https://yourdomain.com")`.
- Never use `AllowAnyOrigin()` in production.

### Pitfall 10: Overusing Pinia for Data That Belongs in useFetch Cache
**What goes wrong:** Every API call result stored in Pinia store. Duplicated state (TanStack Query cache + Pinia store). Manual cache invalidation logic.

**Why it happens:** "Global state" sounds like it needs Pinia. But **server state** (API data) is already managed by TanStack Query.

**Prevention:**
- Use Pinia only for **client state**: UI toggles, theme, sidebar open/closed, form draft.
- Use TanStack Query for **server state**: portfolio, purchases, backtest results.
- Rule: If data comes from API, use TanStack Query. If data is UI-only, use Pinia.

### Pitfall 11: Not Testing with Realistic Data Volume
**What goes wrong:** Dashboard tested with 5 purchases. Works great. Deployed with 500 purchases. Pagination breaks, charts freeze, queries timeout.

**Why it happens:** Local dev with fresh DB. No seed data script.

**Prevention:**
- Seed script: Insert 1000+ mock purchases spanning 2 years.
- Test cases: Empty state (0 purchases), moderate (100), large (1000+).
- Performance budget: <500ms API response, <2s page load.

## Minor Pitfalls

### Pitfall 12: Inconsistent Date Formatting
**What goes wrong:** Purchase history shows "2/13/2026", backtest shows "13-Feb-2026", portfolio shows "Feb 13".

**Why it happens:** Using mix of `toLocaleDateString()`, dayjs, and hardcoded formats.

**Prevention:**
- Create date formatting util:
```typescript
// utils/dates.ts
export const formatDate = (date: string | Date) =>
  dayjs(date).format('MMM D, YYYY')  // "Feb 13, 2026"
export const formatDateTime = (date: string | Date) =>
  dayjs(date).format('MMM D, YYYY h:mm A')  // "Feb 13, 2026 10:00 AM"
```
- Use consistently across app.

### Pitfall 13: No Loading Skeletons
**What goes wrong:** Portfolio page shows blank white screen for 2 seconds while data loads.

**Why it happens:** No loading state UI, only spinner or blank.

**Prevention:**
- Use Nuxt UI `<USkeleton>` during pending state:
```vue
<UCard v-if="pending">
  <USkeleton class="h-32" />
</UCard>
<PortfolioCard v-else :data="data" />
```

### Pitfall 14: Ignoring Mobile Responsiveness
**What goes wrong:** Dashboard looks great on desktop (1920x1080). On phone, table overflows, buttons overlap, charts unreadable.

**Why it happens:** Desktop-first development. No mobile testing.

**Prevention:**
- Use Tailwind responsive utilities: `md:flex-row flex-col`, `lg:text-2xl text-lg`.
- Test in Chrome DevTools mobile view (iPhone SE, iPad).
- Table: Horizontal scroll on mobile: `<div class="overflow-x-auto"><UTable /></div>`.

## Phase-Specific Warnings

| Phase | Likely Pitfall | Mitigation |
|-------|----------------|------------|
| **Phase 1: View-Only Dashboard** | API key exposure (#2), N+1 queries (#3), no pagination (#5) | Use .env.local, aggregation queries, paginate from day 1 |
| **Phase 2: Backtest Integration** | Chart performance (#7), large JSON responses | Use lightweight-charts (canvas), omit purchase log for sweeps >100 combos |
| **Phase 3: Enhanced Insights** | Timezone confusion (#8), date format inconsistency (#12) | Use dayjs with UTC plugin, single format util |
| **Phase 4: Interactive Management** | Premature config editing (#1), CORS wildcard (#9) | Defer to Phase 4, whitelist specific origins |

## Sources

- [Crypto Bot UI/UX Design: Best Practices](https://www.companionlink.com/blog/2025/01/crypto-bot-ui-ux-design-best-practices/)
- [9 Common Crypto Trading Mistakes To Avoid](https://trakx.io/resources/insights/9-common-crypto-trading-mistakes-to-avoid/)
- [Common Crypto Trading Mistakes That Lose Money](https://www.asturati.com/2026/02/common-crypto-trading-mistakes-that.html)
- [Risk Management Settings for AI Trading Bots](https://3commas.io/blog/ai-trading-bot-risk-management-guide)
- [ASP.NET Core CORS Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/cors)

# Project Research Summary

**Project:** BTC Smart DCA Bot - v1.2 Web Dashboard
**Domain:** Trading bot web dashboard with portfolio tracking, backtest visualization, and bot monitoring
**Researched:** 2026-02-13
**Confidence:** HIGH

## Executive Summary

The v1.2 Web Dashboard adds a monitoring and analytics interface to an existing .NET 10 DCA bot. Research shows the winning pattern is **view-only transparency first, interactive management later**. Users need to see their portfolio, verify bot purchases, and validate the strategy through backtest visualization before they need configuration editing. The recommended stack is Nuxt 4 + TanStack Query + lightweight-charts + SignalR, matching modern 2026 best practices for Vue 3 financial dashboards.

The architecture follows a clean frontend-consuming-APIs pattern with strict separation: Nuxt handles presentation, .NET handles business logic and persistence. Critical findings show most backend pieces already exist (Purchase model, backtest API, health checks). Only 5 new endpoints needed for MVP: portfolio aggregation, live price, paginated purchases, config view, and bot status. The existing backtest infrastructure provides immediate differentiation through interactive equity curve visualization.

Three critical risks emerged: (1) premature configuration editing adds 5+ days of complexity with minimal MVP value, (2) API key exposure if stored in frontend code or git, and (3) N+1 query problems causing 2-5s response times for portfolio aggregation. All three are preventable with standard patterns: defer editing to Phase 4, use .env.local for secrets with backend validation, and use EF Core aggregation queries instead of loops.

## Key Findings

### Recommended Stack

The research converged on **Nuxt 4** as the clear choice for 2026. Nuxt 3 reaches EOL July 31, 2026, and Nuxt 4 (stable since July 2025) provides first-class TypeScript, Vue 3.5+ optimizations, and auto-imports. This eliminates the Vite + Vue SPA alternative which requires manual configuration and routing setup.

**Core technologies:**
- **Nuxt 4 (4.3+)**: Vue 3 meta-framework with SSR/SPA modes — industry standard, file-based routing, auto-imports, replaces manual Vite setup
- **TanStack Query (5.x)**: Server state management for ALL API calls — eliminates manual cache logic, automatic refetch, loading/error states, proven at scale (Vercel, Clerk)
- **lightweight-charts (4.x)**: TradingView's canvas-based charting for equity curves — 2kb vs ApexCharts 320kb, 60fps rendering, purpose-built for financial time-series
- **Nuxt UI (4.x)**: 125+ accessible components, Tailwind-based — saves 2-3 weeks of component work, unified UI+Pro (free, MIT), AI-optimized with MCP
- **SignalR (@microsoft/signalr 8.x)**: Real-time price updates — native .NET integration, WebSocket with SSE/polling fallback, matches backend version
- **Zod + VeeValidate (3.x + 4.x)**: Schema validation and forms — type-safe validation with TypeScript inference, auto error messages for config forms (Phase 4)

**Supporting libraries:** Pinia (client state only, not server data), VueUse (browser APIs and utilities), dayjs (date formatting, 2kb Moment.js replacement), @nuxt/icon (200k+ SVG icons).

**Critical version requirements:** Nuxt 4 requires Vue 3.5+, VueUse 14.x requires Vue 3.5+, SignalR 8.x matches .NET 10.

### Expected Features

Research identified 7 table stakes features and 9 differentiators. The MVP insight: **users need transparency before control**. Portfolio overview, purchase history, and live price are non-negotiable. Backtest visualization is a major differentiator leveraging existing backend infrastructure.

**Must have (table stakes):**
- Portfolio Overview — total BTC, cost basis, P&L, avg cost (backend: Purchase model ready)
- Purchase History List — paginated table with date, price, quantity, multiplier, status (backend: Purchase model ready)
- Live BTC Price — WebSocket or polling for context (backend: Hyperliquid client exists)
- Bot Status Indicator — health check status, last successful purchase (backend: health endpoint exists)
- Configuration View (read-only) — display current DCA settings (backend: DcaOptions exists)
- Basic Auth/Security — API key middleware to protect portfolio data (backend: needs implementation)

**Should have (differentiators):**
- Interactive Backtest Visualization — equity curve charts comparing smart vs fixed DCA (backend: backtest API returns daily logs)
- Parameter Comparison Charts — visual ranking of sweep results (backend: sweep API ready)
- Purchase Timeline Chart — see multiplier triggers correlated with price drops (backend: Purchase model has all metadata)
- Multiplier Reasoning Display — explain why specific multiplier used (backend: Purchase has tier, drop%, high/MA fields)
- Next Buy Countdown — time until next scheduled purchase (backend: schedule in DcaOptions)
- Walk-Forward Overfitting Indicator — flag overfit backtest params (backend: sweep API includes validation)

**Defer (Phase 4+):**
- Editable Configuration — convenience feature requiring PUT endpoint + validation + hot-reload (5+ days complexity)
- Real-Time Notifications — WebSocket/SSE for purchase events (Telegram already covers this)
- Weekly Summary Dashboard — aggregated metrics by week (requires analytics queries)

**Anti-features (explicitly avoid):**
- Sell/Take-Profit Controls — out of scope, accumulation-only bot
- Manual Buy Button — defeats DCA discipline, encourages emotional trading
- Multi-Asset Support — BTC only per requirements
- Leverage/Margin Controls — spot only, no perps/futures

### Architecture Approach

The architecture follows **frontend-consuming-backend-APIs with strict read-only separation for MVP**. Nuxt pages consume composables, composables use TanStack Query for data fetching, queries hit new dashboard endpoints on .NET backend. Backend endpoints aggregate Purchase/DailyPrice data and proxy Hyperliquid price. Zero changes to existing bot logic or domain events.

**Major components:**
1. **Nuxt Composables (usePortfolio, usePurchases, useBtcPrice, useBacktest)** — encapsulate ALL API calls with caching, expose reactive refs to components
2. **Dashboard Endpoints (.NET, GET only for MVP)** — 5 new routes: /api/portfolio (aggregate purchases), /api/price (proxy Hyperliquid), /api/purchases (paginated list), /api/config (read DcaOptions), /api/status (health + next buy)
3. **API Key Middleware (.NET)** — validates X-API-Key header, returns 403 if missing/wrong, configured via Dashboard:ApiKey in appsettings
4. **TanStack Query Cache** — client-side cache for API responses with automatic background refetch and stale-while-revalidate pattern
5. **lightweight-charts Wrapper** — Vue component wrapping TradingView chart for equity curves, candlesticks with purchase markers

**Data flow pattern (Portfolio Overview example):**
```
User loads / page
→ Nuxt SSR renders page
→ usePortfolio() composable
→ TanStack Query useFetch('/api/portfolio')
→ Dashboard GET /api/portfolio endpoint
→ EF Core aggregation: SELECT SUM(Cost), SUM(Quantity) FROM Purchase WHERE Status = 'Filled'
→ Hyperliquid: GET current BTC price
→ Calculate: totalBtc, totalCost, currentValue, unrealizedPnL
→ Return JSON
→ TanStack Query caches response (60s TTL)
→ Vue renders <PortfolioCard> component
```

**Key patterns:**
- Composable-first data fetching (never fetch in components directly)
- Server-side pagination with offset/limit (page size 50-100)
- Aggregation queries with GroupBy().Select() (single query, <10ms)
- API key in HTTP header (X-API-Key), never query params
- Polling for live price at 5-10s intervals (not 100ms)

### Critical Pitfalls

Research identified 14 pitfalls across critical/moderate/minor severity. Top 5 are phase blockers or security risks:

1. **Building Config Editing Before Proving Dashboard Value** — Phase 1 including PUT /api/config + hot-reload + form validation delays MVP by 5+ days. Users need transparency first (see portfolio, verify purchases), not management (edit settings). **Prevention:** Read-only config view in Phase 1, defer editing to Phase 4 after dashboard proven useful. If users request editing frequently, add PUT endpoint then.

2. **Storing API Keys in Frontend Code or Git** — Hardcoding API key in Nuxt source or committing .env to git exposes key in browser DevTools and git history. **Security breach.** **Prevention:** Use .env.local (gitignored), API key in X-API-Key header validated by backend middleware, never query params. Phase 4 upgrade to JWT with HTTP-only cookie.

3. **N+1 Query Problem in Portfolio Aggregation** — Loading purchase summary then looping over purchases for totals = 100+ queries, 2-5s response time. **Prevention:** Use EF Core aggregation query with GroupBy().Select() — single query, ~10ms for 1000 purchases. Enable EF Core query logging to detect >3 SELECT queries per API call.

4. **Polling Live Price Too Aggressively** — Frontend polling /api/price every 100ms (10 req/s) hammers Hyperliquid API, causes rate limiting. **Prevention:** Poll at 5-10s intervals using VueUse useIntervalFn, cache price on backend for 5s with IMemoryCache, upgrade to SignalR push in Phase 4.

5. **Not Implementing Pagination for Purchase History** — Returning ALL purchases (1000+) in single response = 500KB+ JSON, 3-5s load time, browser hangs rendering table. **Prevention:** Server-side pagination with Skip/Take, page size 50-100, add index on (ExecutedAt DESC), frontend pagination controls with Nuxt UI.

**Moderate pitfalls:**
- No error states in UI (show retry button, toast notifications)
- Chart performance with large datasets (use canvas-based lightweight-charts, downsample if >1000 points)
- Timezone confusion (store UTC, display with timezone indicator using dayjs)
- CORS wildcard origins (whitelist specific origins, never `AllowAnyOrigin()` in production)

**Phase-specific warnings:**
- Phase 1 (View-Only): API key exposure, N+1 queries, no pagination
- Phase 2 (Backtest Integration): Chart performance, large JSON responses
- Phase 3 (Enhanced Insights): Timezone confusion, date format inconsistency
- Phase 4 (Interactive Management): Premature config editing, CORS misconfiguration

## Implications for Roadmap

Based on research, suggested **4-phase structure prioritizing transparency over management**. Phase 1-2 leverage existing backend (zero new infrastructure), Phase 3 adds visual insights, Phase 4 defers interactive features until dashboard proven valuable.

### Phase 1: View-Only Dashboard (MVP)
**Rationale:** Covers all table stakes, proves value (portfolio tracking), establishes trust (transparency + health), requires ZERO backend infrastructure changes (read-only using existing Purchase/DailyPrice data). Gets dashboard in users' hands fastest to validate core value proposition: "see what the bot is doing."

**Delivers:**
- Portfolio overview (total BTC, cost basis, P&L, avg cost)
- Purchase history list (paginated table with multiplier reasoning)
- Live BTC price (polling at 5s intervals)
- Bot status indicator (health + last purchase)
- Configuration view (read-only DcaOptions)
- Basic auth (API key middleware)

**Backend work:** 5 new GET endpoints (portfolio aggregation, price proxy, paginated purchases, config view, status), API key middleware, CORS configuration. All read-only, no domain logic changes.

**Addresses features:** Portfolio Overview, Purchase History, Live Price, Bot Status, Config View (all table stakes from FEATURES.md)

**Avoids pitfalls:** Premature config editing (#1), implements pagination from day 1 (#5), uses aggregation queries (#3), 5-10s polling (#4), API key in .env.local (#2)

**Research flags:** SKIP research-phase — standard dashboard patterns, well-documented. TanStack Query + Nuxt UI examples plentiful.

### Phase 2: Backtest Integration
**Rationale:** Leverages existing backtest API (POST /api/backtest, /api/backtest/sweep) to provide validation of strategy. Major differentiator: interactive equity curve visualization shows "why smart DCA works" visually. Medium complexity (charting), high value (trust building).

**Delivers:**
- Backtest results view (metrics table: total BTC, cost basis, return %, vs fixed DCA)
- Interactive backtest visualization (equity curve chart comparing strategies)
- Data freshness indicator (display last ingestion timestamp)
- Walk-forward overfitting indicator (badge/warning on sweep results)

**Backend work:** ZERO — backtest API already returns daily purchase logs and sweep results. Possibly add GET /api/backtest/data/status to expose ingestion metadata.

**Uses stack:** lightweight-charts (canvas-based, 60fps), TanStack Query mutation for POST /api/backtest

**Implements architecture:** Chart wrapper component, equity curve line series, purchase markers

**Avoids pitfalls:** Chart performance with large datasets (#7) — lightweight-charts is canvas-based, handles 1000+ points smoothly

**Research flags:** SKIP research-phase — TradingView lightweight-charts has official Vue tutorial, backtest API response format already known

### Phase 3: Enhanced Insights
**Rationale:** Improves user understanding of bot behavior through visual patterns and contextual explanations. Differentiates from basic portfolio trackers. Medium complexity (timeline charts + aggregation), moderate value (insight > control). Builds on Phase 1+2 data.

**Delivers:**
- Purchase timeline chart (candlestick + markers sized by multiplier)
- Multiplier reasoning display (per-purchase breakdown: tier matched, bear boost, drop% from high)
- Next buy countdown (timer based on DailyBuyHour/Minute config)
- Parameter comparison charts (bar/scatter charts ranking sweep results)

**Backend work:** ZERO — all data exists in Purchase model (MultiplierTier, DropPercentage, High30Day, Ma200Day) and DcaOptions (schedule). Possibly add GET /api/next-buy endpoint for countdown.

**Addresses features:** Purchase Timeline Chart, Multiplier Reasoning, Next Buy Countdown, Parameter Comparison (all differentiators from FEATURES.md)

**Avoids pitfalls:** Timezone confusion (#8) — use dayjs with UTC plugin, display "Feb 13, 2026 10:00 AM UTC". Date format consistency (#12) — single formatDate/formatDateTime util.

**Research flags:** SKIP research-phase — standard charting patterns, Purchase model metadata fully documented in codebase

### Phase 4: Interactive Management (Deferred)
**Rationale:** High complexity (backend changes + validation + hot-reload), moderate value (convenience > necessity). Defer until dashboard proven valuable through Phase 1-3 usage. Only build if users frequently request config editing (currently they edit appsettings.json successfully).

**Delivers:**
- Editable configuration (form to edit base amount, schedule, tiers, bear boost)
- Real-time notifications (toast/alert when new purchase executes via WebSocket)
- Weekly summary dashboard (aggregate metrics by week: total spent, BTC accumulated, avg multiplier)

**Backend work:** HIGH effort — PUT /api/config endpoint with validation + IOptionsMonitor invalidation or app restart handling, SignalR hub for purchase events (BotStatusHub.BroadcastPurchaseEvent), weekly aggregation queries or materialized view.

**Uses stack:** VeeValidate + Zod (form validation with TS inference), SignalR (WebSocket for real-time events), Pinia (client state for form drafts)

**Implements architecture:** SignalR connection composable (useBotStatus with auto-reconnect), config form with optimistic updates, weekly aggregation queries

**Avoids pitfalls:** CORS wildcard (#9) — whitelist specific origins for WebSocket connections. Pinia overuse (#10) — use only for form draft state, not API data.

**Research flags:** NEEDS research-phase — SignalR integration pattern (.NET Hub → Nuxt composable) needs validation, config hot-reload strategy (IOptionsMonitor.OnChange vs app restart) needs design decision.

### Phase Ordering Rationale

- **Phase 1 first:** Zero backend infrastructure risk, leverages existing data models, proves core value (transparency). Users see immediate benefit: portfolio tracking + purchase verification.

- **Phase 2 builds on Phase 1:** Backtest visualization uses same TanStack Query + lightweight-charts stack established in Phase 1. Backtest API already exists (no backend work). Provides validation ("strategy works") before users need management features.

- **Phase 3 enhances understanding:** Timeline charts and multiplier reasoning deepen user insights into bot behavior. Depends on Purchase model metadata already exposed in Phase 1. Low backend effort (possibly 1 GET endpoint for next-buy countdown), high educational value.

- **Phase 4 deferred until proven need:** Config editing requires significant backend work (PUT endpoint + validation + hot-reload) with unclear value (users edit appsettings.json today). Real-time notifications are nice-to-have (Telegram already covers this). Weekly summary is analytics, not core monitoring. Build only if Phase 1-3 usage reveals strong demand.

- **Dependency flow:** Phase 1 establishes composable + TanStack Query patterns → Phase 2 extends with charting → Phase 3 adds insights using same patterns → Phase 4 adds interactivity only if validated. No phase blocks another; all can proceed independently.

- **Pitfall avoidance:** Ordering prevents premature optimization (#1). Phase 1 validates dashboard value before investing 5-7 days in config editing. Phase 2-3 keep complexity low (read-only) while building user trust. Phase 4 only built if ROI proven.

### Research Flags

**Phases with standard patterns (SKIP research-phase):**
- **Phase 1 (View-Only Dashboard):** Well-documented patterns — TanStack Query + Nuxt UI + EF Core aggregation queries. Examples plentiful in Nuxt ecosystem. API key middleware is ASP.NET Core standard.
- **Phase 2 (Backtest Integration):** TradingView lightweight-charts has official Vue tutorial. Equity curve charting is standard financial visualization. Backtest API response format already known from existing implementation.
- **Phase 3 (Enhanced Insights):** Purchase timeline chart is lightweight-charts candlestick series + markers (documented). Next buy countdown is simple timer logic (VueUse useIntervalFn). Parameter comparison is bar chart (standard).

**Phases likely needing deeper research:**
- **Phase 4 (Interactive Management):** SignalR integration pattern needs validation — how to structure .NET Hub, how to consume in Nuxt composable, reconnection handling. Config hot-reload strategy needs design decision: IOptionsMonitor.OnChange with in-memory update vs app restart prompt. Form validation for DCA tiers (nested arrays) needs Zod schema design.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All versions verified from official 2026 sources. Nuxt 4 stable since July 2025. TanStack Query proven at scale (Vercel, Clerk). lightweight-charts is TradingView official lib (used by Binance, Coinbase). SignalR matches .NET 10 backend. |
| Features | HIGH | 7 table stakes features identified from 5+ DCA bot dashboard comparisons (3Commas, Crypto.com, DappFort). 9 differentiators validated against crypto portfolio tracker apps (Coinbureau, Bitsgap). MVP recommendation (view-only first) supported by UI/UX best practices research. |
| Architecture | HIGH | Frontend-consuming-APIs pattern is standard for trading dashboards (Mevx, 3Commas). Composable-first data fetching is Nuxt 3 best practice. TanStack Query for server state is ecosystem standard. Backend endpoint design (lean, focused) matches .NET Minimal API patterns. |
| Pitfalls | HIGH | Critical pitfalls validated from multiple sources: premature optimization (UI/UX best practices), API key exposure (ASP.NET Core security docs), N+1 queries (EF Core performance docs), aggressive polling (Hyperliquid rate limits), pagination (standard practice at 1000+ records). |

**Overall confidence:** HIGH

### Gaps to Address

**Backend endpoint pagination details:** Research recommends page size 50-100, but optimal size depends on actual Purchase record size (number of fields returned). Validate during Phase 1 implementation: measure response payload at 50/100/200 page sizes, choose based on <100ms query time and <50KB response.

**SignalR reconnection UX (Phase 4):** Research shows SignalR has automatic reconnection, but doesn't specify UX during reconnection (show "connecting..." indicator? buffer missed events?). Needs design decision during Phase 4 planning: silently reconnect vs show connection status badge.

**Chart downsampling threshold (Phase 2):** lightweight-charts handles 1000+ points smoothly, but exact threshold where downsampling needed depends on browser performance. Research suggests >1000 points, but validate during Phase 2 with 4-year backtest (1,460 daily points). Measure render time, downsample only if >2s initial render.

**Config hot-reload strategy (Phase 4):** Research identifies IOptionsMonitor.OnChange as pattern, but doesn't specify if in-memory reload works for all DcaOptions fields (schedule changes might need restart for cron job reconfiguration). Needs design decision during Phase 4: optimistic in-memory update vs prompt user to restart bot.

## Sources

### Primary (HIGH confidence)
- [Nuxt 4.0 Release Announcement](https://nuxt.com/blog/v4) — Nuxt 4 features, Vue 3.5 requirement, release date (July 2025)
- [Nuxt Lifecycle - endoflife.date](https://endoflife.date/nuxt) — Nuxt 3 EOL July 31, 2026
- [TanStack Query Vue Docs](https://tanstack.com/query/v5/docs/framework/vue/overview) — Official API, caching, mutations, SSR
- [TradingView Lightweight Charts Vue Tutorial](https://tradingview.github.io/lightweight-charts/tutorials/vuejs/wrapper) — Official wrapper pattern
- [ASP.NET Core CORS Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0) — Official CORS configuration
- [Nuxt UI Documentation](https://ui.nuxt.com/) — Component library, Tailwind-based
- [Pinia Documentation](https://pinia.vuejs.org/) — Official Vue 3 store
- [VueUse Documentation](https://vueuse.org/) — Composition utilities, browser APIs

### Secondary (MEDIUM confidence)
- [Best Chart Libraries for Vue 2026 - Weavelinx](https://weavelinx.com/best-chart-libraries-for-vue-projects-in-2026/) — lightweight-charts recommendation, 2kb vs ApexCharts 320kb
- [SignalR + Vue.js Integration - Medium](https://medium.com/@simo.matijevic/real-time-communication-with-signalr-integrating-net-and-vue-js-2b0522904c67) — .NET Hub to Vue composable pattern
- [Crypto Bot UI/UX Design Best Practices - CompanionLink](https://www.companionlink.com/blog/2025/01/crypto-bot-ui-ux-design-best-practices/) — Transparency before control, error states, mobile-first
- [DCA Bot Guide - DappFort](https://www.dappfort.com/blog/dca-trading-bot-development/) — Portfolio overview, purchase history, backtest validation as table stakes
- [3Commas DCA Bot Features](https://3commas.io/dca-bots) — Feature comparison (bot status, price display, history)
- [Crypto.com DCA Bot Help](https://help.crypto.com/en/articles/6172353-dca-trading-bot) — Config management patterns
- [How to Backtest Crypto Trading Strategy - Coinbureau](https://coinbureau.com/guides/how-to-backtest-your-crypto-trading-strategy/) — Equity curve visualization, comparison to baseline
- [Crypto Portfolio Tracker Apps 2026 - VentureBurn](https://ventureburn.com/best-crypto-portfolio-tracker/) — P&L display, cost basis, unrealized gains
- [Custom Analytics Dashboards - Mevx](https://blog.mevx.io/guide/how-to-build-your-custom-analytics-dashboards) — Real-time monitoring patterns

### Tertiary (LOW confidence, used for validation only)
- [5 Best Crypto Trading Bot Platforms 2026 - Medium](https://medium.com/coinmonks/5-best-crypto-trading-bot-platforms-for-2026-top-automated-trading-tools-b5cf60ffd433) — General feature landscape
- [9 Common Crypto Trading Mistakes - Trakx](https://trakx.io/resources/insights/9-common-crypto-trading-mistakes-to-avoid/) — Pitfall validation (emotional trading, overtrading)
- [Crypto Trading Mistakes - Asturati](https://www.asturati.com/2026/02/common-crypto-trading-mistakes-that.html) — Risk management anti-patterns

---
*Research completed: 2026-02-13*
*Ready for roadmap: YES*

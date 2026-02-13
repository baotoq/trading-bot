# Feature Landscape: DCA Bot Web Dashboard

**Domain:** Trading bot web dashboard for DCA (Dollar-Cost Averaging) bot
**Researched:** 2026-02-13

## Table Stakes

Features users expect from a DCA bot dashboard. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes | Backend Ready? |
|---------|--------------|------------|-------|----------------|
| **Portfolio Overview** | Core value proposition - see total holdings, cost basis, P&L at a glance | Low | Total BTC, total cost, current value, unrealized P&L, avg cost per BTC | YES - Purchase model has all data |
| **Purchase History List** | Transparency - users need to verify bot executed buys as expected | Low | Paginated table with date, price, quantity, cost, multiplier, status | YES - Purchase model persists everything |
| **Live BTC Price** | Context - users need current price to understand P&L and next buy | Low | WebSocket or polling Hyperliquid API | YES - Hyperliquid client exists |
| **Bot Status Indicator** | Trust - users need to know bot is running and healthy | Low | Health check status (healthy/degraded/down), last successful purchase timestamp | YES - Health endpoint exists |
| **Configuration View** | Awareness - users need to see current DCA settings (amount, schedule, multipliers) | Low | Read-only display of DcaOptions: base amount, schedule, tiers, bear boost | YES - DcaOptions config exists |
| **Backtest Results View** | Validation - users want proof strategy works better than fixed DCA | Medium | Display backtest metrics: total BTC, cost basis, return %, comparison to fixed DCA | YES - Backtest API exists |
| **Basic Auth/Security** | Security - dashboard exposes sensitive portfolio data | Medium | Simple API key or JWT auth to prevent unauthorized access | NO - needs implementation |

## Differentiators

Features that set the dashboard apart. Not expected, but highly valued.

| Feature | Value Proposition | Complexity | Notes | Backend Ready? |
|---------|-------------------|------------|-------|----------------|
| **Interactive Backtest Visualization** | Insight - equity curve chart shows performance visually, makes results digestible | Medium | Line chart comparing smart DCA vs fixed DCA equity over time, trade markers | PARTIAL - API returns daily purchase log |
| **Parameter Comparison Charts** | Decision-making - visual comparison of sweep results helps find optimal config | Medium | Bar/scatter charts showing parameter sets ranked by return/efficiency | YES - Sweep API returns ranked results |
| **Purchase Timeline Chart** | Pattern recognition - see when multipliers triggered, correlate with price drops | Medium | Timeline or candlestick chart with purchase markers sized by multiplier | YES - Purchase model has all metadata |
| **Real-Time Notifications** | Engagement - instant feedback when bot makes purchase | Medium | Toast/alert when new purchase executes, connected via WebSocket or SSE | NO - Telegram exists, but not web |
| **Editable Configuration** | Convenience - adjust DCA params without editing appsettings.json | High | Form to edit base amount, schedule, tiers, bear boost with validation | PARTIAL - DcaOptions validator exists, but no PUT endpoint |
| **Walk-Forward Overfitting Indicator** | Trust - flag when backtest params are overfit, prevent false confidence | Low | Badge/warning on sweep results showing overfitting detection | YES - Sweep API includes walk-forward validation |
| **Next Buy Countdown** | Anticipation - show time until next scheduled purchase | Low | Countdown timer based on DailyBuyHour/Minute config | YES - Schedule in DcaOptions |
| **Data Freshness Indicator** | Reliability - show when historical data was last updated, prompt refresh | Low | Display last data ingestion timestamp, "stale" warning if >7 days | YES - Data status API includes freshness |
| **Multiplier Reasoning Display** | Education - explain why specific multiplier was used (e.g., "20% drop + bear market") | Low | Per-purchase breakdown: tier matched, bear boost applied, drop from high | YES - Purchase model has MultiplierTier, DropPercentage, High30Day, Ma200Day |
| **Weekly Summary Dashboard** | Performance tracking - aggregate metrics by week (total spent, BTC accumulated, avg multiplier) | Medium | Charts showing weekly totals, running averages, efficiency trends | YES - Purchase history has all data |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Sell/Take-Profit Controls** | Out of scope - this is accumulation-only bot, no sell logic | Show portfolio value, but no "sell" button or profit-taking triggers |
| **Multi-Asset Support** | Out of scope - BTC only per requirements | Hardcode "BTC" symbol, no asset picker or multi-coin portfolio |
| **Manual Buy Button** | Anti-pattern - defeats DCA discipline, encourages emotional trading | Emphasize automated schedule, show next buy countdown instead |
| **Leverage/Margin Controls** | Out of scope - spot only, no perps/futures | Display spot holdings only, no leverage indicators |
| **Social/Copy Trading** | Complexity - not core value, adds privacy/legal concerns | Keep dashboard personal, no sharing or copying strategies |
| **Mobile App (native)** | Premature - responsive web covers 95% of use cases | Build responsive Nuxt app, defer native apps |
| **In-App Chat Support** | Overhead - bot is personal tool, not SaaS product | Provide docs/FAQ, email support if needed |
| **Trade Simulation/Paper Trading Mode** | Redundant - backtest engine already validates strategy | Use backtest API for "what-if" analysis, not separate paper trading |
| **Cryptocurrency News Feed** | Distraction - encourages emotional decisions, deviates from DCA discipline | Keep dashboard focused on bot performance, not market noise |
| **Stop-Loss or Risk Management** | Out of scope - DCA is buy-only, long-term accumulation strategy | Show unrealized P&L for awareness, but no automated sell triggers |

## Feature Dependencies

```
Portfolio Overview → Purchase History (needs cost basis calculation)
Backtest Visualization → Backtest Results View (needs data to chart)
Parameter Comparison Charts → Backtest Sweep API (needs ranked results)
Purchase Timeline Chart → Purchase History + Live Price (needs context)
Editable Configuration → Configuration View (needs read first, then write)
Real-Time Notifications → Bot Status (needs event stream)
Next Buy Countdown → Configuration View (needs schedule from DcaOptions)
Multiplier Reasoning → Purchase History (needs metadata fields)
Weekly Summary → Purchase History (needs aggregation)
```

## MVP Recommendation

Prioritize building a **functional monitoring dashboard** first, defer interactive editing.

### Phase 1: View-Only Dashboard (MVP)
1. **Portfolio Overview** - immediate value, shows bot is working
2. **Purchase History List** - transparency, builds trust
3. **Live BTC Price** - context for P&L
4. **Bot Status Indicator** - reliability signal
5. **Configuration View** (read-only) - awareness of current settings
6. **Basic Auth** - security gate

**Rationale:** Covers table stakes, proves value (portfolio tracking), establishes trust (transparency + health), requires ZERO new backend endpoints (read-only using existing data).

### Phase 2: Backtest Integration
1. **Backtest Results View** - validation of strategy
2. **Interactive Backtest Visualization** - equity curve chart
3. **Data Freshness Indicator** - data reliability signal
4. **Walk-Forward Overfitting Indicator** - trust signal for sweep results

**Rationale:** Leverages existing backtest API, adds visual insight, helps users understand "why smart DCA works." Medium complexity (charting), high value (validation).

### Phase 3: Enhanced Insights
1. **Purchase Timeline Chart** - pattern recognition
2. **Multiplier Reasoning Display** - educational value
3. **Next Buy Countdown** - engagement/anticipation
4. **Parameter Comparison Charts** - decision-making tool for sweeps

**Rationale:** Improves user understanding of bot behavior, differentiates from basic portfolio trackers. Medium complexity, moderate value (insight > control).

### Phase 4: Interactive Management (Later)
1. **Editable Configuration** - convenience (requires new PUT endpoint + config reload)
2. **Real-Time Notifications** - engagement (requires WebSocket/SSE infrastructure)
3. **Weekly Summary Dashboard** - trend analysis (requires aggregation queries)

**Rationale:** High complexity (backend changes + validation), moderate value (convenience > necessity). Defer until dashboard proven valuable.

## Defer to Future Milestones

- **Multi-exchange support** - Hyperliquid only for now (PROJECT.md constraint)
- **Advanced analytics** (Sharpe ratio, volatility, correlation) - nice-to-have, not core DCA value
- **Mobile native apps** - responsive web sufficient initially
- **Alerts/notifications customization** - Telegram already covers this
- **Tax reporting** - complex, requires transaction export + cost basis tracking
- **API access for third-party tools** - premature, no identified use case yet

## Backend Gaps for Dashboard

| Feature | Backend Status | Gap | Mitigation |
|---------|---------------|-----|------------|
| Portfolio Overview | Partial | No dedicated endpoint; need to aggregate Purchase records | Query Purchase table directly from frontend or add GET /api/portfolio endpoint |
| Live BTC Price | Partial | Hyperliquid client exists but no public endpoint | Add GET /api/price endpoint or poll Hyperliquid from frontend |
| Real-Time Notifications | Missing | No WebSocket/SSE for purchase events | Use Telegram for now, add SignalR/SSE later if needed |
| Editable Configuration | Missing | No PUT /api/config endpoint, no hot-reload | Add endpoint + IOptionsMonitor invalidation or require app restart |
| Basic Auth | Missing | No authentication/authorization middleware | Add API key middleware or JWT auth |
| Purchase History Pagination | Missing | No pagination in Purchase queries | Add pagination params (page, pageSize) to query |

## Complexity Assessment

| Complexity | Features | Estimated Effort |
|------------|----------|------------------|
| **Low** | Portfolio overview, purchase history list, live price, bot status, config view (read-only), next buy countdown, data freshness, multiplier reasoning, overfitting indicator | ~3-5 days (mostly frontend) |
| **Medium** | Backtest visualization, purchase timeline chart, parameter comparison charts, weekly summary, basic auth, pagination | ~5-7 days (charting + backend endpoints) |
| **High** | Editable configuration (PUT endpoint + validation + hot-reload), real-time notifications (WebSocket/SSE), advanced analytics | ~7-10 days (infrastructure changes) |

## Technology Recommendations for Dashboard

Based on ecosystem research and Nuxt 3 best practices:

| Category | Recommendation | Why |
|----------|---------------|-----|
| **Chart Library** | Unovis or Billboard.js | Unovis: minimalist, modular, modern (best for 2026 dashboards). Billboard.js: simple D3 wrapper, actively maintained, great for standard charts. Avoid Chart.js bloat. |
| **UI Framework** | Nuxt UI or Shadcn-vue | Nuxt UI: first-class Nuxt integration. Shadcn-vue: Tailwind-based, copy-paste components, highly customizable. |
| **Data Fetching** | Nuxt useFetch + SWR pattern | Built-in SSR support, automatic caching, reactive updates. Use $fetch for client-side only. |
| **State Management** | Pinia (if needed) | Nuxt 3 auto-imports, but most state can live in composables. Only use Pinia for complex global state. |
| **Real-Time** | Nuxt SSE module or raw EventSource | For purchase notifications. Defer WebSocket (SignalR) until proven necessary. |
| **Auth** | Nuxt Auth Utils or simple API key | API key in header for MVP. JWT if multi-user later. |

## Sources

**DCA Bot Dashboards:**
- [The Complete Guide to Dollar Cost Averaging (DCA) Trading Bots in 2026](https://www.dappfort.com/blog/dca-trading-bot-development/)
- [DCA Bot: Automate Your DCA Trading Strategy | 3Commas](https://3commas.io/dca-bots)
- [DCA Trading Bot | Crypto.com Help Center](https://help.crypto.com/en/articles/6172353-dca-trading-bot)

**Crypto Trading Bot UI/UX:**
- [Crypto Bot UI/UX Design: Best Practices](https://www.companionlink.com/blog/2025/01/crypto-bot-ui-ux-design-best-practices/)
- [5 Best Crypto Trading Bot Platforms for 2026](https://medium.com/coinmonks/5-best-crypto-trading-bot-platforms-for-2026-top-automated-trading-tools-b5cf60ffd433)

**Portfolio Dashboard Features:**
- [10 Best Crypto Portfolio Tracker Apps in 2026](https://ventureburn.com/best-crypto-portfolio-tracker/)
- [How to Design a Portfolio Management Dashboard for Cryptocurrency Investments](https://medium.com/@extej/how-to-design-a-portfolio-management-dashboard-for-cryptocurrency-investments-6b255b50e3e9)

**Backtest Visualization:**
- [How To Backtest Your Crypto Trading Strategy](https://coinbureau.com/guides/how-to-backtest-your-crypto-trading-strategy/)
- [Crypto Backtesting Guide 2025 | Bitsgap blog](https://bitsgap.com/blog/crypto-backtesting-guide-2025-tools-tips-and-how-bitsgap-helps)

**Configuration Management:**
- [Crypto Bot UI/UX Design: Best Practices](https://www.companionlink.com/blog/2025/01/crypto-bot-ui-ux-design-best-practices/)
- [Risk Management Settings for AI Trading Bots](https://3commas.io/blog/ai-trading-bot-risk-management-guide)

**Real-Time Monitoring:**
- [How to Build Your Custom Analytics Dashboards - Platform Trading Bot Mevx](https://blog.mevx.io/guide/how-to-build-your-custom-analytics-dashboards)
- [How to Use TradingView Bot Alerts for Automated Trading](https://wundertrading.com/journal/en/trading-bots/article/tradingview-bot-alerts)

**Nuxt 3 Charts:**
- [Best Chart Libraries for Vue Projects in 2026](https://weavelinx.com/best-chart-libraries-for-vue-projects-in-2026/)
- [Which Vue Chart Library To Use in 2025? The Definitive Guide](https://www.luzmo.com/blog/vue-chart-libraries)

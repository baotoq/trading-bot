# Feature Research: Multi-Asset Portfolio Tracker

**Domain:** Personal portfolio tracker — crypto, VN30 ETF, fixed deposits — with manual transaction entry, auto-fetch prices, P&L, and multi-currency (VND/USD)
**Researched:** 2026-02-20
**Confidence:** HIGH (patterns drawn from Delta, Kubera, CoinGecko, Sharesight analysis; VN-specific sources verified via vnstock/TCBS research)

---

## Context: What Already Exists

This milestone adds portfolio tracking on top of the existing BTC DCA bot. The Flutter app already has:

- Portfolio overview screen (BTC only, auto-fetched from Hyperliquid)
- Price chart with 6 timeframes and purchase markers
- Purchase history (infinite scroll, cursor pagination, tier/date filters)
- Bot status, countdown, DCA config view/edit, backtest

v4.0 extends the Flutter app and .NET backend to track ALL investments — not just the DCA bot's BTC accumulation. It does not replace existing screens; it adds new ones alongside them.

**Critical integration point:** DCA bot purchases (already tracked in `Purchase` table) must auto-import into the new portfolio model. User should not have to manually re-enter BTC buys the bot already recorded.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that every portfolio tracker must have. Missing one = app feels broken or untrustworthy.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Total Portfolio Value (VND + USD)** | Core value — single number for net worth | LOW | Sum of all asset values converted to display currency. Toggle between VND and USD. Currency toggle persists to preferences. |
| **Per-Asset Holdings View** | Transparency — see what you own, how much, and at what price | LOW | List of all assets with: name, quantity held, current price, total value, P&L (absolute + percent). Grouped by asset type (Crypto / ETF / Fixed Deposit). |
| **Unrealized P&L per Asset** | Core insight — are you up or down? | MEDIUM | Current value minus cost basis. Display both absolute (VND/USD) and percent. Green/red color coding. Requires weighted average cost basis calculation. |
| **Manual Transaction Entry** | Non-negotiable for non-broker assets | MEDIUM | Form for: asset selection, transaction type (Buy/Sell/Deposit), date, quantity, price per unit, currency. Applied to ETFs and non-auto-tracked crypto. Fixed deposits use separate form (see below). |
| **Auto-Fetch Crypto Prices** | Users will not manually update crypto prices | LOW | CoinGecko `/simple/price` endpoint. Free tier: 30 calls/min, 10k calls/month. Already used in project for historical data. Extend existing `CoinGeckoClient` to fetch current prices. |
| **Asset Allocation View** | Portfolio composition at a glance | LOW | Pie/donut chart showing % allocation by asset or by asset type. Tap segment to see detail. fl_chart `PieChartData` — already a dependency. |
| **Portfolio Value Chart Over Time** | "How am I doing overall?" — the most common portfolio question | HIGH | Reconstruct portfolio value day by day from transaction history plus historical prices. Complex to implement correctly (see Feature Dependencies). Simplify by using daily snapshots stored in DB rather than full reconstruction. |
| **Transaction History** | Audit trail — verify every entry | LOW | Chronological list of all transactions across all assets. Filter by asset, type, date range. |
| **Currency Toggle (VND/USD)** | User's base is VND; crypto is USD; need both | LOW | Exchange rate fetched once per session (or daily cache). All values redisplay in selected currency without re-fetch. Store preference in local settings. |
| **Auto-Import DCA Bot Purchases** | Bot already records every purchase — user must not re-enter these | MEDIUM | Backend: map `Purchase` records to portfolio transactions on asset "BTC". One-way sync. Import runs on first setup and continuously as new purchases occur. Flag auto-imported transactions as read-only (cannot edit/delete manually). |

### Differentiators (Competitive Advantage)

Features that set this tracker apart from generic options, leveraging the specific asset mix (crypto + VN ETF + VND fixed deposits).

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Fixed Deposit Tracker with Accrued Interest** | No generic tracker handles Vietnamese bank FD correctly | MEDIUM | Store: principal (VND), annual interest rate, start date, maturity date, compounding frequency (monthly/quarterly/semi-annual). Calculate accrued value = principal * (1 + r/n)^(n*t). Display: current accrued value, days to maturity, projected maturity amount. Treat as a separate asset type, not a transaction. |
| **VN30 ETF Price Auto-Fetch** | Vietnam-specific — no Western tracker supports HOSE-listed ETFs | MEDIUM | Target ETFs: E1VFVN30 (VFMVN30), FUESSV30 (SSIAM VN30). Yahoo Finance carries these under `.VN` suffix tickers. Alternative: unofficial TCBS REST API (used by vnstock Python library). LOW confidence on API stability — mark for deeper research in implementation phase. Price in VND natively. |
| **VND/USD Exchange Rate Auto-Fetch** | Unified display requires daily rate | LOW | Use exchangerate-api.com or similar (free tier sufficient for 1 fetch/day). Cache in Redis with 24h TTL. Existing Redis infrastructure applies directly. |
| **BTC DCA Bot Integration Badge** | Distinguishes bot purchases from manual entries | LOW | Auto-imported transactions from the DCA bot show a "Bot" badge in transaction history. User can see exactly which BTC was bought by the bot vs. manually added. |
| **Per-Asset Performance Chart** | Drill into individual asset history | MEDIUM | Tap any asset → see its value over time as a line chart. For crypto: historical prices from CoinGecko already ingested. For ETFs: fetch historical prices if API supports it. For FDs: calculated curve (no market data needed). |
| **Net Worth by Asset Type Breakdown** | Crypto vs. ETF vs. savings — answer "am I too concentrated?" | LOW | Stacked bar or grouped breakdown. Crypto vs. ETF vs. Fixed Deposit totals. Derived from allocation data — no extra DB queries. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Real-Time Price Streaming** | "Make it feel live" | DCA bot buys once daily. Portfolio tracker is for weekly review, not second-by-second monitoring. WebSocket connection management adds significant complexity. | Refresh on demand (pull-to-refresh) + 5-minute polling when screen is active. "Last updated: X min ago" label. |
| **Broker/Exchange API Auto-Sync** | "Automatically import my Vietstock/VPS trades" | Vietnamese broker APIs require auth registration (SSI FastConnect requires in-person ID verification). Out of scope for personal tool. API keys/tokens create security surface. | Manual transaction entry. Auto-import only for DCA bot (same system). |
| **Tax Reporting / Capital Gains Calculation** | "Help me calculate my taxes" | Vietnamese capital gains rules for crypto and ETFs are complex and evolving. Tax compliance is a regulated activity. Wrong output is dangerous. | Show cost basis and realized P&L clearly. User provides this to their tax advisor. |
| **Portfolio Rebalancing Suggestions** | "Tell me what to buy/sell" | Financial advice requires regulatory licensing. Out of scope for personal tool. Adds significant domain complexity. | Show current allocation %. User makes rebalancing decisions. |
| **Multi-User / Family Portfolio** | "Share with spouse" | Single-user system by design. No auth infrastructure beyond API key. Multi-user requires authentication overhaul. | Out of scope. Personal tracker only. |
| **Historical Performance vs. Index Benchmark** | "Compare my portfolio to VN30 index" | Requires accurate index price history for VN30. Data quality from free sources is inconsistent. Adds join complexity to already-complex portfolio value history calculation. | Show per-asset allocation vs. VN30 ETF. User can compare mentally. Defer to v5 if ever. |
| **Automated VND/USD Conversion at Transaction Date Rate** | "Use the exact exchange rate on the day I bought" | Historical VND/USD rates need a reliable historical API. Free tiers typically provide current rates only. Adds complexity with minimal accuracy gain for a personal tracker. | Use current display rate for all conversion. Store native currency amounts. Recalculate display in real time. Note this in UI: "Converted at today's rate." |
| **FIFO/LIFO Cost Basis Method Selection** | "I need FIFO for tax purposes" | For this personal tracker's primary use case (DCA accumulation, not active trading), average cost basis is sufficient and far simpler to implement correctly. FIFO requires tracking individual lot records. | Weighted average cost basis (total cost / total quantity). Consistent and simple. |

---

## Feature Dependencies

```
Portfolio Value Chart Over Time
    └──requires──> Daily price snapshots in DB (PortfolioSnapshot table)
    └──requires──> Transaction history (correct and complete)
    └──requires──> Historical crypto prices (already in DailyPrice table from CoinGecko)
    └──requires──> Historical ETF prices (fetch and store if API available)

Auto-Import DCA Bot Purchases
    └──requires──> PortfolioAsset: BTC entry exists
    └──reads──> Purchase table (existing)
    └──writes──> PortfolioTransaction table (new)
    └──enhances──> Per-Asset P&L (BTC cost basis derived from auto-imported purchases)

Per-Asset P&L (Unrealized)
    └──requires──> Transaction history for cost basis calculation
    └──requires──> Current price fetch (CoinGecko / Yahoo Finance / manual)
    └──enhances──> Total Portfolio Value (sum of all per-asset values)

Currency Toggle (VND/USD)
    └──requires──> Exchange rate fetch (daily)
    └──enhances──> Total Portfolio Value display
    └──enhances──> Per-Asset display
    └──enhances──> Fixed Deposit display

Fixed Deposit Accrued Value
    └──requires──> FixedDeposit entity (principal, rate, start, maturity, compounding)
    └──no price fetch needed──> pure calculation
    └──enhances──> Total Portfolio Value (FD contributes to net worth)

VN30 ETF Auto-Fetch
    └──requires──> ETF ticker → price source mapping
    └──requires──> VND-native price (no currency conversion needed)
    └──uncertain──> API stability (Yahoo Finance .VN / TCBS informal API)

Manual Transaction Entry
    └──requires──> Asset catalog (known assets with ticker, currency, type)
    └──writes──> PortfolioTransaction table
    └──enhances──> Per-Asset P&L (adds to cost basis)

Asset Allocation Pie Chart
    └──requires──> Per-Asset Holdings (computed values)
    └──uses──> fl_chart PieChartData (already a Flutter dependency)
```

### Dependency Notes

- **Portfolio value chart is the hardest feature.** It requires joining historical prices with transaction history to reconstruct value at each point in time. The pragmatic approach: snapshot total portfolio value daily in a `PortfolioSnapshot` table (run a background job at midnight). Chart reads from snapshots, not real-time reconstruction.

- **Auto-import must be idempotent.** The background job that maps `Purchase` records to `PortfolioTransaction` must be safe to re-run. Use `ExternalReference` column (e.g., `purchase:{purchaseId}`) as unique constraint to prevent duplicate imports.

- **ETF price fetch is LOW confidence.** Yahoo Finance `.VN` tickers are accessible but not via an official free API. The unofficial TCBS REST endpoint used by vnstock may change without notice. Implementation phase needs deeper research. Design the price-fetch layer with an abstraction that accepts "manual price" as a fallback.

- **VND/USD exchange rate only needed for display.** Store all transactions in their native currency (crypto in USD, ETFs and FDs in VND). Convert at display time using the cached daily rate. This avoids historical rate complexity.

- **Fixed deposits do not need a "price" — they need a calculation.** They are not market-priced assets. Accrued value = deterministic math from stored parameters. No external API needed.

---

## MVP Definition

### Launch With (v4.0 — Core Portfolio Tracker)

Minimum set to deliver the promise: "single view of all investments."

- [ ] Asset catalog with predefined assets (BTC, E1VFVN30, FUESSV30, custom)
- [ ] Manual transaction entry: Buy/Sell for crypto and ETF assets
- [ ] Fixed deposit entry: principal, rate, start, maturity, compounding
- [ ] Auto-import DCA bot purchases into BTC position
- [ ] Portfolio overview: total value (VND/USD toggle), per-asset value, P&L %
- [ ] Auto-fetch BTC and other crypto prices (CoinGecko existing integration)
- [ ] VND/USD exchange rate fetch (daily, cached)
- [ ] Asset allocation pie chart (by asset type: Crypto / ETF / Fixed Deposit)
- [ ] Transaction history list (all assets, filter by asset, type, date)

### Add After Validation (v4.x — Depth Features)

- [ ] VN30 ETF price auto-fetch (requires implementation-phase API research)
- [ ] Portfolio value chart over time (daily snapshots — significant complexity)
- [ ] Per-asset performance chart (drill-down from allocation view)
- [ ] Net worth by asset type breakdown (stacked display)
- [ ] Fixed deposit maturity alerts via push notification

### Future Consideration (v5+)

- [ ] Manual price entry for ETFs as fallback when auto-fetch unavailable
- [ ] Historical VN ETF price chart (requires historical data ingestion pipeline)
- [ ] Additional crypto assets beyond BTC (multi-coin CoinGecko support already possible)
- [ ] Export portfolio data (CSV) for tax advisor use

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Total portfolio value (VND/USD toggle) | HIGH | LOW | P1 |
| Per-asset holdings with P&L | HIGH | MEDIUM | P1 |
| Auto-import DCA bot purchases (BTC) | HIGH | MEDIUM | P1 |
| Manual transaction entry (crypto/ETF) | HIGH | MEDIUM | P1 |
| Fixed deposit entry and accrued value | HIGH | LOW | P1 |
| Asset allocation pie chart | HIGH | LOW | P1 |
| CoinGecko auto-fetch (crypto prices) | HIGH | LOW | P1 |
| VND/USD exchange rate (daily) | HIGH | LOW | P1 |
| Transaction history list | MEDIUM | LOW | P1 |
| VN30 ETF auto-fetch price | MEDIUM | MEDIUM | P2 |
| Portfolio value chart over time | HIGH | HIGH | P2 |
| Per-asset performance chart | MEDIUM | HIGH | P2 |
| Net worth by type breakdown | LOW | LOW | P2 |
| FD maturity push notification | MEDIUM | MEDIUM | P3 |
| Historical ETF price chart | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for v4.0 launch
- P2: Should have, add in v4.x iteration
- P3: Nice to have, future milestone

---

## Competitor Feature Analysis

Reference: Delta (crypto portfolio tracker), Kubera (net worth tracker), CoinGecko Portfolio, Sharesight (multi-asset tracker).

| Feature | Delta | Kubera | CoinGecko Portfolio | Our Approach |
|---------|-------|--------|---------------------|--------------|
| Manual transaction entry | Full (buy/sell/transfer) | Yes (with broker sync) | Yes (crypto only) | Buy/Sell/Deposit per asset type. No transfer (single-user) |
| Fixed deposit / savings tracking | No | Yes (custom assets with manual value) | No | First-class FD entity with calculated accrual — better than Kubera's manual entry |
| VN ETF support | No | Via manual asset entry | No | Auto-fetch if API available; manual fallback |
| DCA bot integration | Via exchange API sync | No | No | Native — auto-import from same DB, no API key needed |
| Multi-currency display | Yes (100+ currencies) | Yes (multi-currency) | USD only | VND/USD toggle with daily rate. Narrowly scoped — sufficient for single user |
| Portfolio value chart | Yes (all-time) | Yes (net worth history) | Basic | Daily snapshots approach. Simpler than full reconstruction. |
| Asset allocation chart | Pie chart by asset | Allocation by category | Pie chart | fl_chart PieChartData (existing dependency). By type (Crypto/ETF/FD) |
| Cost basis method | FIFO/LIFO/Average | N/A | Average | Weighted average cost. Simpler and sufficient for DCA accumulation. |

---

## Asset Types: Behavior Reference

### Crypto (BTC, ETH, etc.)
- Price source: CoinGecko `/simple/price` (USD native)
- Transactions: Buy and Sell
- P&L: (current price * quantity held) - cost basis
- Cost basis method: weighted average (total cost / total quantity)
- Currency: stored in USD, displayed in USD or VND
- Special: BTC purchases auto-imported from DCA bot Purchase records

### VN30 ETF (E1VFVN30, FUESSV30, etc.)
- Price source: Yahoo Finance `.VN` ticker (unofficial) or TCBS REST endpoint (via vnstock pattern)
- Transactions: Buy and Sell
- Price: VND native (no conversion needed)
- P&L: (current price * shares) - cost basis in VND
- Update frequency: end-of-day (HOSE closes at 15:00 ICT)
- LOW confidence on API stability: flag for deeper research in implementation phase

### Fixed Deposit
- No market price: deterministic calculation
- Entry fields: principal (VND), annual rate %, start date, maturity date, compounding (monthly/quarterly/semi-annual/annual)
- Current value = principal * (1 + r/n)^(n*t) where t = years elapsed, n = compounding periods/year
- P&L = current value - principal (always positive for an active FD)
- Maturity date tracked for countdown and alert

### Custom / Manual-Price Asset
- For assets not covered by auto-fetch (unlisted funds, private equity)
- User enters current price manually on update
- Out of scope for v4.0 — note for v5+

---

## Backend Work Required

New backend work for v4.0 (not in existing API surface):

| Feature | New Backend Work | Complexity |
|---------|-----------------|-----------|
| Asset catalog | `Asset` entity (id, name, ticker, type, currency, coinGeckoId?) | LOW |
| Portfolio transactions | `PortfolioTransaction` entity (asset, type, date, quantity, price, currency, externalRef) | LOW |
| Fixed deposits | `FixedDeposit` entity (principal, rate, startDate, maturityDate, compounding) | LOW |
| DCA auto-import | Background service: scan `Purchase` table, upsert into `PortfolioTransaction` using `ExternalReference` | MEDIUM |
| CoinGecko current price | Extend existing `CoinGeckoClient`: add `/simple/price` call | LOW |
| VN ETF price fetch | New price fetcher (Yahoo Finance `.VN` or TCBS endpoint) — research needed | MEDIUM |
| Exchange rate fetch | New `ExchangeRateService` (USD→VND daily, Redis 24h cache) | LOW |
| Portfolio aggregate endpoint | `GET /api/portfolio/overview` — computes total value, P&L per asset, allocation % | MEDIUM |
| Transaction CRUD endpoints | `GET/POST /api/portfolio/transactions`, `GET/POST /api/portfolio/fixed-deposits` | LOW |
| Portfolio snapshot job | Midnight background job: compute and store total portfolio value to `PortfolioSnapshot` | MEDIUM |
| Portfolio chart endpoint | `GET /api/portfolio/chart` — returns snapshot time series | LOW |

---

## Sources

- [Delta Investment Tracker Features](https://delta.app/en/features) — multi-asset transaction entry, P&L, allocation charts. MEDIUM confidence (product marketing page).
- [Delta Review 2026](https://www.matchmybroker.com/tools/delta-investment-tracker-review) — feature analysis including multi-currency support. MEDIUM confidence.
- [Kubera Review 2026](https://thecollegeinvestor.com/36895/kubera-review/) — fixed deposit / savings tracking, net worth calculation, multi-currency. MEDIUM confidence.
- [How Kubera Works](https://www.kubera.com/how-kubera-works) — time-weighted return calculation, asset tracking patterns. MEDIUM confidence.
- [CoinGecko API Pricing](https://www.coingecko.com/en/api/pricing) — free tier: 30 calls/min, 10k/month. HIGH confidence (official docs).
- [CoinGecko Simple Price Endpoint](https://docs.coingecko.com/reference/simple-price) — `/simple/price?ids=bitcoin&vs_currencies=usd`. HIGH confidence (official docs).
- [vnstock GitHub](https://github.com/thinh-vu/vnstock) — Vietnam stock data via TCBS/SSI unofficial REST APIs. LOW confidence (unofficial, may change).
- [VFMVN30 ETF on Yahoo Finance](https://finance.yahoo.com/quote/E1VFVN30.VN/) — confirms `.VN` ticker available. MEDIUM confidence (scraping risk).
- [SSIAM VN30 ETF on Yahoo Finance](https://finance.yahoo.com/quote/FUESSV30.VN/) — confirms `.VN` ticker. MEDIUM confidence.
- [FIFO/LIFO/Average Cost Basis — CoinLedger](https://coinledger.io/blog/cryptocurrency-tax-calculations-fifo-and-lifo-costing-methods-explained) — cost basis method comparison. HIGH confidence.
- [Portfolio Value Chart — StockMarketEye](https://help.stockmarketeye.com/article/70-a-portfolios-historical-market-value-chart) — daily value reconstruction approach. MEDIUM confidence.
- [Sharesight Multi-Currency](https://www.sharesight.com/blog/value-your-investments-in-any-currency-with-sharesight/) — conversion at display time (not transaction time) for simple use cases. MEDIUM confidence.

---
*Feature research for: Multi-Asset Portfolio Tracker (v4.0)*
*Researched: 2026-02-20*

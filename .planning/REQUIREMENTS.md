# Requirements: BTC Smart DCA Bot

**Defined:** 2026-02-20
**Core Value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA

## v4.0 Requirements

Requirements for multi-asset portfolio tracker. Each maps to roadmap phases.

### Portfolio Data Model

- [ ] **PORT-01**: User can create portfolio assets with name, ticker, asset type (Crypto/ETF/FixedDeposit), and native currency (USD/VND)
- [ ] **PORT-02**: User can record buy/sell transactions on tradeable assets with date, quantity, price per unit, and currency
- [ ] **PORT-03**: User can create fixed deposits with principal (VND), annual interest rate, start date, maturity date, and compounding frequency
- [ ] **PORT-04**: DCA bot purchases auto-import into BTC portfolio position idempotently (no duplicates, read-only in UI)
- [ ] **PORT-05**: Historical DCA bot purchases are migrated into portfolio on first setup
- [ ] **PORT-06**: Fixed deposit accrued value is calculated correctly for both simple interest (non-cumulative) and compound interest (cumulative)

### Price Feeds

- [ ] **PRICE-01**: Crypto asset prices auto-fetch from CoinGecko with Redis caching (5-min TTL)
- [ ] **PRICE-02**: VN30 ETF prices auto-fetch from VNDirect finfo API with graceful degradation to cached values (48h TTL)
- [ ] **PRICE-03**: USD/VND exchange rate auto-fetches daily from open.er-api.com with Redis caching (12h TTL)
- [ ] **PRICE-04**: Price staleness is tracked and surfaced (last updated timestamp available for all price types)

### Portfolio Display

- [ ] **DISP-01**: User can see total portfolio value with VND/USD currency toggle that persists across sessions
- [ ] **DISP-02**: User can see per-asset holdings with current value, unrealized P&L (absolute + percentage), grouped by asset type
- [ ] **DISP-03**: User can see asset allocation pie chart by asset type (Crypto / ETF / Fixed Deposit)
- [ ] **DISP-04**: User can add manual buy/sell transactions via a form in the Flutter app
- [ ] **DISP-05**: User can add fixed deposits via a dedicated form with principal, rate, dates, and compounding frequency
- [ ] **DISP-06**: User can see transaction history across all assets with filtering by asset, type, and date range
- [ ] **DISP-07**: User can see fixed deposit details including accrued value, days to maturity, and projected maturity amount
- [ ] **DISP-08**: Auto-imported DCA bot transactions show a "Bot" badge and are not editable/deletable
- [ ] **DISP-09**: VN asset prices show staleness indicator ("price as of [date]") when using cached data
- [ ] **DISP-10**: Cross-currency values show "converted at today's rate" label

## v4.x Requirements

Deferred to future iteration. Tracked but not in current roadmap.

### Charts & Analytics

- **CHART-01**: Portfolio total value chart over time (daily computation with Redis cache)
- **CHART-02**: Per-asset performance chart (drill-down from asset detail)
- **CHART-03**: Net worth by asset type breakdown (stacked display)

### Advanced Features

- **ADV-01**: Fixed deposit maturity alerts via push notification
- **ADV-02**: BTC portfolio quantity reconciliation vs Hyperliquid spot balance
- **ADV-03**: CoinGecko coin search endpoint for adding new crypto assets

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real-time price streaming (WebSocket) | DCA bot buys once daily; 5-min polling sufficient; battery/complexity concern |
| Broker/exchange API auto-sync | Vietnamese brokers require in-person auth registration; security surface |
| Tax reporting / capital gains | Regulatory risk; show P&L clearly for user to provide to tax advisor |
| Portfolio rebalancing suggestions | Financial advice requires regulatory licensing |
| FIFO/LIFO cost basis | Weighted average sufficient for DCA accumulation style |
| Historical VND/USD rate at transaction date | Historical rate APIs require paid tier; display at today's rate with label |
| Multi-user / family portfolio | Single-user system by design; no auth infrastructure beyond API key |
| Manual price entry for ETFs | Defer to v5+ if VN API proves unreliable |
| CSV export | Defer to v5+ |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PORT-01 | — | Pending |
| PORT-02 | — | Pending |
| PORT-03 | — | Pending |
| PORT-04 | — | Pending |
| PORT-05 | — | Pending |
| PORT-06 | — | Pending |
| PRICE-01 | — | Pending |
| PRICE-02 | — | Pending |
| PRICE-03 | — | Pending |
| PRICE-04 | — | Pending |
| DISP-01 | — | Pending |
| DISP-02 | — | Pending |
| DISP-03 | — | Pending |
| DISP-04 | — | Pending |
| DISP-05 | — | Pending |
| DISP-06 | — | Pending |
| DISP-07 | — | Pending |
| DISP-08 | — | Pending |
| DISP-09 | — | Pending |
| DISP-10 | — | Pending |

**Coverage:**
- v4.0 requirements: 20 total
- Mapped to phases: 0
- Unmapped: 20 ⚠️

---
*Requirements defined: 2026-02-20*
*Last updated: 2026-02-20 after initial definition*

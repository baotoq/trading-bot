# Requirements: BTC Smart DCA Bot

**Defined:** 2026-02-13
**Core Value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via web dashboard

## v1.2 Requirements

Requirements for v1.2 Web Dashboard. Each maps to roadmap phases.

### Portfolio

- [ ] **PORT-01**: User can view total BTC accumulated and total cost invested
- [ ] **PORT-02**: User can view current portfolio value and unrealized P&L
- [ ] **PORT-03**: User can view average cost basis per BTC
- [ ] **PORT-04**: User can see live BTC price updating in real-time
- [ ] **PORT-05**: User can view total purchase count and date of first/last purchase

### Purchase History

- [ ] **HIST-01**: User can view paginated purchase history sorted by date
- [ ] **HIST-02**: User can see price, multiplier tier, amount, and BTC quantity per purchase
- [ ] **HIST-03**: User can sort and filter purchases by date range and multiplier tier
- [ ] **HIST-04**: User can view purchase timeline as a chart overlay on price

### Backtest Visualization

- [ ] **BTST-01**: User can configure and run a single backtest from the dashboard
- [ ] **BTST-02**: User can view backtest equity curve comparing smart DCA vs fixed DCA
- [ ] **BTST-03**: User can view backtest metrics (cost basis, total BTC, efficiency, drawdown)
- [ ] **BTST-04**: User can run parameter sweep and view ranked results
- [ ] **BTST-05**: User can compare multiple backtest configurations visually

### Configuration Management

- [ ] **CONF-01**: User can view current DCA configuration (amount, schedule, tiers)
- [ ] **CONF-02**: User can edit DCA base amount and schedule from the dashboard
- [ ] **CONF-03**: User can edit multiplier tier thresholds and values from the dashboard
- [ ] **CONF-04**: Config changes are validated server-side before applying

### Live Status

- [ ] **LIVE-01**: User can see current bot health status (healthy/error/warning)
- [ ] **LIVE-02**: User can see next scheduled buy time with countdown
- [ ] **LIVE-03**: User can see connection status indicator (connected/reconnecting/disconnected)

### Infrastructure

- [ ] **INFR-01**: Nuxt frontend project is created with Nuxt 4, TypeScript, Tailwind, Nuxt UI
- [ ] **INFR-02**: Aspire orchestrates Nuxt dev server alongside .NET API
- [ ] **INFR-03**: API proxy configured for development (no CORS issues)
- [ ] **INFR-04**: Dashboard API endpoints use API key authentication

## Future Requirements

Deferred to v1.3 or later.

### Notifications

- **NOTF-01**: User receives in-dashboard notifications for purchases
- **NOTF-02**: User can view notification history

### Advanced Analytics

- **ANAL-01**: User can view weekly/monthly summary charts
- **ANAL-02**: User can view multiplier tier distribution over time
- **ANAL-03**: User can export purchase history as CSV

## Out of Scope

| Feature | Reason |
|---------|--------|
| Multi-user authentication | Single-user bot, API key auth is sufficient |
| Mobile app | Web dashboard is responsive, no native app needed |
| Real-time order book | Not relevant for DCA spot purchases |
| Social/sharing features | Personal tool, no social features |
| Manual buy/sell buttons | Bot is fully automated, manual trading is out of scope |
| News/sentiment feed | Adds complexity without DCA value |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| INFR-01 | Phase 9 | Pending |
| INFR-02 | Phase 9 | Pending |
| INFR-03 | Phase 9 | Pending |
| INFR-04 | Phase 9 | Pending |
| PORT-01 | Phase 10 | Pending |
| PORT-02 | Phase 10 | Pending |
| PORT-03 | Phase 10 | Pending |
| PORT-04 | Phase 10 | Pending |
| PORT-05 | Phase 10 | Pending |
| LIVE-01 | Phase 10 | Pending |
| LIVE-02 | Phase 10 | Pending |
| LIVE-03 | Phase 10 | Pending |
| HIST-01 | Phase 10 | Pending |
| HIST-02 | Phase 10 | Pending |
| HIST-03 | Phase 10 | Pending |
| HIST-04 | Phase 10 | Pending |
| BTST-01 | Phase 11 | Pending |
| BTST-02 | Phase 11 | Pending |
| BTST-03 | Phase 11 | Pending |
| BTST-04 | Phase 11 | Pending |
| BTST-05 | Phase 11 | Pending |
| CONF-01 | Phase 12 | Pending |
| CONF-02 | Phase 12 | Pending |
| CONF-03 | Phase 12 | Pending |
| CONF-04 | Phase 12 | Pending |

**Coverage:**
- v1.2 requirements: 25 total
- Mapped to phases: 25
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-13*
*Last updated: 2026-02-13 after roadmap creation*

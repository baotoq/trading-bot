# Requirements: BTC Smart DCA Bot

**Defined:** 2026-02-20
**Core Value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via iOS mobile app with push notifications

## v3.0 Requirements

Requirements for Flutter iOS mobile app milestone. Each maps to roadmap phases.

### App Foundation

- [ ] **APP-01**: User can enter API base URL and API key on first launch, stored securely in iOS Keychain
- [ ] **APP-02**: App authenticates all API requests via x-api-key header injected automatically
- [ ] **APP-03**: App redirects to setup screen on 401/403 API responses
- [ ] **APP-04**: App supports system light and dark mode automatically
- [ ] **APP-05**: User can pull-to-refresh on all data screens to re-fetch from API
- [ ] **APP-06**: App shows error snackbars for transient API failures with cached stale data indicator

### Portfolio & Status

- [ ] **PORT-01**: User can view total BTC accumulated, total cost, and unrealized P&L on home screen
- [ ] **PORT-02**: User can see live BTC price updated every 30 seconds
- [ ] **PORT-03**: User can see bot health status badge (healthy/warning/down) always visible
- [ ] **PORT-04**: User can see countdown timer to next scheduled buy
- [ ] **PORT-05**: User can see last buy detail card (date, price, BTC amount, multiplier, drop %)

### Charts & History

- [ ] **CHART-01**: User can view BTC price chart with 6 timeframe options (7D/1M/3M/6M/1Y/All)
- [ ] **CHART-02**: User can see purchase markers on price chart colored by multiplier tier
- [ ] **CHART-03**: User can see average cost basis dashed line on price chart
- [ ] **CHART-04**: User can touch chart to see price and date tooltip
- [ ] **CHART-05**: User can scroll through purchase history with infinite scroll (cursor pagination)
- [ ] **CHART-06**: User can filter purchase history by date range and multiplier tier via bottom sheet

### Configuration

- [ ] **CONF-01**: User can view all DCA configuration parameters (base amount, schedule, tiers, bear market settings)
- [ ] **CONF-02**: User can edit DCA configuration with numeric keyboards and time picker
- [ ] **CONF-03**: User can add, remove, and edit multiplier tiers in config
- [ ] **CONF-04**: User sees inline server validation errors when config edit fails

### Push Notifications

- [ ] **PUSH-01**: User receives push notification when a BTC purchase executes (amount, price, multiplier)
- [ ] **PUSH-02**: User receives push notification when bot misses a buy (>36h no purchase)
- [ ] **PUSH-03**: User receives push notification when a purchase fails
- [ ] **PUSH-04**: User can tap notification to deep-link into the relevant app screen
- [ ] **PUSH-05**: Backend stores FCM device tokens with automatic cleanup of stale tokens

### Deprecation

- [ ] **DEPR-01**: Nuxt dashboard removed from Aspire orchestration (code kept, not deleted)

## Future Requirements

Deferred to v3.1+ milestone. Tracked but not in current roadmap.

### Backtest Mobile

- **BTEST-01**: User can run a backtest from mobile with all DCA parameters as inputs
- **BTEST-02**: User can view backtest results with equity curve chart and metric cards
- **BTEST-03**: User can run parameter sweep and view ranked results as card list
- **BTEST-04**: User can check data ingestion status before running backtest

### Mobile Enhancements

- **ENH-01**: User can view notification history log in-app (local storage)
- **ENH-02**: User can see home screen widget with live BTC price (native Swift)
- **ENH-03**: Flutter Web support for desktop browsers

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real-time WebSocket price feed | Battery drain; 30s polling adequate for once-daily DCA bot |
| Candlestick charts | DCA does not use OHLC data; line chart fully sufficient |
| Price alerts / threshold notifications | Undermines DCA discipline, encourages market timing |
| Manual Buy Button | Defeats DCA automation — the automation IS the feature |
| Background price polling (app closed) | iOS background fetch limits; FCM push is the correct pattern |
| Android support | iOS first; Android can be added in future milestone |
| Flutter Web (this milestone) | iOS native first; web deferred to avoid CanvasKit/Safari pitfalls |
| Backtest from mobile | Deferred to v3.1; existing Nuxt dashboard handles backtest |
| Weekly summary push notification | Telegram already sends weekly summary; duplicate |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| APP-01 | Phase 20 | Pending |
| APP-02 | Phase 20 | Pending |
| APP-03 | Phase 20 | Pending |
| APP-04 | Phase 20 | Pending |
| APP-05 | Phase 20 | Pending |
| APP-06 | Phase 20 | Pending |
| PORT-01 | Phase 21 | Pending |
| PORT-02 | Phase 21 | Pending |
| PORT-03 | Phase 21 | Pending |
| PORT-04 | Phase 21 | Pending |
| PORT-05 | Phase 21 | Pending |
| CHART-01 | Phase 22 | Pending |
| CHART-02 | Phase 22 | Pending |
| CHART-03 | Phase 22 | Pending |
| CHART-04 | Phase 22 | Pending |
| CHART-05 | Phase 22 | Pending |
| CHART-06 | Phase 22 | Pending |
| CONF-01 | Phase 23 | Pending |
| CONF-02 | Phase 23 | Pending |
| CONF-03 | Phase 23 | Pending |
| CONF-04 | Phase 23 | Pending |
| PUSH-01 | Phase 24 | Pending |
| PUSH-02 | Phase 24 | Pending |
| PUSH-03 | Phase 24 | Pending |
| PUSH-04 | Phase 24 | Pending |
| PUSH-05 | Phase 24 | Pending |
| DEPR-01 | Phase 25 | Pending |

**Coverage:**
- v3.0 requirements: 27 total
- Mapped to phases: 27
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-20*
*Last updated: 2026-02-20 after roadmap creation (100% coverage)*

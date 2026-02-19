# Feature Research: Flutter Mobile Trading Dashboard

**Domain:** Mobile portfolio monitoring + push notifications for single-user BTC DCA bot
**Researched:** 2026-02-20
**Confidence:** HIGH (mobile patterns verified against pub.dev, Flutter docs, and trading app analysis)

---

## Context: Web Dashboard Baseline

The Nuxt 4 dashboard (being replaced) ships these features against a known API surface. Mobile adapts them:

| Web Feature | API Endpoint | Mobile Adaptation Strategy |
|-------------|-------------|---------------------------|
| Portfolio overview (BTC, cost, P&L, live price) | `GET /api/dashboard/portfolio` | Stats cards in scrollable column; live price via 30s timer (not 10s — battery) |
| Interactive price chart (6 timeframes, markers, avg cost) | `GET /api/dashboard/chart` | `fl_chart` line chart; timeframe as horizontal chip selector; pinch-to-zoom |
| Purchase history (infinite scroll, cursor pagination, filters) | `GET /api/dashboard/purchases` | `infinite_scroll_pagination` ListView; bottom sheet for filters (not sidebar) |
| Backtest visualization (equity curves, metrics, sweep tables) | `POST /api/backtest`, `POST /api/backtest/sweep` | Tabs within Backtest screen; table replaced with scrollable card list |
| DCA configuration management (view/edit, tier editor) | `GET /api/config`, `PUT /api/config` | Full-screen form with numeric keyboard inputs; tier list with add/remove/edit |
| Live bot status (health badge, countdown timer) | `GET /api/dashboard/status` | Persistent status indicator in AppBar or home screen widget |

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that must exist. Missing one = app feels broken or untrustworthy.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Portfolio Overview Screen** | Core value — see BTC accumulated, cost basis, and P&L at a glance | LOW | Stats cards (total BTC, total cost, unrealized P&L, current price). Green/red color coding on P&L is universal convention. |
| **Pull-to-Refresh on all data screens** | Mobile standard — users expect manual refresh gesture | LOW | `RefreshIndicator` widget wrapping all data screens. API calls re-fetch on drag-down. |
| **Purchase History List** | Transparency — verify every buy the bot made | LOW | `ListView.builder` with cursor pagination via `infinite_scroll_pagination` package (v5.1.1, Flutter Favorite). Load on scroll, not page buttons. |
| **Bot Health Status** | Trust — single most important signal: is the bot running? | LOW | Health badge visible on home screen at all times. Green = Healthy, Yellow = Warning (no buy >36h), Red = Down. API: `GET /api/dashboard/status`. |
| **Next Buy Countdown** | Anticipation — when does next scheduled buy fire? | LOW | Countdown timer derived from `nextBuyTime` in status response. Updates every second client-side (no polling needed). |
| **Price Chart (6 timeframes)** | Context — see BTC price history and where purchases landed | MEDIUM | `fl_chart` line chart (v1.1.1, 150 pub points, 7k likes). Timeframe chips: 7D/1M/3M/6M/1Y/All. Touch tooltip shows price + date. Purchase markers as scatter points. |
| **Average Cost Basis Line on Chart** | Insight — visual gap between cost basis and current price = unrealized gain | LOW | Horizontal dashed line overlay on price chart. Data already in `GET /api/dashboard/chart` response (`averageCostBasis`). |
| **API Key Authentication** | Security — dashboard exposes financial data | LOW | Store API key in `flutter_secure_storage`. Send as `x-api-key` header on every request. One-time setup screen on first launch. |
| **Offline/Error State Handling** | Mobile reality — users will open app with no connectivity | LOW | Show cached data with stale indicator. Error snackbars (not full-screen errors) for transient failures. |
| **Dark Mode Support** | Mobile expectation — system dark mode should be respected | LOW | Use `ThemeData` with `brightness: Brightness.dark`. Financial apps universally support dark mode (easier to read charts). |

### Differentiators (Mobile-Native Improvements)

Features that go beyond web parity and use mobile-specific capabilities to improve the experience.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Push Notification: Buy Executed** | Instant awareness without opening app — know every DCA purchase immediately | HIGH | FCM via `firebase_messaging` (v16.1.1). Backend sends notification after each purchase executes. Critical category. Payload: amount, price, multiplier tier, running total BTC. |
| **Push Notification: Bot Health Alert** | Proactive problem detection — know before checking if bot missed a buy | HIGH | Backend fires when `hoursSinceLastPurchase > 36`. Payload: hours elapsed, last purchase timestamp. Warning category. |
| **Haptic Feedback on Key Actions** | Mobile polish — tactile confirmation makes app feel responsive and alive | LOW | `HapticFeedback.lightImpact()` on pull-to-refresh trigger. `HapticFeedback.mediumImpact()` on config save. `HapticFeedback.heavyImpact()` on health alert notification tap. |
| **Swipe-to-Filter on Purchase History** | Mobile-native filter UX — no sidebar needed, filters emerge from bottom | LOW | Bottom sheet with date range picker + tier filter chips. Replaces web sidebar which wastes mobile screen space. |
| **Pinch-to-Zoom on Price Chart** | Touch-first chart interaction — explore chart data with natural gestures | MEDIUM | `fl_chart` interactive mode + `InteractiveViewer` wrapper. Scales x-axis for tight timeframe inspection. |
| **Last Buy Detail Card on Home Screen** | Glanceable — show last purchase inline without navigating to history | LOW | Expandable card below portfolio stats. Shows: date, price, BTC amount, multiplier tier, drop %. Data from `GET /api/dashboard/status` (`lastPurchase*` fields). |
| **Backtest Run from Mobile** | Full feature parity — not just view, but trigger new backtests | MEDIUM | Form with numeric inputs for all DCA params. Results displayed as scrollable metric cards + equity curve chart. Uses `POST /api/backtest`. |
| **Parameter Sweep Results as Card List** | Mobile-adapted table — data tables are unreadable on mobile, card list works | MEDIUM | Ranked cards instead of table rows. Each card: rank, key metric KPIs (total BTC, efficiency %), color-coded rank badge. Replaces sweep results table from web. |
| **Configuration Edit with Inline Validation** | Convenience — edit DCA params without server access | HIGH | Full-screen form. Numeric keyboard for amounts. Tile list for multiplier tiers with add/remove. Server validates via `PUT /api/config`, error displayed inline. |
| **Notification History In-App** | Audit trail — see past push notifications even if dismissed | MEDIUM | Local log of received FCM payloads stored in SQLite via `sqflite`. Accessible from notification bell icon. Useful if phone was offline when event fired. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Real-Time WebSocket Price Feed** | "Make it feel live" | Battery drain, connection management complexity, 10s polling is imperceptible difference for DCA monitoring (not day trading) | 30s polling timer with visible "last updated" timestamp. Adequate for DCA which buys once daily. |
| **Candlestick Charts** | "Looks professional, traders expect it" | DCA bot doesn't use OHLC data for decisions. Adds library complexity. Daily close price line chart conveys all needed information. | Line chart with daily close prices. Purchase markers show timing context. |
| **Price Alerts (threshold-based)** | "Notify me when BTC hits $X" | This is accumulation-only bot. Price alerts encourage market-timing which undermines DCA discipline. Adds alert management UI complexity. | Show current price prominently. Bot automatically buys more on dips via multipliers. |
| **Manual Buy Button** | "Buy now at this price" | Defeats DCA discipline. Encourages emotional decisions at exactly the wrong time (FOMO). | Show next scheduled buy countdown. The automation IS the feature. |
| **Multi-Asset Portfolio View** | "Show all my crypto" | Out of scope. BTC-only bot. Creating multi-asset view requires exchange connections that don't exist. | Show BTC holdings and cost basis clearly. USDC balance available from Hyperliquid API if needed. |
| **Social Features / Sharing** | "Share my backtest results" | Privacy risk (exposes portfolio and strategy). No multi-user use case. Single-user tool. | Export to image/PDF if sharing is ever needed. Defer indefinitely. |
| **Background Price Polling When App Closed** | "Always show current price in notification bar" | iOS aggressively limits background fetch. Battery impact is significant. No value for once-daily DCA bot. | FCM push notifications handle this correctly — server pushes on meaningful events only. |
| **In-App Charting for Backtest Sweep Comparison (Side-by-Side)** | "Compare two backtest configs visually" | Complex multi-line chart on mobile is unreadable. Web version uses session storage for comparison state, difficult to replicate in mobile. | Show ranked card list. User reads the numbers. Defer visual comparison to future. |
| **iOS Home Screen Widget showing live price** | "Quick glance at price without opening app" | Requires native Swift/Kotlin code via `home_widget` package. iOS timeline API limits refresh frequency. Not aligned with DCA use case (price is context, not the primary concern). | Push notification on buy execution is more valuable. Home widget is v2 feature if ever. |

---

## Feature Dependencies

```
Push Notifications (Buy Executed)
    └──requires──> FCM Integration in Flutter (firebase_messaging)
    └──requires──> Backend FCM sender (FirebaseAdmin NuGet)
    └──requires──> Device token registration endpoint (new: POST /api/notifications/register)
    └──requires──> Purchase domain event → FCM send (new: NotificationService in backend)

Push Notifications (Health Alert)
    └──requires──> FCM Integration (same as above)
    └──requires──> Backend health check job triggers FCM when >36h no purchase
    └──requires──> DCA schedule job awareness (existing DcaOptions.DailyBuyHour)

Portfolio Overview
    └──requires──> API client with x-api-key auth
    └──reads──> GET /api/dashboard/portfolio

Price Chart
    └──requires──> fl_chart dependency
    └──reads──> GET /api/dashboard/chart
    └──enhances──> Portfolio Overview (visual context for P&L)

Purchase History
    └──requires──> infinite_scroll_pagination dependency
    └──reads──> GET /api/dashboard/purchases (cursor pagination)
    └──enhances──> Portfolio Overview (drill-down from stats)

Configuration Edit
    └──requires──> Portfolio Overview (must prove app works before editing)
    └──reads──> GET /api/config
    └──writes──> PUT /api/config
    └──enhances──> Bot Status (config changes affect next buy)

Backtest
    └──requires──> fl_chart (equity curve visualization)
    └──writes──> POST /api/backtest, POST /api/backtest/sweep
    └──reads──> GET /api/backtest/data/status

Notification History
    └──requires──> Push Notifications (FCM)
    └──requires──> sqflite (local storage)
    └──conflicts──> No backend dependency (purely local)

Haptic Feedback
    └──enhances──> All interactive actions (no dependency)
```

### Dependency Notes

- **Push notifications require backend changes:** Backend needs FirebaseAdmin NuGet package + a new `POST /api/notifications/register` endpoint to store device tokens. This is a new backend feature, not just a Flutter feature.
- **FCM token management is stateful:** Device token can change on reinstall or token rotation. Backend must handle upsert, not insert-only, for device tokens.
- **Configuration Edit requires API parity:** `PUT /api/config` already exists (v2.0). Form validation mirrors server-side `DcaOptionsValidator`. Show server errors inline.
- **Backtest requires data ingestion first:** `POST /api/backtest/data/ingest` must have been run (historical data must exist). Show `GET /api/backtest/data/status` on backtest screen before allowing run.

---

## Push Notification Scenarios

These are the specific notification events that make sense for a single-user DCA bot. Tied to actual backend events.

### Category 1: Transactional (Critical — Always Deliver)

These represent actual money movement. User must always receive these.

| Scenario | Trigger | Payload | Priority |
|----------|---------|---------|----------|
| **Buy Executed** | After `Purchase` entity saved and Hyperliquid order confirmed | Title: "BTC Purchased", Body: "$45 at $97,400 (2.0x multiplier)", Data: purchaseId, price, amount, tier | HIGH |
| **Buy Failed** | If DCA engine catches exception during order placement | Title: "Purchase Failed", Body: "Failed to buy BTC — check logs", Data: errorMessage | HIGH (critical) |
| **Multiplier Triggered (high tier)** | When multiplier >= 2.0x fires (significant dip buy) | Title: "Dip Buy Triggered", Body: "BTC down 15% — buying 3.0x ($135)", Data: dropPercentage, multiplier, cost | HIGH |

### Category 2: Operational (Informational — Can Miss If Sleeping)

Bot health and status. Not immediate financial action, but important for trust.

| Scenario | Trigger | Payload | Priority |
|----------|---------|---------|----------|
| **Missed Buy Alert** | Backend job: no purchase in >36h (existing health check logic extended) | Title: "Bot Health Warning", Body: "No purchase in 42h — check bot", Data: hoursSinceLastPurchase | MEDIUM |
| **Bot Back Online** | After missed buy alert fires, next successful purchase | Title: "Bot Recovered", Body: "Purchase resumed normally", Data: purchaseId | LOW |
| **Data Ingestion Complete** | After `IngestionJob` finishes (triggered by user from app) | Title: "Historical Data Ready", Body: "1,825 days of BTC data loaded", Data: daysCovered | LOW |

### Category 3: Excluded — Explicitly Not Building

| Scenario | Why Excluded |
|----------|-------------|
| Price alerts (BTC hits $X) | Encourages market-timing, undermines DCA discipline |
| Weekly summary push | User can open app. Telegram already sends weekly summary. Duplicate. |
| "Next buy in 30 minutes" reminder | Pointless — bot is automated. User doesn't need to do anything. |
| Backtest complete (if server-side) | Backtest is synchronous in current API (returns inline result). Not needed. |

### Notification Design Rules

Based on alert fatigue research (64% of users delete apps receiving 5+ notifications/week):

1. **DCA bot fires at most once per day** — Buy Executed is the dominant notification. Total volume is naturally low.
2. **Do not send redundant notifications** — If bot is healthy and buying daily, only Category 1 fires. Category 2 is exception-path only.
3. **Payload must include numbers** — "BTC Purchased" with no amount is useless. Always include cost, price, BTC amount in body.
4. **Use notification categories (iOS) sparingly** — One actionable button max: "View Purchase" deep-links to purchase detail. No "Dismiss" action needed (that's native behavior).

---

## Mobile UX Patterns — Specific to This App

### Navigation Structure

**Bottom navigation bar with 4 tabs:**

```
[Home/Portfolio] [History] [Backtest] [Config]
```

- Home: Portfolio stats + last buy card + bot status
- History: Purchase list with filters
- Backtest: Run + results visualization
- Config: DCA settings read/edit

**Rationale:** 4 tabs match the web dashboard's 4 primary sections. Bottom nav is standard for finance apps (Robinhood, Coinbase, Delta all use it). Tab state persists across navigation (GoRouter + StatefulShellRoute pattern).

### Data Freshness Pattern

For a DCA bot with a daily buy cycle, real-time data is not critical. Use this polling strategy:

| Screen | Polling Interval | Rationale |
|--------|-----------------|-----------|
| Home / Portfolio | 30 seconds | Live price is context, not action-critical |
| Bot Status / Countdown | None (client-side timer) | `nextBuyTime` computed once, countdown runs locally |
| Purchase History | On focus only | Historical data doesn't change except 1x/day |
| Chart | On timeframe change | Chart data changes 1x/day at most |
| Config | On navigate to screen | Config changes are infrequent |

Pull-to-refresh overrides polling on demand.

### Chart Interaction Pattern

Mobile chart UX for a line chart with purchase markers:

1. **Default view:** 1M timeframe, full width, no tooltip
2. **Touch:** Show tooltip with price + date at touched position
3. **Long-press:** Snap to nearest purchase marker, show purchase detail bottom sheet
4. **Timeframe chips:** Horizontal `SingleChildScrollView` with chips (7D / 1M / 3M / 6M / 1Y / All)
5. **Purchase markers:** Colored dot overlay on chart (color = tier: Base/Tier1/Tier2/Tier3)
6. **Avg cost line:** Horizontal dashed line in accent color

### Form Inputs for Configuration

Mobile-specific adaptations for DCA config editing:

- **BaseDailyAmount** → `TextFormField` with `keyboardType: TextInputType.numberWithOptions(decimal: true)`, input formatter for USD format
- **DailyBuyHour/Minute** → `TimePickerDialog` (native time picker, not number field)
- **MultiplierTiers** → Reorderable tile list. Each tile: drop% field + multiplier field. Add/remove buttons.
- **MaxMultiplierCap** → `Slider` widget (min: 1.0, max: 5.0, divisions: 8) + numeric display
- **BearMarketMaPeriod** → `TextFormField` with integer keyboard (200 default)

All numeric fields: `TextInputAction.next` to advance focus. Final field: `TextInputAction.done` to submit.

### Purchase List Item Design

Each list item in purchase history (mobile card, not table row):

```
[Date & Time]                    [Tier Badge: "2.0x"]
$45.00                           0.000462 BTC
Price: $97,400                   Drop: -15.3%
```

Tap to expand: show full detail (DropPercentage, Multiplier reasoning, Hyperliquid order context).

---

## MVP Definition

### Launch With (v1 — Feature Parity)

Minimum set needed to replace Nuxt dashboard functionally:

- [ ] API client with secure key storage + auth
- [ ] Portfolio Overview screen (stats cards, live price polling 30s)
- [ ] Price chart (6 timeframes, fl_chart, purchase markers, avg cost line)
- [ ] Purchase history (infinite scroll, cursor pagination)
- [ ] Bot status with countdown timer
- [ ] DCA config view (read-only)
- [ ] Pull-to-refresh on all screens
- [ ] Dark mode
- [ ] Error handling (offline/stale states)

### Add After Parity (v1.x — Mobile Enhancements)

- [ ] Configuration editing (PUT /api/config form)
- [ ] Haptic feedback on key interactions
- [ ] Bottom sheet filter for purchase history
- [ ] Last buy detail card on home screen
- [ ] Push notifications (buy executed + health alert) — requires backend FCM changes

### Future Consideration (v2+)

- [ ] Backtest run from mobile (complex form + async results)
- [ ] Parameter sweep card list
- [ ] Notification history log (sqflite)
- [ ] Home screen widget (native Swift/Kotlin, significant effort)

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Portfolio Overview | HIGH | LOW | P1 |
| Purchase History (infinite scroll) | HIGH | LOW | P1 |
| Price Chart (fl_chart, 6 timeframes) | HIGH | MEDIUM | P1 |
| Bot Status + Countdown | HIGH | LOW | P1 |
| Pull-to-Refresh | HIGH | LOW | P1 |
| API auth (secure storage) | HIGH | LOW | P1 |
| Dark Mode | MEDIUM | LOW | P1 |
| Push: Buy Executed | HIGH | HIGH | P2 |
| Push: Health Alert | HIGH | HIGH | P2 |
| Config Edit Form | MEDIUM | MEDIUM | P2 |
| Haptic Feedback | MEDIUM | LOW | P2 |
| Bottom Sheet Filters | MEDIUM | LOW | P2 |
| Last Buy Detail Card | LOW | LOW | P2 |
| Backtest Run + Results | MEDIUM | HIGH | P3 |
| Parameter Sweep Cards | LOW | MEDIUM | P3 |
| Notification History | LOW | MEDIUM | P3 |
| Home Screen Widget | LOW | HIGH | Defer |

**Priority key:**
- P1: Must have for launch (Nuxt replacement parity)
- P2: Should have, ship in v1.x iteration
- P3: Nice to have, future milestone

---

## Competitor Feature Analysis

Reference: Robinhood, Coinbase, Delta (crypto portfolio tracker), and 3Commas (DCA bot dashboard).

| Feature | Robinhood/Coinbase | Delta | 3Commas DCA | Our Approach |
|---------|-------------------|-------|-------------|--------------|
| Portfolio total + P&L | Full-screen hero | Card at top | Card at top | Stats cards in scrollable column (no hero — we have one asset) |
| Price chart | Prominent, interactive, default view | Prominent | Secondary | Prominent but scoped to DCA timeframes (7D-All) |
| Purchase history | Full order history | Transaction list | Bot trade log | DCA-specific: show tier, drop%, multiplier per buy |
| Push notifications | Trade confirms, price alerts | Portfolio alerts | Bot events | Trade confirms only (no price alerts — undermines DCA) |
| Bot health status | N/A | N/A | Bot status badge | Health badge always visible in AppBar |
| Configuration | N/A | N/A | Full bot config | Full DCA config: tiers, amounts, schedule, bear boost |
| Backtest | N/A | N/A | Basic | Full: equity curve, metrics, sweep (differentiator) |

---

## Backend Dependencies for Mobile Features

New backend work required (not in existing API surface):

| Feature | New Backend Work | Complexity |
|---------|-----------------|-----------|
| Push: Buy Executed | `POST /api/notifications/register` (device token storage), FirebaseAdmin NuGet, FCM send in DCA engine after purchase | HIGH |
| Push: Health Alert | FCM send in health check job when threshold exceeded, same device token infrastructure | MEDIUM (once FCM infra exists) |
| Push: Buy Failed | FCM send in DCA engine catch block | LOW (once FCM infra exists) |
| APNS setup | Apple Developer account, APNs key, Firebase project config | MEDIUM (config, not code) |

All existing dashboard API endpoints are already compatible with mobile. No changes needed to:
- `GET /api/dashboard/portfolio`
- `GET /api/dashboard/purchases`
- `GET /api/dashboard/chart`
- `GET /api/dashboard/status`
- `GET /api/config`, `PUT /api/config`
- All backtest endpoints

---

## Sources

- [fl_chart pub.dev](https://pub.dev/packages/fl_chart) — v1.1.1, 150 pub points, 7k likes. Confirmed line chart + candlestick support.
- [infinite_scroll_pagination pub.dev](https://pub.dev/packages/infinite_scroll_pagination) — v5.1.1, 160 pub points, Flutter Favorite. Confirmed cursor pagination support.
- [firebase_messaging pub.dev](https://pub.dev/packages/firebase_messaging) — v16.1.1, Flutter Favorite. Cross-platform FCM.
- [flutter_local_notifications pub.dev](https://pub.dev/packages/flutter_local_notifications) — v20.1.0, 150 pub points. iOS notification categories with actionable buttons.
- [Firebase Cloud Messaging Flutter docs](https://firebase.flutter.dev/docs/messaging/overview/) — FCM foreground/background handling requirements.
- [FlutterFire Notifications docs](https://firebase.flutter.dev/docs/messaging/notifications/) — FCM does not support action buttons natively; use flutter_local_notifications for advanced iOS actions.
- [Firebase Admin SDK send message](https://firebase.google.com/docs/cloud-messaging/send/admin-sdk) — .NET FirebaseAdmin NuGet pattern for server-side FCM send.
- [Alert Fatigue: Impact on Users](https://www.magicbell.com/blog/alert-fatigue) — 64% of users delete apps receiving 5+ notifications/week. Keep notification volume naturally low.
- [Push Notification UX Design 2025](https://uxcam.com/blog/push-notification-guide/) — Notification design as a first-class UX concern, not afterthought.
- [Flutter bottom navigation GoRouter](https://codewithandrea.com/articles/flutter-bottom-navigation-bar-nested-routes-gorouter/) — StatefulShellRoute pattern for persistent tab state.
- [Crypto portfolio tracker UX](https://think.design/work/kryptographe-ui-ux-design/) — Card-based layout, balance front and center, mobile-first design.
- [2025 Guide to Haptics](https://saropa-contacts.medium.com/2025-guide-to-haptics-enhancing-mobile-ux-with-tactile-feedback-676dd5937774) — Use haptics purposefully; avoid overuse on minor actions.

---
*Feature research for: Flutter mobile DCA bot dashboard (v3.0)*
*Researched: 2026-02-20*

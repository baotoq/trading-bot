# Project Research Summary

**Project:** BTC Smart DCA Bot — Flutter iOS App
**Domain:** Native iOS mobile dashboard + push notifications for single-user Bitcoin DCA trading bot
**Researched:** 2026-02-20
**Confidence:** HIGH

## Executive Summary

This milestone adds a native iOS Flutter app to the existing .NET 10.0 BTC DCA trading bot. The app replaces the Nuxt 4 web dashboard as the primary monitoring interface, providing portfolio visibility, purchase history, bot health monitoring, and — critically — push notifications delivered via Firebase Cloud Messaging and Apple Push Notification service (APNs). The backend API surface is already complete and compatible; the primary development effort is Flutter-side UI plus two targeted backend additions: FCM token registration endpoints and a `FcmNotificationService` that hooks into the existing purchase event pipeline alongside the current Telegram notifier.

The recommended approach is: Flutter + Riverpod 3.x + Dio + fl_chart + firebase_messaging, with the iOS Keychain via `flutter_secure_storage` handling the API key. All core packages are verified against pub.dev as of 2026-02-20. The feature set maps to four tabs — Portfolio, History, Config, and Backtest — mirroring the web dashboard sections with mobile-native adaptations (cards instead of tables, bottom sheets instead of sidebars, 30s polling instead of 10s). Push notifications cover two categories: transactional (buy executed, buy failed, high-multiplier triggers) and operational (missed buy health alerts). Telegram remains as the fallback notification channel and must not be removed until the iOS app is confirmed stable in production.

The primary risks are: (1) API key security — the key must be stored in iOS Keychain via `flutter_secure_storage`, never in `--dart-define` or source code; (2) APNs configuration — use `.p8` auth key exclusively, never the deprecated `.p12` certificate (confirmed FlutterFire bug with `.p12`); (3) FCM token lifecycle — tokens must be stored in PostgreSQL and refreshed on every app launch, not hardcoded; (4) testing push notifications requires a real physical iOS device, not a simulator. Flutter Web findings are retained in the research files as future-consideration context but are explicitly out of scope for this milestone — the iOS native path avoids all CanvasKit, Safari, and web push pitfalls entirely.

## Key Findings

### Recommended Stack

The Flutter stack is anchored on Riverpod 3.x for state management (2026 community standard, compile-time safety, no BuildContext dependency), Dio 5.x for HTTP (global interceptor for `x-api-key` injection), fl_chart 1.1.1 for line charts with purchase markers, and `flutter_secure_storage` 10.0.0 for iOS Keychain. Navigation uses go_router 17.1.0 (official Flutter team package). All versions verified from pub.dev on 2026-02-20.

For push notifications, `firebase_messaging` 16.1.1 mediates APNs on iOS. `flutter_local_notifications` 20.1.0 handles foreground notification display (FCM suppresses the system banner when the app is in the foreground — this must be handled manually). On the backend, `FirebaseAdmin` NuGet 3.4.0 sends FCM messages from .NET 10.0 via the FCM HTTP v1 API. JSON serialization uses `json_annotation` + `json_serializable` with `build_runner` codegen. The `intl` package handles BTC/USD number formatting.

**Core technologies:**
- `flutter_riverpod` ^3.2.1: State management — compile-time safety, async providers, no BuildContext leaks
- `dio` ^5.9.1: HTTP client — single global interceptor injects `x-api-key` on every request
- `firebase_messaging` ^16.1.1: FCM push notifications — mediates APNs for iOS, Flutter Favorite
- `flutter_local_notifications` ^20.1.0: Foreground notification display — FCM suppresses banners on iOS foreground
- `fl_chart` ^1.1.1: Line chart with scatter purchase markers, 6 timeframes, touch tooltips (MIT license)
- `flutter_secure_storage` ^10.0.0: iOS Keychain storage for the `x-api-key` API key
- `go_router` ^17.1.0: URL-based navigation, deep linking from notification taps to specific screens
- `FirebaseAdmin` (NuGet) 3.4.0: .NET backend FCM sender, fully compatible with .NET 10.0

**Backend additions required (minimal scope):**
- `POST /api/devices/register` + `DELETE /api/devices/{token}` — FCM token lifecycle endpoints, protected by existing `ApiKeyEndpointFilter`
- `DeviceToken` entity + EF Core migration — follows existing `AuditedEntity` + UUIDv7 pattern
- `FcmNotificationService` — hooks into `PurchaseCompletedHandler` MediatR handler (parallel to existing `TelegramNotificationService`)

**What does NOT change:** All existing dashboard API endpoints are fully compatible with Flutter as-is. No changes to `GET /api/dashboard/portfolio`, `purchases`, `status`, `chart`, `config`, or any backtest endpoint.

### Expected Features

Features are scoped to iOS only. Flutter Web findings are retained in FEATURES.md as future considerations.

**Must have — P1 (Nuxt parity on iOS):**
- Portfolio Overview: BTC accumulated, cost basis, unrealized P&L, live price (30s polling, not 10s — battery)
- Price Chart: 6 timeframes (7D/1M/3M/6M/1Y/All), fl_chart line chart, scatter purchase markers by tier, avg cost dashed line, touch tooltip, pinch-to-zoom
- Purchase History: infinite scroll, cursor pagination via `infinite_scroll_pagination`, card design (not table rows)
- Bot Status + Next Buy Countdown: health badge always visible, client-side timer from `nextBuyTime`
- Pull-to-Refresh: `RefreshIndicator` on all data screens — standard mobile gesture
- API Key Auth: `flutter_secure_storage` (Keychain) + first-run setup screen for base URL + key entry
- Dark Mode: `ThemeData` brightness, charts respect theme
- Error/Offline Handling: cached data with stale indicator, snackbars for transient failures

**Should have — P2 (mobile enhancements, ship after parity):**
- Push Notification: Buy Executed — FCM, includes cost/price/BTC amount/multiplier tier in body
- Push Notification: Bot Health Alert — triggers when no purchase in >36h
- Push Notification: Buy Failed — critical alert from DCA engine catch block
- Config Edit Form: full-screen form with numeric keyboards, `TimePickerDialog` for schedule, tier list add/remove, `Slider` for cap, inline server validation errors
- Haptic Feedback: `HapticFeedback.lightImpact()` on refresh, `mediumImpact()` on config save
- Bottom Sheet Filters on Purchase History: date range + tier filter chips
- Last Buy Detail Card on Home Screen: expandable card from `GET /api/dashboard/status` fields

**Defer — P3/v2+:**
- Backtest Run from Mobile: complex form + 30-45s async response + fl_chart equity curve
- Parameter Sweep Results as Card List (mobile-adapted ranked cards)
- Notification History Log (sqflite local storage)
- Home Screen Widget (native Swift, significant platform effort)

**Confirmed anti-features (do not build):**
- Real-time WebSocket price feed (battery drain; 30s polling adequate for once-daily DCA bot)
- Candlestick charts (DCA does not use OHLC; line chart fully sufficient)
- Price alerts / threshold notifications (undermines DCA discipline, encourages market timing)
- Manual Buy Button (defeats DCA automation — the automation IS the feature)
- Background price polling when app is closed (iOS background fetch limits; FCM push is the correct pattern)

**Push notification scenarios (scoped to what makes sense for a single-user daily DCA bot):**
- Category 1 — Transactional (always deliver): Buy Executed, Buy Failed, High-Multiplier Triggered (>=2x)
- Category 2 — Operational (informational): Missed Buy Alert (>36h), Bot Recovered, Data Ingestion Complete
- Category 3 — Excluded: Price alerts, weekly summary (Telegram already sends), "next buy in 30 min" reminders

### Architecture Approach

The architecture is a feature-first Flutter project consuming the existing .NET API via Dio, with Riverpod providers as the state layer. Each feature follows repository + provider + UI separation: repositories make Dio calls and return typed DTOs (json_serializable), providers expose `AsyncValue<T>` to UI, screens use `ConsumerWidget` to react to async state. A single `ApiKeyInterceptor` on the Dio instance reads from `flutter_secure_storage` and injects `x-api-key` on every outbound request, and redirects to the setup screen on 401/403 responses.

On the backend, `FcmNotificationService` integrates at the `PurchaseCompletedHandler` MediatR handler level — the same integration point as `TelegramNotificationService` — meaning the existing event pipeline (domain event → outbox → Dapr → MediatR) requires zero structural changes. Invalid FCM tokens are cleaned up automatically on each send by inspecting `MessagingErrorCode.Unregistered` in the multicast response. The `DeviceToken` table follows the existing `AuditedEntity` + UUIDv7 conventions.

**Major components:**
1. `core/api/` — Dio singleton + `ApiKeyInterceptor` (reads Keychain, injects header, handles 401/403 redirect to setup)
2. `core/notifications/` — `FcmService` (permission request, token retrieval, `POST /api/devices/register`, `onTokenRefresh` listener, `onMessage` → `flutter_local_notifications` foreground display)
3. `core/router/` — `app_router.dart` with redirect guard (no key → `/setup`) + deep link routes from notification taps (`/purchases/:id`)
4. `features/{portfolio|purchases|status|config}/` — Repository + Riverpod provider + UI screen per feature, following official Flutter architecture recommendations
5. `.NET FcmNotificationService` — Sends FCM via FirebaseAdmin `SendEachForMulticastAsync`, cleans stale tokens on `Unregistered` errors
6. `.NET DeviceEndpoints` — `POST /api/devices/register` (upsert LastSeenAt) + `DELETE /api/devices/{token}`, protected by existing `ApiKeyEndpointFilter`

**Data flow (end-to-end push notification):**
```
DCA Scheduler fires daily
→ DcaExecutionService places order on Hyperliquid
→ Purchase entity created, domain event raised
→ DomainEventOutboxInterceptor saves OutboxMessage
→ OutboxMessageProcessor → Dapr → PurchaseCompletedHandler (MediatR)
  ├─ TelegramNotificationService.SendMessageAsync() [unchanged]
  └─ FcmNotificationService.SendPurchaseNotificationAsync() [NEW]
    → FirebaseAdmin SDK → FCM HTTP v1 API → APNs → iOS device
→ Flutter receives FCM message
  ├─ App in foreground: flutter_local_notifications shows banner
  ├─ App in background: FCM shows system notification
  └─ App terminated: FCM shows system notification; tap cold-starts app
→ Notification tap → go_router navigates to /purchases
```

### Critical Pitfalls

1. **API Key in Flutter Binary** — Never use `--dart-define`, `flutter_dotenv`, or hardcoded constants. These are recoverable from the compiled binary with tools like `strings` or `jadx`. Use `flutter_secure_storage` (iOS Keychain) entered by user on first launch. Enable `flutter build --obfuscate` on release builds. Must be addressed in Phase 1 — if the HTTP client is built incorrectly from the start, the pattern propagates everywhere.

2. **APNs `.p12` Certificate vs `.p8` Auth Key** — Firebase FCM configured with a `.p12` certificate has a confirmed FlutterFire bug (issue #10920) causing silent iOS push failure. Apple deprecated `.p12` authentication in 2025. Always upload a `.p8` APNs Auth Key in Firebase Console (does not expire, works across dev + prod environments). Address before any Flutter notification code is written.

3. **FCM Token Not Refreshed on Rotation** — FCM tokens rotate on app reinstall, OS updates, and after 270+ days of inactivity. Backend must upsert on every app launch (not insert once). Flutter must listen to `FirebaseMessaging.instance.onTokenRefresh` stream and re-register. Backend must inspect `MessagingErrorCode.Unregistered` on each send and delete stale tokens. Address in push notification backend phase before FCM send logic.

4. **Simulator Cannot Test Push Notifications** — APNs does not work on iOS Simulator at all. All push notification verification requires a real physical iOS device. For automated tests, mock `FcmService` with a test double. This is a "looks done but isn't" trap if only tested on simulator.

5. **FCM Foreground Notification Not Displayed Automatically** — FCM does not auto-display a system notification banner when the iOS app is in the foreground. `flutter_local_notifications` must be used to show the banner manually from the `FirebaseMessaging.onMessage` stream listener. Missing this means notifications only work when the app is not open, which fails the most common case (user has app open at time of daily buy).

**Web-specific pitfalls (noted for future reference, do not affect iOS scope):**
- CanvasKit memory leak crashing iOS Safari (Flutter Web only, active confirmed bug as of 2026-02-20 in Flutter 3.27.4-3.38.1)
- 1.5MB CanvasKit cold load on web before any pixels render
- Safari web push requiring PWA installation to home screen (iOS 16.4+ only)

## Implications for Roadmap

Based on combined research, the recommended phase structure follows a dependency-driven order: foundational infrastructure first (API security must be correct from day one), then read-only screens (prove the app works), then write operations (config edit after read screens validated), then push notifications (requires both Flutter + backend readiness and physical device testing), and finally the optional backtest feature. All phases build on the preceding ones with no rework required.

### Phase 1: Flutter Project Setup + Core Infrastructure
**Rationale:** All subsequent phases depend on secure API communication. The API key security pitfall must be resolved here — if the HTTP client is built incorrectly, the mistake is copied into every feature. The Dio interceptor + secure storage pattern is the project's equivalent of setting up CI: foundational, not glamorous, must be right.
**Delivers:** Flutter project scaffold (`TradingBot.Mobile/`), Dio + `ApiKeyInterceptor`, `flutter_secure_storage` wrapper for Keychain, first-run setup screen (API key + base URL entry), go_router with redirect guard, app theme (light/dark), Riverpod `ProviderScope`, `json_serializable` codegen setup, `build_runner` configured.
**Addresses:** API key auth (P1), Dark Mode (P1), first-run UX, project structure for all features to follow.
**Avoids:** Pitfall 1 (API key in binary) — establishes `flutter_secure_storage` as the mandatory pattern before any feature copies the wrong approach.
**Research flag:** Standard patterns, skip `/gsd:research-phase`. Dio interceptors, flutter_secure_storage, go_router redirect guards all have official documentation and pub.dev examples.

### Phase 2: Portfolio + Status Screens (Read-Only Core)
**Rationale:** Highest-value, lowest-complexity screens. Portfolio overview is the primary reason to open the app. Status + countdown establishes trust that the bot is running. Both consume existing API endpoints with zero backend changes. Builds confidence in the Flutter/Riverpod pattern before tackling more complex screens like the price chart.
**Delivers:** Portfolio Overview screen (stats cards for total BTC, cost basis, P&L, live price at 30s polling, green/red color on P&L), Bot Status health badge (always visible), Next Buy Countdown (client-side timer from `nextBuyTime`), Last Buy Detail Card (expandable, data from `GET /api/dashboard/status`), Pull-to-Refresh on all screens, offline/stale state handling, error snackbars.
**Addresses:** Portfolio Overview (P1), Bot Status + Countdown (P1), Pull-to-Refresh (P1), Last Buy Detail Card (P2).
**Uses:** `flutter_riverpod` + `dio` + `intl` (BTC formatting to 8 decimal places, USD formatting).
**Research flag:** Standard patterns, skip `/gsd:research-phase`. Riverpod `FutureProvider` + `ConsumerWidget` is well-documented; `when(data:, loading:, error:)` is the standard pattern.

### Phase 3: Price Chart + Purchase History
**Rationale:** fl_chart has a learning curve for scatter overlay + touch interaction. Cursor-based infinite scroll pagination is moderately complex. Grouping these two screens together is efficient because they share the "data list from existing API" pattern and both need `fl_chart` as a dependency. Chart depends on the API client established in Phase 1.
**Delivers:** Price Chart (6 timeframes, fl_chart line chart, scatter purchase markers colored by multiplier tier, avg cost dashed line overlay, touch tooltip with price/date, pinch-to-zoom via `InteractiveViewer`), Purchase History (infinite scroll via `infinite_scroll_pagination`, cursor pagination matching existing `.NET` `GET /api/dashboard/purchases`, card design showing date/cost/BTC/tier/drop%, bottom sheet filters for date range + tier, pull-to-refresh).
**Addresses:** Price Chart (P1), Average Cost Basis Line (P1), Purchase History (P1), Pinch-to-Zoom (P2), Bottom Sheet Filters (P2).
**Uses:** `fl_chart` ^1.1.1, `infinite_scroll_pagination` (add to pubspec — v5.1.1, Flutter Favorite).
**Research flag:** Verify fl_chart scatter + line overlay before implementation. The combination of `LineChartData` + `ScatterChartData` on a single chart widget is not the default usage pattern. Confirm the API supports this before committing to the approach; if not, fall back to vertical line markers which are simpler.

### Phase 4: Configuration Screen (Read + Edit)
**Rationale:** Config reading is trivial (existing `GET /api/config`). Config editing (`PUT /api/config`) is more complex due to multiplier tier list management (reorderable tiles, add/remove, validation) and inline server-side error display. Placed after read-only screens are stable — configuration changes affect live bot behavior and should only be accessible once the app has been validated working. Haptic feedback is naturally included here as it applies to save actions.
**Delivers:** Config View screen (all DCA params visible: base amount, schedule, tiers, bear market settings), Config Edit Form (numeric keyboards with formatters, `TimePickerDialog` for `DailyBuyHour`/`DailyBuyMinute`, multiplier tier list with add/remove/reorder, `Slider` for `MaxMultiplierCap`, inline server validation error display matching `DcaOptionsValidator` rules), Haptic Feedback on save (`mediumImpact()`), Haptic on pull-to-refresh (`lightImpact()`).
**Addresses:** DCA Config View (P1), Config Edit Form (P2), Haptic Feedback (P2).
**Uses:** `dio` PUT to existing `PUT /api/config` endpoint (no backend changes), form validation logic mirroring `DcaOptionsValidator`.
**Research flag:** Standard patterns, skip `/gsd:research-phase`. Flutter `TextFormField` with formatters and `PUT` endpoint validation are documented patterns. `DcaOptionsValidator` rules are already implemented in the backend.

### Phase 5: Push Notifications (FCM + APNs Integration)
**Rationale:** Highest-complexity phase — requires coordinated changes to both Flutter and the .NET backend, Apple Developer account prerequisites, Firebase project setup, and physical device testing. Must come after the app has stable screens to deep-link into (Phases 2-3). APNs `.p8` configuration is a strict prerequisite before any Flutter notification code is written. Token lifecycle management on the backend must be built before the Flutter token registration flow.
**Delivers (backend):** `DeviceToken` entity + EF Core migration (`device_tokens` table with unique index on Token), `POST /api/devices/register` (upsert pattern, updates `LastSeenAt`) + `DELETE /api/devices/{token}` (protected by existing `ApiKeyEndpointFilter`), `FcmNotificationService` (hooks into `PurchaseCompletedHandler`, sends FCM multicast, cleans `Unregistered` tokens on error), `FirebaseAdmin` NuGet 3.4.0 added to `TradingBot.ApiService`, Firebase service account JSON stored via .NET User Secrets.
**Delivers (Flutter):** Firebase project configuration (`.p8` APNs Auth Key uploaded, `GoogleService-Info.plist` downloaded), Xcode capabilities (Push Notifications + Background Modes → Remote Notifications), `FcmService` class (permission request, token retrieval, `POST /api/devices/register` on launch, `onTokenRefresh` listener, `onMessage` → `flutter_local_notifications` foreground banner), `onMessageOpenedApp` handler → go_router deep link to `/purchases`, notification payload design (title/body includes cost/price/BTC/tier).
**Addresses:** Push: Buy Executed (P2), Push: Health Alert (P2), Push: Buy Failed (P2), FCM token lifecycle management, Notification tap deep-linking.
**Avoids:** Pitfall 2 (`.p12` — must use `.p8`), Pitfall 3 (FCM token lifecycle — upsert + cleanup + refresh listener), Pitfall 4 (must test on physical device, not simulator), Pitfall 5 (foreground display — `flutter_local_notifications` required).
**Research flag:** Needs `/gsd:research-phase` before implementation. APNs `.p8` key upload steps, Firebase console iOS app registration, `GoogleService-Info.plist` placement, Xcode entitlements for Push Notifications capability, `flutter_local_notifications` iOS notification categories with deep-link actions — procedurally dense, many steps that silently fail if done out of order.

### Phase 6: Backtest (Defer, Ship Separately)
**Rationale:** Backtest run from mobile requires a complex multi-field form (all DCA params as numeric inputs), a potentially long async wait (30-45s for parameter sweeps), and equity curve visualization using fl_chart. It is a P3 feature with no dependency on Phase 5 push notifications. Safe to defer as a follow-on milestone — the existing Nuxt dashboard handles backtest until this phase ships.
**Delivers:** Backtest Run form (all DCA params as inputs with numeric keyboards), data ingestion status check (`GET /api/backtest/data/status` before allowing run), Backtest Results screen (metric cards + equity curve via `fl_chart`, Smart vs Fixed DCA comparison), Parameter Sweep ranked card list (mobile-adapted alternative to table — rank badge, key KPIs per card).
**Addresses:** Backtest Run (P3), Parameter Sweep Cards (P3).
**Uses:** `fl_chart` equity curve (same library as Phase 3), `POST /api/backtest` + `POST /api/backtest/sweep` (no backend changes needed).
**Research flag:** Standard patterns for form and fl_chart. Dio `receiveTimeout` must be set to 60s minimum for sweep requests (30-45s server-side execution).

### Phase Ordering Rationale

- **Infrastructure before features:** Phase 1 is a strict prerequisite. API key security cannot be retrofitted after feature screens are built — the wrong pattern propagates into every repository and interceptor.
- **Read before write:** Phases 2-3 (read-only) must be stable before Phase 4 (config edit). Prevents incorrect config changes during validation of core app behavior.
- **Stable screens before push:** Phase 5 notification taps deep-link into specific screens — those screens must exist and be stable first (Phases 2-3). The backend notification infrastructure (Phase 5) is also a dependency for deep links.
- **Physical device availability for Phase 5:** APNs does not work on iOS Simulator. Plan for access to a real iPhone during Phase 5 development and testing. This is not optional.
- **Telegram stays live throughout all phases:** Do not remove or modify the existing `TelegramNotificationService` until Phase 5 push notifications have been confirmed delivering reliably on the physical device over at least one full daily buy cycle.
- **Backtest is an independent stream:** Phase 6 has no dependency on Phase 5. Could be worked in parallel if resources allow, but Phases 1-5 deliver higher user value in sequence.
- **Nuxt dashboard as fallback:** Keep the Nuxt dashboard running until the Flutter app is confirmed stable in production. Both can coexist on different ports. Do not deprecate Nuxt prematurely.

### Research Flags

**Needs `/gsd:research-phase` before planning:**
- **Phase 5 (Push Notifications):** APNs `.p8` key generation + Firebase upload procedure, Firebase iOS app registration steps, Xcode Push Notifications + Background Modes entitlements setup, `flutter_local_notifications` notification categories with iOS actionable buttons, FCM token registration endpoint design for token rotation edge cases. This phase has the highest surface area of procedural configuration steps that must be done in a specific order.
- **Phase 3 (fl_chart scatter + line overlay):** Verify that `fl_chart` 1.1.1 supports rendering scatter purchase markers overlaid on a line price chart within a single chart widget. If the API does not cleanly support this combination, the fallback approach (vertical dashed lines for purchase markers) must be chosen before implementation begins.

**Standard patterns, skip `/gsd:research-phase`:**
- **Phase 1:** Flutter project setup, Dio interceptors, `flutter_secure_storage`, go_router redirect guards — official documentation and pub.dev examples are comprehensive.
- **Phase 2:** Riverpod `FutureProvider` + `ConsumerWidget` + `AsyncValue.when()` — the canonical Riverpod 3.x pattern, documented on riverpod.dev.
- **Phase 4:** Flutter `TextFormField` + `PUT` endpoint validation — standard Flutter form patterns.
- **Phase 6:** Backtest form is a straightforward Flutter form; fl_chart equity curve is the same library as Phase 3, just a different chart type.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All package versions verified directly from pub.dev and nuget.org on 2026-02-20. No inferred or estimated versions. flutter_riverpod 3.2.1 published 16 days prior. FirebaseAdmin 3.4.0 verified .NET 10 compatible. |
| Features | HIGH | Feature set derived from the existing Nuxt dashboard API surface (no guessing about endpoints). Mobile adaptations follow established finance app UX patterns (Robinhood, Coinbase, Delta, 3Commas). Anti-features validated against DCA discipline principles. |
| Architecture | HIGH | Repository + Riverpod + Dio interceptor is the Flutter official recommended architecture per docs.flutter.dev/app-architecture. FCM integration patterns from official FlutterFire docs. Backend FcmNotificationService hooks into the exact same MediatR integration point as the existing Telegram service. |
| Pitfalls | HIGH | APNs `.p12` bug verified against FlutterFire GitHub issue #10920 (confirmed). CanvasKit iOS Safari memory leak verified against Flutter GitHub issue #178524 (confirmed active, Flutter 3.27.4-3.38.1). FCM token lifecycle best practices from official Firebase docs. iOS Simulator APNs limitation is documented engineering fact. |

**Overall confidence:** HIGH

### Gaps to Address

- **fl_chart 1.1.1 scatter overlay on line chart:** Research confirmed support for line charts and scatter charts separately, but the specific combination of scatter purchase markers overlaid on a line price chart within a single widget needs hands-on verification. If the combined `LineChartData` + scatter approach does not work cleanly, the fallback is vertical dashed lines for purchase markers, which fl_chart does support via `LineChartBarData` with `dashArray`. Resolve before Phase 3 planning.

- **Apple Developer account prerequisites:** Phase 5 assumes an active Apple Developer account with the ability to create a `.p8` APNs Auth Key and register an App ID with Push Notifications capability. If the account is not set up or the app bundle ID is not registered, this blocks Phase 5 entirely. Verify account status and bundle ID registration before beginning Phase 5 planning.

- **Firebase project creation:** No Firebase project currently exists for this bot. Creating the project, registering the iOS app bundle ID, downloading `GoogleService-Info.plist`, and configuring the `.p8` APNs Auth Key are all manual steps that must be completed before any Flutter notification code is written. These steps cannot be automated and are the first dependency of Phase 5.

- **Aspire Flutter dev integration:** The `DebuggingMadeJoyful.Aspire.Hosting.Dart` community NuGet package (v1.0.0, ~165 downloads) is very low maturity. If it does not work reliably in practice, the fallback is running Flutter outside Aspire as a separate process (Option B: `flutter run -d chrome` independently, with the .NET API URL configured via environment). This only affects the development inner loop, not production behavior.

- **`infinite_scroll_pagination` version:** The `infinite_scroll_pagination` package (v5.1.1, Flutter Favorite) is referenced in FEATURES.md but not included in the STACK.md pubspec.yaml block. Add it explicitly when creating the pubspec: `infinite_scroll_pagination: ^5.1.1`.

## Sources

### Primary (HIGH confidence)
- [pub.dev/packages/flutter_riverpod](https://pub.dev/packages/flutter_riverpod) — v3.2.1 verified, 2026 standard for state management
- [pub.dev/packages/firebase_messaging](https://pub.dev/packages/firebase_messaging) — v16.1.1, iOS + FCM confirmed, Flutter Favorite
- [pub.dev/packages/fl_chart](https://pub.dev/packages/fl_chart) — v1.1.1, line chart + scatter confirmed, MIT license
- [pub.dev/packages/flutter_secure_storage](https://pub.dev/packages/flutter_secure_storage) — v10.0.0, iOS Keychain confirmed
- [pub.dev/packages/go_router](https://pub.dev/packages/go_router) — v17.1.0, Flutter Favorite, official Flutter team package
- [pub.dev/packages/dio](https://pub.dev/packages/dio) — v5.9.1, interceptor support confirmed
- [pub.dev/packages/infinite_scroll_pagination](https://pub.dev/packages/infinite_scroll_pagination) — v5.1.1, Flutter Favorite, cursor pagination confirmed
- [nuget.org/packages/FirebaseAdmin](https://www.nuget.org/packages/FirebaseAdmin) — v3.4.0, .NET 10.0 compatible confirmed
- [firebase.flutter.dev/docs/messaging/apple-integration](https://firebase.flutter.dev/docs/messaging/apple-integration/) — APNs .p8 setup requirements, official FlutterFire docs
- [firebase.google.com/docs/cloud-messaging/manage-tokens](https://firebase.google.com/docs/cloud-messaging/manage-tokens) — FCM token lifecycle official guidance
- [github.com/firebase/flutterfire/issues/10920](https://github.com/firebase/flutterfire/issues/10920) — Confirmed .p12 FCM iOS silent failure bug
- [github.com/flutter/flutter/issues/178524](https://github.com/flutter/flutter/issues/178524) — CanvasKit iOS Safari memory leak (web only, future consideration)
- [docs.flutter.dev/app-architecture/recommendations](https://docs.flutter.dev/app-architecture/recommendations) — Official Flutter repository + provider pattern recommendation

### Secondary (MEDIUM confidence)
- [foresightmobile.com — Best Flutter State Management 2026](https://foresightmobile.com/blog/best-flutter-state-management) — Riverpod 3.x community consensus
- [medium.com — Mastering HTTP Calls in Flutter 2025](https://medium.com/@pv.jassim/mastering-http-calls-in-flutter-2025-edition-http-vs-dio-vs-retrofit-1962ec46be43) — Dio vs alternatives analysis
- [codewithandrea.com — Flutter bottom navigation + GoRouter](https://codewithandrea.com/articles/flutter-bottom-navigation-bar-nested-routes-gorouter/) — StatefulShellRoute for persistent tab state
- [magicbell.com — Alert Fatigue](https://www.magicbell.com/blog/alert-fatigue) — 64% of users delete apps sending 5+ notifications/week; validates minimal notification design
- [nuget.org/packages/DebuggingMadeJoyful.Aspire.Hosting.Dart](https://www.nuget.org/packages/DebuggingMadeJoyful.Aspire.Hosting.Dart) — v1.0.0 community Aspire Dart integration (LOW maturity, ~165 downloads — treat as best-effort)
- [freecodecamp.org — How to Secure Mobile APIs in Flutter](https://www.freecodecamp.org/news/how-to-secure-mobile-apis-in-flutter/) — Secure storage patterns for mobile API keys

---
*Research completed: 2026-02-20*
*Scope: iOS native Flutter app (Flutter Web findings retained in STACK.md/PITFALLS.md as future-consideration annotations)*
*Ready for roadmap: yes*

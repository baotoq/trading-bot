# Stack Research: Flutter Mobile + Web App

**Project:** BTC Smart DCA Bot — Flutter Mobile (iOS) + Web milestone
**Researched:** 2026-02-20
**Scope:** NEW stack additions for Flutter app. Existing .NET 10.0 backend remains unchanged.
**Confidence:** HIGH

---

## Existing Backend Stack (DO NOT CHANGE)

These are validated and working. Flutter consumes them as-is:

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET 10.0 / ASP.NET Core Minimal APIs | 10.0 | HTTP endpoints (dashboard, backtest, config) |
| EF Core + PostgreSQL | 10.0.0 | Persistence |
| Redis | via Aspire | Caching |
| MediatR + Dapr | 13.x / 1.x | Event pipeline |
| API key auth (`x-api-key` header) | — | Auth for all dashboard endpoints |

**Existing API surface Flutter consumes:**
- `GET /api/dashboard/portfolio`
- `GET /api/dashboard/purchases` (cursor pagination)
- `GET /api/dashboard/status`
- `GET /api/dashboard/chart?timeframe=1M`
- `GET /api/dashboard/config`
- `PUT /api/config` (from `/api/config` endpoint group)
- `POST /api/backtest`, `POST /api/backtest/sweep`

---

## Recommended Flutter Stack

### Core Flutter Packages

| Package | Version | Purpose | Why Recommended |
|---------|---------|---------|-----------------|
| **flutter_riverpod** | ^3.2.1 | State management | Riverpod 3.x is the 2026 standard — compile-time safety, no BuildContext dependency, FutureProvider/StreamProvider for async. Less boilerplate than BLoC for a single-developer personal app. Officially supports iOS + web. |
| **riverpod_annotation** | ^4.0.2 | Code-gen for Riverpod | Eliminates boilerplate Provider definitions; pairs with build_runner. Required for Riverpod 3.x code-gen pattern. |
| **go_router** | ^17.1.0 | Navigation/routing | Official Flutter team package, handles web URL routing (deep links) and iOS navigation stack. Works with Riverpod guards. Mandatory for web target (URL sync). |
| **dio** | ^5.9.1 | HTTP client | Supports interceptors (inject `x-api-key` header globally), request cancellation, timeout config. Works on iOS + web (BrowserHttpClientAdapter). Avoids per-call header injection boilerplate. |
| **fl_chart** | ^1.1.1 | Charting (line + candlestick) | Native Flutter canvas charts — supports line chart for price history and candlestick for OHLCV. Supports iOS + web. MIT license. No licensing cost unlike Syncfusion. |

### Push Notifications Stack

| Package | Version | Purpose | Why Recommended |
|---------|---------|---------|-----------------|
| **firebase_core** | ^4.4.0 | Firebase SDK bootstrap | Required by all FlutterFire packages. Supports iOS + web. |
| **firebase_messaging** | ^16.1.1 | FCM push notifications (iOS + web) | FCM is the only viable cross-platform solution for iOS + web push. On iOS, FCM mediates APNs. On web, FCM uses VAPID keys + service worker. Single SDK handles both. |
| **flutter_local_notifications** | ^20.1.0 | In-app notification display (iOS only) | firebase_messaging delivers the payload; flutter_local_notifications renders the banner when app is in foreground. Does NOT support web (web uses browser native notifications via FCM service worker). |

### Serialization + Storage

| Package | Version | Purpose | Why Recommended |
|---------|---------|---------|-----------------|
| **json_annotation** | ^4.10.0 | JSON deserialization annotations | Google-maintained, pairs with json_serializable. Standard for typed Dart DTO models from .NET API responses. |
| **json_serializable** | ^6.12.0 | Code-gen JSON serialization | Auto-generates `fromJson`/`toJson` for Dart classes. Avoids hand-written parsing of API responses. |
| **flutter_secure_storage** | ^10.0.0 | Secure API key storage | iOS: Keychain (optionally hardware-backed Secure Enclave). Web: WebCrypto encryption (requires HTTPS). Stores the `x-api-key` and API base URL securely. v10.0.0 confirmed web support via `flutter_secure_storage_web`. |

### UI + Utilities

| Package | Version | Purpose | Why Recommended |
|---------|---------|---------|-----------------|
| **intl** | ^0.20.2 | Number/date formatting | Format BTC quantities (8 decimal places), USD amounts, purchase dates. Dart's official i18n/formatting lib. |
| **cupertino_icons** | ^1.0.8 | iOS-style icons | Already in project template. Use for iOS-native look. |

### Development Tools (dev_dependencies)

| Tool | Version | Purpose | Notes |
|------|---------|---------|-------|
| **build_runner** | ^2.11.1 | Code generation runner | Required to run json_serializable and riverpod_annotation codegen (`dart run build_runner build`) |
| **riverpod_generator** | matches riverpod_annotation | Riverpod provider codegen | Companion to riverpod_annotation; generates provider boilerplate |
| **flutter_lints** | ^6.0.0 | Linting | Already in project; enforces Flutter best practices |

---

## Backend Changes Required (New Additions Only)

### 1. FCM Token Registration Endpoint

Add to `TradingBot.ApiService`:

```csharp
// New endpoint: POST /api/notifications/token
// Stores FCM device tokens so backend can push to specific devices
// Tokens stored in new DeviceToken table in PostgreSQL
app.MapPost("/api/notifications/token", RegisterDeviceToken)
    .AddEndpointFilter<ApiKeyEndpointFilter>();

// Token refresh: PUT /api/notifications/token
// Token removal: DELETE /api/notifications/token/{token}
```

The existing `x-api-key` auth filter covers these endpoints. No new auth mechanism needed.

### 2. FirebaseAdmin NuGet Package

```bash
# In TradingBot.ApiService/
dotnet add package FirebaseAdmin --version 3.4.0
```

FirebaseAdmin 3.4.0 supports .NET 6.0+, .NET Standard 2.0+. Fully compatible with .NET 10.0.

**Usage pattern (new PushNotificationService):**

```csharp
// Initialization (Program.cs)
FirebaseApp.Create(new AppOptions {
    Credential = GoogleCredential.FromFile("firebase-service-account.json")
});

// Send after DCA purchase executes
var message = new Message {
    Notification = new Notification {
        Title = "BTC Purchase Executed",
        Body = $"Bought {quantity} BTC at ${price:N0} (Tier: {tier})"
    },
    Token = deviceToken  // stored FCM token
};
await FirebaseMessaging.DefaultInstance.SendAsync(message);
```

### 3. CORS Update

Add Flutter web origin to existing CORS policy in `Program.cs` (Flutter web dev server runs on port 8080 by default):

```csharp
policy.WithOrigins(
    "http://localhost:3000",   // Nuxt dev (keep)
    "http://localhost:8080",   // Flutter web dev
    "https://your-domain.com"  // Production
)
```

### 4. New Database Table

```csharp
// New entity for FCM token storage
public class DeviceToken : AuditedEntity
{
    public string Token { get; set; } = "";
    public string Platform { get; set; } = ""; // "ios" | "web"
    public DateTimeOffset LastSeenAt { get; set; }
}
```

---

## Installation

```yaml
# pubspec.yaml — dependencies section
dependencies:
  flutter:
    sdk: flutter
  cupertino_icons: ^1.0.8

  # State management
  flutter_riverpod: ^3.2.1
  riverpod_annotation: ^4.0.2

  # Navigation
  go_router: ^17.1.0

  # HTTP
  dio: ^5.9.1

  # Push notifications (Firebase)
  firebase_core: ^4.4.0
  firebase_messaging: ^16.1.1
  flutter_local_notifications: ^20.1.0

  # JSON serialization
  json_annotation: ^4.10.0

  # Secure storage
  flutter_secure_storage: ^10.0.0

  # Charting
  fl_chart: ^1.1.1

  # Utilities
  intl: ^0.20.2

dev_dependencies:
  flutter_test:
    sdk: flutter
  flutter_lints: ^6.0.0
  build_runner: ^2.11.1
  json_serializable: ^6.12.0
  riverpod_generator: ^2.6.x  # matches riverpod_annotation 4.x
```

```bash
# After updating pubspec.yaml:
flutter pub get

# Run code generation (needed for json_serializable + riverpod_annotation):
dart run build_runner build --delete-conflicting-outputs
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| **flutter_riverpod** | flutter_bloc | If team is large and needs strict event/state audit trails (enterprise). For a single-dev personal bot, Riverpod's lower boilerplate wins. |
| **dio** | http (dart team) | If the app has <5 trivial API calls and no need for interceptors. Dio's global interceptor for `x-api-key` injection is worth the dep for this project. |
| **fl_chart** | syncfusion_flutter_charts | If you need advanced financial widgets (OHLCV with zoom/crosshair) and qualify for Syncfusion's free community license (<$1M revenue, ≤5 devs). fl_chart covers the line+candlestick needs here without commercial licensing risk. |
| **firebase_messaging** (FCM) | APNs direct (no FCM) | If targeting iOS only and wanting to avoid Firebase dependency. Requires `UserNotifications` framework via method channel + your own APNs HTTP/2 client on .NET backend. Significantly more complex, not worth it for iOS+Web target. |
| **flutter_secure_storage** | SharedPreferences | If data is non-sensitive (UI preferences, cached API data). Do NOT use SharedPreferences for the API key — it's plaintext on disk. |
| **go_router** | Navigator 2.0 (imperative) | If you have <4 screens and no web deep linking needs. go_router adds URL-based routing critical for Flutter web. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **provider** (old package) | Superseded by Riverpod. Requires BuildContext, no compile-time safety. Still works but Riverpod 3.x is the 2026 standard. | flutter_riverpod ^3.2.1 |
| **shared_preferences** for API key storage | Stores data as plaintext in NSUserDefaults (iOS) and localStorage (web). API key would be trivially extractable. | flutter_secure_storage ^10.0.0 |
| **http** package alone | No interceptor support — every request must manually add `x-api-key` header. Error-prone at scale. | dio ^5.9.1 with interceptor |
| **chart_flutter** / old chart libs | Unmaintained or poor web support. | fl_chart ^1.1.1 |
| **syncfusion_flutter_charts** | Commercial license required if revenue >$1M or >5 devs. Licensing risk for anything that grows. | fl_chart ^1.1.1 (MIT) |
| **flutter_local_notifications on web** | Explicitly unsupported (web not listed as supported platform). FCM service worker handles web notifications natively. | FCM service worker (automatic via firebase_messaging) |
| **GetX** | Monolithic — conflates routing, state, DI into one opinionated package. Drops compile-time safety. Riverpod covers state + DI cleanly, go_router covers routing. | flutter_riverpod + go_router |
| **Azure Notification Hubs** | Confirmed incompatibility with FCM v1 web push tokens for Flutter web as of 2025. | FirebaseAdmin direct FCM v1 HTTP API |

---

## Platform-Specific Notes

### iOS Requirements

- Enable "Push Notifications" capability in Xcode
- Enable "Background Modes" → "Remote notifications" in Xcode
- Upload APNs authentication key (.p8) to Firebase console
- FCM on iOS requires a **real device** (not simulator) for push notification testing
- `flutter_secure_storage` uses iOS Keychain by default. Optionally enable Secure Enclave: `AppleOptions(useSecureEnclave: true)`

### Flutter Web Requirements

- FCM web requires HTTPS (or localhost for dev)
- Create `web/firebase-messaging-sw.js` service worker file
- Request VAPID key from Firebase console (Cloud Messaging → Web Push certificates)
- `flutter_secure_storage` on web requires HTTPS — uses WebCrypto for encryption
- Safari has known FCM limitations: `getToken()` can fail, permission revocation after 3-6 ignored notifications. Treat web notifications as best-effort.
- Background notifications on web handled by service worker, NOT by `flutter_local_notifications`

### Flutter Web vs iOS Notification Behavior

| Behavior | iOS | Web |
|----------|-----|-----|
| Foreground notifications | `flutter_local_notifications` renders banner | Browser native notification (FCM service worker) |
| Background notifications | FCM delivers to APNs → system shows | FCM service worker shows browser notification |
| In-app notification handling | `FirebaseMessaging.onMessage` stream | `FirebaseMessaging.onMessage` stream |
| Permission prompt | System dialog | Browser dialog |
| Safari support | Full | Partial (known FCM token issues) |

---

## Integration Pattern with Existing .NET API

### API Key Injection via Dio Interceptor

```dart
// lib/services/api_client.dart
final dio = Dio(BaseOptions(baseUrl: 'https://your-api.com'));
dio.interceptors.add(InterceptorsWrapper(
  onRequest: (options, handler) async {
    final storage = FlutterSecureStorage();
    final apiKey = await storage.read(key: 'api_key');
    options.headers['x-api-key'] = apiKey;
    handler.next(options);
  },
));
```

### Riverpod Provider for API Data

```dart
// lib/providers/portfolio_provider.dart
@riverpod
Future<PortfolioResponse> portfolio(PortfolioRef ref) async {
  final dio = ref.watch(dioProvider);
  final response = await dio.get('/api/dashboard/portfolio');
  return PortfolioResponse.fromJson(response.data);
}
```

### FCM Token Registration Flow

```dart
// On app start, after requesting permission:
final token = await FirebaseMessaging.instance.getToken(
  vapidKey: 'your-web-vapid-key',  // web only
);
// POST to /api/notifications/token with x-api-key header
await dio.post('/api/notifications/token', data: {'token': token, 'platform': 'ios'});

// Listen for token refresh:
FirebaseMessaging.instance.onTokenRefresh.listen((newToken) {
  dio.put('/api/notifications/token', data: {'token': newToken});
});
```

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| flutter_riverpod ^3.2.1 | Dart SDK ^3.11.0 | Confirmed — project uses Dart ^3.11.0 |
| firebase_core ^4.4.0 | Flutter >=3.18.0 | Project template locks Flutter >=3.18.0-pre |
| firebase_messaging ^16.1.1 | firebase_core ^4.4.0 | Must use matching FlutterFire versions |
| fl_chart ^1.1.1 | Flutter >=3.0 | No known SDK floor issues |
| flutter_secure_storage ^10.0.0 | Flutter >=3.22 (local_notifications requires 3.22) | secure_storage itself is fine, verify with flutter_local_notifications |
| flutter_local_notifications ^20.1.0 | Flutter SDK >=3.22 | Explicitly requires Flutter 3.22+ |
| go_router ^17.1.0 | Flutter >=3.x | Flutter team package, tracks SDK closely |
| FirebaseAdmin (NuGet) ^3.4.0 | .NET 6.0+ / .NET Standard 2.0+ | Fully compatible with .NET 10.0 backend |

---

## Confidence Assessment

| Decision | Confidence | Rationale |
|----------|------------|-----------|
| **Riverpod 3.x for state** | HIGH | Version verified from pub.dev (3.2.1, published 16 days ago). 2026 community consensus. |
| **Dio for HTTP** | HIGH | Version verified from pub.dev (5.9.1). Web + iOS support confirmed in docs. |
| **FCM (firebase_messaging) for push** | HIGH | Only viable cross-platform option for iOS + web. Version 16.1.1 verified. |
| **fl_chart for charts** | HIGH | Version 1.1.1 verified. Candlestick support confirmed. Web + iOS support confirmed. |
| **flutter_secure_storage ^10.0.0** | HIGH | Version verified. Web uses WebCrypto. iOS uses Keychain. |
| **FirebaseAdmin NuGet 3.4.0** | HIGH | Version verified from nuget.org (3.4.0, .NET 10 compatible). |
| **Safari web push limitations** | MEDIUM | Confirmed from GitHub issues + FlutterFire docs, but Safari behavior may improve in 2026. |
| **Flutter web as production target** | MEDIUM | FCM web works but has known Safari gaps. For personal use, acceptable. |

---

## Sources

- [pub.dev/packages/flutter_riverpod](https://pub.dev/packages/flutter_riverpod) — v3.2.1 confirmed
- [pub.dev/packages/riverpod_annotation](https://pub.dev/packages/riverpod_annotation) — v4.0.2 confirmed
- [pub.dev/packages/go_router](https://pub.dev/packages/go_router) — v17.1.0 confirmed
- [pub.dev/packages/dio](https://pub.dev/packages/dio) — v5.9.1, web support confirmed
- [pub.dev/packages/firebase_core](https://pub.dev/packages/firebase_core) — v4.4.0 confirmed
- [pub.dev/packages/firebase_messaging](https://pub.dev/packages/firebase_messaging) — v16.1.1, iOS+web confirmed
- [pub.dev/packages/flutter_local_notifications](https://pub.dev/packages/flutter_local_notifications) — v20.1.0, no web support confirmed
- [pub.dev/packages/fl_chart](https://pub.dev/packages/fl_chart) — v1.1.1, candlestick+web confirmed
- [pub.dev/packages/flutter_secure_storage](https://pub.dev/packages/flutter_secure_storage) — v10.0.0 confirmed
- [pub.dev/packages/flutter_secure_storage_web](https://pub.dev/packages/flutter_secure_storage_web) — WebCrypto confirmed, HTTPS required
- [pub.dev/packages/json_annotation](https://pub.dev/packages/json_annotation) — v4.10.0 confirmed (Google)
- [pub.dev/packages/json_serializable](https://pub.dev/packages/json_serializable) — v6.12.0 confirmed
- [pub.dev/packages/build_runner](https://pub.dev/packages/build_runner) — v2.11.1 confirmed
- [pub.dev/packages/intl](https://pub.dev/packages/intl) — v0.20.2 confirmed
- [nuget.org/packages/FirebaseAdmin](https://www.nuget.org/packages/FirebaseAdmin) — v3.4.0, .NET 10 compatible
- [FlutterFire FCM Apple Integration](https://firebase.flutter.dev/docs/messaging/apple-integration/) — APNs setup requirements
- [Firebase FCM Flutter Get Started](https://firebase.google.com/docs/cloud-messaging/flutter/client) — Official FCM setup
- [firebase/flutterfire#13048](https://github.com/firebase/flutterfire/issues/13048) — Safari FCM known limitation
- [FCM Web Service Worker Requirements](https://firebase.google.com/docs/cloud-messaging/web/get-started) — VAPID + HTTPS requirements
- [Best Flutter State Management 2026](https://foresightmobile.com/blog/best-flutter-state-management) — Riverpod 3.x recommendation
- [Mastering HTTP Calls in Flutter 2025](https://medium.com/@pv.jassim/mastering-http-calls-in-flutter-2025-edition-http-vs-dio-vs-retrofit-1962ec46be43) — Dio recommendation

---

*Stack research for: Flutter Mobile (iOS) + Web milestone*
*Researched: 2026-02-20*
*Conclusion: flutter_riverpod + dio + firebase_messaging + fl_chart + flutter_secure_storage = complete Flutter stack for iOS+Web with push notifications*
*Backend additions: FirebaseAdmin NuGet + FCM token registration endpoint*
*Confidence: HIGH — all package versions verified directly from pub.dev and nuget.org*

# Architecture Research: Flutter Mobile + .NET Integration

**Domain:** Flutter mobile app (iOS + Web) integrating with existing .NET 10.0 trading bot API
**Researched:** 2026-02-20
**Confidence:** HIGH (Flutter patterns, FCM, Aspire hosting), MEDIUM (community Aspire Dart package maturity)

## System Overview

```
┌────────────────────────────────────────────────────────────────┐
│                      CLIENT LAYER                              │
│                                                                │
│  ┌─────────────────────────┐  ┌──────────────────────────────┐ │
│  │   iOS (Flutter)         │  │   Web (Flutter)              │ │
│  │   - native push (APNs)  │  │   - web push (VAPID/FCM)     │ │
│  │   - flutter_secure_store│  │   - sessionStorage API key   │ │
│  │   - Keychain storage    │  │   - service worker required  │ │
│  └──────────┬──────────────┘  └──────────────┬───────────────┘ │
│             │                                │                 │
│             └───────────────┬────────────────┘                 │
│                             │ HTTPS + x-api-key header         │
└─────────────────────────────┼──────────────────────────────────┘
                              │
┌─────────────────────────────▼──────────────────────────────────┐
│                  .NET 10.0 API SERVICE                         │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  ApiKeyEndpointFilter (existing, unchanged)              │  │
│  │  CORS AllowAll (existing, adequate for mobile)           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Existing Endpoints (no changes needed)                  │  │
│  │  GET  /api/dashboard/portfolio                           │  │
│  │  GET  /api/dashboard/purchases                           │  │
│  │  GET  /api/dashboard/status                              │  │
│  │  GET  /api/dashboard/chart                               │  │
│  │  GET  /api/dashboard/config                              │  │
│  │  GET  /api/config, PUT /api/config                       │  │
│  │  POST /api/backtest, POST /api/backtest/sweep            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  NEW: Push Notification Endpoints                        │  │
│  │  POST /api/devices/register   → store FCM token          │  │
│  │  DELETE /api/devices/{token}  → remove FCM token         │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  NEW: FcmNotificationService (INotificationHandler)      │  │
│  │  Hooks into PurchaseCompletedEvent (same as Telegram)    │  │
│  │  Uses FirebaseAdmin NuGet → FCM HTTP v1 API              │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Infrastructure: DeviceToken table (EF Core)             │  │
│  │  - Id (UUIDv7), Token (string), Platform (iOS/Web)       │  │
│  │  - CreatedAt, LastSeenAt (for staleness tracking)        │  │
│  └──────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴────────────────┐
              │                                │
┌─────────────▼───────────────┐  ┌────────────▼───────────────┐
│  Firebase (FCM)             │  │  PostgreSQL + Redis         │
│  - Routes to APNs (iOS)     │  │  (existing, unchanged)     │
│  - Routes to Web Push       │  └────────────────────────────┘
│  - FCM HTTP v1 API          │
└─────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | Status |
|-----------|----------------|--------|
| **Flutter ApiService (Dio)** | All HTTP calls to .NET API, API key injection via interceptor | NEW (Flutter) |
| **flutter_secure_storage** | Stores API key on iOS (Keychain); web uses sessionStorage | NEW (Flutter) |
| **firebase_messaging** | FCM token retrieval, foreground/background notification handling | NEW (Flutter) |
| **flutter_local_notifications** | Show notification UI when app is in foreground | NEW (Flutter) |
| **Riverpod providers** | State management, caches API responses, drives UI rebuilds | NEW (Flutter) |
| **go_router** | URL-based navigation, deep linking from notification taps | NEW (Flutter) |
| **ApiKeyEndpointFilter** | Validates x-api-key header (existing, unchanged) | EXISTING |
| **DeviceToken entity** | Persists FCM tokens with platform + staleness tracking | NEW (.NET) |
| **DeviceEndpoints** | POST /api/devices/register, DELETE /api/devices/{token} | NEW (.NET) |
| **FcmNotificationService** | Sends push notifications via FirebaseAdmin SDK | NEW (.NET) |
| **PurchaseCompletedHandler** | Extended to also send FCM push (alongside existing Telegram) | MODIFIED (.NET) |
| **AppHost.cs** | Replaces `AddNodeApp("dashboard")` with `AddFlutterApp` | MODIFIED (Aspire) |

## Recommended Flutter Project Structure

```
TradingBot.Mobile/
├── lib/
│   ├── main.dart                    # App entry point, Firebase init, provider setup
│   ├── app.dart                     # MaterialApp, GoRouter, ProviderScope
│   │
│   ├── core/
│   │   ├── api/
│   │   │   ├── api_client.dart      # Dio singleton, base URL, timeout config
│   │   │   ├── api_interceptor.dart # Injects x-api-key header from secure storage
│   │   │   └── api_exceptions.dart  # Typed exceptions (NetworkException, AuthException)
│   │   ├── storage/
│   │   │   └── secure_storage.dart  # flutter_secure_storage wrapper (API key CRUD)
│   │   ├── notifications/
│   │   │   ├── fcm_service.dart     # Token retrieval, permission request, token refresh listener
│   │   │   └── notification_handler.dart # foreground/background/terminated tap routing
│   │   └── router/
│   │       └── app_router.dart      # go_router routes definition
│   │
│   ├── features/
│   │   ├── portfolio/
│   │   │   ├── data/portfolio_repository.dart  # calls GET /api/dashboard/portfolio
│   │   │   ├── providers/portfolio_provider.dart
│   │   │   └── ui/portfolio_screen.dart
│   │   ├── purchases/
│   │   │   ├── data/purchases_repository.dart  # calls GET /api/dashboard/purchases
│   │   │   ├── providers/purchases_provider.dart
│   │   │   └── ui/purchases_screen.dart
│   │   ├── status/
│   │   │   ├── data/status_repository.dart
│   │   │   ├── providers/status_provider.dart
│   │   │   └── ui/status_screen.dart
│   │   ├── config/
│   │   │   ├── data/config_repository.dart
│   │   │   ├── providers/config_provider.dart
│   │   │   └── ui/config_screen.dart
│   │   └── setup/
│   │       └── ui/setup_screen.dart  # First-run: enter API key, base URL
│   │
│   └── shared/
│       ├── widgets/                 # Reusable UI components
│       └── theme/                   # App theme
│
├── web/
│   └── firebase-messaging-sw.js     # Service worker for web push background handling
│
├── ios/
│   └── Runner/
│       └── GoogleService-Info.plist # Firebase iOS config (gitignored)
│
└── pubspec.yaml
```

### Structure Rationale

- **core/api/**: Single Dio instance with interceptor centralizes API key injection. No auth token scattered through feature code.
- **core/notifications/**: Isolates FCM complexity. Token registration, permission flow, and tap routing are platform-specific and need to be testable independently.
- **features/{name}/data/**: Repository pattern per Flutter official recommendations. Allows swapping implementations for tests.
- **features/{name}/providers/**: Riverpod `@riverpod`-annotated providers. Handles loading/error states with `AsyncValue`.
- **web/firebase-messaging-sw.js**: Required file at web root — FCM web background notifications need a service worker at this exact location.

## Architectural Patterns

### Pattern 1: API Key Injection via Dio Interceptor

**What:** Single Dio instance with an interceptor that reads the API key from secure storage and attaches it to every request as `x-api-key` header.

**When to use:** All API calls from Flutter to .NET backend.

**Trade-offs:** Async interceptor adds slight latency on first call (key read from Keychain). Acceptable for this use case. Simpler than per-call header management.

**Example:**
```dart
// core/api/api_interceptor.dart
class ApiKeyInterceptor extends Interceptor {
  final SecureStorage _storage;

  ApiKeyInterceptor(this._storage);

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    final apiKey = await _storage.getApiKey();
    if (apiKey != null) {
      options.headers['x-api-key'] = apiKey;
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.response?.statusCode == 401 || err.response?.statusCode == 403) {
      // Navigate to setup screen — key is wrong or missing
      throw ApiKeyInvalidException();
    }
    handler.next(err);
  }
}

// core/api/api_client.dart
class ApiClient {
  static Dio create(SecureStorage storage, String baseUrl) {
    final dio = Dio(BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 30),
    ));
    dio.interceptors.add(ApiKeyInterceptor(storage));
    return dio;
  }
}
```

### Pattern 2: Repository + Riverpod AsyncNotifier

**What:** Each feature has a repository (data access) and a Riverpod provider (state + caching). UI consumes `AsyncValue<T>` which handles loading/error/data states.

**When to use:** All screen data (portfolio, purchases, status, config).

**Trade-offs:** More boilerplate than direct API calls in widgets. Worth it for testability and consistent loading/error UX.

**Example:**
```dart
// features/portfolio/data/portfolio_repository.dart
class PortfolioRepository {
  final Dio _dio;
  PortfolioRepository(this._dio);

  Future<PortfolioResponse> getPortfolio() async {
    final response = await _dio.get('/api/dashboard/portfolio');
    return PortfolioResponse.fromJson(response.data);
  }
}

// features/portfolio/providers/portfolio_provider.dart
@riverpod
Future<PortfolioResponse> portfolio(PortfolioRef ref) async {
  final repo = ref.watch(portfolioRepositoryProvider);
  return repo.getPortfolio();
}

// features/portfolio/ui/portfolio_screen.dart
class PortfolioScreen extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final portfolioAsync = ref.watch(portfolioProvider);
    return portfolioAsync.when(
      data: (portfolio) => PortfolioView(portfolio: portfolio),
      loading: () => const CircularProgressIndicator(),
      error: (e, _) => ErrorView(message: e.toString()),
    );
  }
}
```

### Pattern 3: FCM Token Registration Flow

**What:** On app start, Flutter requests notification permission, retrieves FCM token, and registers it with the .NET backend via `POST /api/devices/register`. On token refresh, re-registers.

**When to use:** App startup and FCM token refresh events.

**Trade-offs:** Backend needs a DeviceToken table. Tokens must be cleaned up when stale (>270 days for Android, >30 days of no activity recommended by Google).

**Example:**
```dart
// core/notifications/fcm_service.dart
class FcmService {
  final Dio _dio;
  FcmService(this._dio);

  Future<void> initialize() async {
    await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);

    // Request permission (required on iOS)
    await FirebaseMessaging.instance.requestPermission();

    // Get and register initial token
    final token = await _getToken();
    if (token != null) await _registerToken(token);

    // Listen for token refresh
    FirebaseMessaging.instance.onTokenRefresh.listen(_registerToken);

    // Foreground message handler → show local notification
    FirebaseMessaging.onMessage.listen(_handleForegroundMessage);
  }

  Future<String?> _getToken() async {
    if (kIsWeb) {
      // Web requires VAPID key
      return FirebaseMessaging.instance.getToken(
        vapidKey: 'YOUR_VAPID_KEY',
      );
    }
    return FirebaseMessaging.instance.getToken();
  }

  Future<void> _registerToken(String token) async {
    await _dio.post('/api/devices/register', data: {
      'token': token,
      'platform': kIsWeb ? 'web' : Platform.operatingSystem,
    });
  }

  void _handleForegroundMessage(RemoteMessage message) {
    // Show local notification using flutter_local_notifications
    // (FCM suppresses notification UI when app is in foreground)
    FlutterLocalNotificationsPlugin().show(
      0, message.notification?.title, message.notification?.body, null,
    );
  }
}
```

### Pattern 4: First-Run Setup Screen (API Key Entry)

**What:** On first launch, if no API key is in secure storage, route to a setup screen. User enters base URL and API key. Store both via `flutter_secure_storage`. Navigate to main app.

**When to use:** Mobile-specific pattern — replaces the server-to-server approach used by Nuxt (where the API key was an environment variable injected at deploy time, never visible to browser).

**Trade-offs:** User must manually copy API key from their server secrets. For a single-user personal app, this is acceptable. The key is stored in iOS Keychain (hardware-backed on modern devices), which is the highest security tier available without OAuth.

**Example:**
```dart
// core/router/app_router.dart
final router = GoRouter(
  redirect: (context, state) async {
    final storage = ref.read(secureStorageProvider);
    final apiKey = await storage.getApiKey();
    if (apiKey == null && state.matchedLocation != '/setup') return '/setup';
    return null;
  },
  routes: [
    GoRoute(path: '/setup', builder: (_, __) => const SetupScreen()),
    GoRoute(path: '/', builder: (_, __) => const HomeScreen(), routes: [...]),
  ],
);
```

## Data Flow

### Flutter → API (Query Flow)

```
User opens Portfolio screen
  → GoRouter renders PortfolioScreen
  → ref.watch(portfolioProvider) triggers Riverpod fetch
  → PortfolioRepository.getPortfolio()
  → Dio GET /api/dashboard/portfolio
  → ApiKeyInterceptor reads API key from flutter_secure_storage
  → x-api-key header attached
  → .NET ApiKeyEndpointFilter validates header
  → EF Core: SELECT + aggregate purchases
  → JSON response
  → PortfolioResponse.fromJson()
  → AsyncValue<PortfolioResponse>.data
  → PortfolioScreen renders data
```

### Push Notification End-to-End Flow

```
DCA Scheduler runs at configured time (daily)
  → DcaExecutionService.ExecuteDailyPurchaseAsync()
  → Purchase entity created, domain event raised
  → DomainEventOutboxInterceptor saves OutboxMessage to DB
  → OutboxMessageProcessor reads and publishes via Dapr
  → PurchaseCompletedHandler.Handle() (MediatR)
    ├─→ TelegramNotificationService.SendMessageAsync()  [existing]
    └─→ FcmNotificationService.SendPurchaseNotificationAsync()  [NEW]
          → Query DeviceToken table (all tokens)
          → FirebaseAdmin SDK: FirebaseMessaging.SendMulticastAsync()
          → FCM HTTP v1 API
            ├─→ APNs → iOS device (push notification)
            └─→ Web Push → browser (if service worker active)
  → Flutter app receives notification
    ├── Foreground: flutter_local_notifications shows banner
    ├── Background: FCM shows system notification
    └── Terminated: FCM shows system notification; tap opens app
  → Notification tap → go_router navigates to /purchases
```

### FCM Token Registration Flow

```
Flutter app starts
  → Firebase.initializeApp()
  → FirebaseMessaging.instance.requestPermission() [iOS: shows system dialog]
  → FirebaseMessaging.instance.getToken()
  → FcmService._registerToken(token)
  → Dio POST /api/devices/register { token, platform }
  → .NET: DeviceEndpoints.RegisterDeviceAsync()
  → Upsert DeviceToken in PostgreSQL (update LastSeenAt if exists)
  → 200 OK

Token refresh (FCM rotates tokens periodically):
  → FirebaseMessaging.instance.onTokenRefresh listener fires
  → Same registration flow → upserts new token
```

## New Backend Components

### DeviceToken Entity

```csharp
// Models/DeviceToken.cs
public class DeviceToken : AuditedEntity  // inherits CreatedAt/UpdatedAt
{
    public required string Token { get; set; }
    public required string Platform { get; set; }  // "ios", "web"
    public DateTimeOffset LastSeenAt { get; set; }
}

// EF Core migration adds:
// - DeviceTokens table
// - Unique index on Token column
```

### Device Registration Endpoint

```csharp
// Endpoints/DeviceEndpoints.cs
public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapPost("/register", RegisterDeviceAsync);
        group.MapDelete("/{token}", UnregisterDeviceAsync);

        return app;
    }

    private static async Task<IResult> RegisterDeviceAsync(
        RegisterDeviceRequest request,
        TradingBotDbContext db,
        CancellationToken ct)
    {
        var existing = await db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == request.Token, ct);

        if (existing is not null)
        {
            existing.LastSeenAt = DateTimeOffset.UtcNow;
            existing.Platform = request.Platform;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                Token = request.Token,
                Platform = request.Platform,
                LastSeenAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
```

### FCM Notification Service

```csharp
// Infrastructure/Firebase/FcmNotificationService.cs
public class FcmNotificationService(
    TradingBotDbContext db,
    ILogger<FcmNotificationService> logger)
{
    public async Task SendPurchaseNotificationAsync(
        string title, string body, CancellationToken ct)
    {
        var tokens = await db.DeviceTokens
            .Select(d => d.Token)
            .ToListAsync(ct);

        if (tokens.Count == 0) return;

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = title, Body = body },
            // Data payload for deep linking on tap
            Data = new Dictionary<string, string> { ["route"] = "/purchases" },
        };

        var response = await FirebaseMessaging.DefaultInstance
            .SendEachForMulticastAsync(message, ct);

        // Clean up invalid tokens (FCM marks them UNREGISTERED)
        foreach (var (result, token) in response.Responses.Zip(tokens))
        {
            if (!result.IsSuccess &&
                result.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                var stale = await db.DeviceTokens
                    .FirstOrDefaultAsync(d => d.Token == token, ct);
                if (stale is not null) db.DeviceTokens.Remove(stale);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
```

## Aspire Integration

### Replace Nuxt with Flutter Web

```csharp
// TradingBot.AppHost/AppHost.cs (modified section)

// REMOVE (current Nuxt dashboard):
// var dashboard = builder.AddNodeApp("dashboard", "../TradingBot.Dashboard", "dev")
//     .WithHttpEndpoint(port: 3000, env: "PORT")
//     ...

// REPLACE WITH (Flutter web):
// Option A: Community package (DebuggingMadeJoyful.Aspire.Hosting.Dart v1.0.0)
var flutter = builder.AddFlutterApp("mobile", "../TradingBot.Mobile")
    .WithDartDefine("API_URL", apiService.GetEndpoint("http"))
    .WithDartDefine("API_KEY", dashboardApiKey)  // compile-time injection for dev only
    .WithWebPort(3000)
    .WithBrowser(FlutterBrowser.Chrome)
    .WithReference(apiService)
    .WaitFor(apiService);

// NOTE: Option A requires NuGet package:
// <PackageReference Include="DebuggingMadeJoyful.Aspire.Hosting.Dart" Version="1.0.0" />
```

**Caveat on `--dart-define` for API key:** Compile-time defines are embedded in the compiled JS/WASM output. For development this is acceptable. For production Flutter web deployment, use a runtime config endpoint instead (see Anti-Patterns).

**Alternative (Option B — no community package):** Run Flutter web as a separate process outside Aspire. Flutter outputs static files to `build/web/`; serve with any static file server. Aspire orchestrates the .NET API; Flutter web is deployed independently. This is the production pattern regardless.

### Firebase Configuration (Backend)

```csharp
// TradingBot.ApiService/Infrastructure/Firebase/ServiceCollectionExtensions.cs
public static IServiceCollection AddFirebase(
    this IServiceCollection services, IConfiguration configuration)
{
    var credentialJson = configuration["Firebase:ServiceAccountJson"];
    if (!string.IsNullOrEmpty(credentialJson))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(credentialJson)
        });
    }

    services.AddScoped<FcmNotificationService>();
    return services;
}
```

```bash
# User secrets (TradingBot.ApiService/)
dotnet user-secrets set "Firebase:ServiceAccountJson" "<json-from-firebase-console>"
```

## Integration Points

### Flutter → Existing .NET API

| Endpoint | Flutter Feature | Notes |
|----------|----------------|-------|
| `GET /api/dashboard/portfolio` | Portfolio screen | No changes to backend |
| `GET /api/dashboard/purchases` | Purchases list | No changes; Flutter uses existing cursor pagination |
| `GET /api/dashboard/status` | Status/home screen | No changes |
| `GET /api/dashboard/chart` | Chart screen | No changes |
| `GET /api/config` | Config view | No changes |
| `PUT /api/config` | Config edit | No changes; already protected by ApiKeyEndpointFilter |

### New Backend ↔ Flutter Integration Points

| Endpoint | Direction | Purpose |
|----------|-----------|---------|
| `POST /api/devices/register` | Flutter → .NET | FCM token registration on app start and token refresh |
| `DELETE /api/devices/{token}` | Flutter → .NET | Token cleanup (optional; FCM error cleanup handles most cases) |
| Push notification payload | FCM → Flutter | `data.route = "/purchases"` for deep link on tap |

### Auth: Mobile vs Nuxt Comparison

| Aspect | Nuxt (current) | Flutter (new) |
|--------|----------------|---------------|
| API key location | Server env var (`NUXT_API_KEY`), never in browser | `flutter_secure_storage` → iOS Keychain |
| API key exposure | Never exposed to client | Key is on device, not in app bundle |
| Auth method | Server-to-server (Nuxt server → .NET API) | Client-to-server (Flutter → .NET API) |
| Key entry | Set at deploy time by operator | Entered by user on first run via setup screen |
| Security tier | Server-secret (highest) | Device keychain (high, hardware-backed on modern iOS) |

The Nuxt server-to-server approach (key is a server env var, never touches the browser) cannot be replicated in Flutter for web without a proxy layer. However, for a personal single-user app, the Keychain approach is appropriate. The threat model is: key is exposed if device is compromised, which is acceptable for a personal DCA monitoring app.

## Scaling Considerations

This is a single-user personal app. Scaling is not a concern. But noting the natural limits:

| Concern | Current (1 user) | If multi-user |
|---------|------------------|---------------|
| Device tokens | 1-3 devices | Add user_id FK to DeviceToken table |
| FCM multicast | 1-3 tokens (trivial) | Up to 500 tokens per multicast call |
| API key auth | Single shared secret | Would need per-user auth (JWT or OAuth) |

## Anti-Patterns

### Anti-Pattern 1: Hardcoding API Key in Flutter Source Code

**What people do:** Put API key in `--dart-define` or `const String apiKey = "abc123"` in source.

**Why it's wrong:** `--dart-define` values are embedded in compiled JavaScript output for Flutter web. They can be extracted from the WASM/JS bundle. For mobile, values in source code end up in the compiled binary.

**Do this instead:** Use `flutter_secure_storage` on first run. User enters key via a setup screen. Key is stored in Keychain (iOS) or EncryptedSharedPreferences (Android). For web, this is a genuine limitation — use `sessionStorage` at minimum (not localStorage, which persists across sessions), or accept that web users must enter the key each session.

### Anti-Pattern 2: Calling the .NET API Directly from Widget Build

**What people do:** Call `dio.get('/api/...')` directly in a widget's `build()` method or `initState`.

**Why it's wrong:** Called on every rebuild. No caching. No loading/error state management. Impossible to test without the network.

**Do this instead:** Use Riverpod providers. The provider is cached, handles async state, and can be invalidated explicitly when data changes (e.g., after a config update).

### Anti-Pattern 3: Using `flutter_secure_storage` on Flutter Web for Long-Term Key Storage

**What people do:** Use `flutter_secure_storage` on web assuming it provides the same Keychain-level security as iOS.

**Why it's wrong:** On web, `flutter_secure_storage_web` falls back to `localStorage` with an encryption layer. `localStorage` persists across browser sessions. This is better than plain storage, but not true hardware-backed security.

**Do this instead:** Accept the trade-off — for a personal app accessed from a browser you control, encrypted localStorage is adequate. Alternatively, prompt for API key on each web session and store only for session duration in memory/sessionStorage.

### Anti-Pattern 4: Sending Push Notifications to All Stored Tokens Without Cleanup

**What people do:** Store FCM tokens but never remove stale ones. Notification sends fail silently for expired tokens.

**Why it's wrong:** Android tokens expire after 270 days of inactivity. Sending to expired tokens wastes FCM quota and can degrade delivery latency.

**Do this instead:** On each `SendEachForMulticastAsync` call, inspect response errors. If `MessagingErrorCode.Unregistered`, delete that token from the database. This is implemented in the `FcmNotificationService` example above.

### Anti-Pattern 5: Assuming FCM Works on iOS Simulator

**What people do:** Test push notifications on iOS Simulator during development.

**Why it's wrong:** APNs (which FCM uses on iOS) does not work on simulators. Period.

**Do this instead:** Use a physical iOS device for all notification testing. For automated testing, mock `FcmService` with a fake that records calls.

### Anti-Pattern 6: Treating Flutter Web Push as Equivalent to iOS Push

**What people do:** Assume that if FCM works on iOS, web push just works.

**Why it's wrong:** Flutter web push requires: (1) HTTPS deployment, (2) `firebase-messaging-sw.js` service worker in `web/` directory, (3) VAPID key configured in Firebase Console, (4) user explicitly grants browser permission. Service workers don't run when browser is fully closed (unlike iOS which can receive pushes when app is terminated).

**Do this instead:** Test web push in a deployed HTTPS environment. Local `localhost` development works for web push in Chrome but may behave differently than production. Treat web push as best-effort supplemental channel.

## Sources

**Flutter HTTP:**
- [Flutter Architecture Recommendations — docs.flutter.dev](https://docs.flutter.dev/app-architecture/recommendations)
- [Dio package 5.9.1 — pub.dev](https://pub.dev/packages/dio)
- [flutter_riverpod 3.2.1 — pub.dev](https://pub.dev/packages/flutter_riverpod)

**Navigation:**
- [go_router 17.1.0 (Flutter Favorite) — pub.dev](https://pub.dev/packages/go_router)

**Auth / Secure Storage:**
- [flutter_secure_storage 10.0.0 — pub.dev](https://pub.dev/packages/flutter_secure_storage)

**Firebase / FCM:**
- [firebase_messaging 16.1.1 — pub.dev](https://pub.dev/packages/firebase_messaging)
- [firebase_core 4.4.0 — pub.dev](https://pub.dev/packages/firebase_core)
- [FCM Apple Integration (APNs) — firebase.flutter.dev](https://firebase.flutter.dev/docs/messaging/apple-integration/)
- [FCM Token Management Best Practices — firebase.google.com](https://firebase.google.com/docs/cloud-messaging/manage-tokens)
- [FCM Server Integration — firebase.flutter.dev](https://firebase.flutter.dev/docs/messaging/server-integration/)

**Backend FCM:**
- [FirebaseAdmin 3.4.0 (.NET SDK) — nuget.org](https://www.nuget.org/packages/FirebaseAdmin)
- [Firebase Cloud Messaging with ASP.NET Core — Medium/cedricgabrang](https://cedricgabrang.medium.com/firebase-cloud-messaging-with-asp-net-core-df666291c427)

**Aspire Flutter Integration:**
- [DebuggingMadeJoyful.Aspire.Hosting.Dart 1.0.0 — nuget.org](https://www.nuget.org/packages/DebuggingMadeJoyful.Aspire.Hosting.Dart) (community package, v1.0.0, 165 downloads, LOW maturity)

---
*Architecture research for: Flutter mobile + web integration with existing .NET 10.0 trading bot API*
*Researched: 2026-02-20*

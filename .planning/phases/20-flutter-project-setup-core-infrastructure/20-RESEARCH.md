# Phase 20: Flutter Project Setup + Core Infrastructure - Research

**Researched:** 2026-02-20
**Domain:** Flutter iOS app scaffolding, Riverpod 3, go_router 17, Dio 5, dark theming, build-time config
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Authentication & Configuration
- No first-run setup screen — API base URL and API key are both injected at build time via --dart-define or .env
- App launches directly to the home tab on first open
- Auth failures (401/403) show a snackbar warning "Authentication failed" — user stays on current screen, no redirect
- No Keychain storage for user-entered credentials (build-time config eliminates this)

#### Color Palette & Theming
- Dark-only theme — no light mode support, ignore iOS system setting
- Dark backgrounds with crypto-app aesthetic (similar to Binance/Coinbase dark mode)
- Primary accent color: Bitcoin orange (#F7931A) for buttons, highlights, and interactive elements
- P&L colors: Green (#00C087 or similar) for profit/up, Red for loss/down — standard Western convention

#### Error Presentation
- Network errors / API unreachable: snackbar at bottom + keep showing last-known cached data
- Auth failures (401/403): snackbar warning, no screen redirect
- No staleness indicator — user can pull-to-refresh if concerned
- Cold start with no cached data + API failure: centered "Could not load data" message with a Retry button

#### Navigation Structure
- Bottom tab bar with 4 tabs: Home | Chart | History | Config
- Icons: SF Symbols (Cupertino style) with text labels below each icon
- Standard iOS bottom tab bar pattern — always visible, quick switching

### Claude's Discretion
- Exact snackbar styling and duration
- Loading skeleton/spinner design
- Specific SF Symbol icon choices per tab
- Dark theme exact color values beyond Bitcoin orange and P&L green/red

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| APP-01 | Build-time API base URL and API key injection via --dart-define (replaces Keychain/setup screen) | `String.fromEnvironment` at compile time; `--dart-define=API_URL=... --dart-define=API_KEY=...` flags |
| APP-02 | App authenticates all API requests via x-api-key header injected automatically | Dio `InterceptorsWrapper.onRequest` adds header from compile-time const; verified pattern |
| APP-03 | Auth failures (401/403) show snackbar warning, no screen redirect (replaces redirect behavior) | Dio `InterceptorsWrapper.onError` checks `statusCode`; `ScaffoldMessenger.showSnackBar` from error interceptor |
| APP-04 | Dark-only theme ignoring iOS system setting (replaces light/dark system following) | `MaterialApp(theme: ThemeData(colorScheme: ColorScheme.fromSeed(..., brightness: Brightness.dark)))` — no `darkTheme`/`themeMode`; `useMaterial3: true` |
| APP-05 | Pull-to-refresh on all data screens re-fetches from API | `RefreshIndicator(onRefresh: () => ref.refresh(provider.future))` — official Riverpod pattern |
| APP-06 | Error snackbars for transient API failures with cached data visible (no staleness indicator) | `AsyncValue` keeps prior `.value` during re-fetch; show snackbar on error; `when(skipError: true)` preserves last data |
</phase_requirements>

---

## Summary

This phase scaffolds a Flutter iOS app with a locked-down stack: go_router 17 for navigation with `StatefulShellRoute.indexedStack` for the 4-tab bottom bar, Riverpod 3 (with code generation) for state management, and Dio 5 with a custom interceptor for API key injection. The key architectural decision is build-time configuration via `--dart-define` / `String.fromEnvironment` — no runtime setup screen, no Keychain. The app must be dark-only (Material 3 dark `ColorScheme`, Bitcoin orange seed).

The standard pattern for pull-to-refresh with Riverpod is well-documented and simple: `RefreshIndicator(onRefresh: () => ref.refresh(provider.future))`. The `AsyncValue` pattern natively preserves previous data during re-fetching, enabling the "show cached data + snackbar on error" UX without additional complexity.

The one gotcha to watch: **go_router 17 is a major release (January 2026)** and introduced breaking changes — most notably around `ShellRoute` observer notifications. Review the v17 migration guide before wiring up the router. Also, `CupertinoTabBar` has known limitations with `StatefulShellRoute`; use `NavigationBar` (Material 3) styled with Cupertino icons instead — this is the Flutter team's recommended approach for cross-platform bottom nav.

**Primary recommendation:** Scaffold with `flutter create --platforms=ios`, use Material 3 dark theme with `ColorScheme.fromSeed(seedColor: Color(0xFFF7931A), brightness: Brightness.dark)`, `StatefulShellRoute.indexedStack` with `NavigationBar` + `CupertinoIcons`, Dio interceptor for `x-api-key`, and `String.fromEnvironment` for build-time config.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| go_router | ^17.1.0 | Declarative routing, `StatefulShellRoute` for tab bar | Flutter team maintained; URL-based deep linking; official tab bar pattern |
| hooks_riverpod | ^3.2.1 | State management + hooks integration | Most popular Flutter state management; excellent async data handling; code gen support |
| riverpod_annotation | ^4.0.2 | `@riverpod` annotation for code generation | Eliminates boilerplate; type-safe providers |
| flutter_hooks | latest | Stateful hooks inside `HookConsumerWidget` | Pairs with hooks_riverpod; reduces StatefulWidget boilerplate |
| dio | ^5.9.1 | HTTP client with interceptor chain | Industry standard; clean interceptor API for API key injection; rich error types |
| cupertino_icons | ^1.0.8 | SF Symbols-style icons (1000+ icons) | Ships with Flutter SDK; iOS-native aesthetic |

### Supporting
| Library | Purpose | When to Use |
|---------|---------|-------------|
| riverpod_generator | ^4.0.3 | Build-time provider code gen | Always — paired with riverpod_annotation |
| build_runner | latest (dev) | Runs code generation | Required for riverpod_generator |
| riverpod_lint | latest (dev) | Riverpod-specific lint rules | Recommended; catches provider mistakes at dev time |
| custom_lint | latest (dev) | Custom lint runner | Required peer dep of riverpod_lint |

### Not Needed (Eliminated by Decisions)
| Package | Reason Not Needed |
|---------|-------------------|
| flutter_secure_storage | Build-time config eliminates runtime credential storage |
| envied | `--dart-define` is sufficient; ENVied adds complexity for personal single-user app |
| adaptive_theme | Dark-only — no theme switching needed |

### Installation
```bash
flutter create --platforms=ios trading_bot_app
cd trading_bot_app

# Core dependencies
flutter pub add go_router hooks_riverpod riverpod_annotation flutter_hooks dio cupertino_icons

# Dev dependencies
flutter pub add --dev build_runner riverpod_generator riverpod_lint custom_lint
```

---

## Architecture Patterns

### Recommended Project Structure

Feature-first is standard for Riverpod apps per Andrea Bizzotto (Flutter community authority). For this 4-tab personal app, a simplified feature-first layout is appropriate:

```
lib/
├── main.dart                    # ProviderScope, MaterialApp.router
├── app/
│   ├── router.dart              # GoRouter + StatefulShellRoute config
│   ├── theme.dart               # AppTheme (dark ThemeData, color constants)
│   └── di.dart                  # Shared Dio provider, ApiClient provider
├── core/
│   ├── api/
│   │   ├── api_client.dart      # Dio setup + ApiKeyInterceptor + error interceptor
│   │   └── api_exception.dart   # Typed exceptions (NetworkError, AuthError, etc.)
│   └── widgets/
│       ├── error_snackbar.dart  # Shared snackbar helper
│       └── retry_widget.dart    # "Could not load data" + Retry button
├── features/
│   ├── home/
│   │   └── presentation/
│   │       └── home_screen.dart
│   ├── chart/
│   │   └── presentation/
│   │       └── chart_screen.dart
│   ├── history/
│   │   └── presentation/
│   │       └── history_screen.dart
│   └── config/
│       └── presentation/
│           └── config_screen.dart
└── shared/
    └── navigation_shell.dart    # ScaffoldWithNestedNavigation widget
```

### Pattern 1: Build-Time Configuration via --dart-define

**What:** Compile-time constants injected at build; accessed via `String.fromEnvironment`.
**When to use:** Always — single source of truth for API URL and API key.

```dart
// lib/core/api/config.dart
// Source: https://dart.dev/libraries/core/environment-declarations
// Source: https://codewithandrea.com/articles/flutter-api-keys-dart-define-env-files/

const String kApiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5000',
);

const String kApiKey = String.fromEnvironment(
  'API_KEY',
  defaultValue: '',
);
```

Build command:
```bash
flutter run --dart-define=API_BASE_URL=https://your-api.example.com --dart-define=API_KEY=your-secret-key
flutter build ios --dart-define=API_BASE_URL=https://your-api.example.com --dart-define=API_KEY=your-secret-key
```

**Note:** `String.fromEnvironment` MUST be used as a `const` expression — the value is baked into the binary at compile time, not looked up at runtime.

### Pattern 2: Dio ApiKeyInterceptor

**What:** Dio interceptor that injects `x-api-key` header on every request; handles 401/403 with snackbar.
**When to use:** Always — single place for auth logic.

```dart
// lib/core/api/api_client.dart
// Source: Context7 /cfug/dio

class ApiKeyInterceptor extends Interceptor {
  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    options.headers['x-api-key'] = kApiKey;
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.response?.statusCode == 401 || err.response?.statusCode == 403) {
      // Signal auth failure — UI layer shows snackbar via error propagation
      handler.next(
        DioException(
          requestOptions: err.requestOptions,
          response: err.response,
          type: DioExceptionType.badResponse,
          error: AuthenticationException(),
        ),
      );
      return;
    }
    handler.next(err);
  }
}

Dio createDio() {
  final dio = Dio(
    BaseOptions(
      baseUrl: kApiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 30),
    ),
  );
  dio.interceptors.add(ApiKeyInterceptor());
  return dio;
}
```

### Pattern 3: StatefulShellRoute.indexedStack for 4-Tab Navigation

**What:** go_router's `StatefulShellRoute.indexedStack` preserves each tab's navigation stack independently.
**When to use:** Always for bottom tab bar with state preservation.

```dart
// lib/app/router.dart
// Source: Context7 /websites/pub_dev_packages_go_router
// Source: https://codewithandrea.com/articles/flutter-bottom-navigation-bar-nested-routes-gorouter/

final _rootNavigatorKey = GlobalKey<NavigatorState>();
final _homeNavKey = GlobalKey<NavigatorState>(debugLabel: 'home');
final _chartNavKey = GlobalKey<NavigatorState>(debugLabel: 'chart');
final _historyNavKey = GlobalKey<NavigatorState>(debugLabel: 'history');
final _configNavKey = GlobalKey<NavigatorState>(debugLabel: 'config');

final GoRouter appRouter = GoRouter(
  navigatorKey: _rootNavigatorKey,
  initialLocation: '/home',
  routes: [
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) =>
          ScaffoldWithNavigation(navigationShell: navigationShell),
      branches: [
        StatefulShellBranch(
          navigatorKey: _homeNavKey,
          routes: [GoRoute(path: '/home', builder: (_, __) => const HomeScreen())],
        ),
        StatefulShellBranch(
          navigatorKey: _chartNavKey,
          routes: [GoRoute(path: '/chart', builder: (_, __) => const ChartScreen())],
        ),
        StatefulShellBranch(
          navigatorKey: _historyNavKey,
          routes: [GoRoute(path: '/history', builder: (_, __) => const HistoryScreen())],
        ),
        StatefulShellBranch(
          navigatorKey: _configNavKey,
          routes: [GoRoute(path: '/config', builder: (_, __) => const ConfigScreen())],
        ),
      ],
    ),
  ],
);
```

### Pattern 4: Navigation Shell with NavigationBar + CupertinoIcons

**What:** Material 3 `NavigationBar` with `CupertinoIcons` — gives iOS icon aesthetic without `CupertinoTabScaffold` bugs.
**When to use:** Preferred over `CupertinoTabBar` + `StatefulShellRoute` (known compatibility bugs, see Pitfalls).

```dart
// lib/shared/navigation_shell.dart
// Source: https://codewithandrea.com/articles/flutter-bottom-navigation-bar-nested-routes-gorouter/

class ScaffoldWithNavigation extends StatelessWidget {
  const ScaffoldWithNavigation({required this.navigationShell, super.key});
  final StatefulNavigationShell navigationShell;

  void _goBranch(int index) {
    navigationShell.goBranch(
      index,
      // Pop to root of current tab when tapping active tab
      initialLocation: index == navigationShell.currentIndex,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        onDestinationSelected: _goBranch,
        destinations: const [
          NavigationDestination(
            icon: Icon(CupertinoIcons.house),
            selectedIcon: Icon(CupertinoIcons.house_fill),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(CupertinoIcons.chart_bar),
            selectedIcon: Icon(CupertinoIcons.chart_bar_fill),
            label: 'Chart',
          ),
          NavigationDestination(
            icon: Icon(CupertinoIcons.clock),
            selectedIcon: Icon(CupertinoIcons.clock_fill),
            label: 'History',
          ),
          NavigationDestination(
            icon: Icon(CupertinoIcons.settings),
            selectedIcon: Icon(CupertinoIcons.settings_solid),
            label: 'Config',
          ),
        ],
      ),
    );
  }
}
```

### Pattern 5: Dark-Only Material 3 Theme

**What:** Single dark `ThemeData` with Bitcoin orange seed color; no `darkTheme` or `themeMode` — ignores system setting.
**When to use:** Always — dark-only is the requirement.

```dart
// lib/app/theme.dart
// Source: Context7 /websites/flutter_dev

class AppTheme {
  // Locked brand colors
  static const Color bitcoinOrange = Color(0xFFF7931A);
  static const Color profitGreen = Color(0xFF00C087);
  static const Color lossRed = Color(0xFFFF4D4D);

  static ThemeData get dark => ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: bitcoinOrange,
      brightness: Brightness.dark,
    ),
    brightness: Brightness.dark,
    // NavigationBar theme for bottom tabs
    navigationBarTheme: const NavigationBarThemeData(
      backgroundColor: Color(0xFF1A1A1A),
      indicatorColor: Color(0x33F7931A), // orange with opacity
    ),
  );
}

// In main.dart / MaterialApp:
// theme: AppTheme.dark,
// (NO darkTheme, NO themeMode — forces dark always)
```

### Pattern 6: Pull-to-Refresh with Riverpod

**What:** `RefreshIndicator` wrapping scrollable content; `ref.refresh(provider.future)` triggers re-fetch.
**When to use:** All data screens.

```dart
// Source: https://riverpod.dev/docs/how_to/pull_to_refresh
// Source: Context7 /websites/riverpod_dev

@riverpod
Future<Portfolio> portfolio(Ref ref) async {
  final client = ref.watch(apiClientProvider);
  return client.getPortfolio();
}

class HomeScreen extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final portfolio = ref.watch(portfolioProvider);

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(portfolioProvider.future),
        child: ListView(
          children: [
            switch (portfolio) {
              // Data available (including stale data during re-fetch)
              AsyncValue<Portfolio>(:final value?) => PortfolioWidget(value),
              // Error with no prior data — show retry widget
              AsyncValue(:final error?) => const RetryWidget(),
              // Initial loading
              _ => const CircularProgressIndicator(),
            },
          ],
        ),
      ),
    );
  }
}
```

**Key insight:** During a re-fetch triggered by pull-to-refresh, `AsyncValue` preserves `.value` from the previous successful load. This gives the "show cached data while refreshing" behavior for free — show snackbar on `error` state while `value` is still non-null.

### Pattern 7: Snackbar Error Notifications

**What:** Centralized snackbar helper using `ScaffoldMessenger`.
**When to use:** API errors, auth failures — called from provider error handlers or UI.

```dart
// lib/core/widgets/error_snackbar.dart
// Source: https://docs.flutter.dev/cookbook/design/snackbars

void showErrorSnackbar(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
        backgroundColor: Colors.red.shade800,
        duration: const Duration(seconds: 4),
      ),
    );
}
```

Trigger from widget build (using Riverpod listener):
```dart
ref.listen(portfolioProvider, (previous, next) {
  if (next.hasError && !next.isLoading) {
    final isAuthError = next.error is AuthenticationException;
    showErrorSnackbar(
      context,
      isAuthError ? 'Authentication failed' : 'Could not load data',
    );
  }
});
```

### Anti-Patterns to Avoid

- **Using `CupertinoTabScaffold` with `StatefulShellRoute`:** Known bug causes crashes ("multiple widgets used the same GlobalKey") — use `NavigationBar` instead.
- **Calling `showSnackBar` during build:** Must be called in callbacks or `ref.listen` — not inside `build()` directly.
- **`String.fromEnvironment` at runtime:** Must be `const` — calling it non-const returns `defaultValue` always.
- **Sharing a single `Dio` instance across tests:** Create a new instance per test or mock via Riverpod override.
- **Not calling `handler.next(options)` in interceptor:** Causes requests to hang indefinitely.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Header injection on every request | Custom HTTP wrapper | Dio `InterceptorsWrapper.onRequest` | Handles concurrent requests, cancellation, retry correctly |
| Tab state preservation | `IndexedStack` + manual state | `StatefulShellRoute.indexedStack` | go_router manages navigator keys, back stack, deep links |
| Async data loading + refresh | Manual `Future`/`setState` | Riverpod `FutureProvider` + `ref.watch` | Handles loading/error/data states, deduplication, caching |
| Error state display during refresh | Flag variables | `AsyncValue.value` during re-fetch | Riverpod natively preserves prior value during re-fetch |

**Key insight:** Riverpod's `AsyncValue` combining `value` and `error` simultaneously (during re-fetch) eliminates the need for custom "stale data" tracking logic.

---

## Common Pitfalls

### Pitfall 1: CupertinoTabBar + StatefulShellRoute Crashes
**What goes wrong:** Using `CupertinoTabScaffold` or `CupertinoTabBar` with `StatefulShellRoute` causes "multiple widgets used the same GlobalKey" crash.
**Why it happens:** `CupertinoTabScaffold` manages its own navigator state internally, conflicting with go_router's navigator keys.
**How to avoid:** Use Material 3 `NavigationBar` with `CupertinoIcons` for iOS-aesthetic icons — no crash, full feature parity.
**Warning signs:** App crashes on tab switch; `GlobalKey` error in console.

### Pitfall 2: go_router v17 Breaking Changes
**What goes wrong:** ShellRoute observer behavior changed in v17 — `notifyRootObserver` parameter added; navigation observers may fire unexpectedly or not fire at all depending on prior expectations.
**Why it happens:** v17 is a major release (January 2026) that changed shell route observer default behavior.
**How to avoid:** Start fresh with v17 — don't port patterns from v6-v14 tutorials without checking the v17 migration guide. The `StatefulShellRoute.indexedStack` API itself is stable; it's the observer plumbing that changed.
**Warning signs:** Route change callbacks not firing; analytics/logging missing navigation events.

### Pitfall 3: String.fromEnvironment Not a const
**What goes wrong:** Using `String.fromEnvironment` without `const` always returns `defaultValue` at runtime.
**Why it happens:** Dart's `fromEnvironment` is a compile-time constant — only resolved at `const` evaluation time.
**How to avoid:** Always declare as `const String kApiKey = String.fromEnvironment('API_KEY', defaultValue: '');`
**Warning signs:** API key is always empty; interceptor sends wrong header.

### Pitfall 4: Snackbar Called During Build
**What goes wrong:** Calling `ScaffoldMessenger.of(context).showSnackBar()` inside `Widget.build()` throws "setState() or markNeedsBuild() called during build".
**Why it happens:** Scheduling UI side effects during widget build is illegal in Flutter.
**How to avoid:** Always call snackbar in `ref.listen` callback (Riverpod), event callbacks, or `WidgetsBinding.instance.addPostFrameCallback`.
**Warning signs:** Red error screen with setState during build message.

### Pitfall 5: Riverpod Code Gen Not Running
**What goes wrong:** `@riverpod` annotated functions don't generate `*Provider` symbols; build fails.
**Why it happens:** `build_runner` must be run (or watching) before code is usable.
**How to avoid:** Run `dart run build_runner watch -d` during development. Add to CI: `dart run build_runner build --delete-conflicting-outputs`.
**Warning signs:** Undefined name `*Provider` compilation errors.

### Pitfall 6: Missing `.future` in RefreshIndicator
**What goes wrong:** `onRefresh: () { ref.refresh(provider); }` (no `.future`) — refresh spinner disappears instantly, doesn't wait for data.
**Why it happens:** `onRefresh` must return a `Future` that completes when refresh is done.
**How to avoid:** Always use `onRefresh: () => ref.refresh(provider.future)`.
**Warning signs:** Pull-to-refresh spinner disappears in < 100ms; data not visibly updated.

---

## Code Examples

### Complete pubspec.yaml

```yaml
# Source: https://riverpod.dev/docs/introduction/getting_started
name: trading_bot_app
description: BTC Smart DCA Bot mobile dashboard
publish_to: 'none'
version: 1.0.0+1

environment:
  sdk: ^3.7.0
  flutter: ">=3.0.0"

dependencies:
  flutter:
    sdk: flutter
  cupertino_icons: ^1.0.8
  go_router: ^17.1.0
  hooks_riverpod: ^3.2.1
  flutter_hooks: any
  riverpod_annotation: ^4.0.2
  dio: ^5.9.1

dev_dependencies:
  flutter_test:
    sdk: flutter
  flutter_lints: ^5.0.0
  build_runner: any
  riverpod_generator: ^4.0.3
  riverpod_lint: any
  custom_lint: any

flutter:
  uses-material-design: true
```

### main.dart Entry Point

```dart
// Source: Context7 /websites/riverpod_dev
import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'app/router.dart';
import 'app/theme.dart';

void main() {
  runApp(
    const ProviderScope(
      child: TradingBotApp(),
    ),
  );
}

class TradingBotApp extends StatelessWidget {
  const TradingBotApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Trading Bot',
      // Dark-only — no darkTheme, no themeMode
      theme: AppTheme.dark,
      routerConfig: appRouter,
      debugShowCheckedModeBanner: false,
    );
  }
}
```

### flutter create scaffold command

```bash
# Create iOS-only Flutter project
flutter create --platforms=ios --org=com.yourname trading_bot_app

# Run code gen watcher during development
dart run build_runner watch --delete-conflicting-outputs

# Run app with build-time config
flutter run \
  --dart-define=API_BASE_URL=http://192.168.1.100:5000 \
  --dart-define=API_KEY=your-secret-key
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Provider` package | Riverpod 3 + code generation | 2024-2025 | `@riverpod` eliminates manual provider wiring; type-safe |
| `Navigator 1.0` push/pop | go_router `StatefulShellRoute` | 2022-2025 | Tab state preservation, deep linking, URL-based routing |
| `ShellRoute` for tabs | `StatefulShellRoute.indexedStack` | go_router v7+ | Each tab has independent Navigator stack; state preserved |
| `CupertinoTabBar` + go_router | `NavigationBar` + `CupertinoIcons` | Ongoing | `CupertinoTabBar` has known bug with `StatefulShellRoute`; Material NavigationBar works correctly |
| `http` package | `dio` with interceptors | N/A | Interceptor chain cleanly handles auth headers without per-call boilerplate |
| `setState` for async data | `FutureProvider` + `AsyncValue` | Riverpod v2+ | Loading/error/data states managed automatically |

**Deprecated/outdated:**
- `Provider` package: Replaced by Riverpod for new projects
- Manual `Navigator.push` for tab switching: Replaced by go_router `goBranch`
- `flutter_dotenv` for config: `--dart-define-from-file` is the official alternative (Flutter 3.7+)

---

## Open Questions

1. **go_router v17 migration guide specifics**
   - What we know: v17 changed ShellRoute observer notification defaults; `notifyRootObserver` parameter added
   - What's unclear: Whether any other `StatefulShellRoute.indexedStack` behavior changed beyond observers
   - Recommendation: Read https://flutter.dev/go/go-router-v17-breaking-changes before wiring up router; start with clean v17 implementation, not ported v6-v14 tutorials

2. **NavigationBar visual styling in dark theme**
   - What we know: `NavigationBarThemeData` allows `backgroundColor` and `indicatorColor` customization
   - What's unclear: Exact color values needed to achieve Binance/Coinbase dark look without running the app
   - Recommendation: Use `Color(0xFF1A1A1A)` for nav bar background; `Color(0xFF121212)` for scaffold; adjust via trial after first run

3. **Dio global error snackbar without BuildContext**
   - What we know: Dio interceptors don't have access to `BuildContext`; `ScaffoldMessenger` needs a context
   - What's unclear: Best pattern for surfacing interceptor errors to UI (Riverpod state vs. global messenger key)
   - Recommendation: Use a `GlobalKey<ScaffoldMessengerState>` passed to `MaterialApp.scaffoldMessengerKey`, or re-throw typed exceptions and handle with `ref.listen` in the widget layer — the latter is cleaner and more testable

---

## Sources

### Primary (HIGH confidence)
- Context7 `/cfug/dio` — Dio interceptor patterns, `InterceptorsWrapper`, `DioException` types
- Context7 `/websites/pub_dev_packages_go_router` — `StatefulShellRoute`, `StatefulShellBranch`, `goBranch`
- Context7 `/websites/riverpod_dev` — ProviderScope setup, pull-to-refresh pattern, `AsyncValue` states
- Context7 `/websites/flutter_dev` — ThemeData, dark mode, `NavigationBar`, `CupertinoTabBar`
- https://riverpod.dev/docs/introduction/getting_started — Exact pubspec.yaml versions (hooks_riverpod 3.2.1, riverpod_annotation 4.0.2, riverpod_generator 4.0.3)
- https://riverpod.dev/docs/how_to/pull_to_refresh — Pull-to-refresh with `ref.refresh(provider.future)`
- https://pub.dev/packages/dio — Latest stable: 5.9.1
- https://pub.dev/packages/go_router — Latest stable: 17.1.0 (v17 breaking changes confirmed)
- https://pub.dev/packages/hooks_riverpod — Latest stable: 3.2.1
- https://dart.dev/libraries/core/environment-declarations — `String.fromEnvironment` must be const

### Secondary (MEDIUM confidence)
- https://codewithandrea.com/articles/flutter-bottom-navigation-bar-nested-routes-gorouter/ — `StatefulShellRoute.indexedStack` pattern with 4-branch navigation shell (verified against Context7)
- https://codewithandrea.com/articles/flutter-api-keys-dart-define-env-files/ — `--dart-define` vs `.env` tradeoffs (verified against official Dart docs)
- https://codewithandrea.com/articles/flutter-project-structure/ — Feature-first project structure recommendation
- https://docs.flutter.dev/cookbook/design/snackbars — `ScaffoldMessenger.showSnackBar` pattern
- https://pub.dev/packages/go_router/changelog — v14-v17 breaking change summaries

### Tertiary (LOW confidence)
- GitHub issue https://github.com/flutter/flutter/issues/113757 — `CupertinoTabScaffold` + go_router GlobalKey crash (reported; resolution status unclear but issue is real and open)
- GitHub issue https://github.com/flutter/flutter/issues/164300 — Ongoing `CupertinoTabScaffold` navigation issue with StatefulShellRoute (February 2025)

---

## Metadata

**Confidence breakdown:**
- Standard stack versions: HIGH — verified from pub.dev directly (Feb 2026)
- go_router 17 patterns: HIGH — verified via Context7 + changelog
- Riverpod 3 patterns: HIGH — verified via Context7 + official docs
- Dio interceptor patterns: HIGH — verified via Context7
- CupertinoTabBar bug: MEDIUM — verified via multiple GitHub issues but exact version fix status unclear
- Dark theme color values: LOW — recommended values are estimates; visual confirmation needed after implementation

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (packages in active development; verify go_router version before planning)

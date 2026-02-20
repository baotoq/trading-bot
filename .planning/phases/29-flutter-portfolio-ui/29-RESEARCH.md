# Phase 29: Flutter Portfolio UI - Research

**Researched:** 2026-02-20
**Domain:** Flutter mobile UI, Riverpod state management, fl_chart PieChart, shared_preferences
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Portfolio overview layout:**
- Expandable sections per asset type (Crypto, ETF, Fixed Deposit) — collapsible headers with count and subtotal
- VND/USD toggle in the app bar as a global action — applies to all screens, persists across sessions
- Summary card at top shows total portfolio value + total unrealized P&L (absolute + percentage)
- Each asset row shows: current value, absolute P&L (e.g., +₫2.5M), and percentage P&L — all visible without tapping
- Green/red coloring for positive/negative P&L

**Transaction & deposit forms:**
- Full-screen form navigation for adding transactions (not bottom sheet)
- Unified form with tabs at top: Buy/Sell | Fixed Deposit — fields change per selection
- Asset picker uses type-ahead search — filters existing assets, with option to add new asset if not found
- After submission: snackbar success message + auto-navigate back to portfolio

**Allocation chart:**
- Donut chart with total portfolio value displayed in the center
- Placed below the summary card, above the expandable holdings sections — always visible on scroll
- Tap a segment to highlight it and show tooltip with asset type name, exact percentage, and value
- Color scheme uses the app's existing theme palette for consistency with other screens

### Claude's Discretion
- Staleness indicator styling ("price as of [date]", "converted at today's rate") — placement and visual treatment
- "Bot" badge design for auto-imported DCA transactions
- Transaction history screen layout and filter UX (bottom sheet filters already exist in history feature)
- Loading states and skeleton screens
- Empty states for each section
- Form field validation UX (inline errors vs summary)
- Chart library selection (fl_chart or similar)
- Navigation structure (new tab, sub-screen of existing feature, etc.)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DISP-01 | User can see total portfolio value with VND/USD currency toggle that persists across sessions | shared_preferences `setBool`/`getBool` for toggle state; Riverpod `StateProvider` wrapping SharedPreferences for global currency state |
| DISP-02 | User can see per-asset holdings with current value, unrealized P&L (absolute + percentage), grouped by asset type | `GET /api/portfolio/assets` returns `PortfolioAssetResponse` with `CurrentValueUsd`, `CurrentValueVnd`, `UnrealizedPnlUsd`, `UnrealizedPnlPercent`, `IsPriceStale`, `PriceUpdatedAt`; grouped by `AssetType` in Flutter with `ExpansionTile` |
| DISP-03 | User can see asset allocation pie chart by asset type (Crypto / ETF / Fixed Deposit) | `GET /api/portfolio/summary` returns `Allocations: List<AllocationDto>`; fl_chart `PieChart` with `centerSpaceRadius` for donut shape; `PieTouchData` for segment tap |
| DISP-04 | User can add manual buy/sell transactions via a form in the Flutter app | `POST /api/portfolio/assets/{id}/transactions` with `CreateTransactionRequest`; assets fetched via `GET /api/portfolio/assets`; full-screen form navigation via GoRouter push |
| DISP-05 | User can add fixed deposits via a dedicated form with principal, rate, dates, and compounding frequency | `POST /api/portfolio/fixed-deposits/` with `CreateFixedDepositRequest`; same full-screen form as DISP-04 with tab switching |
| DISP-06 | User can see transaction history across all assets with filtering by asset, type, and date range | **Backend gap**: no `GET /api/portfolio/assets/{id}/transactions` or cross-asset transaction list endpoint exists; needs new API endpoint + Flutter history sub-screen |
| DISP-07 | User can see fixed deposit details including accrued value, days to maturity, projected maturity amount | `GET /api/portfolio/fixed-deposits/{id}` returns `FixedDepositResponse` with `AccruedValueVnd`, `DaysToMaturity`, `ProjectedMaturityValueVnd`; detail sub-screen within portfolio |
| DISP-08 | Auto-imported DCA bot transactions show a "Bot" badge and are not editable/deletable | `TransactionResponse.Source == "Bot"` — conditionally render badge chip and hide edit/delete actions |
| DISP-09 | VN asset prices show staleness indicator ("price as of [date]") when using cached data | `PortfolioAssetResponse.IsPriceStale == true` — render small text below value using `timeago` or `intl DateFormat` |
| DISP-10 | Cross-currency values show "converted at today's rate" label | `PortfolioSummaryResponse.ExchangeRateUpdatedAt` — render caption text below cross-currency values |
</phase_requirements>

---

## Summary

Phase 29 is a pure Flutter frontend build on top of the completed Phase 28 backend. The backend API is fully in place for portfolio summary, per-asset breakdown, fixed deposit CRUD, and transaction creation. The single backend gap is DISP-06 (transaction history listing) — the API only has `POST /assets/{id}/transactions` for creation, no GET endpoint exists for listing transactions across assets. This endpoint must be added to the backend before or alongside the Flutter work.

The existing Flutter codebase (v3.0 conventions) is well-established: `hooks_riverpod` + `flutter_hooks`, `go_router` with `StatefulShellRoute.indexedStack`, `HookConsumerWidget` pattern, manual `fromJson` (no codegen), `fl_chart` for charts (already in pubspec), Material 3 dark theme. Portfolio will be a new 5th tab following identical structure to existing features. VND/USD toggle persistence requires `shared_preferences` (not yet in pubspec — needs adding).

**Primary recommendation:** Add the portfolio feature as a new `features/portfolio/` module with the standard `data/`, `presentation/` layering. Add `shared_preferences` to pubspec for currency toggle persistence. Use `fl_chart` `PieChart` with `centerSpaceRadius` for donut chart. Add one GET transactions endpoint to the backend for DISP-06.

---

## Standard Stack

### Core (already in pubspec — no additions needed except one)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| hooks_riverpod | ^3.2.1 | State management — all providers | Already in app, used by every feature |
| flutter_hooks | any | `useState`, `useTextEditingController`, `useEffect` in forms | Already in app, config form uses it extensively |
| riverpod_annotation | ^4.0.2 | Code-gen providers with `@riverpod` decorator | Already in app, all features use it |
| go_router | ^17.1.0 | Navigation — new tab + sub-screens | Already in app, `StatefulShellRoute` pattern established |
| fl_chart | ^1.1.1 | Donut/pie chart for allocation chart | Already in app, line chart in chart feature |
| intl | any | VND/USD number formatting, date formatting | Already in app, used by chart and history features |
| dio | ^5.9.1 | HTTP client with `ApiKeyInterceptor` | Already in app, all repositories use it |

### Needs Adding
| Library | Version | Purpose | Why |
|---------|---------|---------|-----|
| shared_preferences | ^2.5.0 | Persist VND/USD toggle across app restarts | DISP-01 requires session-persistent currency preference; not in pubspec |

**Installation:**
```bash
cd TradingBot.Mobile && flutter pub add shared_preferences
```

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| timeago | ^3.7.0 | "price as of X" staleness indicator | Already in app; use for relative date rendering of `PriceUpdatedAt` |
| build_runner | any | Code gen for `@riverpod` providers | Run after adding new providers with `@riverpod` annotation |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| shared_preferences | hive / flutter_secure_storage | Overkill for a single bool preference; shared_preferences is the standard Flutter first-party plugin |
| fl_chart PieChart | syncfusion_flutter_charts | fl_chart already in project, MIT license; syncfusion requires commercial license |
| ExpansionTile | Custom expandable widget | ExpansionTile is built-in Material 3 widget, zero deps, matches existing app style |

---

## Architecture Patterns

### Recommended Project Structure
```
lib/features/portfolio/
├── data/
│   ├── models/
│   │   ├── portfolio_summary_response.dart    # PortfolioSummaryResponse + AllocationDto
│   │   ├── portfolio_asset_response.dart      # PortfolioAssetResponse
│   │   ├── transaction_response.dart          # TransactionResponse
│   │   └── fixed_deposit_response.dart        # FixedDepositResponse
│   ├── portfolio_repository.dart              # HTTP calls to /api/portfolio/*
│   └── portfolio_providers.dart              # @riverpod providers
├── presentation/
│   ├── portfolio_screen.dart                  # Main tab screen (summary card + donut + expandable sections)
│   └── widgets/
│       ├── portfolio_summary_card.dart        # Total value + P&L card
│       ├── allocation_donut_chart.dart        # fl_chart PieChart donut
│       ├── asset_type_section.dart            # ExpansionTile section (Crypto/ETF/Fixed Deposit)
│       ├── asset_row.dart                     # Single asset row (value + P&L)
│       ├── fixed_deposit_row.dart             # Fixed deposit row (accrued + maturity)
│       ├── currency_toggle.dart               # VND/USD AppBar action toggle
│       └── staleness_label.dart               # "price as of [date]" caption
└── sub_screens/
    ├── add_transaction_screen.dart            # Full-screen form (Buy/Sell tab + Fixed Deposit tab)
    ├── transaction_history_screen.dart        # History list with filter bottom sheet
    └── fixed_deposit_detail_screen.dart       # Fixed deposit details
```

### Pattern 1: Global Currency Toggle with SharedPreferences + Riverpod

**What:** A `StateNotifierProvider` (or `@riverpod` notifier) that reads initial value from `SharedPreferences` and persists writes back to it. All currency-displaying widgets `ref.watch` this provider.

**When to use:** Any state that must survive app restarts.

**Example:**
```dart
// Source: pub.dev shared_preferences docs + existing project @riverpod pattern
@riverpod
class CurrencyPreference extends _$CurrencyPreference {
  static const _key = 'currency_vnd'; // true = VND, false = USD

  @override
  Future<bool> build() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool(_key) ?? true; // default: VND
  }

  Future<void> toggle() async {
    final current = await future;
    final next = !current;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_key, next);
    state = AsyncData(next);
  }
}
```

Widgets watch `currencyPreferenceProvider` to decide whether to show VND or USD values. Since the API always returns both, the toggle is pure display logic with no re-fetch.

### Pattern 2: Donut Chart with fl_chart PieChart

**What:** `fl_chart` `PieChart` with `centerSpaceRadius > 0` creates a donut. Tap callback updates touched segment index, which causes a `setState` to expand that segment's radius.

**When to use:** Allocation breakdown. API returns `List<AllocationDto>` with `AssetType`, `ValueUsd`, `Percentage`.

**Example:**
```dart
// Source: fl_chart pub.dev documentation + existing PriceLineChart.dart pattern
PieChart(
  PieChartData(
    centerSpaceRadius: 55,                 // Creates the donut hole
    centerSpaceColor: AppTheme.surfaceDark,
    sectionsSpace: 2,
    pieTouchData: PieTouchData(
      touchCallback: (event, response) {
        if (response?.touchedSection != null) {
          setState(() {
            _touchedIndex = response!.touchedSection!.touchedSectionIndex;
          });
        }
      },
    ),
    sections: allocations.asMap().entries.map((entry) {
      final isTouched = entry.key == _touchedIndex;
      return PieChartSectionData(
        value: entry.value.percentage,
        color: _colorForType(entry.value.assetType),
        radius: isTouched ? 65 : 55,       // Expand touched segment
        showTitle: false,                   // Text in center, not on slices
      );
    }).toList(),
  ),
)
```

Center value display requires a `Stack` with `PieChart` and a centered `Column` with total value text.

### Pattern 3: Expandable Asset Type Sections

**What:** `ExpansionTile` for each asset type (Crypto, ETF, Fixed Deposit). Header shows type name, asset count, and subtotal. Children are `AssetRow` widgets.

**Example:**
```dart
ExpansionTile(
  initiallyExpanded: true,
  title: Text('Crypto'),
  subtitle: Text(isVnd
      ? _formatVnd(cryptoSubtotalVnd)
      : _formatUsd(cryptoSubtotalUsd)),
  trailing: Text('${cryptoAssets.length} assets'),
  children: cryptoAssets.map((a) => AssetRow(asset: a, isVnd: isVnd)).toList(),
)
```

### Pattern 4: Full-Screen Form Navigation (Add Transaction / Add Fixed Deposit)

**What:** GoRouter `context.push('/portfolio/add-transaction')` to a new route outside the `StatefulShellBranch` (uses `rootNavigatorKey` to push over the bottom nav bar). Follows same approach as the existing history/config features would use for full-screen navigation.

**When to use:** Forms that must take full screen (user decision: not bottom sheet).

**Router additions:**
```dart
// In router.dart — add sub-routes to portfolio branch
StatefulShellBranch(
  navigatorKey: _portfolioNavKey,
  routes: [
    GoRoute(
      path: '/portfolio',
      builder: (_, __) => const PortfolioScreen(),
      routes: [
        GoRoute(
          path: 'add-transaction',
          parentNavigatorKey: rootNavigatorKey, // pushes over nav bar
          builder: (_, __) => const AddTransactionScreen(),
        ),
        GoRoute(
          path: 'transaction-history',
          parentNavigatorKey: rootNavigatorKey,
          builder: (_, __) => const TransactionHistoryScreen(),
        ),
        GoRoute(
          path: 'fixed-deposit/:id',
          parentNavigatorKey: rootNavigatorKey,
          builder: (context, state) => FixedDepositDetailScreen(
            id: state.pathParameters['id']!,
          ),
        ),
      ],
    ),
  ],
),
```

### Pattern 5: Type-Ahead Asset Picker

**What:** A `TextField` with `onChanged` filtering against the local list of assets. Show results in a `ListView` overlay (use `Overlay` or a simple `Column` below the field). Assets come from `portfolioAssetsProvider` already loaded for the portfolio screen.

**Note:** No dedicated search/autocomplete library needed; the project avoids extra dependencies. A `ValueNotifier<String>` driving a filtered list is sufficient. The existing `TierListEditor` pattern shows how the project handles inline editable lists without libraries.

### Pattern 6: Unified Tab Form (Buy/Sell | Fixed Deposit)

**What:** `TabBar` + `TabBarView` within the full-screen form, or simpler: `SegmentedButton` (Material 3) at top switching between two form modes. `useTextEditingController` for all fields (existing `config_edit_form.dart` pattern).

```dart
SegmentedButton<FormMode>(
  segments: const [
    ButtonSegment(value: FormMode.transaction, label: Text('Buy / Sell')),
    ButtonSegment(value: FormMode.fixedDeposit, label: Text('Fixed Deposit')),
  ],
  selected: {_mode},
  onSelectionChanged: (selection) => setState(() => _mode = selection.first),
)
```

### Anti-Patterns to Avoid

- **Re-fetching on currency toggle**: API returns both USD and VND values. Never invalidate the provider on toggle — just read the different field. Toggle is pure display logic.
- **Storing currency toggle in AsyncNotifier state without initializing from SharedPreferences**: Always read SharedPreferences in `build()` to ensure the toggle survives restarts.
- **Using `setState` inside `HookConsumerWidget`**: This project uses `useState` from `flutter_hooks` or `ConsumerStatefulWidget`. Pick one pattern per screen and be consistent. Config form uses `HookWidget` + `usestate`; History screen uses `ConsumerStatefulWidget`. For the portfolio screen (read-heavy), `HookConsumerWidget` is cleaner.
- **PieChart with `centerSpaceRadius: double.infinity`**: That fills the center and removes the donut hole. Set explicit radius (e.g. `55`).
- **Not adding `parentNavigatorKey: rootNavigatorKey` for full-screen routes**: Without this, the route pushes within the branch and the bottom nav bar stays visible.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Persistent bool preference | Custom file/DB storage | `shared_preferences` | Platform-native, first-party Flutter plugin, handles all platforms |
| Donut chart | Custom `CustomPainter` arc drawing | `fl_chart PieChart` with `centerSpaceRadius` | Already in project, handles touch, animation, and accessibility |
| Date formatting for staleness | Manual DateTime arithmetic string building | `intl DateFormat` + `timeago` | Both already in project, handles localization edge cases |
| VND number formatting | Custom string padding | `intl NumberFormat.currency(symbol: '₫', decimalDigits: 0)` | Handles thousands separators correctly across locales |

**Key insight:** The project already has all chart, formatting, and form tools. The only genuine addition is `shared_preferences` for the currency toggle.

---

## Common Pitfalls

### Pitfall 1: Backend Gap for Transaction History (DISP-06)

**What goes wrong:** DISP-06 requires browsing transaction history across assets. The backend only has `POST /api/portfolio/assets/{id}/transactions`. There is no GET endpoint.

**Why it happens:** Phase 28 built CRUD for creating transactions but omitted a list/filter endpoint.

**How to avoid:** Add `GET /api/portfolio/assets/{id}/transactions` to `PortfolioEndpoints.cs` before implementing the Flutter history screen. The response shape should reuse `TransactionResponse` and support optional query params `?type=Buy|Sell&startDate=&endDate=`. For cross-asset history, either add `GET /api/portfolio/transactions` or implement per-asset fetch with client-side merge.

**Recommendation:** Add `GET /api/portfolio/assets/{id}/transactions` (per-asset list with optional filters). Cross-asset view in Flutter fetches all assets then their transactions in parallel.

**Warning signs:** 404 errors when trying to list transactions from the portfolio screen.

### Pitfall 2: Currency Toggle Provider Async Bootstrap

**What goes wrong:** `SharedPreferences.getInstance()` is async. If the provider returns `AsyncValue<bool>`, all currency-displaying widgets must handle the loading state — adding boilerplate everywhere.

**Why it happens:** Riverpod `AsyncNotifier.build()` returns `Future<bool>`, so the initial state is `AsyncLoading`.

**How to avoid:** Default to VND (`true`) synchronously and replace with the stored value. One clean approach: pass a pre-loaded `SharedPreferences` instance from `main.dart` into the provider via a dependency, so the initial value is always available synchronously. Or accept `AsyncValue` and use `currencyProvider.value ?? true` everywhere.

**Recommendation:** Pre-load SharedPreferences in `main()` before `runApp()`, expose it as a `@Riverpod(keepAlive: true)` provider override.

```dart
// main.dart
void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  runApp(
    ProviderScope(
      overrides: [sharedPreferencesProvider.overrideWithValue(prefs)],
      child: const MyApp(),
    ),
  );
}
```

### Pitfall 3: PieChart Touch and Rebuild Performance

**What goes wrong:** Using `ConsumerWidget` or `HookConsumerWidget` for the donut chart means every touch event rebuilds the entire portfolio screen.

**Why it happens:** Touch state (`_touchedIndex`) is local state that triggers visual updates.

**How to avoid:** Extract `AllocationDonutChart` as a `StatefulWidget` with its own `_touchedIndex` local state. Touch events only rebuild the chart widget, not the parent screen.

### Pitfall 4: AssetType Enum Mismatch for FixedDeposit

**What goes wrong:** The backend `PortfolioAsset.AssetType` enum is `{ Crypto, ETF }` — Fixed Deposits are a separate aggregate (`FixedDeposit` model, not part of `PortfolioAsset`). The `AllocationDto` in summary uses `AssetType = "FixedDeposit"` string, but `GET /api/portfolio/assets` does NOT return fixed deposits.

**Why it happens:** Fixed deposits have their own `/api/portfolio/fixed-deposits/` endpoint. They appear in the summary `Allocations` but not in the assets list.

**How to avoid:** In Flutter, the portfolio screen must fetch from THREE sources:
1. `GET /api/portfolio/summary` — for summary card + allocations chart
2. `GET /api/portfolio/assets` — for Crypto and ETF sections
3. `GET /api/portfolio/fixed-deposits/` — for Fixed Deposit section

Use `Future.wait([...])` to fetch all three in parallel.

### Pitfall 5: Missing `parentNavigatorKey` on Full-Screen Routes

**What goes wrong:** A sub-route inside `StatefulShellBranch` without `parentNavigatorKey: rootNavigatorKey` pushes within the branch, leaving the bottom `NavigationBar` visible. This contradicts the "full-screen form" requirement.

**How to avoid:** Always set `parentNavigatorKey: rootNavigatorKey` on routes that should cover the bottom nav bar. See router pattern above.

---

## Code Examples

Verified patterns from official sources and existing project code:

### VND/USD Currency Formatting

```dart
// intl already in project — use these formatters
final vndFormatter = NumberFormat.currency(
  symbol: '₫',
  decimalDigits: 0,
  locale: 'vi_VN',
);
final usdFormatter = NumberFormat.currency(
  symbol: '\$',
  decimalDigits: 2,
  locale: 'en_US',
);

String formatValue(double usd, double vnd, bool isVnd) {
  return isVnd
      ? vndFormatter.format(vnd)
      : usdFormatter.format(usd);
}
```

### P&L Color Logic

```dart
// Uses existing AppTheme colors — matches project pattern
Color pnlColor(double pnl) {
  if (pnl > 0) return AppTheme.profitGreen;
  if (pnl < 0) return AppTheme.lossRed;
  return Colors.white54;
}

// P&L display with sign prefix
String formatPnl(double pnl, bool isVnd, double pnlVnd) {
  final sign = pnl >= 0 ? '+' : '';
  return isVnd
      ? '$sign${vndFormatter.format(pnlVnd)}'
      : '$sign${usdFormatter.format(pnl)}';
}
```

### Staleness Indicator

```dart
// DISP-09: "price as of [date]" — placed below the current value in AssetRow
if (asset.isPriceStale && asset.priceUpdatedAt != null)
  Text(
    'price as of ${DateFormat('MMM d').format(asset.priceUpdatedAt!.toLocal())}',
    style: const TextStyle(color: Colors.white38, fontSize: 10),
  ),

// DISP-10: "converted at today's rate" — placed below cross-currency values
if (showCrossRate)
  const Text(
    'converted at today\'s rate',
    style: TextStyle(color: Colors.white38, fontSize: 10),
  ),
```

### Bot Badge for Transactions

```dart
// DISP-08: badge chip on transaction list items — Claude's discretion for visual treatment
if (transaction.source == 'Bot')
  Container(
    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
    decoration: BoxDecoration(
      color: AppTheme.bitcoinOrange.withAlpha(40),
      borderRadius: BorderRadius.circular(4),
      border: Border.all(color: AppTheme.bitcoinOrange.withAlpha(128)),
    ),
    child: const Text(
      'Bot',
      style: TextStyle(
        color: AppTheme.bitcoinOrange,
        fontSize: 10,
        fontWeight: FontWeight.w600,
      ),
    ),
  ),
```

### Parallel Portfolio Data Fetch

```dart
// Source: existing home_providers.dart pattern — Future.wait for parallel fetching
@riverpod
Future<PortfolioPageData> portfolioPageData(Ref ref) async {
  final repo = ref.watch(portfolioRepositoryProvider);

  final results = await Future.wait([
    repo.fetchSummary(),         // GET /api/portfolio/summary
    repo.fetchAssets(),           // GET /api/portfolio/assets
    repo.fetchFixedDeposits(),    // GET /api/portfolio/fixed-deposits/
  ]);

  return PortfolioPageData(
    summary: results[0] as PortfolioSummaryResponse,
    assets: results[1] as List<PortfolioAssetResponse>,
    fixedDeposits: results[2] as List<FixedDepositResponse>,
  );
}
```

### Navigation to Full-Screen Form

```dart
// In portfolio_screen.dart — FAB to open add transaction form
FloatingActionButton(
  onPressed: () => context.push('/portfolio/add-transaction'),
  backgroundColor: AppTheme.bitcoinOrange,
  foregroundColor: Colors.black,
  child: const Icon(CupertinoIcons.add),
)

// In add_transaction_screen.dart — navigate back after success
if (context.mounted) {
  ScaffoldMessenger.of(context).showSnackBar(
    const SnackBar(content: Text('Transaction added')),
  );
  context.pop(); // returns to portfolio screen
}
```

---

## Backend Gap: Transaction History Endpoint

DISP-06 requires listing transactions. The backend must gain one new endpoint before the Flutter history screen is implementable.

**Recommended new endpoint:**
```
GET /api/portfolio/assets/{id}/transactions
  ?type=Buy|Sell
  &startDate=yyyy-MM-dd
  &endDate=yyyy-MM-dd
```

Response: `List<TransactionResponse>` (reusing existing DTO).

This is a simple EF Core query: `db.AssetTransactions.Where(t => t.PortfolioAssetId == id)` with optional filters, ordered by `Date DESC`.

For cross-asset view in Flutter: load all assets, then `Future.wait(assets.map(a => repo.fetchTransactions(a.id)))` and merge/sort client-side. This avoids a new complex cross-asset endpoint.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `StatefulWidget` with `setState` for all state | `HookConsumerWidget` + `useState` / `ConsumerStatefulWidget` | Established in v3.0 | Use hooks for functional-style state in this phase |
| `json_serializable` for models | Manual `fromJson` (no codegen for models) | Established in v3.0 | Write `fromJson` by hand; only `@riverpod` uses codegen |
| `Provider` package | `hooks_riverpod` + `riverpod_annotation` | Established in v3.0 | All providers use `@riverpod` annotation |
| `Navigator.push` | `context.push(path)` via GoRouter | Established in v3.0 | Always use GoRouter for navigation |

**Deprecated/outdated in this project:**
- `ChangeNotifier`/`Provider`: Not used; project is Riverpod-only.
- `StatefulWidget` for screens: Use `ConsumerStatefulWidget` if lifecycle needed, `HookConsumerWidget` if hooks preferred.

---

## Open Questions

1. **Transaction history: per-asset or cross-asset endpoint?**
   - What we know: Backend only has POST; GET is missing. DISP-06 says "across all assets."
   - What's unclear: Should a new `GET /api/portfolio/transactions` aggregate across all assets server-side, or is per-asset fetch + client merge acceptable?
   - Recommendation: Per-asset fetch + client merge is simpler to implement and avoids a new complex endpoint. Acceptable given the small number of assets.

2. **VND/USD toggle scope: global vs. per-screen?**
   - What we know: User decision says "global action — applies to all screens."
   - What's unclear: Does this affect the existing Home screen (BTC P&L in USD) or only the portfolio feature?
   - Recommendation: Scope the toggle to the portfolio feature only (new tab). Existing home screen shows BTC stats which are inherently USD-denominated. Avoids retrofitting existing screens.

3. **NavigationBar 5th tab or sub-screen?**
   - What we know: CONTEXT.md says "portfolio will be a new feature module following the same structure." Existing app has 4 tabs.
   - What's unclear: 5th tab was implied but not explicitly stated.
   - Recommendation: Add portfolio as the 5th `NavigationDestination` in `ScaffoldWithNavigation`. This matches the `StatefulShellBranch` pattern exactly.

---

## Sources

### Primary (HIGH confidence)
- `/websites/pub_dev_fl_chart` Context7 — PieChart, PieChartData, PieChartSectionData, PieTouchData API surface
- `/websites/pub_dev_packages_shared_preferences` Context7 — SharedPreferences getInstance, getBool, setBool
- Existing codebase (`TradingBot.Mobile/lib/`) — ALL patterns verified from live source files
- `TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs` — API shapes verified directly
- `TradingBot.ApiService/Endpoints/FixedDepositEndpoints.cs` — Fixed deposit API verified
- `TradingBot.ApiService/Endpoints/PortfolioDtos.cs` + `FixedDepositDtos.cs` — DTO shapes confirmed

### Secondary (MEDIUM confidence)
- `pubspec.yaml` — verified current dependency versions

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — verified from pubspec.yaml and existing feature code
- Architecture: HIGH — directly derived from existing feature structure (home, chart, history, config)
- API shapes: HIGH — read directly from C# DTO and endpoint files
- fl_chart PieChart: HIGH — verified via Context7 docs
- shared_preferences: HIGH — verified via Context7 docs
- Pitfalls: HIGH — backend gap confirmed by grep of all endpoints; others derived from project patterns

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (stable Flutter ecosystem; fl_chart and shared_preferences APIs are stable)

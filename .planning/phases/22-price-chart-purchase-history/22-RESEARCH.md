# Phase 22: Price Chart + Purchase History - Research

**Researched:** 2026-02-20
**Domain:** Flutter charting (fl_chart), infinite scroll pagination (Riverpod AsyncNotifier), bottom sheet filters
**Confidence:** HIGH

## Summary

Phase 22 implements two screens that are already scaffolded as empty stubs: `ChartScreen` (at `/chart`) and `HistoryScreen` (at `/history`). The backend API is fully implemented — `/api/dashboard/chart` returns price points, purchase markers (with tier), and average cost basis; `/api/dashboard/purchases` supports cursor pagination with date/tier filters. No backend changes are required.

The chart screen requires `fl_chart` (v1.1.1), which the project does not yet have as a dependency. The key technical challenge is rendering purchase markers as colored dots on top of a BTC price line chart — this is achievable via a second transparent `LineChartBarData` entry whose dots use `FlDotData` with per-spot color logic based on tier. A horizontal dashed `HorizontalLine` in `ExtraLinesData` handles the average cost basis line. Touch tooltips are built-in via `LineTouchData`.

The history screen requires implementing infinite scroll with cursor pagination. The recommended approach is a manual `AsyncNotifier` with accumulated list state (no third-party pagination packages) using `NotificationListener<ScrollEndNotification>` for load-more detection. The filter bottom sheet uses Flutter's built-in `showModalBottomSheet` + `showDateRangePicker` with local `StatefulWidget` state — no additional packages.

**Primary recommendation:** Add `fl_chart: ^1.1.1` to pubspec.yaml. Use a second transparent `LineChartBarData` for purchase dot markers. Use a manual `AsyncNotifier` accumulation pattern for cursor pagination. Keep filter bottom sheet fully stateful and self-contained.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CHART-01 | User can view BTC price chart with 6 timeframe options (7D/1M/3M/6M/1Y/All) | `LineChart` with `LineChartData`; timeframe selector rebuilds provider with new timeframe param; backend `/api/dashboard/chart?timeframe=X` already supports all 6 values |
| CHART-02 | User can see purchase markers on price chart colored by multiplier tier | Second invisible `LineChartBarData` with `FlDotData(getDotPainter:...)` using `checkToShowDot` against a Set of purchase dates; color mapped by tier string |
| CHART-03 | User can see average cost basis dashed line on price chart | `ExtraLinesData(horizontalLines: [HorizontalLine(y: avgCostBasis, dashArray: [5, 5])])` in `LineChartData` |
| CHART-04 | User can touch chart to see price and date tooltip | `LineTouchData(handleBuiltInTouches: true, touchTooltipData: LineTouchTooltipData(getTooltipItems:...))` — built into fl_chart |
| CHART-05 | User can scroll through purchase history with infinite scroll (cursor pagination) | Manual `AsyncNotifier` accumulation pattern + `NotificationListener<ScrollEndNotification>`; backend already returns `hasMore` + `nextCursor` |
| CHART-06 | User can filter purchase history by date range and multiplier tier via bottom sheet | `showModalBottomSheet` + `showDateRangePicker` + chip group for tier selection; filter params passed to provider |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| fl_chart | ^1.1.1 | BTC price line chart with touch, dots, dashed lines | The de-facto Flutter charting library; 7k+ likes, 1M+ downloads; supports all required features natively |
| hooks_riverpod | ^3.2.1 | State management (already in project) | Already used for HomeScreen providers |
| flutter_hooks | any | Hook-based widget lifecycle (already in project) | Already used in project |
| riverpod_annotation | ^4.0.2 | Code generation for providers (already in project) | Already used for home providers |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| intl | any | Date/price formatting (already in project) | NumberFormat for price display in tooltip and history list |
| timeago | ^3.7.0 | Relative time in history list items (already in project) | "2 days ago" for purchase date in history |

### Not Adding (Deliberate)
| Instead of | Not Using | Reason |
|------------|-----------|--------|
| riverpod_paging_utils | manual AsyncNotifier | Package is v1.0.0 published 47 days ago — too new, unproven in production; manual pattern is simple enough (< 30 lines) |
| riverpod_infinite_scroll_pagination | manual AsyncNotifier | Targets Riverpod 2.x, not 3.x — incompatible |
| bottom_picker / date_picker_bottom_sheet | Flutter built-in showDateRangePicker | No extra dependency needed; Material built-ins are sufficient |

**Installation:**
```bash
# Run from TradingBot.Mobile/
flutter pub add fl_chart
```

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.Mobile/lib/features/
├── chart/
│   ├── data/
│   │   ├── models/
│   │   │   ├── chart_response.dart        # PriceChartResponse, PricePointDto, PurchaseMarkerDto
│   │   ├── chart_repository.dart          # fetchChart(timeframe) -> ChartResponse
│   │   └── chart_providers.dart           # chartDataProvider(timeframe), chartRepositoryProvider
│   │   └── chart_providers.g.dart
│   └── presentation/
│       ├── chart_screen.dart              # ChartScreen (timeframe selector + chart)
│       └── widgets/
│           ├── timeframe_selector.dart    # Segmented button row for 7D/1M/3M/6M/1Y/All
│           ├── price_line_chart.dart      # fl_chart LineChart widget
│           └── purchase_dot_painter.dart  # Custom FlDotPainter for tier-colored dots
├── history/
│   ├── data/
│   │   ├── models/
│   │   │   ├── purchase_history_response.dart  # PurchaseHistoryResponse, PurchaseDto
│   │   ├── history_repository.dart             # fetchPurchases(cursor, filters)
│   │   └── history_providers.dart              # purchaseHistoryProvider (AsyncNotifier)
│   │   └── history_providers.g.dart
│   └── presentation/
│       ├── history_screen.dart                 # HistoryScreen (list + FAB filter)
│       └── widgets/
│           ├── purchase_list_item.dart          # Single row: date, price, BTC, multiplier chip
│           └── filter_bottom_sheet.dart         # Date range + tier chips filter panel
```

### Pattern 1: Chart Data Layer
**What:** Single `@riverpod` async provider parameterized by timeframe; fetches from `/api/dashboard/chart?timeframe=X`; no auto-refresh (pull-to-refresh only).
**When to use:** Chart data is historical; real-time refresh not required.

```dart
// Source: project pattern from home_providers.dart
@riverpod
ChartRepository chartRepository(Ref ref) {
  return ChartRepository(ref.watch(dioProvider));
}

@riverpod
Future<ChartResponse> chartData(Ref ref, {String timeframe = '1M'}) async {
  final repo = ref.watch(chartRepositoryProvider);
  return repo.fetchChart(timeframe);
}
```

### Pattern 2: fl_chart LineChart with Purchase Markers
**What:** Two `LineChartBarData` entries in `lineBarsData`. First: the visible BTC price line. Second: an invisible line (no visible line, `barWidth: 0`) whose `FlDotData.getDotPainter` renders colored circles only at indices matching purchase dates.
**When to use:** When markers need to appear at non-uniform x positions on a continuous line chart.

```dart
// Source: fl_chart docs - LineChartBarData, FlDotData
LineChart(
  LineChartData(
    lineBarsData: [
      // 1. Main BTC price line
      LineChartBarData(
        spots: priceSpots,  // FlSpot(dayIndex.toDouble(), price)
        isCurved: true,
        color: AppTheme.bitcoinOrange,
        barWidth: 2,
        dotData: const FlDotData(show: false),  // no dots on price line
        belowBarData: BarAreaData(
          show: true,
          gradient: LinearGradient(
            colors: [AppTheme.bitcoinOrange.withAlpha(51), Colors.transparent],
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
          ),
        ),
      ),
      // 2. Invisible line for purchase markers
      LineChartBarData(
        spots: priceSpots,  // same x-axis, same y positions
        barWidth: 0,
        color: Colors.transparent,
        dotData: FlDotData(
          show: true,
          checkToShowDot: (spot, barData) => purchaseDayIndices.contains(spot.x.toInt()),
          getDotPainter: (spot, percent, barData, index) {
            final tier = purchasesByDayIndex[spot.x.toInt()]?.tier ?? 'Base';
            return FlDotCirclePainter(
              radius: 5,
              color: _tierColor(tier),
              strokeWidth: 1.5,
              strokeColor: Colors.white,
            );
          },
        ),
      ),
    ],
    extraLinesData: averageCostBasis != null ? ExtraLinesData(
      horizontalLines: [
        HorizontalLine(
          y: averageCostBasis,
          color: Colors.white.withAlpha(153),
          strokeWidth: 1,
          dashArray: [6, 4],
          label: HorizontalLineLabel(
            show: true,
            labelResolver: (_) => 'Avg',
            style: const TextStyle(color: Colors.white54, fontSize: 10),
          ),
        ),
      ],
    ) : null,
    lineTouchData: LineTouchData(
      handleBuiltInTouches: true,
      touchTooltipData: LineTouchTooltipData(
        getTooltipItems: (touchedSpots) {
          return touchedSpots.take(1).map((spot) {
            return LineTooltipItem(
              '${_dateLabel(spot.x.toInt())}\n\$${_formatPrice(spot.y)}',
              const TextStyle(color: Colors.white, fontSize: 12),
            );
          }).toList();
        },
      ),
    ),
  ),
)
```

### Pattern 3: Cursor-Based Infinite Scroll with AsyncNotifier
**What:** Manual `AsyncNotifier` that accumulates pages; `NotificationListener<ScrollEndNotification>` triggers `loadNextPage()` when the user hits the bottom of the list.
**When to use:** Cursor pagination where the next cursor comes from the server response.

```dart
// Source: dinkomarinac.dev pattern, adapted for project conventions
@riverpod
class PurchaseHistory extends _$PurchaseHistory {
  String? _nextCursor;
  bool _hasMore = true;
  // Filter state
  DateTimeRange? _dateRange;
  String? _tierFilter;

  @override
  Future<List<PurchaseDto>> build() async {
    _nextCursor = null;
    _hasMore = true;
    return _fetchPage(cursor: null);
  }

  Future<void> loadNextPage() async {
    if (!_hasMore || state.isLoading) return;
    final current = state.value ?? [];
    state = AsyncLoading<List<PurchaseDto>>().copyWithPrevious(AsyncData(current));
    state = await AsyncValue.guard(() async {
      final page = await _fetchPage(cursor: _nextCursor);
      return [...current, ...page];
    });
  }

  Future<List<PurchaseDto>> _fetchPage({String? cursor}) async {
    final repo = ref.read(historyRepositoryProvider);
    final response = await repo.fetchPurchases(
      cursor: cursor,
      startDate: _dateRange?.start,
      endDate: _dateRange?.end,
      tier: _tierFilter,
    );
    _nextCursor = response.nextCursor;
    _hasMore = response.hasMore;
    return response.items;
  }

  void applyFilter({DateTimeRange? dateRange, String? tier}) {
    _dateRange = dateRange;
    _tierFilter = tier;
    ref.invalidateSelf();
  }
}
```

In the widget, detect scroll end:
```dart
NotificationListener<ScrollEndNotification>(
  onNotification: (notification) {
    if (notification.metrics.pixels >= notification.metrics.maxScrollExtent - 100) {
      ref.read(purchaseHistoryProvider.notifier).loadNextPage();
    }
    return false;
  },
  child: ListView.builder(
    itemCount: items.length + (_hasMore ? 1 : 0),
    itemBuilder: (context, index) {
      if (index == items.length) return const CircularProgressIndicator();
      return PurchaseListItem(purchase: items[index]);
    },
  ),
)
```

### Pattern 4: Filter Bottom Sheet
**What:** `showModalBottomSheet` with a `StatefulWidget` child (local state for pending filter values); contains a date range button (`showDateRangePicker`) and tier chip group; Apply button calls `provider.applyFilter(...)`.

```dart
// Source: Flutter Material docs + project pattern
void _openFilter(BuildContext context, WidgetRef ref) {
  showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    builder: (_) => FilterBottomSheet(
      onApply: (dateRange, tier) {
        ref.read(purchaseHistoryProvider.notifier).applyFilter(
          dateRange: dateRange,
          tier: tier,
        );
      },
    ),
  );
}
```

Inside `FilterBottomSheet` (StatefulWidget):
```dart
class FilterBottomSheet extends StatefulWidget {
  // holds DateTimeRange? _selectedRange and String? _selectedTier
  // showDateRangePicker when date row tapped
  // ChoiceChip / FilterChip row for tier: Base, 2x, 3x, 4x
}
```

### Pattern 5: x-Axis Coordinate System
**What:** fl_chart uses `double` for x and y. For a price chart, use day index (0, 1, 2, ...) as x and price as y. Store a mapping `List<String> dateLabels` to convert x-index back to "YYYY-MM-DD" for tooltips and title labels.

**Important:** The API returns `date: "2024-01-15"` strings, not timestamps. Convert to index on the client:
```dart
final spots = prices.asMap().entries.map((e) {
  return FlSpot(e.key.toDouble(), (e.value.price as num).toDouble());
}).toList();
final dateLabels = prices.map((p) => p.date).toList();
```

### Anti-Patterns to Avoid
- **Parsing dates into milliseconds for x-axis**: Produces huge x values (1.7e12) that cause floating-point display issues in fl_chart axis titles. Use index (0, 1, 2...) instead and map back to dates.
- **Keeping full purchase list in multiple providers**: One `AsyncNotifier` holds accumulated pages; filter resets by calling `invalidateSelf()`, which re-runs `build()`.
- **Using setState in AsyncNotifier**: Never call `setState`. Use `state =` assignment or `ref.invalidateSelf()`.
- **Opening filter sheet and navigating away**: Bottom sheet filter state is local to the sheet widget; applying sends values to the notifier before closing.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Line chart rendering | Custom Canvas painter | `fl_chart LineChart` | Touch detection, axis labels, animation, hit testing are 500+ lines of complexity |
| Dashed horizontal line | Custom painter overlay | `HorizontalLine(dashArray: [6, 4])` | Built into `ExtraLinesData` |
| Touch tooltip | GestureDetector + Overlay | `LineTouchData(handleBuiltInTouches: true)` | fl_chart handles hit testing, tooltip positioning, and dismissal |
| Date range picker | Custom date grid | `showDateRangePicker` (Flutter built-in) | Material 3 date range picker, localized, accessible |

**Key insight:** fl_chart already handles every required chart interaction natively. The only custom code needed is the dot color mapping (tier -> Color) and tooltip text formatting.

## Common Pitfalls

### Pitfall 1: Purchase markers not showing on correct y-position
**What goes wrong:** The purchase marker line uses price data from the chart response, but the purchase's `price` may not exactly match a price-line spot — causing the dot to render at the wrong height.
**Why it happens:** The chart endpoint returns daily `close` prices; a purchase can happen at any intraday price.
**How to avoid:** For the marker `LineChartBarData`, each spot's y must come from the purchase's own price, not the closest close price. Construct a separate `purchaseSpots` list: `FlSpot(dayIndex, purchase.price)`. This requires matching purchases to their day index in the chart date list. The second `LineChartBarData` uses `purchaseSpots` instead of `priceSpots`.
**Warning signs:** All purchase dots appearing at the price line level exactly — inspect if purchase prices match close prices.

### Pitfall 2: x-axis title labels cluttering small timeframes
**What goes wrong:** "7D" timeframe shows 7 dates and all are visible, fine. "All" timeframe can have 3650 labels overlapping.
**Why it happens:** fl_chart renders `SideTitles` for every data point by default.
**How to avoid:** Use `interval` on `SideTitles` proportional to the timeframe:
```dart
SideTitles(
  showTitles: true,
  interval: _xInterval(timeframe),  // 7D=1, 1M=7, 3M=14, 6M=30, 1Y=60, All=180
  getTitlesWidget: (value, meta) { ... },
)
```

### Pitfall 3: Riverpod 3.x provider invalidation resets cursor
**What goes wrong:** Calling `ref.invalidateSelf()` after `applyFilter()` correctly resets the list, but if `loadNextPage()` is called while invalidation is in-flight, state corruption occurs.
**Why it happens:** Race between `invalidateSelf()` resetting `_nextCursor = null` (in `build()`) and an in-flight `loadNextPage()` that still holds a stale cursor.
**How to avoid:** In `loadNextPage()`, guard with `if (!_hasMore || state.isLoading) return;`. Since `invalidateSelf()` sets state to `AsyncLoading`, the guard will prevent concurrent loads.
**Warning signs:** Duplicate items appearing in the list, or a "cursor not found" error from the server.

### Pitfall 4: Bottom sheet filter state lost on theme rebuild
**What goes wrong:** The filter bottom sheet loses its pending selection when the device orientation changes.
**Why it happens:** `showModalBottomSheet` creates a new route; StatefulWidget rebuilds on config change but `StatefulWidget` state persists within the route lifecycle.
**How to avoid:** This is acceptable behavior for a modal filter sheet. Document that the sheet must be re-opened if orientation changes during editing. Do not add complex state restoration for this case.

### Pitfall 5: fl_chart 1.0.0+ breaking changes from older docs
**What goes wrong:** Copy-pasting examples from pre-1.0 documentation (pre-2024) — the `tooltipBgColor` parameter was removed in 1.0.0 and replaced with `getTooltipColor`.
**Why it happens:** Much fl_chart blog content targets version 0.6x/0.7x.
**How to avoid:** Always reference Context7 or pub.dev docs for version 1.1.1 API. `LineTouchTooltipData` no longer accepts `tooltipBgColor`; use `getTooltipColor: (_) => Colors.grey[900]!` instead.
**Warning signs:** Compile error on `tooltipBgColor` parameter.

### Pitfall 6: Known risk from STATE.md — scatter markers fallback
**What goes wrong:** STATE.md notes "scatter markers overlaid on line chart may need fallback to vertical dashed lines".
**Why it happens:** fl_chart does not have a "scatter layer" on top of a line chart; the approach requires the second transparent `LineChartBarData` trick.
**Resolution:** The two-`LineChartBarData` approach is confirmed valid and does not need a fallback. The second bar uses `purchaseSpots` (purchase price, not close price) and renders dots with `checkToShowDot` returning true only for positions in `purchaseDayIndices`. This is a verified fl_chart pattern.
**Warning signs:** If a purchase date is not in the chart date list (e.g., data gap), the marker simply will not appear — this is acceptable.

## Code Examples

Verified patterns from official sources:

### Average Cost Basis Dashed Line
```dart
// Source: Context7 - fl_chart ExtraLinesData / HorizontalLine docs
ExtraLinesData(
  horizontalLines: [
    HorizontalLine(
      y: 45000.0,  // averageCostBasis value
      color: Colors.white54,
      strokeWidth: 1.5,
      dashArray: [6, 4],
    ),
  ],
)
```

### Touch Tooltip with Date and Price
```dart
// Source: Context7 - fl_chart LineTouchTooltipData docs
LineTouchData(
  handleBuiltInTouches: true,
  touchTooltipData: LineTouchTooltipData(
    getTooltipColor: (_) => const Color(0xFF2C2C2C),
    getTooltipItems: (touchedSpots) {
      // Only show tooltip from first bar (price line), not purchase marker bar
      final priceSpot = touchedSpots.firstWhere(
        (s) => s.barIndex == 0,
        orElse: () => touchedSpots.first,
      );
      final dateLabel = dateLabels[priceSpot.x.toInt()];
      final price = NumberFormat.currency(symbol: '\$', decimalDigits: 0)
          .format(priceSpot.y);
      return [
        LineTooltipItem(
          '$dateLabel\n$price',
          const TextStyle(color: Colors.white, fontSize: 12),
        ),
        // Return null for second bar (purchase markers) to suppress second tooltip row
        if (touchedSpots.length > 1) null,
      ].whereType<LineTooltipItem>().toList();
    },
  ),
)
```

### FlDotCirclePainter for Tier-Colored Markers
```dart
// Source: Context7 - fl_chart FlDotData / FlDotCirclePainter docs
Color _tierColor(String tier) {
  return switch (tier) {
    '2x' || 'Tier2' => Colors.amber,
    '3x' || 'Tier3' => Colors.orange,
    '4x' || 'Tier4' || _ when tier.contains('4') => AppTheme.lossRed,
    _ => AppTheme.bitcoinOrange,  // Base / 1x
  };
}

FlDotData(
  show: true,
  checkToShowDot: (spot, barData) =>
      purchaseDayIndexSet.contains(spot.x.toInt()),
  getDotPainter: (spot, percent, barData, index) => FlDotCirclePainter(
    radius: 5,
    color: _tierColor(purchasesByIndex[spot.x.toInt()]?.tier ?? 'Base'),
    strokeWidth: 1.5,
    strokeColor: Colors.white,
  ),
)
```

### Cursor Pagination AsyncNotifier Structure
```dart
// Source: Project pattern from home_providers.dart + dinkomarinac.dev
@riverpod
class PurchaseHistory extends _$PurchaseHistory {
  String? _nextCursor;
  bool _hasMore = true;

  @override
  Future<List<PurchaseDto>> build() async {
    _nextCursor = null;
    _hasMore = true;
    final repo = ref.read(historyRepositoryProvider);
    final result = await repo.fetchPurchases(cursor: null);
    _nextCursor = result.nextCursor;
    _hasMore = result.hasMore;
    return result.items;
  }

  Future<void> loadNextPage() async {
    if (!_hasMore || state.isLoading) return;
    final current = state.requireValue;
    state = const AsyncLoading<List<PurchaseDto>>().copyWithPrevious(
      AsyncData(current),
    );
    state = await AsyncValue.guard(() async {
      final repo = ref.read(historyRepositoryProvider);
      final result = await repo.fetchPurchases(cursor: _nextCursor);
      _nextCursor = result.nextCursor;
      _hasMore = result.hasMore;
      return [...current, ...result.items];
    });
  }
}
```

### API Date Query Parameter Format
```dart
// Source: DashboardEndpoints.cs - startDate: DateOnly?, endDate: DateOnly?, tier: string?
// Backend uses query string: /api/dashboard/purchases?cursor=X&pageSize=20&startDate=2024-01-01&endDate=2024-06-30&tier=2x
Future<PurchaseHistoryResponse> fetchPurchases({
  String? cursor,
  DateTime? startDate,
  DateTime? endDate,
  String? tier,
  int pageSize = 20,
}) async {
  final params = <String, dynamic>{'pageSize': pageSize};
  if (cursor != null) params['cursor'] = cursor;
  if (startDate != null) params['startDate'] = DateFormat('yyyy-MM-dd').format(startDate);
  if (endDate != null) params['endDate'] = DateFormat('yyyy-MM-dd').format(endDate);
  if (tier != null && tier.isNotEmpty) params['tier'] = tier;

  final response = await _dio.get('/api/dashboard/purchases', queryParameters: params);
  return PurchaseHistoryResponse.fromJson(response.data as Map<String, dynamic>);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| fl_chart `tooltipBgColor` param | `getTooltipColor` callback | fl_chart 1.0.0 (2024) | Old blog posts will cause compile errors |
| `StateNotifier` for pagination | `AsyncNotifier` | Riverpod 3.0 (Sep 2025) | All pagination packages targeting StateNotifier are obsolete |
| `riverpod_infinite_scroll` | Manual `AsyncNotifier` + mixin | Riverpod 3.x | Package targets Riverpod 2.x |
| fl_chart 0.6x `LineChartBarData` positional args | Named parameters throughout | fl_chart 1.0+ | Old StackOverflow answers will break |

**Deprecated/outdated:**
- `tooltipBgColor` in `LineTouchTooltipData`: removed in fl_chart 1.0.0, replaced with `getTooltipColor` callback
- `StateNotifier`-based pagination packages: `riverpod_infinite_scroll` requires Riverpod 2.x; use manual `AsyncNotifier` instead

## Open Questions

1. **Tier string values from the API**
   - What we know: `PurchaseMarkerDto.Tier` returns `p.MultiplierTier ?? "Base"` from the endpoint code. `MultiplierTier` is set from `DcaOptions.MultiplierTiers[n].DropPercentage` labels.
   - What's unclear: The exact string values (e.g., "Base", "2x", "3x") depend on what was configured in `DcaOptions` at purchase time. They are stored as strings in the DB.
   - Recommendation: In the tier color mapper, handle both "Base" and numeric-prefixed strings (e.g., "2x", "3x", "4x") with a `switch` expression. Add a default fallback color. The filter chip list should be hardcoded as ["Base", "2x", "3x", "4x"] to match the expected DCA configuration.

2. **Chart screen layout — chart height vs. full screen**
   - What we know: The chart screen currently has a placeholder `CustomScrollView`. Chart needs to be a fixed height (e.g., 250-300dp) with timeframe selector above or below.
   - What's unclear: Whether the chart and timeframe selector should be in a `SliverAppBar` area or a fixed `Column` at the top with the purchase history list below (combined screen vs. separate tabs).
   - Recommendation: Chart screen and history screen are separate bottom nav tabs (already wired in router). ChartScreen = full-screen chart with timeframe selector. HistoryScreen = infinite scroll list with filter button. Keep them separate — matching the existing router setup.

3. **Purchase markers for "All" timeframe performance**
   - What we know: "All" = 3650 days of price data. fl_chart renders all spots synchronously.
   - What's unclear: Whether 3650 `FlSpot` objects cause jank on initial render for iOS.
   - Recommendation: Proceed with direct rendering. fl_chart is canvas-based and handles thousands of points well. If jank is observed during verification, downsample to weekly points for "All" timeframe (every 7th day) — but only if testing proves it necessary.

## Sources

### Primary (HIGH confidence)
- `/websites/pub_dev_fl_chart` (Context7) - LineChart, LineChartBarData, FlDotData, FlDotCirclePainter, ExtraLinesData, HorizontalLine, LineTouchData, LineTouchTooltipData, LineChartData constructors and properties
- `/imanneo/fl_chart` (Context7) - LineChart samples 2, 3, 5, 7; ExtraLinesData configuration docs
- [pub.dev/packages/fl_chart](https://pub.dev/packages/fl_chart) - Version 1.1.1 confirmed, changelog verified
- [pub.dev/packages/fl_chart/changelog](https://pub.dev/packages/fl_chart/changelog) - Breaking changes in 1.0.0 verified (tooltipBgColor removed, Flutter min 3.27.4)
- [TradingBot.ApiService/Endpoints/DashboardDtos.cs] - Backend DTOs: PriceChartResponse, PurchaseMarkerDto, PurchaseHistoryResponse, PurchaseDto all confirmed implemented
- [TradingBot.ApiService/Endpoints/DashboardEndpoints.cs] - API endpoints confirmed: `/api/dashboard/chart?timeframe=X` and `/api/dashboard/purchases?cursor=X&startDate=Y&tier=Z`

### Secondary (MEDIUM confidence)
- [dinkomarinac.dev](https://dinkomarinac.dev/implementing-infinite-scroll-with-riverpods-asyncnotifier) - AsyncNotifier infinite scroll mixin pattern; verified compatible with Riverpod 3.x accumulation model
- [pub.dev/packages/riverpod_paging_utils](https://pub.dev/packages/riverpod_paging_utils) - `CursorPagingNotifierMixin` pattern inspected; chose manual approach over package due to newness (v1.0.0, 47 days old)
- [api.flutter.dev - showDateRangePicker](https://api.flutter.dev/flutter/material/showDateRangePicker.html) - Built-in Material date range picker confirmed available
- [api.flutter.dev - showModalBottomSheet](https://api.flutter.dev/flutter/material/showModalBottomSheet.html) - Bottom sheet API confirmed

### Tertiary (LOW confidence)
- WebSearch result: fl_chart scatter markers — confirmed via Context7 that two-`LineChartBarData` approach is valid; not a "scatter" chart but a custom dot layer

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - fl_chart 1.1.1 confirmed on pub.dev, API verified via Context7, changelog reviewed for breaking changes
- Architecture: HIGH - Backend API fully implemented and confirmed, Flutter project structure established, provider pattern consistent with Phase 21
- Pitfalls: HIGH - tooltipBgColor breaking change verified against changelog, coordinate system pitfall verified against fl_chart docs, cursor race condition verified against Riverpod 3.x behavior
- Purchase marker approach: HIGH - Two-`LineChartBarData` approach confirmed via fl_chart `FlDotData` + `checkToShowDot` API; replaces the "may need fallback" risk noted in STATE.md

**Research date:** 2026-02-20
**Valid until:** 2026-03-22 (fl_chart stable; Riverpod 3.x stable)

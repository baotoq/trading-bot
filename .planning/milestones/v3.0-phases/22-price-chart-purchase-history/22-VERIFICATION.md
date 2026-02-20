---
phase: 22-price-chart-purchase-history
verified: 2026-02-20T08:30:00Z
status: passed
score: 8/8 must-haves verified
re_verification: false
---

# Phase 22: Price Chart + Purchase History Verification Report

**Phase Goal:** Users can visually explore their DCA performance on a price chart with purchase markers and scroll through the full purchase history
**Verified:** 2026-02-20T08:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can switch between 7D, 1M, 3M, 6M, 1Y, and All timeframes on the price chart and the chart re-renders with the selected timeframe's data | VERIFIED | `TimeframeSelector` with `_timeframes = ['7D','1M','3M','6M','1Y','All']`; `useState('1M')` in `ChartScreen`; `ref.watch(chartDataProvider(timeframe: selectedTimeframe.value))` re-fetches on change |
| 2 | Purchase markers appear on the chart as colored dots at the correct date and purchase price, with color varying by multiplier tier | VERIFIED | Second `LineChartBarData` with `purchaseSpots` at actual purchase prices; `FlDotCirclePainter` with `_tierColor(tier)` switch: `'2x'->amber`, `'3x'->orange`, `'4x'->lossRed`, `_->bitcoinOrange` |
| 3 | A dashed average cost basis horizontal line is visible on the chart when the user has purchases | VERIFIED | `extraLinesData: ExtraLinesData(horizontalLines: [HorizontalLine(y: averageCostBasis, dashArray: [6, 4])])` — guarded by `data.averageCostBasis != null` |
| 4 | Touching the chart shows a tooltip with the date and price at the touched point | VERIFIED | `LineTouchData(handleBuiltInTouches: true, touchTooltipData: LineTouchTooltipData(getTooltipItems: ...))` formats `'$dateLabel\n$formattedPrice'`; returns null for bar index 1 to suppress duplicate |
| 5 | User can scroll through the full purchase history and new pages load automatically as they scroll near the bottom | VERIFIED | `ScrollController` listener: `if (pos.pixels >= pos.maxScrollExtent - 200) notifier.loadNextPage()`; `PurchaseHistory` AsyncNotifier with cursor-based `_fetchPage()`, `_nextCursor`, `_hasMore` |
| 6 | User can open a bottom sheet to filter purchase history by date range and multiplier tier, and applying the filter resets the list | VERIFIED | FAB opens `FilterBottomSheet`; `onApply` calls `notifier.applyFilter(dateRange, tier)`; `applyFilter()` stores filters then calls `ref.invalidateSelf()` which resets cursor and re-fetches |
| 7 | Each purchase list item shows date, price, BTC amount, cost, multiplier tier badge, and drop percentage | VERIFIED | `PurchaseListItem` three-row layout: (1) `dateFormatter.format(executedAt)` + `timeago.format`, (2) `priceFormatter.format(price)` + `btcFormatter.format(quantity) BTC`, (3) `costFormatter.format(cost)` + tier badge + drop% |
| 8 | A loading indicator appears at the bottom of the list while the next page loads | VERIFIED | `showLoadingIndicator = hasMore \|\| notifier.isLoadingMore`; `itemCount = items.length + (showLoadingIndicator ? 1 : 0)`; last item renders `CircularProgressIndicator` |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `TradingBot.Mobile/lib/features/chart/data/models/chart_response.dart` | ChartResponse, PricePointDto, PurchaseMarkerDto with fromJson | VERIFIED | All 3 classes with correct fromJson; `averageCostBasis` parsed as `(json['averageCostBasis'] as num?)?.toDouble()` |
| `TradingBot.Mobile/lib/features/chart/data/chart_repository.dart` | ChartRepository.fetchChart(timeframe) calling /api/dashboard/chart | VERIFIED | `_dio.get('/api/dashboard/chart', queryParameters: {'timeframe': timeframe})` |
| `TradingBot.Mobile/lib/features/chart/data/chart_providers.dart` | chartRepositoryProvider and chartDataProvider(@riverpod) parameterized by timeframe | VERIFIED | `@riverpod chartRepository` + `@riverpod Future<ChartResponse> chartData(Ref ref, {String timeframe = '1M'})` |
| `TradingBot.Mobile/lib/features/chart/data/chart_providers.g.dart` | Generated Riverpod provider code | VERIFIED | Generated `ChartDataFamily` with `call({String timeframe = '1M'})` — family provider confirmed |
| `TradingBot.Mobile/lib/features/chart/presentation/widgets/price_line_chart.dart` | fl_chart LineChart with price line, purchase markers, avg cost dashed line, touch tooltip | VERIFIED | 371-line substantive widget: two `LineChartBarData`, `extraLinesData`, `lineTouchData`, `titlesData`, `gridData` |
| `TradingBot.Mobile/lib/features/chart/presentation/widgets/timeframe_selector.dart` | Segmented row of 6 timeframe buttons (7D/1M/3M/6M/1Y/All) | VERIFIED | `SingleChildScrollView` with `ChoiceChip` per timeframe; `selectedColor: AppTheme.bitcoinOrange` |
| `TradingBot.Mobile/lib/features/chart/presentation/chart_screen.dart` | ChartScreen assembling timeframe selector + price chart with pull-to-refresh | VERIFIED | `HookConsumerWidget`, `useState`, stale cache pattern (`cachedValue`), `RefreshIndicator`, error snackbar via `ref.listen` |
| `TradingBot.Mobile/lib/features/history/data/models/purchase_history_response.dart` | PurchaseHistoryResponse and PurchaseDto models | VERIFIED | All fields present: `items`, `nextCursor`, `hasMore`; PurchaseDto: id, executedAt, price, cost, quantity, multiplierTier, multiplier, dropPercentage |
| `TradingBot.Mobile/lib/features/history/data/history_repository.dart` | HistoryRepository.fetchPurchases with cursor/filter params | VERIFIED | Conditionally adds `cursor`, `startDate`, `endDate` (intl formatted), `tier` to query params; calls `/api/dashboard/purchases` |
| `TradingBot.Mobile/lib/features/history/data/history_providers.dart` | PurchaseHistory AsyncNotifier with loadNextPage() and applyFilter() | VERIFIED | `_isLoadingMore` flag pattern; `loadNextPage()` guards with `!_hasMore \|\| state.isLoading \|\| _isLoadingMore`; `applyFilter()` calls `ref.invalidateSelf()` |
| `TradingBot.Mobile/lib/features/history/data/history_providers.g.dart` | Generated Riverpod code for purchaseHistoryProvider | VERIFIED | `PurchaseHistoryProvider._()` extending `$AsyncNotifierProvider<PurchaseHistory, List<PurchaseDto>>` |
| `TradingBot.Mobile/lib/features/history/presentation/widgets/purchase_list_item.dart` | PurchaseListItem widget with purchase details | VERIFIED | Three-row `Container` card with all required fields; `_tierBadgeColor` switch for tier color coding |
| `TradingBot.Mobile/lib/features/history/presentation/widgets/filter_bottom_sheet.dart` | FilterBottomSheet with date range picker and tier chips | VERIFIED | `StatefulWidget`; `showDateRangePicker`; `ChoiceChip` for `['All','Base','2x','3x','4x']`; "Clear All" + "Apply" buttons |
| `TradingBot.Mobile/lib/features/history/presentation/history_screen.dart` | HistoryScreen with infinite scroll list and filter FAB | VERIFIED | `ConsumerStatefulWidget`; `ScrollController` with scroll listener; FAB; `RefreshIndicator`; empty state |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `chart_screen.dart` | `chartDataProvider` | `ref.watch(chartDataProvider(timeframe: selectedTimeframe.value))` | WIRED | Used 3× in screen (watch, listen, refresh) |
| `chart_providers.dart` | `/api/dashboard/chart` | `ChartRepository.fetchChart` | WIRED | `_dio.get('/api/dashboard/chart', queryParameters: {'timeframe': timeframe})` |
| `price_line_chart.dart` | fl_chart `LineChart` | `lineBarsData` with two `LineChartBarData` entries | WIRED | Main price line + transparent purchase marker line confirmed |
| `history_screen.dart` | `purchaseHistoryProvider` | `ref.watch` + `ref.read(purchaseHistoryProvider.notifier).loadNextPage()` | WIRED | Provider watched for display; notifier read for loadNextPage, applyFilter |
| `history_providers.dart` | `/api/dashboard/purchases` | `HistoryRepository.fetchPurchases` with cursor param | WIRED | Called in `_fetchPage()` with cursor, startDate, endDate, tier params |
| `filter_bottom_sheet.dart` | `purchaseHistoryProvider` | `onApply` callback calling `notifier.applyFilter()` | WIRED | `onApply` in `_openFilter()` calls `ref.read(purchaseHistoryProvider.notifier).applyFilter(...)` |
| `chart_screen.dart` / `history_screen.dart` | go_router | `/chart` and `/history` routes in `router.dart` | WIRED | Both screens imported and registered as `StatefulShellBranch` routes |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| CHART-01 | 22-01-PLAN.md | User can view BTC price chart with 6 timeframe options (7D/1M/3M/6M/1Y/All) | SATISFIED | `TimeframeSelector` with 6 `ChoiceChip`; `chartDataProvider` family parameterized by timeframe |
| CHART-02 | 22-01-PLAN.md | User can see purchase markers on price chart colored by multiplier tier | SATISFIED | Second `LineChartBarData` + `FlDotCirclePainter` with `_tierColor()` producing tier-specific colors |
| CHART-03 | 22-01-PLAN.md | User can see average cost basis dashed line on price chart | SATISFIED | `HorizontalLine(dashArray: [6, 4])` in `extraLinesData`; label shows `'Avg $XX,XXX'` |
| CHART-04 | 22-01-PLAN.md | User can touch chart to see price and date tooltip | SATISFIED | `LineTouchData` with `LineTouchTooltipData`; date label from `dateLabels[xIndex]` + formatted price |
| CHART-05 | 22-02-PLAN.md | User can scroll through purchase history with infinite scroll (cursor pagination) | SATISFIED | `ScrollController` at -200px threshold; `PurchaseHistory` AsyncNotifier accumulates pages via `_nextCursor` |
| CHART-06 | 22-02-PLAN.md | User can filter purchase history by date range and multiplier tier via bottom sheet | SATISFIED | `FilterBottomSheet` with `showDateRangePicker` + tier `ChoiceChip`; `applyFilter()` resets via `ref.invalidateSelf()` |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `price_line_chart.dart` | 153 | `return null` in `getTooltipItems` | Info | Intentional — suppresses tooltip rows for the purchase marker bar (index 1), standard fl_chart multi-bar tooltip pattern |

No blockers or warnings found. The `return null` on line 153 is correct tooltip suppression, not a stub.

### Human Verification Required

#### 1. Chart renders correctly on device

**Test:** Launch app on iOS simulator, navigate to Chart tab, observe the chart with real API data.
**Expected:** Orange curved BTC price line with gradient fill, purchase marker dots in tier colors at their buy prices, dashed avg cost line, touch tooltip appearing on tap.
**Why human:** Visual rendering, fl_chart interaction, and tooltip positioning cannot be verified by static analysis.

#### 2. Timeframe switching re-renders chart

**Test:** Tap each of the 6 timeframe chips (7D, 1M, 3M, 6M, 1Y, All) in sequence.
**Expected:** Chart transitions to the selected timeframe's data; loading indicator briefly appears between switches; x-axis labels adjust to appropriate intervals.
**Why human:** The actual data re-fetch and chart transition experience requires a running app.

#### 3. Infinite scroll triggers page loads

**Test:** Navigate to History tab with many purchases; scroll to the bottom of the visible list.
**Expected:** Loading indicator appears at bottom; more purchases append to the list without losing scroll position.
**Why human:** Scroll physics behavior and loading indicator timing require live interaction.

#### 4. Filter bottom sheet state preservation

**Test:** Apply a date range + tier filter, close the sheet, then reopen it.
**Expected:** Filter bottom sheet pre-populates with the previously applied filters (date range and selected tier chip).
**Why human:** State round-trip between screen's local `_currentDateRange`/`_currentTier` and sheet initialization is only verifiable at runtime.

### Gaps Summary

No gaps. All 8 observable truths verified. All 14 artifacts substantive and wired. All 6 requirement IDs (CHART-01 through CHART-06) satisfied. All 4 git commits (5c5e4d3, 408daa9, 1c6b618, fc36f36) confirmed present.

The one deviation from plan — replacing `copyWithPrevious` (internal Riverpod API) with an explicit `_isLoadingMore` boolean flag — is a correct fix documented in the SUMMARY and produces equivalent UX without triggering `invalid_use_of_internal_member` warnings.

---

_Verified: 2026-02-20T08:30:00Z_
_Verifier: Claude (gsd-verifier)_

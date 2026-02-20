---
phase: 22-price-chart-purchase-history
plan: "02"
subsystem: mobile-history
tags: [flutter, riverpod, infinite-scroll, cursor-pagination, purchase-history]
dependency_graph:
  requires: [21-02]
  provides: [purchase-history-screen, history-data-layer]
  affects: [TradingBot.Mobile]
tech_stack:
  added: []
  patterns: [AsyncNotifier-cursor-pagination, ConsumerStatefulWidget-scroll-controller, FilterBottomSheet-ChoiceChips]
key_files:
  created:
    - TradingBot.Mobile/lib/features/history/data/models/purchase_history_response.dart
    - TradingBot.Mobile/lib/features/history/data/history_repository.dart
    - TradingBot.Mobile/lib/features/history/data/history_providers.dart
    - TradingBot.Mobile/lib/features/history/data/history_providers.g.dart
    - TradingBot.Mobile/lib/features/history/presentation/widgets/purchase_list_item.dart
    - TradingBot.Mobile/lib/features/history/presentation/widgets/filter_bottom_sheet.dart
  modified:
    - TradingBot.Mobile/lib/features/history/presentation/history_screen.dart
decisions:
  - "Replaced copyWithPrevious (internal Riverpod API) with explicit isLoadingMore boolean flag on the AsyncNotifier — avoids invalid_use_of_internal_member warning while achieving same UX: existing items stay visible while more pages load"
metrics:
  duration: "~4 min"
  completed: "2026-02-20"
  tasks_completed: 2
  tasks_total: 2
  files_created: 6
  files_modified: 1
---

# Phase 22 Plan 02: Purchase History Screen Summary

Purchase history screen with cursor-based infinite scroll, per-purchase detail rows, and date-range/tier filter bottom sheet connecting to /api/dashboard/purchases.

## What Was Built

### Task 1: History Data Layer

**Models** (`purchase_history_response.dart`): `PurchaseHistoryResponse` (items, nextCursor, hasMore) and `PurchaseDto` (id, executedAt, price, cost, quantity, multiplierTier, multiplier, dropPercentage) with manual `fromJson` following the Phase 21 pattern — no code generation for models.

**Repository** (`history_repository.dart`): `HistoryRepository.fetchPurchases()` calls `GET /api/dashboard/purchases` with `pageSize`, `cursor`, `startDate`, `endDate` (formatted as `yyyy-MM-dd` via intl), and `tier` query params. Conditionally omits null/empty params.

**AsyncNotifier** (`history_providers.dart`): `PurchaseHistory` AsyncNotifier with:
- `build()` — resets cursor/hasMore, fetches first page
- `loadNextPage()` — guards against concurrent calls via `_isLoadingMore` flag, appends page items
- `applyFilter()` — stores date range and tier, calls `ref.invalidateSelf()` to reset to page 1
- `clearFilters()` — resets all filters, invalidates self
- `_fetchPage()` — private helper updating `_nextCursor`/`_hasMore` from response

### Task 2: History UI

**PurchaseListItem** (`purchase_list_item.dart`): Dark card widget showing three rows: (1) formatted date + relative timeago, (2) large price + BTC quantity, (3) USD cost + tier badge (color-coded by tier) + drop percentage. Uses `NumberFormat` for currency formatting and `timeago` for relative timestamps.

**FilterBottomSheet** (`filter_bottom_sheet.dart`): Modal bottom sheet with date range section (OutlinedButton opening `showDateRangePicker`, X button to clear), tier ChoiceChips (All/Base/2x/3x/4x), Clear All button, and Apply button. Preserves initial filter state from screen for pre-population.

**HistoryScreen** (`history_screen.dart`): `ConsumerStatefulWidget` with `ScrollController` listening for `pixels >= maxScrollExtent - 200` to trigger `loadNextPage()`. Uses `asyncPurchases.when()` with `skipLoadingOnReload: true` and `skipLoadingOnRefresh: true` so existing items stay visible during re-fetch. Pull-to-refresh via `ref.invalidate` + `ref.read(future)`. FAB opens `FilterBottomSheet`. Empty state shows "No purchases yet" with icon. Loading more indicator at list bottom.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced `copyWithPrevious` internal Riverpod API**
- **Found during:** Task 1 verification (dart analyze)
- **Issue:** Plan specified `AsyncLoading().copyWithPrevious(AsyncData(current))` but `copyWithPrevious` is annotated `@internal` in Riverpod — causes `invalid_use_of_internal_member` warning and breaks `dart analyze`
- **Fix:** Added explicit `_isLoadingMore` boolean field to `PurchaseHistory` notifier. `loadNextPage()` sets flag to true before fetch, false in `finally`. HistoryScreen reads `notifier.isLoadingMore` alongside `notifier.hasMore` to decide whether to show the loading indicator at list bottom.
- **Files modified:** `history_providers.dart`, `history_screen.dart`
- **Commit:** 1c6b618, fc36f36

## Self-Check: PASSED

All 7 files created/modified confirmed present on disk. Both task commits (1c6b618, fc36f36) verified in git log.

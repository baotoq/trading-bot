import 'dart:async';

import 'package:flutter/material.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_client.dart';
import 'history_repository.dart';
import 'models/purchase_history_response.dart';

part 'history_providers.g.dart';

@riverpod
HistoryRepository historyRepository(Ref ref) {
  return HistoryRepository(ref.watch(dioProvider));
}

@riverpod
class PurchaseHistory extends _$PurchaseHistory {
  String? _nextCursor;
  bool _hasMore = true;
  DateTimeRange? _dateRange;
  String? _tierFilter;

  bool get hasMore => _hasMore;

  @override
  Future<List<PurchaseDto>> build() async {
    _nextCursor = null;
    _hasMore = true;
    return _fetchPage(cursor: null);
  }

  bool _isLoadingMore = false;

  bool get isLoadingMore => _isLoadingMore;

  Future<void> loadNextPage() async {
    if (!_hasMore || state.isLoading || _isLoadingMore) return;

    final current = state.requireValue;
    _isLoadingMore = true;
    // Notify listeners that loading more has started (keeps existing data visible)
    state = AsyncData(current);

    try {
      final page = await _fetchPage(cursor: _nextCursor);
      state = AsyncData([...current, ...page]);
    } catch (e, st) {
      state = AsyncError(e, st);
    } finally {
      _isLoadingMore = false;
    }
  }

  void applyFilter({DateTimeRange? dateRange, String? tier}) {
    _dateRange = dateRange;
    _tierFilter = tier;
    ref.invalidateSelf();
  }

  void clearFilters() {
    _dateRange = null;
    _tierFilter = null;
    ref.invalidateSelf();
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
}

import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../app/theme.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../data/history_providers.dart';
import '../data/models/purchase_history_response.dart';
import 'widgets/filter_bottom_sheet.dart';
import 'widgets/purchase_list_item.dart';

class HistoryScreen extends ConsumerStatefulWidget {
  const HistoryScreen({super.key});

  @override
  ConsumerState<HistoryScreen> createState() => _HistoryScreenState();
}

class _HistoryScreenState extends ConsumerState<HistoryScreen> {
  late final ScrollController _scrollController;

  // Track filter state locally to avoid exposing notifier internals
  DateTimeRange? _currentDateRange;
  String? _currentTier;

  @override
  void initState() {
    super.initState();
    _scrollController = ScrollController();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController
      ..removeListener(_onScroll)
      ..dispose();
    super.dispose();
  }

  void _onScroll() {
    final pos = _scrollController.position;
    if (pos.pixels >= pos.maxScrollExtent - 200) {
      ref.read(purchaseHistoryProvider.notifier).loadNextPage();
    }
  }

  void _openFilter() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: const Color(0xFF1A1A1A),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => FilterBottomSheet(
        initialDateRange: _currentDateRange,
        initialTier: _currentTier,
        onApply: (dateRange, tier) {
          setState(() {
            _currentDateRange = dateRange;
            _currentTier = tier;
          });
          ref
              .read(purchaseHistoryProvider.notifier)
              .applyFilter(dateRange: dateRange, tier: tier);
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final asyncPurchases = ref.watch(purchaseHistoryProvider);
    final notifier = ref.read(purchaseHistoryProvider.notifier);

    // Show error snackbar for background errors while stale data is shown
    ref.listen<AsyncValue<List<PurchaseDto>>>(purchaseHistoryProvider, (
      _,
      next,
    ) {
      if (next is AsyncError && next.hasValue) {
        showErrorSnackbar(
          context,
          'Failed to load more purchases. Please try again.',
        );
      }
    });

    return Scaffold(
      floatingActionButton: FloatingActionButton.small(
        onPressed: _openFilter,
        backgroundColor: AppTheme.bitcoinOrange,
        foregroundColor: Colors.black,
        child: const Icon(CupertinoIcons.slider_horizontal_3),
      ),
      body: RefreshIndicator(
        onRefresh: () {
          ref.invalidate(purchaseHistoryProvider);
          return ref.read(purchaseHistoryProvider.future);
        },
        child: asyncPurchases.when(
          data: (items) => _buildList(items, notifier.hasMore),
          loading: () => _buildLoadingSkeleton(),
          error: (_, __) => RetryWidget(
            onRetry: () => ref.invalidate(purchaseHistoryProvider),
          ),
          skipLoadingOnReload: true,
          skipLoadingOnRefresh: true,
        ),
      ),
    );
  }

  /// Skeleton loading state — 7 fake purchase rows shaped like real list items.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: ListView.builder(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.only(top: 8, bottom: 80),
        itemCount: 7,
        itemBuilder: (context, _) => Container(
          margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: const Color(0xFF1E1E1E),
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: Colors.white12),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Bone.text(words: 3, fontSize: 13),
                  Bone.text(words: 2, fontSize: 12),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Bone.text(words: 1, fontSize: 17),
                  Bone.text(words: 2, fontSize: 14),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Bone.text(words: 1, fontSize: 13),
                  Row(
                    children: [
                      Bone(width: 36, height: 18, borderRadius: BorderRadius.circular(4)),
                      const SizedBox(width: 8),
                      Bone.text(words: 1, fontSize: 13),
                    ],
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildList(List<PurchaseDto> items, bool hasMore) {
    final notifier = ref.read(purchaseHistoryProvider.notifier);

    if (items.isEmpty) {
      return CustomScrollView(
        controller: _scrollController,
        slivers: [
          SliverFillRemaining(
            child: Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(
                    CupertinoIcons.bitcoin,
                    size: 48,
                    color: Colors.white24,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'No purchases yet',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: Colors.white54,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      );
    }

    // +1 item if hasMore or currently loading more (for loading indicator)
    final showLoadingIndicator = hasMore || notifier.isLoadingMore;
    final itemCount = items.length + (showLoadingIndicator ? 1 : 0);

    return ListView.builder(
      controller: _scrollController,
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.only(top: 8, bottom: 80),
      itemCount: itemCount,
      itemBuilder: (context, index) {
        if (index == items.length) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: CircularProgressIndicator(),
            ),
          );
        }
        return PurchaseListItem(purchase: items[index]);
      },
    );
  }
}

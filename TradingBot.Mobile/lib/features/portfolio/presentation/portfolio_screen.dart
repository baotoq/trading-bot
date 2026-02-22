import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:go_router/go_router.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../app/theme.dart';
import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../data/currency_provider.dart';
import '../data/models/portfolio_asset_response.dart';
import '../data/portfolio_providers.dart';
import 'widgets/allocation_donut_chart.dart';
import 'widgets/asset_row.dart';
import 'widgets/fixed_deposit_row.dart';
import 'widgets/portfolio_filter_chips.dart';
import 'widgets/portfolio_hero_header.dart';
import 'widgets/portfolio_tab_bar.dart';

class PortfolioScreen extends HookConsumerWidget {
  const PortfolioScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final portfolioData = ref.watch(portfolioPageDataProvider);
    final cachedValue = portfolioData.value;
    final isVnd = ref.watch(currencyPreferenceProvider);

    // Tab and filter state — survive widget rebuilds via useState hooks.
    // Pattern: STATE.md "all AnimationControllers in HookConsumerWidget via hooks"
    final selectedTab = useState(0); // 0 = Overview, 1 = Transactions
    final activeFilter = useState('Holding amount');

    // Show snackbar on errors — stale cached data remains visible
    ref.listen(portfolioPageDataProvider, (previous, next) {
      if (next.hasError && !next.isLoading) {
        if (next.error is AuthenticationException) {
          showAuthErrorSnackbar(context);
        } else {
          showErrorSnackbar(context, 'Could not load portfolio data');
        }
      }
    });

    // Determine data to render — either fresh or cached on error.
    final dataToShow = portfolioData.value ?? cachedValue;

    // Build the sliver list based on async state.
    final List<Widget> contentSlivers;
    if (dataToShow != null) {
      contentSlivers = _buildContent(
        dataToShow,
        isVnd,
        context,
        selectedTab,
        activeFilter,
      );
    } else if (portfolioData.hasError) {
      contentSlivers = [
        SliverFillRemaining(
          child: RetryWidget(
            onRetry: () => ref.invalidate(portfolioPageDataProvider),
          ),
        ),
      ];
    } else {
      contentSlivers = [
        SliverToBoxAdapter(child: _buildLoadingSkeleton()),
      ];
    }

    return Scaffold(
      // No backgroundColor — global transparent from AppTheme.dark applies so
      // AmbientBackground orbs show through. Do NOT set a solid color here.
      // See STATE.md: "scaffoldBackgroundColor: Colors.transparent globally"
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(portfolioPageDataProvider.future),
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              title: const Text('Portfolio'),
              floating: false,
              pinned: false,
              elevation: 0,
              backgroundColor: Colors.transparent,
              // CurrencyToggle moved into PortfolioHeroHeader — removed from actions.
            ),
            ...contentSlivers,
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => context.push('/portfolio/add-transaction'),
        backgroundColor: AppTheme.bitcoinOrange,
        foregroundColor: Colors.black,
        child: const Icon(CupertinoIcons.add),
      ),
    );
  }

  /// Builds the list of slivers for the loaded content state.
  ///
  /// Returns [List<Widget>] of slivers spread into the outer [CustomScrollView].
  List<Widget> _buildContent(
    PortfolioPageData data,
    bool isVnd,
    BuildContext context,
    ValueNotifier<int> selectedTab,
    ValueNotifier<String> activeFilter,
  ) {
    final totalValue =
        isVnd ? data.summary.totalValueVnd : data.summary.totalValueUsd;

    // Sort assets by active filter.
    final sortedAssets = List<PortfolioAssetResponse>.from(data.assets);
    switch (activeFilter.value) {
      case 'Holding amount':
        sortedAssets.sort(
          (a, b) => b.currentValueUsd.compareTo(a.currentValueUsd),
        );
      case 'Cumulative profit':
        sortedAssets.sort(
          (a, b) => b.unrealizedPnlUsd.compareTo(a.unrealizedPnlUsd),
        );
      case 'Analysis':
        // Sort by unrealizedPnlPercent DESC — nulls go last.
        sortedAssets.sort((a, b) {
          final ap = a.unrealizedPnlPercent;
          final bp = b.unrealizedPnlPercent;
          if (ap == null && bp == null) return 0;
          if (ap == null) return 1;
          if (bp == null) return -1;
          return bp.compareTo(ap);
        });
    }

    return [
      // 1. Hero header with total value and all-time P&L
      SliverToBoxAdapter(
        child: PortfolioHeroHeader(summary: data.summary, isVnd: isVnd),
      ),

      // 2. Sticky tab bar — pinned at top during scroll
      SliverPersistentHeader(
        pinned: true,
        delegate: StickyTabBarDelegate(
          tabBar: PortfolioTabBar(selectedTab: selectedTab),
        ),
      ),

      // 3. Overview tab content
      if (selectedTab.value == 0) ...[
        // Filter chips row
        SliverToBoxAdapter(
          child: PortfolioFilterChips(activeFilter: activeFilter),
        ),

        // Allocation donut chart (glow treatment deferred to Phase 37)
        SliverToBoxAdapter(
          child: AllocationDonutChart(
            allocations: data.summary.allocations,
            totalValue: totalValue,
            isVnd: isVnd,
          ),
        ),

        // Assets section header with count
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: Row(
              children: [
                const Text(
                  'Assets',
                  style: TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  '${sortedAssets.length}',
                  style: const TextStyle(
                    color: Colors.white38,
                    fontSize: 14,
                  ),
                ),
              ],
            ),
          ),
        ),

        // Flat asset list — sorted by active filter chip.
        // AssetRow widgets kept for now; Plan 02 replaces with glass rows.
        SliverList.builder(
          itemCount: sortedAssets.length,
          itemBuilder: (context, index) => AssetRow(
            asset: sortedAssets[index],
            isVnd: isVnd,
          ),
        ),

        // Fixed deposits section (if any)
        if (data.fixedDeposits.isNotEmpty) ...[
          const SliverToBoxAdapter(child: Divider(indent: 16, endIndent: 16)),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
              child: const Text(
                'Fixed Deposits',
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 16,
                ),
              ),
            ),
          ),
          SliverList.builder(
            itemCount: data.fixedDeposits.length,
            itemBuilder: (context, index) => FixedDepositRow(
              fd: data.fixedDeposits[index],
              isVnd: isVnd,
            ),
          ),
        ],

        // View Transaction History text button
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: TextButton.icon(
              onPressed: () => context.push('/portfolio/transaction-history'),
              icon: const Icon(CupertinoIcons.clock),
              label: const Text('View Transaction History'),
            ),
          ),
        ),

        // FAB clearance
        const SliverToBoxAdapter(child: SizedBox(height: 80)),
      ],

      // 4. Transactions tab — deferred to future phase; navigate directly.
      if (selectedTab.value == 1) ...[
        SliverFillRemaining(
          child: Center(
            child: TextButton.icon(
              onPressed: () => context.push('/portfolio/transaction-history'),
              icon: const Icon(CupertinoIcons.clock),
              label: const Text('View Full Transaction History'),
            ),
          ),
        ),
      ],
    ];
  }

  /// Skeleton loading state — hero card + donut chart placeholder + asset rows.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: Column(
        children: [
          // Hero header placeholder
          Card(
            margin: const EdgeInsets.all(16),
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Bone.text(words: 2, fontSize: 14),
                  const SizedBox(height: 8),
                  Bone.text(words: 1, fontSize: 32),
                  const SizedBox(height: 8),
                  Bone.text(words: 3, fontSize: 14),
                ],
              ),
            ),
          ),
          // Donut chart placeholder
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Bone(
              width: double.infinity,
              height: 200,
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          const SizedBox(height: 16),
          // Asset section header
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Bone.text(words: 1, fontSize: 16),
          ),
          // Asset rows
          ...List.generate(
            3,
            (_) => Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
              child: Row(
                children: [
                  const Bone.icon(size: 40),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Bone.text(words: 1, fontSize: 14),
                        const SizedBox(height: 4),
                        Bone.text(words: 2, fontSize: 12),
                      ],
                    ),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Bone.text(words: 1, fontSize: 14),
                      const SizedBox(height: 4),
                      Bone.text(words: 1, fontSize: 12),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 80), // Space for FAB
        ],
      ),
    );
  }
}

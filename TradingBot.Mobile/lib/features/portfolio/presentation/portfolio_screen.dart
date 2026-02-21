import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
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
import 'widgets/asset_type_section.dart';
import 'widgets/currency_toggle.dart';
import 'widgets/portfolio_summary_card.dart';

class PortfolioScreen extends HookConsumerWidget {
  const PortfolioScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final portfolioData = ref.watch(portfolioPageDataProvider);
    final cachedValue = portfolioData.value;
    final isVnd = ref.watch(currencyPreferenceProvider);

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

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(portfolioPageDataProvider.future),
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              title: const Text('Portfolio'),
              floating: true,
              snap: true,
              actions: const [
                CurrencyToggle(),
                SizedBox(width: 8),
              ],
            ),
            switch (portfolioData) {
              AsyncData(:final value) => _buildContent(value, isVnd, context),
              AsyncError() when cachedValue != null =>
                _buildContent(cachedValue, isVnd, context),
              AsyncError() => SliverFillRemaining(
                  child: RetryWidget(
                    onRetry: () => ref.invalidate(portfolioPageDataProvider),
                  ),
                ),
              _ => SliverToBoxAdapter(child: _buildLoadingSkeleton()),
            },
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

  SliverList _buildContent(
    PortfolioPageData data,
    bool isVnd,
    BuildContext context,
  ) {
    final cryptoAssets = data.assets
        .where((a) => a.assetType == 'Crypto')
        .toList();
    final etfAssets = data.assets
        .where((a) => a.assetType == 'ETF')
        .toList();

    final cryptoSubtotalUsd =
        _sumField(cryptoAssets, (a) => a.currentValueUsd);
    final cryptoSubtotalVnd =
        _sumField(cryptoAssets, (a) => a.currentValueVnd);
    final etfSubtotalUsd = _sumField(etfAssets, (a) => a.currentValueUsd);
    final etfSubtotalVnd = _sumField(etfAssets, (a) => a.currentValueVnd);
    final fdSubtotalVnd = data.fixedDeposits.fold<double>(
        0, (sum, fd) => sum + fd.accruedValueVnd);

    final totalValue = isVnd
        ? data.summary.totalValueVnd
        : data.summary.totalValueUsd;

    return SliverList.list(
      children: [
        PortfolioSummaryCard(summary: data.summary, isVnd: isVnd),
        AllocationDonutChart(
          allocations: data.summary.allocations,
          totalValue: totalValue,
          isVnd: isVnd,
        ),
        if (cryptoAssets.isNotEmpty)
          AssetTypeSection(
            title: 'Crypto',
            assets: cryptoAssets,
            isVnd: isVnd,
            subtotalUsd: cryptoSubtotalUsd,
            subtotalVnd: cryptoSubtotalVnd,
          ),
        if (etfAssets.isNotEmpty)
          AssetTypeSection(
            title: 'ETF',
            assets: etfAssets,
            isVnd: isVnd,
            subtotalUsd: etfSubtotalUsd,
            subtotalVnd: etfSubtotalVnd,
          ),
        if (data.fixedDeposits.isNotEmpty)
          AssetTypeSection(
            title: 'Fixed Deposit',
            fixedDeposits: data.fixedDeposits,
            isVnd: isVnd,
            subtotalUsd: 0,
            subtotalVnd: fdSubtotalVnd,
          ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: TextButton.icon(
            onPressed: () => context.push('/portfolio/transaction-history'),
            icon: const Icon(CupertinoIcons.clock),
            label: const Text('View Transaction History'),
          ),
        ),
        const SizedBox(height: 80), // Space for FAB
      ],
    );
  }

  /// Skeleton loading state — summary card + donut chart placeholder + asset rows.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: Column(
        children: [
          // Summary card placeholder
          Card(
            margin: const EdgeInsets.all(16),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Bone.text(words: 2, fontSize: 14),
                  const SizedBox(height: 8),
                  Bone.text(words: 1, fontSize: 28),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Bone.text(words: 2, fontSize: 14),
                      Bone.text(words: 2, fontSize: 14),
                    ],
                  ),
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

  double _sumField(
    List<PortfolioAssetResponse> assets,
    double Function(PortfolioAssetResponse) selector,
  ) {
    return assets.fold<double>(0, (sum, a) => sum + selector(a));
  }
}

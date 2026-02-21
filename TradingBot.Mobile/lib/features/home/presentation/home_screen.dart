import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../data/home_providers.dart';
import 'widgets/countdown_text.dart';
import 'widgets/health_badge.dart';
import 'widgets/last_buy_card.dart';
import 'widgets/portfolio_stats_section.dart';

class HomeScreen extends HookConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final homeData = ref.watch(homeDataProvider);
    final cachedValue = homeData.value;

    // Show snackbar on errors — stale cached data remains visible
    ref.listen(homeDataProvider, (previous, next) {
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
        onRefresh: () => ref.refresh(homeDataProvider.future),
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              title: const Text('Portfolio'),
              floating: true,
              snap: true,
              actions: [
                Padding(
                  padding: const EdgeInsets.only(right: 16),
                  child: switch (homeData) {
                    AsyncData(:final value) => HealthBadge(
                        healthStatus: value.status.healthStatus,
                        healthMessage: value.status.healthMessage,
                      ),
                    AsyncError() when cachedValue != null => HealthBadge(
                        healthStatus: cachedValue.status.healthStatus,
                        healthMessage: cachedValue.status.healthMessage,
                      ),
                    _ => const SizedBox.shrink(),
                  },
                ),
              ],
            ),
            switch (homeData) {
              AsyncData(:final value) => SliverPadding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  sliver: SliverList.list(
                    children: _buildContent(value),
                  ),
                ),
              AsyncError() when cachedValue != null => SliverPadding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  sliver: SliverList.list(
                    children: _buildContent(cachedValue),
                  ),
                ),
              AsyncError() => SliverFillRemaining(
                  child: RetryWidget(
                    onRetry: () => ref.invalidate(homeDataProvider),
                  ),
                ),
              _ => SliverToBoxAdapter(child: _buildLoadingSkeleton()),
            },
          ],
        ),
      ),
    );
  }

  List<Widget> _buildContent(homeData) {
    return [
      PortfolioStatsSection(portfolio: homeData.portfolio),
      const SizedBox(height: 12),
      CountdownText(nextBuyTimeIso: homeData.status.nextBuyTime),
      const SizedBox(height: 16),
      LastBuyCard(status: homeData.status),
      const SizedBox(height: 16),
    ];
  }

  /// Skeleton loading state — mirrors the real content layout so Skeletonizer
  /// can generate bone shapes that match the actual widget sizes and positions.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Portfolio value hero
            const SizedBox(height: 8),
            Bone.text(words: 2, fontSize: 14),
            const SizedBox(height: 6),
            Bone.text(words: 1, fontSize: 32),
            const SizedBox(height: 6),
            Bone.text(words: 2, fontSize: 14),
            const SizedBox(height: 20),
            // 2x2 stat grid
            GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 8,
              crossAxisSpacing: 8,
              childAspectRatio: 2.2,
              children: List.generate(
                4,
                (_) => Card(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Bone.text(words: 2, fontSize: 12),
                        const SizedBox(height: 4),
                        Bone.text(words: 1, fontSize: 16),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
            // Countdown text
            Bone.text(words: 4, fontSize: 14),
            const SizedBox(height: 16),
            // Last buy card
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Bone.text(words: 2, fontSize: 16),
                        Bone.text(words: 1, fontSize: 12),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Bone.text(words: 1, fontSize: 24),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Bone.text(words: 2, fontSize: 14),
                        const SizedBox(width: 10),
                        Bone(width: 40, height: 20, borderRadius: BorderRadius.circular(12)),
                      ],
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}

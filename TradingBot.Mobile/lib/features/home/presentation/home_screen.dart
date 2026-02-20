import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
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
              _ => const SliverFillRemaining(
                  child: Center(child: CircularProgressIndicator()),
                ),
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
}

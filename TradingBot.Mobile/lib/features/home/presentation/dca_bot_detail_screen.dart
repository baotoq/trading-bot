import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../app/theme.dart';
import '../../../core/widgets/glass_card.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../../../features/chart/data/chart_providers.dart';
import '../../../features/config/data/config_providers.dart';
import '../../../features/history/data/history_providers.dart';
import '../../../features/history/presentation/widgets/purchase_list_item.dart';
import '../data/home_providers.dart';
import 'widgets/bot_action_buttons.dart';
import 'widgets/bot_identity_header.dart';
import 'widgets/bot_info_card.dart';
import 'widgets/bot_stats_grid.dart';
import 'widgets/pnl_chart_card.dart';

/// Detail screen for the DCA bot — shows a 3-tab layout (Overview, History, Parameters).
///
/// This screen is pushed full-screen above the navigation shell via GoRouter's
/// `parentNavigatorKey: rootNavigatorKey`. The bottom navigation bar is not
/// visible while this screen is active.
///
/// No [backgroundColor] is set on the [Scaffold] — the global
/// `scaffoldBackgroundColor: Colors.transparent` from [AppTheme.dark] applies,
/// allowing [AmbientBackground] orbs to show through behind the content.
class DcaBotDetailScreen extends HookConsumerWidget {
  const DcaBotDetailScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return DefaultTabController(
      length: 3,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Details'),
          backgroundColor: Colors.transparent,
          elevation: 0,
          surfaceTintColor: Colors.transparent,
          actions: [
            IconButton(
              icon: const Icon(Icons.share_outlined),
              onPressed: () {
                // Share action handled by BotActionButtons — this is a no-op placeholder
              },
            ),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Overview'),
              Tab(text: 'History'),
              Tab(text: 'Parameters'),
            ],
            indicatorColor: AppTheme.bitcoinOrange,
            labelColor: Colors.white,
            unselectedLabelColor: Colors.white54,
            dividerColor: Colors.transparent,
          ),
        ),
        body: const TabBarView(
          children: [
            _OverviewTab(),
            _HistoryTab(),
            _ParametersTab(),
          ],
        ),
      ),
    );
  }
}

/// Overview tab private widget.
///
/// Watches [homeDataProvider] for portfolio + status and [configDataProvider]
/// for DCA configuration, then renders the bot identity, stats grid,
/// action buttons, PnL chart, bot info, and a 5-item event history preview.
class _OverviewTab extends HookConsumerWidget {
  const _OverviewTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final homeData = ref.watch(homeDataProvider);
    final configData = ref.watch(configDataProvider);

    // Extract stale cached value for error fallback display
    final cachedHomeValue = homeData.value;

    // Show error snackbar when refresh fails but cached data is still visible
    ref.listen(homeDataProvider, (previous, next) {
      if (next.hasError && !next.isLoading && cachedHomeValue != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not refresh data')),
        );
      }
    });

    // Determine the effective home data (live or stale cache on error)
    final effectiveHome = switch (homeData) {
      AsyncData(:final value) => value,
      AsyncError() when cachedHomeValue != null => cachedHomeValue,
      _ => null,
    };

    // Show loading skeleton while initial data is in-flight
    if (homeData.isLoading || configData.isLoading) {
      return _buildLoadingSkeleton();
    }

    return switch ((effectiveHome, configData)) {
      (final home?, AsyncData(:final value)) => SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              BotIdentityHeader(firstPurchaseDate: home.portfolio.firstPurchaseDate),
              BotStatsGrid(
                portfolio: home.portfolio,
                status: home.status,
                config: value,
              ),
              BotActionButtons(portfolio: home.portfolio),
              // PnL chart — watches chart data with 1M timeframe
              _PnlChartSection(
                unrealizedPnlPercent: home.portfolio.unrealizedPnlPercent,
              ),
              BotInfoCard(firstPurchaseDate: home.portfolio.firstPurchaseDate),
              // Event history preview — last 5 purchases
              const _EventHistoryPreviewSection(),
              const SizedBox(height: 80),
            ],
          ),
        ),
      _ => const Center(
          child: Text(
            'Failed to load data',
            style: TextStyle(color: Colors.white54),
          ),
        ),
    };
  }

  /// Loading skeleton that mirrors the Overview tab layout structure.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: SingleChildScrollView(
        physics: const NeverScrollableScrollPhysics(),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Bot identity header skeleton
            GlassCard(
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  const Bone(width: 48, height: 48, borderRadius: BorderRadius.all(Radius.circular(24))),
                  const SizedBox(width: 12),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Bone.text(words: 2, fontSize: 14),
                      const SizedBox(height: 4),
                      Bone.text(words: 3, fontSize: 12),
                    ],
                  ),
                ],
              ),
            ),
            // Stats grid skeleton
            GlassCard(
              variant: GlassVariant.stationary,
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                      const SizedBox(width: 16),
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                      const SizedBox(width: 16),
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                      const SizedBox(width: 16),
                      Expanded(child: Bone.text(words: 2, fontSize: 14)),
                    ],
                  ),
                ],
              ),
            ),
            // Action buttons skeleton
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  Expanded(child: Bone(height: 44, borderRadius: BorderRadius.circular(12))),
                  const SizedBox(width: 12),
                  Expanded(child: Bone(height: 44, borderRadius: BorderRadius.circular(12))),
                ],
              ),
            ),
            // PnL chart skeleton
            GlassCard(
              variant: GlassVariant.stationary,
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Bone.text(words: 2, fontSize: 13),
                  const SizedBox(height: 8),
                  Bone.text(words: 1, fontSize: 20),
                  const SizedBox(height: 12),
                  const Bone(height: 200, borderRadius: BorderRadius.all(Radius.circular(8))),
                ],
              ),
            ),
            // Bot info skeleton
            GlassCard(
              variant: GlassVariant.stationary,
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Bone.text(words: 1, fontSize: 13),
                      Bone.text(words: 2, fontSize: 13),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Bone.text(words: 2, fontSize: 13),
                      Bone.text(words: 3, fontSize: 13),
                    ],
                  ),
                ],
              ),
            ),
            // Event history skeleton — 3 rows
            ...List.generate(
              3,
              (_) => Container(
                margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: const Color(0xFF1E1E1E),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Bone.text(words: 2, fontSize: 13),
                    const SizedBox(height: 8),
                    Bone.text(words: 2, fontSize: 17),
                    const SizedBox(height: 6),
                    Bone.text(words: 2, fontSize: 13),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 80),
          ],
        ),
      ),
    );
  }
}

/// PnL chart section that watches [chartDataProvider] and renders [PnlChartCard].
///
/// Separated into its own widget so the chart provider subscription is isolated
/// from the main Overview tab rebuild cycle.
class _PnlChartSection extends HookConsumerWidget {
  const _PnlChartSection({required this.unrealizedPnlPercent});

  final double? unrealizedPnlPercent;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final chartData = ref.watch(chartDataProvider(timeframe: '1M'));

    return switch (chartData) {
      AsyncData(:final value) => PnlChartCard(
          prices: value.prices,
          averageCostBasis: value.averageCostBasis,
          unrealizedPnlPercent: unrealizedPnlPercent,
        ),
      AsyncLoading() => GlassCard(
          margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          padding: const EdgeInsets.all(16),
          child: const SizedBox(
            height: 200,
            child: Center(child: CircularProgressIndicator()),
          ),
        ),
      _ => GlassCard(
          margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          padding: const EdgeInsets.all(16),
          child: const SizedBox(
            height: 200,
            child: Center(
              child: Text(
                'Chart unavailable',
                style: TextStyle(color: Colors.white54),
              ),
            ),
          ),
        ),
    };
  }
}

/// Event history preview section showing the last 5 purchases.
///
/// Watches [purchaseHistoryProvider] and renders the first 5 [PurchaseListItem]s.
class _EventHistoryPreviewSection extends HookConsumerWidget {
  const _EventHistoryPreviewSection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final purchases = ref.watch(purchaseHistoryProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Text(
            'Event history',
            style: Theme.of(context).textTheme.titleSmall,
          ),
        ),
        switch (purchases) {
          AsyncData(:final value) when value.isEmpty => const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                'No purchases yet',
                style: TextStyle(color: Colors.white54),
              ),
            ),
          AsyncData(:final value) => Column(
              children: value
                  .take(5)
                  .map((item) => PurchaseListItem(purchase: item))
                  .toList(),
            ),
          _ => const SizedBox.shrink(),
        },
      ],
    );
  }
}

/// History tab — full paginated purchase list.
///
/// Watches [purchaseHistoryProvider] and renders all purchases using
/// [PurchaseListItem]. Uses a [ScrollController] to trigger pagination
/// when the user scrolls near the bottom.
class _HistoryTab extends HookConsumerWidget {
  const _HistoryTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final purchasesAsync = ref.watch(purchaseHistoryProvider);
    final notifier = ref.read(purchaseHistoryProvider.notifier);
    final scrollController = useScrollController();

    // Trigger loadNextPage when near the bottom of the list
    useEffect(() {
      void listener() {
        if (scrollController.position.pixels >=
            scrollController.position.maxScrollExtent - 200) {
          notifier.loadNextPage();
        }
      }

      scrollController.addListener(listener);
      return () => scrollController.removeListener(listener);
    }, [scrollController]);

    return switch (purchasesAsync) {
      AsyncData(:final value) when value.isEmpty => const Center(
          child: Text(
            'No purchase history',
            style: TextStyle(color: Colors.white54),
          ),
        ),
      AsyncData(:final value) => ListView.builder(
          controller: scrollController,
          itemCount:
              value.length + (notifier.hasMore ? 1 : 0),
          itemBuilder: (context, index) {
            if (index == value.length) {
              return const Center(
                child: Padding(
                  padding: EdgeInsets.all(16),
                  child: CircularProgressIndicator(),
                ),
              );
            }
            return PurchaseListItem(purchase: value[index]);
          },
        ),
      AsyncError() => const Center(
          child: Text('Failed to load history'),
        ),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }
}

/// Parameters tab — read-only DCA configuration display.
///
/// Watches [configDataProvider] and renders the config values in three
/// [GlassCard] sections: Schedule, Strategy, and Multiplier Tiers.
class _ParametersTab extends HookConsumerWidget {
  const _ParametersTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final configAsync = ref.watch(configDataProvider);

    return switch (configAsync) {
      AsyncData(:final value) => SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Schedule section
              GlassCard(
                variant: GlassVariant.stationary,
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Schedule',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 12),
                    _ParamRow(
                      label: 'Daily buy time',
                      value:
                          '${_padTwo(value.dailyBuyHour)}:${_padTwo(value.dailyBuyMinute)}',
                    ),
                    _ParamRow(
                      label: 'Base amount (USDT)',
                      value: value.baseDailyAmount.toStringAsFixed(2),
                    ),
                    _ParamRow(
                      label: 'Dry run',
                      value: value.dryRun ? 'Yes' : 'No',
                    ),
                  ],
                ),
              ),
              // Strategy section
              GlassCard(
                variant: GlassVariant.stationary,
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Strategy',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 12),
                    _ParamRow(
                      label: 'High lookback days',
                      value: value.highLookbackDays.toString(),
                    ),
                    _ParamRow(
                      label: 'Bear market MA period',
                      value: value.bearMarketMaPeriod.toString(),
                    ),
                    _ParamRow(
                      label: 'Bear boost factor',
                      value: '${value.bearBoostFactor}x',
                    ),
                    _ParamRow(
                      label: 'Max multiplier cap',
                      value: '${value.maxMultiplierCap}x',
                    ),
                  ],
                ),
              ),
              // Multiplier Tiers section
              GlassCard(
                variant: GlassVariant.stationary,
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Multiplier Tiers',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 12),
                    if (value.tiers.isEmpty)
                      const Text(
                        'No tiers configured',
                        style: TextStyle(color: Colors.white54),
                      )
                    else
                      ...value.tiers.map(
                        (tier) => _ParamRow(
                          label:
                              'Drop ${(tier.dropPercentage * 100).toStringAsFixed(1)}%',
                          value: '${tier.multiplier}x',
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 80),
            ],
          ),
        ),
      AsyncError() => const Center(child: Text('Failed to load config')),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }

  /// Pads a single-digit integer with a leading zero (e.g. 9 → "09").
  String _padTwo(int n) => n.toString().padLeft(2, '0');
}

/// A single parameter label-value row used in [_ParametersTab].
class _ParamRow extends StatelessWidget {
  const _ParamRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 13, color: Colors.white54),
          ),
          Text(
            value,
            style: const TextStyle(
              fontSize: 13,
              color: Colors.white,
            ).merge(AppTheme.moneyStyle),
          ),
        ],
      ),
    );
  }
}

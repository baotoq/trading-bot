import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';

import '../../../app/theme.dart';
import '../../../features/config/data/config_providers.dart';
import '../data/home_providers.dart';
import 'widgets/bot_action_buttons.dart';
import 'widgets/bot_identity_header.dart';
import 'widgets/bot_stats_grid.dart';

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
            Center(
              child: Text(
                'Purchase history',
                style: TextStyle(color: Colors.white54),
              ),
            ),
            Center(
              child: Text(
                'Bot parameters',
                style: TextStyle(color: Colors.white54),
              ),
            ),
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
/// and action buttons. A chart placeholder space is included for Plan 02.
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
              const SizedBox(height: 16),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  'Profit change',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: Colors.white,
                  ),
                ),
              ),
              // Placeholder space for PnL chart (Plan 02)
              const SizedBox(height: 200),
              // Bottom padding
              const SizedBox(height: 80),
            ],
          ),
        ),
      _ when homeData.isLoading || configData.isLoading =>
        const Center(child: CircularProgressIndicator()),
      _ => const Center(
          child: Text(
            'Failed to load data',
            style: TextStyle(color: Colors.white54),
          ),
        ),
    };
  }
}

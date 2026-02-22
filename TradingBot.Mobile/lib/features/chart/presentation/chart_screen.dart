import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../data/chart_providers.dart';
import 'widgets/price_line_chart.dart';
import 'widgets/timeframe_selector.dart';

class ChartScreen extends HookConsumerWidget {
  const ChartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final selectedTimeframe = useState('1M');
    final chartData = ref.watch(
      chartDataProvider(timeframe: selectedTimeframe.value),
    );
    final cachedValue = chartData.value;

    // Show snackbar on errors — stale cached data remains visible
    ref.listen(
      chartDataProvider(timeframe: selectedTimeframe.value),
      (previous, next) {
        if (next.hasError && !next.isLoading) {
          if (next.error is AuthenticationException) {
            showAuthErrorSnackbar(context);
          } else {
            showErrorSnackbar(context, 'Could not load chart data');
          }
        }
      },
    );

    return Scaffold(
      // Transparent AppBar lets AmbientBackground orbs show through the app bar area
      appBar: AppBar(
        title: const Text('Price Chart'),
        backgroundColor: Colors.transparent,
        elevation: 0,
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(
          chartDataProvider(timeframe: selectedTimeframe.value).future,
        ),
        // AlwaysScrollableScrollPhysics ensures pull-to-refresh works even when
        // content does not fill the viewport (chart + timeframe selector is short).
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                child: TimeframeSelector(
                  selected: selectedTimeframe.value,
                  onChanged: (tf) => selectedTimeframe.value = tf,
                ),
              ),
              // Chart area — PriceLineChart uses AspectRatio(1.6) for intrinsic height,
              // so no Expanded needed here. Stack overlay from PriceLineChart works
              // correctly inside a Column without nested scroll views.
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: switch (chartData) {
                  AsyncData(:final value) => PriceLineChart(
                      data: value,
                      timeframe: selectedTimeframe.value,
                    ),
                  AsyncError() when cachedValue != null => PriceLineChart(
                      data: cachedValue,
                      timeframe: selectedTimeframe.value,
                    ),
                  AsyncError() => RetryWidget(
                      onRetry: () => ref.invalidate(
                        chartDataProvider(timeframe: selectedTimeframe.value),
                      ),
                    ),
                  _ => _buildLoadingSkeleton(),
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// Skeleton loading state — timeframe chips + rectangular chart placeholder.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: SingleChildScrollView(
        physics: const NeverScrollableScrollPhysics(),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Chart area placeholder
              Bone(
                width: double.infinity,
                height: 300,
                borderRadius: BorderRadius.circular(12),
              ),
              const SizedBox(height: 16),
              // Price label placeholders below chart
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Bone.text(words: 1, fontSize: 14),
                    Bone.text(words: 1, fontSize: 14),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Bone.text(words: 3, fontSize: 12),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../data/home_providers.dart';

class HomeScreen extends HookConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final homeData = ref.watch(homeDataProvider);

    ref.listen(homeDataProvider, (previous, next) {
      if (next.hasError && !next.isLoading) {
        if (next.error is AuthenticationException) {
          showAuthErrorSnackbar(context);
        } else {
          showErrorSnackbar(context, 'Could not load data');
        }
      }
    });

    final cachedValue = homeData.value;

    return RefreshIndicator(
      onRefresh: () => ref.refresh(homeDataProvider.future),
      child: CustomScrollView(
        slivers: [
          SliverFillRemaining(
            child: switch (homeData) {
              AsyncData(:final value) => Center(
                  child: Text(
                    // Placeholder: displays total BTC to prove data wiring works.
                    // Plan 02 will replace with the full portfolio UI.
                    '${value.portfolio.totalBtc} BTC',
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                ),
              AsyncError() when cachedValue != null => Center(
                  child: Text(
                    '${cachedValue.portfolio.totalBtc} BTC',
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                ),
              AsyncError() =>
                RetryWidget(onRetry: () => ref.invalidate(homeDataProvider)),
              _ => const Center(child: CircularProgressIndicator()),
            },
          ),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';

part 'home_screen.g.dart';

@riverpod
Future<String> homeData(Ref ref) async {
  // Placeholder — Phase 21 replaces with real portfolio data.
  // This demonstrates the Riverpod async pattern for error handling.
  await Future<void>.delayed(const Duration(milliseconds: 500));
  return 'Home';
}

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
                    value,
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                ),
              AsyncError() when cachedValue != null => Center(
                  child: Text(
                    cachedValue,
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

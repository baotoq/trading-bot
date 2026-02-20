import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';

import '../../data/currency_provider.dart';

class CurrencyToggle extends ConsumerWidget {
  const CurrencyToggle({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isVnd = ref.watch(currencyPreferenceProvider);
    return TextButton(
      onPressed: () =>
          ref.read(currencyPreferenceProvider.notifier).toggle(),
      style: TextButton.styleFrom(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        minimumSize: Size.zero,
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
      ),
      child: Text(
        isVnd ? 'VND' : 'USD',
        style: const TextStyle(
          fontWeight: FontWeight.w600,
          fontSize: 14,
        ),
      ),
    );
  }
}

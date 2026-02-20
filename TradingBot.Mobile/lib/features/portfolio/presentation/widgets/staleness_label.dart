import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

class StalenessLabel extends StatelessWidget {
  const StalenessLabel({
    required this.priceUpdatedAt,
    required this.isPriceStale,
    super.key,
  });

  final DateTime? priceUpdatedAt;
  final bool isPriceStale;

  static Widget crossCurrencyLabel() {
    return const Text(
      "converted at today's rate",
      style: TextStyle(color: Colors.white38, fontSize: 10),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!isPriceStale || priceUpdatedAt == null) {
      return const SizedBox.shrink();
    }
    return Text(
      'price as of ${DateFormat('MMM d').format(priceUpdatedAt!.toLocal())}',
      style: const TextStyle(color: Colors.white38, fontSize: 10),
    );
  }
}

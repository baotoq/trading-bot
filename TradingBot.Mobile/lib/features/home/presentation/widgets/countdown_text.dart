import 'package:flutter/material.dart';

/// Displays approximate human-readable time until the next buy.
///
/// Parses [nextBuyTimeIso] (ISO 8601) and renders:
/// - "Next buy in ~X days" (if > 24 hours)
/// - "Next buy in ~X hours" (if > 1 hour)
/// - "Next buy in ~X minutes" (if < 1 hour)
/// - "Buying soon..." (if null or in the past)
class CountdownText extends StatelessWidget {
  const CountdownText({this.nextBuyTimeIso, super.key});

  final String? nextBuyTimeIso;

  @override
  Widget build(BuildContext context) {
    final text = _buildText();

    return Text(
      text,
      style: Theme.of(context)
          .textTheme
          .bodyMedium
          ?.copyWith(color: Colors.grey[400]),
    );
  }

  String _buildText() {
    if (nextBuyTimeIso == null) return 'Buying soon...';

    final DateTime nextBuy;
    try {
      nextBuy = DateTime.parse(nextBuyTimeIso!).toUtc();
    } catch (_) {
      return 'Buying soon...';
    }

    final now = DateTime.now().toUtc();
    final diff = nextBuy.difference(now);

    if (diff.isNegative) return 'Buying soon...';

    if (diff.inHours >= 24) {
      final days = (diff.inHours / 24).ceil();
      return 'Next buy in ~$days day${days == 1 ? '' : 's'}';
    }

    if (diff.inMinutes >= 60) {
      return 'Next buy in ~${diff.inHours} hour${diff.inHours == 1 ? '' : 's'}';
    }

    final minutes = diff.inMinutes.clamp(1, 59);
    return 'Next buy in ~$minutes minute${minutes == 1 ? '' : 's'}';
  }
}

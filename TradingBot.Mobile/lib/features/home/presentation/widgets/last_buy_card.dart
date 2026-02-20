import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:timeago/timeago.dart' as timeago;

import '../../../../app/theme.dart';
import '../../data/models/status_response.dart';

/// Card showing the most recent purchase with price, BTC amount, multiplier
/// badge, drop percentage, and relative time.
///
/// If [status.lastPurchaseTime] is null, shows an "No purchases yet" placeholder.
class LastBuyCard extends StatelessWidget {
  const LastBuyCard({required this.status, super.key});

  final StatusResponse status;

  static final _usdIntFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 0);
  static final _btcFmt = NumberFormat('0.########');

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    if (status.lastPurchaseTime == null) {
      return Card(
        child: SizedBox(
          width: double.infinity,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Center(
              child: Text(
                'No purchases yet',
                style: textTheme.bodyMedium?.copyWith(color: Colors.grey[400]),
              ),
            ),
          ),
        ),
      );
    }

    final purchaseTime = DateTime.parse(status.lastPurchaseTime!);
    final relativeTime = timeago.format(purchaseTime);
    final price = status.lastPurchasePrice;
    final btc = status.lastPurchaseBtc;
    final multiplier = status.lastPurchaseMultiplier;
    final dropPercent = status.lastPurchaseDropPercentage;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Title row: "Last Buy" + relative time
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Last Buy',
                  style: textTheme.titleMedium?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                Text(
                  relativeTime,
                  style: textTheme.bodySmall?.copyWith(color: Colors.grey[400]),
                ),
              ],
            ),
            const SizedBox(height: 8),
            // Price — most prominent
            if (price != null)
              Text(
                _usdIntFmt.format(price),
                style: textTheme.headlineSmall?.copyWith(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                ),
              ),
            const SizedBox(height: 8),
            // Details row: BTC amount + multiplier badge
            Row(
              children: [
                if (btc != null)
                  Text(
                    '${_btcFmt.format(btc)} BTC',
                    style: textTheme.bodyMedium?.copyWith(color: Colors.grey[300]),
                  ),
                if (btc != null && multiplier != null)
                  const SizedBox(width: 10),
                if (multiplier != null)
                  _MultiplierBadge(multiplier: multiplier),
              ],
            ),
            // Drop percentage
            if (dropPercent != null) ...[
              const SizedBox(height: 6),
              _DropPercentText(dropPercent: dropPercent, textTheme: textTheme),
            ],
          ],
        ),
      ),
    );
  }
}

/// Colored badge for the purchase multiplier (e.g. "2.5x").
class _MultiplierBadge extends StatelessWidget {
  const _MultiplierBadge({required this.multiplier});

  final double multiplier;

  @override
  Widget build(BuildContext context) {
    final isBase = multiplier <= 1.0;
    final bgColor = isBase ? Colors.grey[700]! : AppTheme.bitcoinOrange;
    final label = '${multiplier % 1 == 0 ? multiplier.toInt() : multiplier}x';

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.bold,
            ),
      ),
    );
  }
}

/// Drop percentage with color-coded severity.
class _DropPercentText extends StatelessWidget {
  const _DropPercentText({
    required this.dropPercent,
    required this.textTheme,
  });

  final double dropPercent;
  final TextTheme textTheme;

  @override
  Widget build(BuildContext context) {
    // dropPercent is stored as absolute value (e.g. 12.5 means -12.5%)
    final absPercent = dropPercent.abs();

    final Color color;
    if (absPercent >= 20) {
      color = AppTheme.lossRed;
    } else if (absPercent >= 10) {
      color = Colors.orange;
    } else if (absPercent >= 5) {
      color = Colors.amber;
    } else {
      color = Colors.grey[400]!;
    }

    return Text(
      '-${absPercent.toStringAsFixed(1)}%',
      style: textTheme.bodySmall?.copyWith(color: color),
    );
  }
}

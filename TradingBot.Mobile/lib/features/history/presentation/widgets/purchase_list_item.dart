import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:timeago/timeago.dart' as timeago;

import '../../../../app/theme.dart';
import '../../data/models/purchase_history_response.dart';

class PurchaseListItem extends StatelessWidget {
  const PurchaseListItem({required this.purchase, super.key});

  final PurchaseDto purchase;

  @override
  Widget build(BuildContext context) {
    final dateFormatter = DateFormat('MMM d, yyyy');
    final priceFormatter = NumberFormat.currency(
      symbol: '\$',
      decimalDigits: 0,
    );
    final costFormatter = NumberFormat.currency(
      symbol: '\$',
      decimalDigits: 2,
    );
    final btcFormatter = NumberFormat('0.00000000');

    final badgeColor = _tierBadgeColor(purchase.multiplierTier);
    final dropText = purchase.dropPercentage > 0
        ? '-${purchase.dropPercentage.toStringAsFixed(1)}%'
        : '--';

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1E1E),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.white12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Top row: date on left, relative time on right
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                dateFormatter.format(purchase.executedAt),
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
                ),
              ),
              Text(
                timeago.format(purchase.executedAt),
                style: const TextStyle(fontSize: 12, color: Colors.white54),
              ),
            ],
          ),
          const SizedBox(height: 8),
          // Middle row: price on left, BTC amount on right
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                priceFormatter.format(purchase.price),
                style: const TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.bold,
                  color: Colors.white,
                ),
              ),
              Text(
                '${btcFormatter.format(purchase.quantity)} BTC',
                style: const TextStyle(
                  fontSize: 14,
                  color: Colors.white70,
                  fontFamily: 'monospace',
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          // Bottom row: cost on left, tier badge + drop% on right
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                costFormatter.format(purchase.cost),
                style: const TextStyle(fontSize: 13, color: Colors.white70),
              ),
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: badgeColor.withAlpha(38),
                      borderRadius: BorderRadius.circular(4),
                      border: Border.all(color: badgeColor.withAlpha(100)),
                    ),
                    child: Text(
                      purchase.multiplierTier,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: badgeColor,
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    dropText,
                    style: TextStyle(
                      fontSize: 13,
                      color: purchase.dropPercentage > 0
                          ? AppTheme.profitGreen
                          : Colors.white38,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Color _tierBadgeColor(String tier) {
    switch (tier.toLowerCase()) {
      case 'base':
        return AppTheme.bitcoinOrange;
      case '2x':
        return Colors.amber;
      case '3x':
        return Colors.orange;
      case '4x':
        return AppTheme.lossRed;
      default:
        return AppTheme.bitcoinOrange;
    }
  }
}

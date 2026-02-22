import 'package:flutter/material.dart';

import '../../../../app/theme.dart';

/// Bot identity header showing BTC icon, bot name, and uptime duration.
///
/// Displays at the top of the Overview tab. Takes an optional [firstPurchaseDate]
/// ISO 8601 string from [PortfolioResponse] to compute elapsed time.
class BotIdentityHeader extends StatelessWidget {
  const BotIdentityHeader({required this.firstPurchaseDate, super.key});

  /// ISO 8601 date string of the first purchase; null when no purchases exist.
  final String? firstPurchaseDate;

  String _formatUptime(String? isoDate) {
    if (isoDate == null) return 'Not started';
    final start = DateTime.parse(isoDate);
    final duration = DateTime.now().difference(start);
    final days = duration.inDays;
    final hours = duration.inHours % 24;
    final minutes = duration.inMinutes % 60;
    return 'Ongoing for ${days}D ${hours}h ${minutes}m';
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      child: Row(
        children: [
          CircleAvatar(
            radius: 24,
            backgroundColor: AppTheme.bitcoinOrange,
            child: const Icon(Icons.currency_bitcoin, color: Colors.white, size: 28),
          ),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'BTC Recurring buy',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                  color: Colors.white,
                ),
              ),
              Text(
                _formatUptime(firstPurchaseDate),
                style: const TextStyle(fontSize: 13, color: Colors.white54),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

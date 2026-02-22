import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';

/// A GlassCard showing the Bot ID (derived from the first purchase timestamp)
/// and the bot creation time.
///
/// The Bot ID is copyable via an [IconButton] that writes to the system clipboard
/// and shows a [SnackBar] confirmation.
class BotInfoCard extends StatelessWidget {
  const BotInfoCard({required this.firstPurchaseDate, super.key});

  /// ISO 8601 date string of the bot's first purchase; null if no purchases yet.
  final String? firstPurchaseDate;

  @override
  Widget build(BuildContext context) {
    final botId = _deriveBotId(firstPurchaseDate);
    final creationTime = _formatCreationTime(firstPurchaseDate);

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Bot info',
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: Colors.white),
          ),
          const SizedBox(height: 12),
          _InfoRow(
            label: 'Bot ID',
            value: botId,
            trailing: IconButton(
              icon: const Icon(Icons.copy, size: 16, color: Colors.white54),
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(),
              onPressed: () => _copyBotId(context, botId),
            ),
          ),
          const Divider(color: Colors.white12, height: 24),
          _InfoRow(
            label: 'Creation time',
            value: creationTime,
          ),
        ],
      ),
    );
  }

  /// Derives a stable Bot ID from the first purchase date in milliseconds since epoch.
  ///
  /// Returns '—' when no purchase date is available.
  String _deriveBotId(String? firstPurchaseDate) {
    if (firstPurchaseDate == null) return '—';
    try {
      final dt = DateTime.parse(firstPurchaseDate);
      return dt.millisecondsSinceEpoch.toString();
    } catch (_) {
      return '—';
    }
  }

  /// Formats the creation time as 'MM/dd/yyyy, HH:mm:ss' in local time.
  ///
  /// Returns '—' when no purchase date is available.
  String _formatCreationTime(String? firstPurchaseDate) {
    if (firstPurchaseDate == null) return '—';
    try {
      final dt = DateTime.parse(firstPurchaseDate).toLocal();
      return DateFormat('MM/dd/yyyy, HH:mm:ss').format(dt);
    } catch (_) {
      return '—';
    }
  }

  /// Copies [botId] to the system clipboard and shows a floating [SnackBar].
  void _copyBotId(BuildContext context, String botId) {
    Clipboard.setData(ClipboardData(text: botId));
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Bot ID copied to clipboard'),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }
}

/// A single label-value row with optional trailing widget.
///
/// Used in [BotInfoCard] for Bot ID (with copy button trailing) and creation time.
class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.label,
    required this.value,
    this.trailing,
  });

  final String label;
  final String value;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 13, color: Colors.white54),
        ),
        Row(
          children: [
            Text(
              value,
              style: const TextStyle(
                fontSize: 13,
                color: Colors.white,
              ).merge(AppTheme.moneyStyle),
            ),
            if (trailing != null) trailing!,
          ],
        ),
      ],
    );
  }
}

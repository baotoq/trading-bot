import 'package:flutter/material.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';

/// Quick action navigation shortcuts card for the Home dashboard.
///
/// Shows 3 navigation buttons: View Bot, Chart, and History. Each button
/// calls [onActionTap] with an action key string so the parent screen can
/// handle the navigation appropriately.
class HomeQuickActionsCard extends StatelessWidget {
  const HomeQuickActionsCard({
    required this.onActionTap,
    super.key,
  });

  /// Callback invoked when an action button is tapped.
  ///
  /// Possible action keys:
  /// - `"bot-detail"` — navigate to the DCA Bot Detail screen
  /// - `"chart"` — switch to the Chart tab
  /// - `"history"` — switch to the History tab
  final void Function(String) onActionTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Quick Actions',
            style: theme.textTheme.titleSmall?.copyWith(
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              _ActionButton(
                icon: Icons.smart_toy_outlined,
                label: 'View Bot',
                onTap: () => onActionTap('bot-detail'),
              ),
              _ActionButton(
                icon: Icons.show_chart,
                label: 'Chart',
                onTap: () => onActionTap('chart'),
              ),
              _ActionButton(
                icon: Icons.history,
                label: 'History',
                onTap: () => onActionTap('history'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              icon,
              size: 28,
              color: AppTheme.bitcoinOrange,
            ),
            const SizedBox(height: 6),
            Text(
              label,
              style: const TextStyle(
                fontSize: 12,
                color: Colors.white70,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

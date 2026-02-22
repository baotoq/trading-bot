import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/pressable_scale.dart';
import '../../data/models/portfolio_response.dart';

/// More and Share action buttons with [PressableScale] micro-interaction.
///
/// "More" renders as a glass-styled button with a no-op press (future actions TBD).
/// "Share" copies a short portfolio summary to the clipboard and shows a SnackBar.
class BotActionButtons extends StatelessWidget {
  const BotActionButtons({required this.portfolio, super.key});

  final PortfolioResponse portfolio;

  @override
  Widget build(BuildContext context) {
    final glass = Theme.of(context).extension<GlassTheme>();
    final tintColor = glass?.tintColor ?? Colors.white;
    final borderColor = glass?.borderColor ?? Colors.white.withAlpha(31);

    final pnlPct = portfolio.unrealizedPnlPercent;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Row(
        children: [
          // More button
          Expanded(
            child: PressableScale(
              onTap: () {
                // TODO: More actions TBD
              },
              child: Container(
                decoration: BoxDecoration(
                  color: tintColor.withAlpha(20),
                  border: Border.all(color: borderColor),
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.apps, size: 18, color: Colors.white70),
                    SizedBox(width: 8),
                    Text('More', style: TextStyle(fontSize: 14, color: Colors.white)),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(width: 12),
          // Share button
          Expanded(
            child: PressableScale(
              onTap: () {
                final pnlText = pnlPct != null
                    ? pnlPct.toStringAsFixed(2)
                    : '--';
                Clipboard.setData(
                  ClipboardData(
                    text:
                        'BTC DCA Bot - Invested: \$${portfolio.totalCost.toStringAsFixed(2)} | PnL: $pnlText%',
                  ),
                );
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Summary copied to clipboard')),
                );
              },
              child: Container(
                decoration: BoxDecoration(
                  color: tintColor.withAlpha(20),
                  border: Border.all(color: borderColor),
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.share_outlined, size: 18, color: Colors.white70),
                    SizedBox(width: 8),
                    Text('Share', style: TextStyle(fontSize: 14, color: Colors.white)),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

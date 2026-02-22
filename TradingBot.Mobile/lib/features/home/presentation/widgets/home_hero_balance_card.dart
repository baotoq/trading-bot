import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import 'health_badge.dart';

/// Hero balance card for the Home dashboard.
///
/// Displays total portfolio value with a count-up animation on first load
/// (driven by [shouldAnimate]). Subsequent refreshes snap directly to
/// the updated value without re-animating.
///
/// The [shouldAnimate] flag is controlled by the parent HomeScreen using a
/// `useRef<bool>` guard — see Pitfall 1 in 36-RESEARCH.md.
class HomeHeroBalanceCard extends StatelessWidget {
  const HomeHeroBalanceCard({
    required this.portfolioValue,
    required this.pnlAbsolute,
    required this.pnlPercentage,
    required this.shouldAnimate,
    this.healthStatus,
    this.healthMessage,
    super.key,
  });

  /// Total portfolio value in USD.
  final double portfolioValue;

  /// Absolute P&L in USD (positive = profit, negative = loss).
  final double pnlAbsolute;

  /// P&L as a percentage (positive = profit, negative = loss).
  final double? pnlPercentage;

  /// Whether to animate the balance from 0 to [portfolioValue].
  /// Set to false after the first load to prevent count-up replay on refresh.
  final bool shouldAnimate;

  /// Optional health status for the HealthBadge. Pass null to hide the badge.
  final String? healthStatus;

  /// Optional health message tooltip text.
  final String? healthMessage;

  static final _currencyFmt = NumberFormat.currency(
    symbol: '\$',
    decimalDigits: 2,
  );

  static final _percentFmt = NumberFormat('+0.00%;-0.00%');

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isProfit = pnlAbsolute >= 0;
    final pnlColor = isProfit ? AppTheme.profitGreen : AppTheme.lossRed;
    final pnlArrow = isProfit ? Icons.arrow_drop_up : Icons.arrow_drop_down;
    final pnlSign = isProfit ? '+' : '';

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Label row with optional health badge
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Portfolio Value',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: Colors.white70,
                ),
              ),
              if (healthStatus != null)
                HealthBadge(
                  healthStatus: healthStatus!,
                  healthMessage: healthMessage,
                ),
            ],
          ),
          const SizedBox(height: 8),
          // Hero balance with count-up animation
          TweenAnimationBuilder<double>(
            tween: Tween<double>(
              begin: shouldAnimate ? 0.0 : portfolioValue,
              end: portfolioValue,
            ),
            duration: const Duration(milliseconds: 1200),
            curve: Curves.easeOut,
            builder: (context, value, _) {
              return Text(
                _currencyFmt.format(value),
                style: theme.textTheme.headlineMedium
                    ?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: Colors.white,
                    )
                    .merge(AppTheme.moneyStyle),
              );
            },
          ),
          const SizedBox(height: 8),
          // P&L row
          Row(
            children: [
              Icon(pnlArrow, color: pnlColor, size: 20),
              const SizedBox(width: 2),
              Text(
                '$pnlSign${_currencyFmt.format(pnlAbsolute)}',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: pnlColor,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (pnlPercentage != null) ...[
                const SizedBox(width: 6),
                Text(
                  '(${_percentFmt.format(pnlPercentage! / 100)})',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: pnlColor.withAlpha(200),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

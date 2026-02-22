import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_summary_response.dart';
import '../../../../core/widgets/glass_card.dart';
import 'currency_toggle.dart';
import 'staleness_label.dart';

/// Glass hero header for the Portfolio screen.
///
/// Displays total portfolio value with tabular figures and all-time P&L stats.
/// Uses GlassCard (stationary variant) — BackdropFilter is safe here because
/// the hero sits above the scroll area and the AmbientBackground does not scroll
/// underneath it.
///
/// Decision: 24h change is omitted — [PortfolioSummaryResponse] has no rolling
/// 24h delta field. See RESEARCH.md Pitfall 1.
// TODO: 24h change requires backend support (not available in PortfolioSummaryResponse)
class PortfolioHeroHeader extends StatelessWidget {
  const PortfolioHeroHeader({
    required this.summary,
    required this.isVnd,
    super.key,
  });

  final PortfolioSummaryResponse summary;
  final bool isVnd;

  static final _vndFormatter = NumberFormat.currency(
    symbol: '\u20AB',
    decimalDigits: 0,
    locale: 'vi_VN',
  );

  static final _usdFormatter = NumberFormat.currency(
    symbol: '\$',
    decimalDigits: 2,
    locale: 'en_US',
  );

  String _formatValue(double usd, double vnd) {
    return isVnd ? _vndFormatter.format(vnd) : _usdFormatter.format(usd);
  }

  Color _pnlColor(double pnl) {
    if (pnl > 0) return AppTheme.profitGreen;
    if (pnl < 0) return AppTheme.lossRed;
    return Colors.white54;
  }

  @override
  Widget build(BuildContext context) {
    final pnlValue =
        isVnd ? summary.unrealizedPnlVnd : summary.unrealizedPnlUsd;
    final pnlColor = _pnlColor(pnlValue);
    final pnlFormatted = _formatValue(
      summary.unrealizedPnlUsd.abs(),
      summary.unrealizedPnlVnd.abs(),
    );

    return GlassCard(
      margin: const EdgeInsets.all(16),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Title row with currency toggle
          Row(
            children: [
              Text(
                'My Portfolio',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const Spacer(),
              const CurrencyToggle(),
            ],
          ),
          const SizedBox(height: 4),
          // Total value — headlineLarge + moneyStyle tabular figures
          Text(
            _formatValue(summary.totalValueUsd, summary.totalValueVnd),
            style: Theme.of(context).textTheme.headlineLarge?.merge(
                  AppTheme.moneyStyle.copyWith(fontWeight: FontWeight.bold),
                ),
          ),
          const SizedBox(height: 12),
          // All-time P&L row
          Row(
            children: [
              const Text(
                'All time: ',
                style: TextStyle(color: Colors.white54),
              ),
              Text(
                pnlValue >= 0 ? '+$pnlFormatted' : '-$pnlFormatted',
                style: TextStyle(
                  color: pnlColor,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (summary.unrealizedPnlPercent != null) ...[
                const SizedBox(width: 6),
                Icon(
                  summary.unrealizedPnlPercent! >= 0
                      ? Icons.arrow_drop_up
                      : Icons.arrow_drop_down,
                  color: pnlColor,
                  size: 20,
                ),
                Text(
                  '${summary.unrealizedPnlPercent!.abs().toStringAsFixed(2)}%',
                  style: TextStyle(
                    color: pnlColor,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ],
          ),
          // Exchange rate staleness label (only when VND mode and rate is available)
          if (isVnd && summary.exchangeRateUpdatedAt != null) ...[
            const SizedBox(height: 4),
            StalenessLabel.crossCurrencyLabel(),
          ],
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_summary_response.dart';
import 'staleness_label.dart';

class PortfolioSummaryCard extends StatelessWidget {
  const PortfolioSummaryCard({
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

  String _formatPnl(double pnlUsd, double pnlVnd) {
    final value = isVnd ? pnlVnd : pnlUsd;
    final formatted = isVnd
        ? _vndFormatter.format(pnlVnd.abs())
        : _usdFormatter.format(pnlUsd.abs());
    return value >= 0 ? '+$formatted' : '-$formatted';
  }

  Color _pnlColor(double pnl) {
    if (pnl > 0) return AppTheme.profitGreen;
    if (pnl < 0) return AppTheme.lossRed;
    return Colors.white54;
  }

  @override
  Widget build(BuildContext context) {
    final pnlValue = isVnd ? summary.unrealizedPnlVnd : summary.unrealizedPnlUsd;

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Total Portfolio Value',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: Colors.white54,
                  ),
            ),
            const SizedBox(height: 4),
            Text(
              _formatValue(summary.totalValueUsd, summary.totalValueVnd),
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Text(
                  _formatPnl(summary.unrealizedPnlUsd, summary.unrealizedPnlVnd),
                  style: TextStyle(
                    color: _pnlColor(pnlValue),
                    fontWeight: FontWeight.w600,
                    fontSize: 16,
                  ),
                ),
                if (summary.unrealizedPnlPercent != null) ...[
                  const SizedBox(width: 8),
                  Text(
                    '(${summary.unrealizedPnlPercent! >= 0 ? '+' : ''}${summary.unrealizedPnlPercent!.toStringAsFixed(2)}%)',
                    style: TextStyle(
                      color: _pnlColor(pnlValue),
                      fontSize: 14,
                    ),
                  ),
                ],
              ],
            ),
            if (isVnd && summary.exchangeRateUpdatedAt != null) ...[
              const SizedBox(height: 4),
              StalenessLabel.crossCurrencyLabel(),
            ],
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_response.dart';

/// Displays portfolio stats: hero portfolio value, 2x2 stat grid, and P&L.
///
/// Takes [PortfolioResponse] as a parameter — no provider access inside.
class PortfolioStatsSection extends StatelessWidget {
  const PortfolioStatsSection({required this.portfolio, super.key});

  final PortfolioResponse portfolio;

  static final _usdFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 2);
  static final _usdIntFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 0);
  static final _btcFmt = NumberFormat('0.########');

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;

    // Hero value: current portfolio value (price * BTC) or total cost as fallback
    final portfolioValue = portfolio.currentPrice != null
        ? portfolio.currentPrice! * portfolio.totalBtc
        : portfolio.totalCost;

    final pnl = portfolio.unrealizedPnl;
    final pnlPercent = portfolio.unrealizedPnlPercent;
    final currentPrice = portfolio.currentPrice;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Hero number: portfolio value
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Portfolio Value',
                style: textTheme.bodySmall?.copyWith(color: Colors.grey[400]),
              ),
              const SizedBox(height: 4),
              Text(
                _usdFmt.format(portfolioValue),
                style: textTheme.displaySmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: Colors.white,
                ),
              ),
              const SizedBox(height: 4),
              _PnlText(pnl: pnl, pnlPercent: pnlPercent, textTheme: textTheme),
            ],
          ),
        ),
        const SizedBox(height: 12),
        // 2x2 stat grid
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: 2.2,
          children: [
            _StatCard(
              label: 'Total BTC',
              value: '${_btcFmt.format(portfolio.totalBtc)} BTC',
            ),
            _StatCard(
              label: 'Total Cost',
              value: _usdFmt.format(portfolio.totalCost),
            ),
            _StatCard(
              label: 'Current Price',
              value: currentPrice != null
                  ? _usdIntFmt.format(currentPrice)
                  : '--',
            ),
            _StatCard(
              label: 'Avg Cost',
              value: portfolio.averageCostBasis != null
                  ? _usdIntFmt.format(portfolio.averageCostBasis!)
                  : '--',
            ),
          ],
        ),
      ],
    );
  }
}

class _PnlText extends StatelessWidget {
  const _PnlText({
    required this.pnl,
    required this.pnlPercent,
    required this.textTheme,
  });

  final double? pnl;
  final double? pnlPercent;
  final TextTheme textTheme;

  @override
  Widget build(BuildContext context) {
    if (pnl == null || pnlPercent == null) {
      return Text(
        '--',
        style: textTheme.bodyMedium?.copyWith(color: Colors.grey[400]),
      );
    }

    final Color color;
    final String sign;
    if (pnl! > 0) {
      color = AppTheme.profitGreen;
      sign = '+';
    } else if (pnl! < 0) {
      color = AppTheme.lossRed;
      sign = '';
    } else {
      color = Colors.white;
      sign = '';
    }

    final usdFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 2);
    final pnlText =
        '$sign${usdFmt.format(pnl!)} ($sign${pnlPercent!.toStringAsFixed(1)}%)';

    return Text(
      pnlText,
      style: textTheme.bodyMedium?.copyWith(color: color),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              label,
              style: textTheme.bodySmall?.copyWith(color: Colors.grey[400]),
              overflow: TextOverflow.ellipsis,
            ),
            const SizedBox(height: 2),
            Text(
              value,
              style: textTheme.titleMedium?.copyWith(color: Colors.white),
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import '../../../config/data/models/config_response.dart';
import '../../data/models/portfolio_response.dart';
import '../../data/models/status_response.dart';

/// Six-field stat grid inside a [GlassCard] showing key DCA bot metrics.
///
/// Layout:
/// - Row 1: Invested amount (USDT) | Total PnL (USDT)
/// - Row 2: Frequency/Triggered | Amount per period (USDT) | Average price (USDT)
/// - Row 3: Next purchase at (full width)
class BotStatsGrid extends StatelessWidget {
  const BotStatsGrid({
    required this.portfolio,
    required this.status,
    required this.config,
    super.key,
  });

  final PortfolioResponse portfolio;
  final StatusResponse status;
  final ConfigResponse config;

  static final _usdFormatter =
      NumberFormat.currency(symbol: '', decimalDigits: 2);
  static final _priceFormatter =
      NumberFormat.currency(symbol: '', decimalDigits: 1);

  Color _pnlColor(double? pnl) {
    if (pnl == null) return Colors.white54;
    if (pnl > 0) return AppTheme.profitGreen;
    if (pnl < 0) return AppTheme.lossRed;
    return Colors.white54;
  }

  String _padTwo(int n) => n.toString().padLeft(2, '0');

  String _formatNextBuyTime(String? isoDate) {
    if (isoDate == null) return '--';
    final dt = DateTime.tryParse(isoDate);
    if (dt == null) return '--';
    final local = dt.toLocal();
    return DateFormat('MM/dd/yyyy, HH:mm').format(local);
  }

  @override
  Widget build(BuildContext context) {
    final pnl = portfolio.unrealizedPnl;
    final pnlPct = portfolio.unrealizedPnlPercent;
    final pnlColor = _pnlColor(pnl);

    final pnlValue = pnl != null
        ? '${pnl >= 0 ? '+' : ''}${_usdFormatter.format(pnl)}'
        : '--';
    final pnlSubtitle = pnlPct != null
        ? '(${pnlPct >= 0 ? '+' : ''}${pnlPct.toStringAsFixed(2)}%)'
        : null;

    final frequencyLabel =
        'Daily at ${_padTwo(config.dailyBuyHour)}:${_padTwo(config.dailyBuyMinute)} / ${portfolio.totalPurchaseCount}';

    final avgPrice = portfolio.averageCostBasis != null
        ? 'BTC ${_priceFormatter.format(portfolio.averageCostBasis!)}'
        : '--';

    final nextBuyTime = _formatNextBuyTime(status.nextBuyTime);

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Row 1: Invested amount + Total PnL
          Row(
            children: [
              Expanded(
                child: _StatCell(
                  label: 'Invested amount\n(USDT)',
                  value: _usdFormatter.format(portfolio.totalCost),
                ),
              ),
              Expanded(
                child: _StatCell(
                  label: 'Total PnL (USDT)',
                  value: pnlValue,
                  valueColor: pnl != null ? pnlColor : Colors.white54,
                  subtitle: pnlSubtitle,
                  subtitleColor: pnlColor,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          const Divider(color: Colors.white12),
          const SizedBox(height: 12),
          // Row 2: Frequency + Amount per period + Average price
          Row(
            children: [
              Expanded(
                child: _StatCell(
                  label: 'Frequency/\nTriggered',
                  value: frequencyLabel,
                ),
              ),
              Expanded(
                child: _StatCell(
                  label: 'Amount per\nperiod (USDT)',
                  value: config.baseDailyAmount.toStringAsFixed(0),
                ),
              ),
              Expanded(
                child: _StatCell(
                  label: 'Average price\n(USDT)',
                  value: avgPrice,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          const Divider(color: Colors.white12),
          const SizedBox(height: 12),
          // Row 3: Next purchase at (full width)
          Row(
            children: [
              const Text(
                'Next purchase at',
                style: TextStyle(color: Colors.white54, fontSize: 12),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  nextBuyTime,
                  style: AppTheme.moneyStyle.copyWith(fontSize: 13),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Individual stat cell used inside [BotStatsGrid].
class _StatCell extends StatelessWidget {
  const _StatCell({
    required this.label,
    required this.value,
    this.valueColor,
    this.subtitle,
    this.subtitleColor,
  });

  final String label;
  final String value;
  final Color? valueColor;
  final String? subtitle;
  final Color? subtitleColor;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 11, color: Colors.white54),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: AppTheme.moneyStyle.copyWith(
            fontSize: 14,
            color: valueColor ?? Colors.white,
            fontWeight: FontWeight.w600,
          ),
        ),
        if (subtitle != null)
          Text(
            subtitle!,
            style: AppTheme.moneyStyle.copyWith(
              fontSize: 11,
              color: subtitleColor ?? Colors.white54,
            ),
          ),
      ],
    );
  }
}

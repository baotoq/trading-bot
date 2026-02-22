import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import '../../../chart/data/models/chart_response.dart';

/// A GlassCard showing the PnL area chart derived from price history and
/// the bot's average cost basis.
///
/// The chart line and gradient fill are colored green (profit) or red (loss)
/// based on [unrealizedPnlPercent]. A dashed zero line marks the break-even point.
class PnlChartCard extends StatelessWidget {
  const PnlChartCard({
    required this.prices,
    required this.averageCostBasis,
    required this.unrealizedPnlPercent,
    super.key,
  });

  /// Daily BTC price points used to derive PnL% over time.
  final List<PricePointDto> prices;

  /// Overall average cost basis across all purchases; null if no purchases yet.
  final double? averageCostBasis;

  /// Current unrealized PnL percentage; null if no purchases yet.
  final double? unrealizedPnlPercent;

  @override
  Widget build(BuildContext context) {
    final pnl = unrealizedPnlPercent;
    final chartColor = _pnlColor(pnl);

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Profit change',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 4),
          Text(
            pnl != null
                ? '${pnl >= 0 ? '+' : ''}${pnl.toStringAsFixed(2)}%'
                : '--',
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: pnl != null ? chartColor : Colors.white54,
            ).merge(AppTheme.moneyStyle),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 160,
            child: _buildChart(chartColor),
          ),
        ],
      ),
    );
  }

  Widget _buildChart(Color chartColor) {
    final avgCost = averageCostBasis;
    if (prices.isEmpty || avgCost == null) {
      return const SizedBox.shrink();
    }

    final pnlSpots = _buildPnlSpots(prices, avgCost);

    // Calculate interval to show ~4 date labels along x-axis
    final interval = prices.length > 1
        ? (prices.length / 4).round().toDouble().clamp(1.0, double.infinity)
        : 1.0;

    return LineChart(
      LineChartData(
        lineBarsData: [
          LineChartBarData(
            spots: pnlSpots,
            isCurved: true,
            curveSmoothness: 0.3,
            color: chartColor,
            barWidth: 1.5,
            dotData: const FlDotData(show: false),
            belowBarData: BarAreaData(
              show: true,
              gradient: LinearGradient(
                colors: [chartColor.withAlpha(77), Colors.transparent],
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
              ),
            ),
          ),
        ],
        extraLinesData: ExtraLinesData(
          horizontalLines: [
            HorizontalLine(
              y: 0,
              color: Colors.white24,
              strokeWidth: 0.5,
              dashArray: [4, 4],
            ),
          ],
        ),
        titlesData: FlTitlesData(
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 50,
              getTitlesWidget: (value, meta) {
                return Text(
                  '${value.toStringAsFixed(2)}%',
                  style: const TextStyle(
                    color: Colors.white54,
                    fontSize: 9,
                  ),
                );
              },
            ),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 20,
              interval: interval,
              getTitlesWidget: (value, meta) {
                final idx = value.toInt();
                if (idx < 0 || idx >= prices.length) {
                  return const SizedBox.shrink();
                }
                // Format "yyyy-MM-dd" → "MM/dd"
                final dateStr = prices[idx].date;
                final label = dateStr.length >= 10
                    ? '${dateStr.substring(5, 7)}/${dateStr.substring(8, 10)}'
                    : dateStr;
                return Text(
                  label,
                  style: const TextStyle(
                    color: Colors.white54,
                    fontSize: 9,
                  ),
                );
              },
            ),
          ),
          topTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          rightTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
        ),
        gridData: FlGridData(
          show: true,
          drawVerticalLine: false,
          getDrawingHorizontalLine: (value) => const FlLine(
            color: Colors.white12,
            strokeWidth: 0.5,
          ),
        ),
        borderData: FlBorderData(show: false),
        lineTouchData: const LineTouchData(enabled: false),
      ),
    );
  }

  /// Returns the semantic color for profit (green), loss (red), or neutral (white54).
  Color _pnlColor(double? pnl) {
    if (pnl == null) return Colors.white54;
    return pnl >= 0 ? AppTheme.profitGreen : AppTheme.lossRed;
  }

  /// Derives PnL percentage FlSpots from raw price data and the average cost basis.
  ///
  /// Each spot: x = index, y = ((price - avgCost) / avgCost) * 100
  List<FlSpot> _buildPnlSpots(List<PricePointDto> prices, double avgCost) {
    return prices.asMap().entries.map((entry) {
      final pnlPercent = ((entry.value.price - avgCost) / avgCost) * 100;
      return FlSpot(entry.key.toDouble(), pnlPercent);
    }).toList();
  }
}

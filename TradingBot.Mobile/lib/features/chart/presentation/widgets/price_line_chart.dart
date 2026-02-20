import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../../app/theme.dart';
import '../../data/models/chart_response.dart';

/// fl_chart LineChart displaying BTC price history with:
/// - Orange curved price line with gradient fill
/// - Tier-colored purchase marker dots at actual purchase prices
/// - Dashed horizontal average cost basis line
/// - Touch tooltip showing date and price
class PriceLineChart extends StatelessWidget {
  const PriceLineChart({
    required this.data,
    required this.timeframe,
    super.key,
  });

  final ChartResponse data;
  final String timeframe;

  /// x-axis label interval by timeframe to avoid overcrowding.
  double _xInterval() {
    return switch (timeframe) {
      '7D' => 1,
      '1M' => 7,
      '3M' => 14,
      '6M' => 30,
      '1Y' => 60,
      _ => 180, // All
    };
  }

  /// Maps a multiplier tier label to a display color.
  static Color _tierColor(String tier) {
    return switch (tier) {
      '2x' || 'Tier2' => Colors.amber,
      '3x' || 'Tier3' => Colors.orange,
      '4x' || 'Tier4' => AppTheme.lossRed,
      _ when tier.contains('4') => AppTheme.lossRed,
      _ => AppTheme.bitcoinOrange, // Base / 1x
    };
  }

  @override
  Widget build(BuildContext context) {
    final dateLabels = data.prices.map((p) => p.date).toList();

    // Build x-axis index → price spots
    final priceSpots = data.prices
        .asMap()
        .entries
        .map((e) => FlSpot(e.key.toDouble(), e.value.price))
        .toList();

    // Map purchase date strings to their day index in the price list
    final purchasesByIndex = <int, PurchaseMarkerDto>{};
    for (final purchase in data.purchases) {
      final idx = dateLabels.indexOf(purchase.date);
      if (idx != -1) {
        purchasesByIndex[idx] = purchase;
      }
    }

    // Build separate spots for purchases at their actual purchase prices
    final purchaseSpots = purchasesByIndex.entries
        .map((e) => FlSpot(e.key.toDouble(), e.value.price))
        .toList();

    final purchaseDayIndexSet = purchasesByIndex.keys.toSet();

    // Price formatter for y-axis labels
    final priceFormat = NumberFormat.currency(symbol: '\$', decimalDigits: 0);

    return AspectRatio(
      aspectRatio: 1.6,
      child: Padding(
        padding: const EdgeInsets.only(right: 8, top: 8),
        child: LineChart(
          LineChartData(
            lineBarsData: [
              // 1. Main BTC price line
              LineChartBarData(
                spots: priceSpots,
                isCurved: true,
                color: AppTheme.bitcoinOrange,
                barWidth: 2,
                dotData: const FlDotData(show: false),
                belowBarData: BarAreaData(
                  show: true,
                  gradient: LinearGradient(
                    colors: [
                      AppTheme.bitcoinOrange.withAlpha(51),
                      Colors.transparent,
                    ],
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                  ),
                ),
              ),
              // 2. Invisible line for purchase marker dots
              // Uses purchaseSpots so dots appear at actual purchase prices
              LineChartBarData(
                spots: purchaseSpots.isEmpty
                    ? [const FlSpot(0, 0)]
                    : purchaseSpots,
                barWidth: 0,
                color: Colors.transparent,
                dotData: FlDotData(
                  show: purchaseSpots.isNotEmpty,
                  checkToShowDot: (spot, _) =>
                      purchaseDayIndexSet.contains(spot.x.toInt()),
                  getDotPainter: (spot, _, __, ___) => FlDotCirclePainter(
                    radius: 5,
                    color: _tierColor(
                      purchasesByIndex[spot.x.toInt()]?.tier ?? 'Base',
                    ),
                    strokeWidth: 1.5,
                    strokeColor: Colors.white,
                  ),
                ),
              ),
            ],
            extraLinesData: data.averageCostBasis != null
                ? ExtraLinesData(
                    horizontalLines: [
                      HorizontalLine(
                        y: data.averageCostBasis!,
                        color: Colors.white54,
                        strokeWidth: 1,
                        dashArray: [6, 4],
                        label: HorizontalLineLabel(
                          show: true,
                          labelResolver: (_) =>
                              'Avg ${priceFormat.format(data.averageCostBasis!)}',
                          style: const TextStyle(
                            color: Colors.white54,
                            fontSize: 10,
                          ),
                        ),
                      ),
                    ],
                  )
                : null,
            lineTouchData: LineTouchData(
              handleBuiltInTouches: true,
              touchTooltipData: LineTouchTooltipData(
                getTooltipColor: (_) => const Color(0xFF2C2C2C),
                getTooltipItems: (touchedSpots) {
                  return touchedSpots.map((spot) {
                    // Only show tooltip for the price line (bar index 0)
                    if (spot.barIndex != 0) return null;
                    final xIndex = spot.x.toInt();
                    final dateLabel = xIndex < dateLabels.length
                        ? dateLabels[xIndex]
                        : '';
                    final formattedPrice = priceFormat.format(spot.y);
                    return LineTooltipItem(
                      '$dateLabel\n$formattedPrice',
                      const TextStyle(color: Colors.white, fontSize: 12),
                    );
                  }).whereType<LineTooltipItem>().toList();
                },
              ),
            ),
            titlesData: FlTitlesData(
              bottomTitles: AxisTitles(
                sideTitles: SideTitles(
                  showTitles: true,
                  interval: _xInterval(),
                  reservedSize: 28,
                  getTitlesWidget: (value, meta) {
                    final idx = value.toInt();
                    if (idx < 0 || idx >= dateLabels.length) {
                      return const SizedBox.shrink();
                    }
                    final dateStr = dateLabels[idx];
                    // Format "yyyy-MM-dd" → "MMM d" (e.g. "Jan 15")
                    try {
                      final date = DateTime.parse(dateStr);
                      final label = DateFormat('MMM d').format(date);
                      return Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          label,
                          style: const TextStyle(
                            color: Colors.white54,
                            fontSize: 10,
                          ),
                        ),
                      );
                    } catch (_) {
                      return const SizedBox.shrink();
                    }
                  },
                ),
              ),
              leftTitles: AxisTitles(
                sideTitles: SideTitles(
                  showTitles: true,
                  reservedSize: 52,
                  getTitlesWidget: (value, meta) {
                    // Format as "$XXk" or "$X.Xk"
                    final inK = value / 1000;
                    final label = inK >= 10
                        ? '\$${inK.toStringAsFixed(0)}k'
                        : '\$${inK.toStringAsFixed(1)}k';
                    return Text(
                      label,
                      style: const TextStyle(
                        color: Colors.white54,
                        fontSize: 10,
                      ),
                    );
                  },
                ),
              ),
              rightTitles: const AxisTitles(
                sideTitles: SideTitles(showTitles: false),
              ),
              topTitles: const AxisTitles(
                sideTitles: SideTitles(showTitles: false),
              ),
            ),
            gridData: FlGridData(
              show: true,
              drawVerticalLine: false,
              getDrawingHorizontalLine: (_) => const FlLine(
                color: Colors.white10,
                strokeWidth: 0.5,
              ),
            ),
            borderData: FlBorderData(show: false),
          ),
        ),
      ),
    );
  }
}

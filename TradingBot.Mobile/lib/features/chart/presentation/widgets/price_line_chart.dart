import 'dart:math' show max;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import '../../data/models/chart_response.dart';
import 'glass_chart_tooltip.dart';
import 'glow_dot_painter.dart';

/// fl_chart LineChart displaying BTC price history with:
/// - Orange curved price line with enhanced gradient fill (CHART-01)
/// - Left-to-right draw-in animation on first tab entry (CHART-02)
/// - Tier-colored purchase marker dots with radial glow halo (CHART-03)
/// - Frosted glass tooltip overlay on touch (CHART-04)
/// - Dashed horizontal average cost basis line
class PriceLineChart extends HookConsumerWidget {
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
  Widget build(BuildContext context, WidgetRef ref) {
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

    // Price formatter for y-axis labels and tooltip
    final priceFormat = NumberFormat.currency(symbol: '\$', decimalDigits: 0);

    // --- Draw-in animation (CHART-02) ---
    // useRef(false) guards against re-triggering the animation on tab revisit.
    // The draw-in fires ONLY once per session (first time this widget is mounted).
    final hasAnimated = useRef(false);
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 1000),
    );
    final animation = useAnimation(
      CurvedAnimation(parent: controller, curve: Curves.easeInOut),
    );

    useEffect(() {
      if (!hasAnimated.value && !GlassCard.shouldReduceMotion(context)) {
        // First entry, motion allowed: run draw-in animation
        hasAnimated.value = true;
        controller.forward();
      } else if (!hasAnimated.value) {
        // First entry, Reduce Motion enabled: skip animation, show full chart
        hasAnimated.value = true;
        controller.value = 1.0;
      } else {
        // Already animated on a previous build (tab revisit): show full chart
        controller.value = 1.0;
      }
      return null;
    }, [data]);

    // Slice priceSpots to animate the line drawing left-to-right
    final totalSpots = priceSpots.length;
    final visibleCount =
        controller.isCompleted || GlassCard.shouldReduceMotion(context)
            ? totalSpots
            : max(1, (animation * totalSpots).round());
    final visiblePriceSpots = priceSpots.sublist(0, visibleCount);

    // --- Glass tooltip touch state (CHART-04) ---
    // Tracks the currently touched spot to render the custom glass overlay.
    // Cleared on tap-up, pan-end, or long-press-end events.
    final touchedSpot = useState<LineBarSpot?>(null);

    final lineChartData = LineChartData(
      clipData: const FlClipData.all(),
      maxX: visiblePriceSpots.last.x,
      lineBarsData: [
        // 1. Main BTC price line (animated draw-in)
        LineChartBarData(
          spots: visiblePriceSpots,
          isCurved: true,
          color: AppTheme.bitcoinOrange,
          barWidth: 2,
          dotData: const FlDotData(show: false),
          belowBarData: BarAreaData(
            show: true,
            // Enhanced multi-stop gradient for vivid "glow" appearance (CHART-01)
            // Top alpha increased from 51 (~0.20) to 77 (~0.30)
            // Middle stop at 0.5 with alpha 26 (~0.10) sustains gradient glow
            gradient: LinearGradient(
              colors: [
                AppTheme.bitcoinOrange.withAlpha(77), // ~0.30 at top
                AppTheme.bitcoinOrange.withAlpha(26), // ~0.10 in middle
                AppTheme.bitcoinOrange.withAlpha(0),  // transparent at bottom
              ],
              stops: const [0.0, 0.5, 1.0],
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
            ),
          ),
        ),
        // 2. Invisible line for purchase marker dots
        // Uses purchaseSpots so dots appear at actual purchase prices.
        // Dots are hidden during draw-in animation (controller not completed).
        LineChartBarData(
          spots: purchaseSpots.isEmpty
              ? [const FlSpot(0, 0)]
              : purchaseSpots,
          barWidth: 0,
          color: Colors.transparent,
          dotData: FlDotData(
            // Hide dots during draw-in; show only when animation completes (CHART-03)
            show: purchaseSpots.isNotEmpty && controller.isCompleted,
            checkToShowDot: (spot, _) =>
                purchaseDayIndexSet.contains(spot.x.toInt()),
            getDotPainter: (spot, _, __, ___) => GlowDotPainter(
              radius: 5,
              color: _tierColor(
                purchasesByIndex[spot.x.toInt()]?.tier ?? 'Base',
              ),
              glowColor: AppTheme.bitcoinOrange,
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
      // Custom touch handling for glass tooltip overlay (CHART-04)
      // Pattern: keep handleBuiltInTouches: true to preserve the vertical
      // indicator line, suppress built-in tooltip text via null returns,
      // and track the touched spot for the custom glass overlay.
      // See: 35-RESEARCH.md Pitfall 4 solution.
      lineTouchData: LineTouchData(
        handleBuiltInTouches: true,
        touchTooltipData: LineTouchTooltipData(
          // Suppress built-in tooltip rectangle by returning null for all items
          getTooltipItems: (touchedSpots) =>
              touchedSpots.map((_) => null).toList(),
        ),
        // Track touched spot in state for custom glass overlay
        touchCallback: (FlTouchEvent event, LineTouchResponse? response) {
          if (event is FlTapUpEvent ||
              event is FlPanEndEvent ||
              event is FlLongPressEnd) {
            touchedSpot.value = null;
            return;
          }
          if (response?.lineBarSpots != null &&
              response!.lineBarSpots!.isNotEmpty) {
            final spot = response.lineBarSpots!
                .where((s) => s.barIndex == 0)
                .firstOrNull;
            touchedSpot.value = spot;
          }
        },
        // Style the vertical indicator line with a dashed white line
        getTouchedSpotIndicator: (barData, spotIndexes) {
          return spotIndexes.map((index) {
            return TouchedSpotIndicatorData(
              const FlLine(
                color: Colors.white38,
                strokeWidth: 1,
                dashArray: [4, 4],
              ),
              FlDotData(show: false),
            );
          }).toList();
        },
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
    );

    return Stack(
      clipBehavior: Clip.none,
      children: [
        // Existing AspectRatio > Padding > LineChart (preserved from Plan 01)
        AspectRatio(
          aspectRatio: 1.6,
          child: Padding(
            padding: const EdgeInsets.only(right: 8, top: 8),
            child: LineChart(lineChartData),
          ),
        ),
        // Glass tooltip overlay — centered above the chart, visible on touch
        if (touchedSpot.value != null)
          _buildTooltipOverlay(
            context,
            touchedSpot.value!,
            dateLabels,
            priceFormat,
          ),
      ],
    );
  }

  /// Builds the frosted glass tooltip positioned above the chart area.
  ///
  /// Anchored at [top: 0] of the Stack and centered horizontally.
  /// This avoids complex pixel-position calculations (see 35-RESEARCH.md
  /// Open Question #1) and works reliably above the chart content area.
  Widget _buildTooltipOverlay(
    BuildContext context,
    LineBarSpot spot,
    List<String> dateLabels,
    NumberFormat priceFormat,
  ) {
    final xIndex = spot.x.toInt();
    final dateStr = xIndex < dateLabels.length ? dateLabels[xIndex] : '';
    String formattedDate = dateStr;
    try {
      final date = DateTime.parse(dateStr);
      formattedDate = DateFormat('MMM d, yyyy').format(date);
    } catch (_) {}

    return Positioned(
      top: 0,
      left: 16,
      right: 16,
      child: Center(
        child: GlassChartTooltip(
          date: formattedDate,
          price: priceFormat.format(spot.y),
        ),
      ),
    );
  }
}

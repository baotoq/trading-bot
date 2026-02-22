import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import '../../../portfolio/data/models/portfolio_summary_response.dart';

/// Mini allocation donut chart in a GlassCard for the Home dashboard.
///
/// Shows a compact [PieChart] (height 140px, no touch interaction) with an
/// allocation label row below. Full interactivity lives on the Portfolio screen.
class HomeMiniDonutCard extends StatelessWidget {
  const HomeMiniDonutCard({
    required this.allocations,
    required this.isVnd,
    super.key,
  });

  /// Allocation slices from [PortfolioSummaryResponse.allocations].
  final List<AllocationDto> allocations;

  /// Whether to display values in VND (true) or USD (false).
  final bool isVnd;

  Color _colorForType(String assetType) {
    switch (assetType) {
      case 'Crypto':
        return AppTheme.bitcoinOrange;
      case 'ETF':
        return const Color(0xFF42A5F5);
      case 'FixedDeposit':
        return AppTheme.profitGreen;
      default:
        return Colors.grey;
    }
  }

  String _labelForType(String assetType) {
    switch (assetType) {
      case 'FixedDeposit':
        return 'Fixed';
      default:
        return assetType;
    }
  }

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
            'Allocation',
            style: theme.textTheme.titleSmall?.copyWith(
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 12),
          if (allocations.isEmpty)
            const SizedBox(
              height: 140,
              child: Center(
                child: Text(
                  'No allocations',
                  style: TextStyle(color: Colors.white38),
                ),
              ),
            )
          else ...[
            SizedBox(
              height: 140,
              child: PieChart(
                PieChartData(
                  centerSpaceRadius: 40,
                  centerSpaceColor: Colors.transparent,
                  sectionsSpace: 2,
                  sections: allocations
                      .map(
                        (a) => PieChartSectionData(
                          value: a.percentage,
                          color: _colorForType(a.assetType),
                          radius: 45,
                          showTitle: false,
                        ),
                      )
                      .toList(),
                ),
              ),
            ),
            const SizedBox(height: 10),
            // Compact allocation labels below the chart
            Wrap(
              spacing: 12,
              runSpacing: 6,
              children: allocations.map((a) {
                final color = _colorForType(a.assetType);
                return Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: color,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 4),
                    Text(
                      '${_labelForType(a.assetType)} ${a.percentage.toStringAsFixed(0)}%',
                      style: const TextStyle(
                        fontSize: 12,
                        color: Colors.white70,
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ],
        ],
      ),
    );
  }
}

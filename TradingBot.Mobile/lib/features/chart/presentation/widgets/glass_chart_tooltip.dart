import 'package:flutter/material.dart';

import '../../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';

/// Frosted glass tooltip overlay for the price chart.
///
/// Renders a [GlassCard] (stationary variant — BackdropFilter frosted blur)
/// with the touched date and locale-formatted price. The widget does NOT
/// handle its own positioning — it is placed inside a [Positioned] by the
/// parent [Stack] in [PriceLineChart].
class GlassChartTooltip extends StatelessWidget {
  const GlassChartTooltip({
    required this.date,
    required this.price,
    super.key,
  });

  final String date;
  final String price;

  @override
  Widget build(BuildContext context) {
    return GlassCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      borderRadius: 12,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            date,
            style: const TextStyle(color: Colors.white70, fontSize: 11),
          ),
          Text(
            price,
            style: AppTheme.moneyStyle.copyWith(
              color: Colors.white,
              fontSize: 14,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    );
  }
}

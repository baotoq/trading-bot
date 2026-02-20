import 'package:flutter/material.dart';

import '../../../../app/theme.dart';

/// Compact health badge displaying a colored dot and label.
///
/// Three states based on [healthStatus]:
/// - "Healthy" → green dot + "Healthy"
/// - "Warning" → amber dot + "Warning"
/// - "Down" / "Error" / anything else → red dot + "Down"
class HealthBadge extends StatelessWidget {
  const HealthBadge({
    required this.healthStatus,
    this.healthMessage,
    super.key,
  });

  final String healthStatus;
  final String? healthMessage;

  @override
  Widget build(BuildContext context) {
    final (color, label) = switch (healthStatus.toLowerCase()) {
      'healthy' => (AppTheme.profitGreen, 'Healthy'),
      'warning' => (Colors.amber, 'Warning'),
      _ => (AppTheme.lossRed, 'Down'),
    };

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
        const SizedBox(width: 6),
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(color: color),
        ),
      ],
    );
  }
}

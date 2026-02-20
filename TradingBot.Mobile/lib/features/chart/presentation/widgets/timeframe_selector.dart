import 'package:flutter/material.dart';

import '../../../../../app/theme.dart';

/// Row of 6 selectable timeframe buttons: 7D, 1M, 3M, 6M, 1Y, All.
class TimeframeSelector extends StatelessWidget {
  const TimeframeSelector({
    required this.selected,
    required this.onChanged,
    super.key,
  });

  final String selected;
  final ValueChanged<String> onChanged;

  static const List<String> _timeframes = ['7D', '1M', '3M', '6M', '1Y', 'All'];

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: _timeframes.map((tf) {
          final isSelected = tf == selected;
          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: ChoiceChip(
              label: Text(tf),
              selected: isSelected,
              onSelected: (_) => onChanged(tf),
              selectedColor: AppTheme.bitcoinOrange,
              labelStyle: TextStyle(
                color: isSelected ? Colors.black : Colors.white70,
                fontWeight:
                    isSelected ? FontWeight.bold : FontWeight.normal,
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

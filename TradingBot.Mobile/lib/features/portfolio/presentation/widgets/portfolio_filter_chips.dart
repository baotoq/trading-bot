import 'package:flutter/material.dart';

import '../../../../app/theme.dart';

/// Horizontal scrolling filter chip row for the Portfolio Overview tab.
///
/// Filter options control the sort order of the flat asset list in
/// [PortfolioScreen]. Active chip is highlighted in [AppTheme.bitcoinOrange].
/// Inactive chips use the GlassTheme tint-only surface (no BackdropFilter).
///
/// Sort semantics (applied in [PortfolioScreen]):
/// - 'Holding amount'   → sort by currentValueUsd DESC
/// - 'Cumulative profit' → sort by unrealizedPnlUsd DESC
/// - 'Analysis'         → sort by unrealizedPnlPercent DESC (nulls last)
class PortfolioFilterChips extends StatelessWidget {
  const PortfolioFilterChips({
    required this.activeFilter,
    super.key,
  });

  final ValueNotifier<String> activeFilter;

  static const _filters = ['Holding amount', 'Cumulative profit', 'Analysis'];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    // GlassTheme is registered as a ThemeExtension — read for tint/border tokens.
    final glass = theme.extension<GlassTheme>();

    return ValueListenableBuilder<String>(
      valueListenable: activeFilter,
      builder: (context, current, _) {
        return SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            children: _filters.map((label) {
              final isActive = label == current;
              return Padding(
                padding: const EdgeInsets.only(right: 8),
                child: GestureDetector(
                  onTap: () => activeFilter.value = label,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 14,
                      vertical: 8,
                    ),
                    decoration: BoxDecoration(
                      color: isActive
                          ? AppTheme.bitcoinOrange.withAlpha(51)
                          : (glass?.tintColor.withAlpha(
                                (glass.tintOpacity * 255).round(),
                              ) ??
                              Colors.white.withAlpha(20)),
                      borderRadius: BorderRadius.circular(20),
                      border: Border.all(
                        color: isActive
                            ? AppTheme.bitcoinOrange
                            : (glass?.borderColor ??
                                Colors.white.withAlpha(31)),
                      ),
                    ),
                    child: Text(
                      label,
                      style: TextStyle(
                        color:
                            isActive ? AppTheme.bitcoinOrange : Colors.white70,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ),
              );
            }).toList(),
          ),
        );
      },
    );
  }
}

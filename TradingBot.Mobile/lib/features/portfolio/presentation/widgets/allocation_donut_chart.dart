import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_summary_response.dart';

class AllocationDonutChart extends StatefulWidget {
  const AllocationDonutChart({
    required this.allocations,
    required this.totalValue,
    required this.isVnd,
    super.key,
  });

  final List<AllocationDto> allocations;
  final double totalValue;
  final bool isVnd;

  @override
  State<AllocationDonutChart> createState() => _AllocationDonutChartState();
}

class _AllocationDonutChartState extends State<AllocationDonutChart> {
  int _touchedIndex = -1;

  static final _vndFormatter = NumberFormat.currency(
    symbol: '\u20AB',
    decimalDigits: 0,
    locale: 'vi_VN',
  );

  static final _usdFormatter = NumberFormat.currency(
    symbol: '\$',
    decimalDigits: 2,
    locale: 'en_US',
  );

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
        return 'Fixed Deposit';
      default:
        return assetType;
    }
  }

  String _formatValue(double value) {
    return widget.isVnd
        ? _vndFormatter.format(value)
        : _usdFormatter.format(value);
  }

  @override
  Widget build(BuildContext context) {
    if (widget.allocations.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(32),
        child: Center(
          child: Text(
            'No assets yet',
            style: TextStyle(color: Colors.white38),
          ),
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Column(
        children: [
          SizedBox(
            height: 200,
            child: Stack(
              alignment: Alignment.center,
              children: [
                PieChart(
                  PieChartData(
                    centerSpaceRadius: 55,
                    centerSpaceColor: AppTheme.surfaceDark,
                    sectionsSpace: 2,
                    pieTouchData: PieTouchData(
                      touchCallback: (event, response) {
                        setState(() {
                          if (!event.isInterestedForInteractions ||
                              response == null ||
                              response.touchedSection == null) {
                            _touchedIndex = -1;
                            return;
                          }
                          _touchedIndex =
                              response.touchedSection!.touchedSectionIndex;
                        });
                      },
                    ),
                    sections: widget.allocations
                        .asMap()
                        .entries
                        .map((entry) {
                      final isTouched = entry.key == _touchedIndex;
                      return PieChartSectionData(
                        value: entry.value.percentage,
                        color: _colorForType(entry.value.assetType),
                        radius: isTouched ? 65 : 55,
                        showTitle: false,
                      );
                    }).toList(),
                  ),
                ),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      _formatValue(widget.totalValue),
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                    Text(
                      'Total',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: Colors.white54,
                          ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          if (_touchedIndex >= 0 &&
              _touchedIndex < widget.allocations.length) ...[
            const SizedBox(height: 8),
            _buildTooltip(widget.allocations[_touchedIndex]),
          ],
          const SizedBox(height: 8),
          _buildLegend(),
        ],
      ),
    );
  }

  Widget _buildTooltip(AllocationDto allocation) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.white10,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        '${_labelForType(allocation.assetType)}: ${allocation.percentage.toStringAsFixed(1)}% - ${_formatValue(allocation.valueUsd)}',
        style: const TextStyle(fontSize: 13),
      ),
    );
  }

  Widget _buildLegend() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: widget.allocations.map((a) {
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: _colorForType(a.assetType),
                  shape: BoxShape.circle,
                ),
              ),
              const SizedBox(width: 4),
              Text(
                _labelForType(a.assetType),
                style: const TextStyle(fontSize: 12, color: Colors.white70),
              ),
            ],
          ),
        );
      }).toList(),
    );
  }
}

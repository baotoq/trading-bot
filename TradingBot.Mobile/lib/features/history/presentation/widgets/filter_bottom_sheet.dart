import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';

class FilterBottomSheet extends StatefulWidget {
  const FilterBottomSheet({
    required this.onApply,
    this.initialDateRange,
    this.initialTier,
    super.key,
  });

  final void Function(DateTimeRange? dateRange, String? tier) onApply;
  final DateTimeRange? initialDateRange;
  final String? initialTier;

  @override
  State<FilterBottomSheet> createState() => _FilterBottomSheetState();
}

class _FilterBottomSheetState extends State<FilterBottomSheet> {
  DateTimeRange? _selectedRange;
  String? _selectedTier;

  static const _tiers = ['All', 'Base', '2x', '3x', '4x'];

  @override
  void initState() {
    super.initState();
    _selectedRange = widget.initialDateRange;
    _selectedTier = widget.initialTier ?? 'All';
  }

  @override
  Widget build(BuildContext context) {
    final dateLabel = _selectedRange != null
        ? '${_formatDate(_selectedRange!.start)} – ${_formatDate(_selectedRange!.end)}'
        : 'Any';

    return Padding(
      padding: EdgeInsets.fromLTRB(
        16,
        16,
        16,
        MediaQuery.of(context).viewInsets.bottom + 24,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Filter Purchases',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              IconButton(
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ],
          ),
          const SizedBox(height: 12),
          const Divider(color: Colors.white12),
          const SizedBox(height: 12),

          // Date range section
          Text(
            'Date Range',
            style: Theme.of(
              context,
            ).textTheme.labelLarge?.copyWith(color: Colors.white70),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Colors.white24),
                    alignment: Alignment.centerLeft,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 12,
                    ),
                  ),
                  onPressed: _openDateRangePicker,
                  child: Text(
                    dateLabel,
                    style: TextStyle(
                      color: _selectedRange != null
                          ? Colors.white
                          : Colors.white54,
                    ),
                  ),
                ),
              ),
              if (_selectedRange != null) ...[
                const SizedBox(width: 8),
                IconButton(
                  icon: const Icon(Icons.close, size: 18),
                  color: Colors.white54,
                  onPressed: () => setState(() => _selectedRange = null),
                ),
              ],
            ],
          ),
          const SizedBox(height: 20),

          // Tier section
          Text(
            'Multiplier Tier',
            style: Theme.of(
              context,
            ).textTheme.labelLarge?.copyWith(color: Colors.white70),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            children: _tiers.map((tier) {
              final isSelected = _selectedTier == tier;
              return ChoiceChip(
                label: Text(tier),
                selected: isSelected,
                selectedColor: AppTheme.bitcoinOrange,
                labelStyle: TextStyle(
                  color: isSelected ? Colors.black : Colors.white70,
                  fontWeight: isSelected ? FontWeight.w700 : FontWeight.normal,
                ),
                onSelected: (_) => setState(() => _selectedTier = tier),
              );
            }).toList(),
          ),
          const SizedBox(height: 24),

          // Action row
          Row(
            children: [
              TextButton(
                onPressed: () {
                  setState(() {
                    _selectedRange = null;
                    _selectedTier = 'All';
                  });
                },
                child: const Text(
                  'Clear All',
                  style: TextStyle(color: Colors.white54),
                ),
              ),
              const Spacer(),
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppTheme.bitcoinOrange,
                  foregroundColor: Colors.black,
                ),
                onPressed: _applyFilter,
                child: const Text(
                  'Apply',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _openDateRangePicker() async {
    final result = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
      initialDateRange: _selectedRange,
    );
    if (result != null) {
      setState(() => _selectedRange = result);
    }
  }

  void _applyFilter() {
    final effectiveTier =
        (_selectedTier == null || _selectedTier == 'All') ? null : _selectedTier;
    widget.onApply(_selectedRange, effectiveTier);
    Navigator.of(context).pop();
  }

  String _formatDate(DateTime date) {
    return DateFormat('MMM d').format(date);
  }
}

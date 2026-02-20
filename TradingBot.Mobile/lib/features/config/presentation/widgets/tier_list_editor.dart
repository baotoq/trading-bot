import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

import '../../data/models/config_response.dart';

/// Editable multiplier tier list with add, remove, and reorder support.
class TierListEditor extends StatelessWidget {
  const TierListEditor({
    required this.tiers,
    required this.onChanged,
    this.tierErrors = const {},
    super.key,
  });

  final List<MultiplierTierDto> tiers;
  final ValueChanged<List<MultiplierTierDto>> onChanged;

  /// Map of error codes to messages for tier-related validation errors.
  /// Keys: "TiersNotAscending", "TierMultiplierOutOfRange", "TierDropPercentageDuplicate"
  final Map<String, String> tierErrors;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (tiers.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 16),
            child: Text(
              'No tiers. Tap + to add one.',
              style: TextStyle(color: Colors.grey),
            ),
          )
        else
          ReorderableListView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            itemCount: tiers.length,
            onReorder: (oldIndex, newIndex) {
              final updated = List<MultiplierTierDto>.from(tiers);
              if (newIndex > oldIndex) newIndex--;
              final item = updated.removeAt(oldIndex);
              updated.insert(newIndex, item);
              onChanged(updated);
            },
            itemBuilder: (context, index) {
              final tier = tiers[index];
              return _TierRow(
                key: ValueKey('tier-$index'),
                index: index,
                tier: tier,
                onDropChanged: (value) {
                  final updated = List<MultiplierTierDto>.from(tiers);
                  updated[index] = tier.copyWith(dropPercentage: value);
                  onChanged(updated);
                },
                onMultiplierChanged: (value) {
                  final updated = List<MultiplierTierDto>.from(tiers);
                  updated[index] = tier.copyWith(multiplier: value);
                  onChanged(updated);
                },
                onRemove: () {
                  final updated = List<MultiplierTierDto>.from(tiers);
                  updated.removeAt(index);
                  onChanged(updated);
                },
              );
            },
          ),

        // Add tier button
        Center(
          child: TextButton.icon(
            onPressed: () {
              final updated = List<MultiplierTierDto>.from(tiers);
              updated.add(MultiplierTierDto(dropPercentage: 0, multiplier: 1));
              onChanged(updated);
            },
            icon: const Icon(CupertinoIcons.plus_circle),
            label: const Text('Add Tier'),
          ),
        ),

        // Tier validation errors
        if (tierErrors.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: tierErrors.values
                  .map(
                    (msg) => Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Text(
                        msg,
                        style: TextStyle(
                          color: theme.colorScheme.error,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ),
      ],
    );
  }
}

class _TierRow extends StatelessWidget {
  const _TierRow({
    required this.index,
    required this.tier,
    required this.onDropChanged,
    required this.onMultiplierChanged,
    required this.onRemove,
    super.key,
  });

  final int index;
  final MultiplierTierDto tier;
  final ValueChanged<double> onDropChanged;
  final ValueChanged<double> onMultiplierChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          // Drag handle area (implicit via ReorderableListView)
          const Icon(Icons.drag_handle, color: Colors.grey, size: 20),
          const SizedBox(width: 8),
          // Drop percentage field
          Expanded(
            child: TextFormField(
              initialValue: tier.dropPercentage.toString(),
              decoration: const InputDecoration(
                labelText: 'Drop %',
                isDense: true,
                border: OutlineInputBorder(),
              ),
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              onChanged: (value) {
                final parsed = double.tryParse(value);
                if (parsed != null) onDropChanged(parsed);
              },
            ),
          ),
          const SizedBox(width: 8),
          // Multiplier field
          Expanded(
            child: TextFormField(
              initialValue: tier.multiplier.toString(),
              decoration: const InputDecoration(
                labelText: 'Multiplier',
                isDense: true,
                border: OutlineInputBorder(),
              ),
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              onChanged: (value) {
                final parsed = double.tryParse(value);
                if (parsed != null) onMultiplierChanged(parsed);
              },
            ),
          ),
          // Remove button
          IconButton(
            onPressed: onRemove,
            icon: const Icon(CupertinoIcons.minus_circle, color: Colors.red),
            iconSize: 20,
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
          ),
        ],
      ),
    );
  }
}

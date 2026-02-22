import 'package:flutter/material.dart';

import '../../../../core/widgets/glass_card.dart';
import '../../../history/data/models/purchase_history_response.dart';
import '../../../history/presentation/widgets/purchase_list_item.dart';

/// Recent purchase activity card for the Home dashboard.
///
/// Shows up to 3 [PurchaseListItem] widgets in a static [Column]
/// (not a [ListView]) — this keeps [BackdropFilter] safe per the project
/// constraint that prevents Impeller frame drops in scrollable lists.
class HomeRecentActivityCard extends StatelessWidget {
  const HomeRecentActivityCard({
    required this.purchases,
    super.key,
  });

  /// Recent purchases to display. Should be at most 3 items.
  final List<PurchaseDto> purchases;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final items = purchases.take(3).toList();

    return GlassCard(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Recent Purchases',
            style: theme.textTheme.titleSmall?.copyWith(
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 8),
          if (items.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Center(
                child: Text(
                  'No purchases yet',
                  style: TextStyle(color: Colors.white38),
                ),
              ),
            )
          else
            Column(
              children: [
                for (int i = 0; i < items.length; i++) ...[
                  if (i > 0) const SizedBox(height: 8),
                  PurchaseListItem(purchase: items[i]),
                ],
              ],
            ),
        ],
      ),
    );
  }
}

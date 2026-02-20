import 'package:flutter/material.dart';

import '../../../../app/theme.dart';
import '../../data/models/config_response.dart';

/// Read-only view of all DCA configuration parameters, grouped into cards.
class ConfigViewSection extends StatelessWidget {
  const ConfigViewSection({required this.config, super.key});

  final ConfigResponse config;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Column(
      children: [
        // DCA Settings card
        Card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Text('DCA Settings', style: textTheme.titleMedium),
              ),
              ListTile(
                leading: const Icon(Icons.attach_money),
                title: const Text('Base Daily Amount'),
                trailing: Text(
                  '\$${config.baseDailyAmount.toStringAsFixed(2)}',
                  style: textTheme.bodyLarge?.copyWith(
                    color: AppTheme.bitcoinOrange,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              ListTile(
                leading: const Icon(Icons.schedule),
                title: const Text('Daily Buy Time'),
                trailing: Text(
                  '${config.dailyBuyHour.toString().padLeft(2, '0')}:${config.dailyBuyMinute.toString().padLeft(2, '0')} UTC',
                  style: textTheme.bodyLarge,
                ),
              ),
              ListTile(
                leading: const Icon(Icons.science),
                title: const Text('Dry Run'),
                trailing: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: config.dryRun
                        ? Colors.amber.withAlpha(51)
                        : Colors.green.withAlpha(51),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    config.dryRun ? 'Yes' : 'No',
                    style: textTheme.bodyMedium?.copyWith(
                      color: config.dryRun ? Colors.amber : Colors.green,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),

        // Market Analysis card
        Card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Text('Market Analysis', style: textTheme.titleMedium),
              ),
              ListTile(
                leading: const Icon(Icons.trending_up),
                title: const Text('High Lookback Days'),
                trailing: Text(
                  '${config.highLookbackDays} days',
                  style: textTheme.bodyLarge,
                ),
              ),
              ListTile(
                leading: const Icon(Icons.show_chart),
                title: const Text('Bear Market MA Period'),
                trailing: Text(
                  '${config.bearMarketMaPeriod} days',
                  style: textTheme.bodyLarge,
                ),
              ),
              ListTile(
                leading: const Icon(Icons.rocket_launch),
                title: const Text('Bear Boost Factor'),
                trailing: Text(
                  '${config.bearBoostFactor}x',
                  style: textTheme.bodyLarge,
                ),
              ),
              ListTile(
                leading: const Icon(Icons.speed),
                title: const Text('Max Multiplier Cap'),
                trailing: Text(
                  '${config.maxMultiplierCap}x',
                  style: textTheme.bodyLarge,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),

        // Multiplier Tiers card
        Card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Text('Multiplier Tiers', style: textTheme.titleMedium),
              ),
              if (config.tiers.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text(
                    'No multiplier tiers (base-only DCA)',
                    style: TextStyle(color: Colors.grey),
                  ),
                )
              else
                ...config.tiers.map(
                  (tier) => ListTile(
                    leading: const Icon(Icons.layers),
                    title: Text(
                      'Drop >= ${tier.dropPercentage}%',
                    ),
                    trailing: Text(
                      '${tier.multiplier}x multiplier',
                      style: textTheme.bodyLarge?.copyWith(
                        color: AppTheme.bitcoinOrange,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

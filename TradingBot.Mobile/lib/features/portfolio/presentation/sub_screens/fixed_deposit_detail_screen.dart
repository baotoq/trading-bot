import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/currency_provider.dart';
import '../../data/models/fixed_deposit_response.dart';
import '../../data/portfolio_providers.dart';

class FixedDepositDetailScreen extends ConsumerWidget {
  const FixedDepositDetailScreen({required this.id, super.key});

  final String id;

  static final _vndFormatter = NumberFormat.currency(
    symbol: '\u20AB',
    decimalDigits: 0,
    locale: 'vi_VN',
  );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pageData = ref.watch(portfolioPageDataProvider);
    final isVnd = ref.watch(currencyPreferenceProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Fixed Deposit')),
      body: switch (pageData) {
        AsyncData(:final value) => _buildBody(context, value, isVnd),
        AsyncError() => const Center(child: Text('Could not load data')),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }

  Widget _buildBody(
      BuildContext context, PortfolioPageData data, bool isVnd) {
    final fd = data.fixedDeposits.cast<FixedDepositResponse?>().firstWhere(
          (f) => f!.id == id,
          orElse: () => null,
        );

    if (fd == null) {
      return const Center(
        child: Text('Fixed deposit not found',
            style: TextStyle(color: Colors.white54)),
      );
    }

    // Calculate progress
    final startDate = DateTime.parse(fd.startDate);
    final maturityDate = DateTime.parse(fd.maturityDate);
    final totalDays = maturityDate.difference(startDate).inDays;
    final elapsed = totalDays - fd.daysToMaturity;
    final progress = totalDays > 0 ? (elapsed / totalDays).clamp(0.0, 1.0) : 1.0;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Bank name + status
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        fd.bankName,
                        style:
                            Theme.of(context).textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                      ),
                    ),
                    _statusBadge(fd.status),
                  ],
                ),
                const SizedBox(height: 20),

                // Principal
                _detailRow(
                  context,
                  'Principal',
                  _vndFormatter.format(fd.principalVnd),
                ),
                const SizedBox(height: 12),

                // Interest rate
                _detailRow(
                  context,
                  'Annual Interest Rate',
                  '${(fd.annualInterestRate * 100).toStringAsFixed(2)}%',
                ),
                const SizedBox(height: 12),

                // Dates
                _detailRow(context, 'Start Date', fd.startDate),
                const SizedBox(height: 12),
                _detailRow(context, 'Maturity Date', fd.maturityDate),
                const SizedBox(height: 12),

                // Compounding
                _detailRow(
                  context,
                  'Compounding',
                  fd.compoundingFrequency == 'None'
                      ? 'Simple Interest'
                      : fd.compoundingFrequency,
                ),
                const SizedBox(height: 12),

                // Days to maturity
                _detailRow(
                  context,
                  'Days to Maturity',
                  fd.daysToMaturity == 0
                      ? 'Matured'
                      : '${fd.daysToMaturity} days',
                ),
                const SizedBox(height: 16),

                // Progress bar
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'Progress',
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                color: Colors.white54,
                              ),
                        ),
                        Text(
                          '${(progress * 100).toStringAsFixed(1)}%',
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                color: Colors.white54,
                              ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value: progress,
                        backgroundColor: Colors.white10,
                        color: AppTheme.profitGreen,
                        minHeight: 8,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),

                // Accrued value
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppTheme.profitGreen.withAlpha(20),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Accrued Value',
                        style: TextStyle(fontWeight: FontWeight.w500),
                      ),
                      Text(
                        _vndFormatter.format(fd.accruedValueVnd),
                        style: TextStyle(
                          color: AppTheme.profitGreen,
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),

                // Projected maturity value
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.white10,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Projected at Maturity',
                        style: TextStyle(fontWeight: FontWeight.w500),
                      ),
                      Text(
                        _vndFormatter.format(fd.projectedMaturityValueVnd),
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _detailRow(BuildContext context, String label, String value) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: Theme.of(context)
              .textTheme
              .bodySmall
              ?.copyWith(color: Colors.white54),
        ),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w500)),
      ],
    );
  }

  Widget _statusBadge(String status) {
    final isActive = status == 'Active';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: isActive
            ? AppTheme.profitGreen.withAlpha(40)
            : Colors.white10,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        status,
        style: TextStyle(
          color: isActive ? AppTheme.profitGreen : Colors.white54,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

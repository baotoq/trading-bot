import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/fixed_deposit_response.dart';

class FixedDepositRow extends StatelessWidget {
  const FixedDepositRow({
    required this.fd,
    required this.isVnd,
    super.key,
  });

  final FixedDepositResponse fd;
  final bool isVnd;

  static final _vndFormatter = NumberFormat.currency(
    symbol: '\u20AB',
    decimalDigits: 0,
    locale: 'vi_VN',
  );

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => context.push('/portfolio/fixed-deposit/${fd.id}'),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
        child: Row(
          children: [
            // Leading: bank icon
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: Colors.white10,
                borderRadius: BorderRadius.circular(8),
              ),
              alignment: Alignment.center,
              child: const Icon(Icons.account_balance, size: 18),
            ),
            const SizedBox(width: 12),
            // Title + subtitle
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    fd.bankName,
                    style: const TextStyle(
                      fontWeight: FontWeight.w500,
                      fontSize: 14,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Principal: ${_vndFormatter.format(fd.principalVnd)}',
                    style: const TextStyle(
                      color: Colors.white54,
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
            // Trailing: accrued value + days
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  _vndFormatter.format(fd.accruedValueVnd),
                  style: TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                    color: AppTheme.profitGreen,
                  ),
                ),
                const SizedBox(height: 2),
                if (fd.daysToMaturity == 0)
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppTheme.profitGreen.withAlpha(40),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      'Matured',
                      style: TextStyle(
                        color: AppTheme.profitGreen,
                        fontSize: 10,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  )
                else
                  Text(
                    '${fd.daysToMaturity} days left',
                    style: const TextStyle(
                      color: Colors.white54,
                      fontSize: 12,
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../data/models/fixed_deposit_response.dart';
import '../../data/models/portfolio_asset_response.dart';
import 'asset_row.dart';
import 'fixed_deposit_row.dart';

class AssetTypeSection extends StatelessWidget {
  const AssetTypeSection({
    required this.title,
    required this.isVnd,
    required this.subtotalUsd,
    required this.subtotalVnd,
    this.assets = const [],
    this.fixedDeposits = const [],
    super.key,
  });

  final String title;
  final bool isVnd;
  final double subtotalUsd;
  final double subtotalVnd;
  final List<PortfolioAssetResponse> assets;
  final List<FixedDepositResponse> fixedDeposits;

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

  int get _count => assets.length + fixedDeposits.length;

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      initiallyExpanded: true,
      title: Text(title),
      subtitle: Text(
        isVnd
            ? _vndFormatter.format(subtotalVnd)
            : _usdFormatter.format(subtotalUsd),
        style: const TextStyle(color: Colors.white54, fontSize: 13),
      ),
      trailing: Text(
        '$_count ${_count == 1 ? 'asset' : 'assets'}',
        style: const TextStyle(color: Colors.white38, fontSize: 12),
      ),
      children: [
        ...assets.map((a) => AssetRow(asset: a, isVnd: isVnd)),
        ...fixedDeposits.map((fd) => FixedDepositRow(fd: fd, isVnd: isVnd)),
      ],
    );
  }
}

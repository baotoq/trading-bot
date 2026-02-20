import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_asset_response.dart';
import 'staleness_label.dart';

class AssetRow extends StatelessWidget {
  const AssetRow({
    required this.asset,
    required this.isVnd,
    super.key,
  });

  final PortfolioAssetResponse asset;
  final bool isVnd;

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

  String _formatValue(double usd, double vnd) {
    return isVnd ? _vndFormatter.format(vnd) : _usdFormatter.format(usd);
  }

  String _formatPnl(double pnlUsd) {
    final formatted = _usdFormatter.format(pnlUsd.abs());
    return pnlUsd >= 0 ? '+$formatted' : '-$formatted';
  }

  Color _pnlColor(double pnl) {
    if (pnl > 0) return AppTheme.profitGreen;
    if (pnl < 0) return AppTheme.lossRed;
    return Colors.white54;
  }

  bool get _isCrossCurrency {
    if (isVnd && asset.nativeCurrency == 'USD') return true;
    if (!isVnd && asset.nativeCurrency == 'VND') return true;
    return false;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: Row(
        children: [
          // Leading: ticker badge
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: Colors.white10,
              borderRadius: BorderRadius.circular(8),
            ),
            alignment: Alignment.center,
            child: Text(
              asset.ticker,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 11,
              ),
            ),
          ),
          const SizedBox(width: 12),
          // Title + subtitle
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  asset.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w500,
                    fontSize: 14,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${asset.quantity.toStringAsFixed(asset.assetType == 'ETF' ? 0 : 4)} @ ${_usdFormatter.format(asset.averageCost)}',
                  style: const TextStyle(
                    color: Colors.white54,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          // Trailing: value + P&L
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                _formatValue(asset.currentValueUsd, asset.currentValueVnd),
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 14,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                _formatPnl(asset.unrealizedPnlUsd),
                style: TextStyle(
                  color: _pnlColor(asset.unrealizedPnlUsd),
                  fontWeight: FontWeight.w500,
                  fontSize: 12,
                ),
              ),
              if (asset.unrealizedPnlPercent != null)
                Text(
                  '${asset.unrealizedPnlPercent! >= 0 ? '+' : ''}${asset.unrealizedPnlPercent!.toStringAsFixed(2)}%',
                  style: TextStyle(
                    color: _pnlColor(asset.unrealizedPnlUsd),
                    fontSize: 11,
                  ),
                ),
              if (asset.isPriceStale)
                StalenessLabel(
                  priceUpdatedAt: asset.priceUpdatedAt,
                  isPriceStale: asset.isPriceStale,
                ),
              if (_isCrossCurrency) StalenessLabel.crossCurrencyLabel(),
            ],
          ),
        ],
      ),
    );
  }
}

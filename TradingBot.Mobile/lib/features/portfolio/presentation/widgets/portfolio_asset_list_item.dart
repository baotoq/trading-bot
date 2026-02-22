import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/glass_card.dart';
import '../../../../core/widgets/pressable_scale.dart';
import '../../data/models/portfolio_asset_response.dart';

/// Glass-styled asset row for the portfolio screen's flat asset list.
///
/// Uses [GlassVariant.scrollItem] (non-blur tint+border surface) to avoid
/// Impeller frame drops caused by BackdropFilter repainting on every scroll
/// offset change. Visual consistency with the hero header is preserved via
/// the same GlassTheme tokens.
///
/// Layout: PressableScale → GlassCard(scrollItem) → Row(badge | name+price | value+qty)
class PortfolioAssetListItem extends StatelessWidget {
  const PortfolioAssetListItem({
    super.key,
    required this.asset,
    required this.isVnd,
    this.onTap,
  });

  final PortfolioAssetResponse asset;
  final bool isVnd;

  /// Optional tap callback — reserved for future navigation to asset detail.
  final VoidCallback? onTap;

  // --- Formatters ----------------------------------------------------------

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

  // --- Color palette for ticker badges -------------------------------------

  /// Deterministic color palette — six distinct hues so adjacent assets have
  /// different colors without any runtime randomness.
  static const List<Color> _palette = [
    AppTheme.bitcoinOrange, // 0
    AppTheme.profitGreen, // 1
    Color(0xFF6366F1), // indigo — 2
    Color(0xFFEC4899), // pink — 3
    Color(0xFF14B8A6), // teal — 4
    Color(0xFF8B5CF6), // purple — 5
  ];

  // --- Private helpers -----------------------------------------------------

  /// Returns a deterministic palette color for [ticker].
  static Color _tickerColor(String ticker) {
    // Using .abs() guards against negative hashCode values on some runtimes.
    return _palette[ticker.hashCode.abs() % _palette.length];
  }

  /// Formats [usd] or [vnd] depending on the active currency preference.
  String _formatValue(double usd, double vnd) {
    return isVnd ? _vndFormatter.format(vnd) : _usdFormatter.format(usd);
  }

  /// Returns the display quantity string trimming trailing zeros for crypto.
  ///
  /// ETF: integer (no decimals). Crypto: up to 4 decimal places, trailing
  /// zeros stripped, trailing decimal point stripped.
  static String _formatQuantity(PortfolioAssetResponse asset) {
    if (asset.assetType == 'ETF') {
      return asset.quantity.toStringAsFixed(0);
    }
    return asset.quantity
        .toStringAsFixed(4)
        .replaceAll(RegExp(r'0+$'), '')
        .replaceAll(RegExp(r'\.$'), '');
  }

  /// Returns the semantic P&L color — green / red / neutral.
  static Color _pnlColor(double pnl) {
    if (pnl > 0) return AppTheme.profitGreen;
    if (pnl < 0) return AppTheme.lossRed;
    return Colors.white54;
  }

  // --- Build ---------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final pnl = asset.unrealizedPnlPercent;

    return PressableScale(
      onTap: onTap,
      child: GlassCard(
        variant: GlassVariant.scrollItem,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
        child: Row(
          children: [
            // 1. Leading: colored ticker badge --------------------------------
            CircleAvatar(
              radius: 20,
              backgroundColor: _tickerColor(asset.ticker),
              child: Text(
                asset.ticker.length <= 3
                    ? asset.ticker
                    : asset.ticker.substring(0, 3),
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 12,
                ),
              ),
            ),

            const SizedBox(width: 12),

            // 2. Middle: name + price + P&L percentage -----------------------
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Asset name row with optional staleness indicator
                  Row(
                    children: [
                      Text(
                        asset.name,
                        style: const TextStyle(
                          fontWeight: FontWeight.w600,
                          fontSize: 14,
                        ),
                      ),
                      if (asset.isPriceStale) ...[
                        const SizedBox(width: 4),
                        const Icon(
                          CupertinoIcons.exclamationmark_triangle,
                          size: 12,
                          color: Colors.amber,
                        ),
                      ],
                    ],
                  ),

                  const SizedBox(height: 2),

                  // Price row with P&L percentage badge
                  Row(
                    children: [
                      Text(
                        _usdFormatter.format(asset.currentPrice),
                        style: AppTheme.moneyStyle.copyWith(
                          fontSize: 13,
                          color: Colors.white70,
                        ),
                      ),
                      if (pnl != null) ...[
                        const SizedBox(width: 8),
                        Text(
                          '${pnl >= 0 ? '+' : ''}${pnl.toStringAsFixed(2)}%',
                          style: TextStyle(
                            color: _pnlColor(pnl),
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),

            // 3. Trailing: holding value + quantity --------------------------
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  _formatValue(asset.currentValueUsd, asset.currentValueVnd),
                  style: AppTheme.moneyStyle.copyWith(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${_formatQuantity(asset)} ${asset.ticker}',
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

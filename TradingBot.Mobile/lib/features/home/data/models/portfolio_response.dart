class PortfolioResponse {
  PortfolioResponse({
    required this.totalBtc,
    required this.totalCost,
    required this.averageCostBasis,
    required this.currentPrice,
    required this.unrealizedPnl,
    required this.unrealizedPnlPercent,
    required this.totalPurchaseCount,
    required this.firstPurchaseDate,
    required this.lastPurchaseDate,
  });

  factory PortfolioResponse.fromJson(Map<String, dynamic> json) {
    return PortfolioResponse(
      totalBtc: (json['totalBtc'] as num).toDouble(),
      totalCost: (json['totalCost'] as num).toDouble(),
      averageCostBasis: json['averageCostBasis'] != null
          ? (json['averageCostBasis'] as num).toDouble()
          : null,
      currentPrice: json['currentPrice'] != null
          ? (json['currentPrice'] as num).toDouble()
          : null,
      unrealizedPnl: json['unrealizedPnl'] != null
          ? (json['unrealizedPnl'] as num).toDouble()
          : null,
      unrealizedPnlPercent: json['unrealizedPnlPercent'] != null
          ? (json['unrealizedPnlPercent'] as num).toDouble()
          : null,
      totalPurchaseCount: json['totalPurchaseCount'] as int,
      firstPurchaseDate: json['firstPurchaseDate'] as String?,
      lastPurchaseDate: json['lastPurchaseDate'] as String?,
    );
  }

  /// Total BTC accumulated (Vogen Quantity serializes as a raw number)
  final double totalBtc;

  /// Total USD spent (decimal, not nullable — zero is valid)
  final double totalCost;

  /// Average cost per BTC; null when no purchases exist
  final double? averageCostBasis;

  /// Current BTC/USD price; null when Hyperliquid is unreachable
  final double? currentPrice;

  /// Unrealized profit/loss in USD; null when currentPrice is unavailable
  final double? unrealizedPnl;

  /// Unrealized profit/loss as a percentage; null when currentPrice is unavailable
  final double? unrealizedPnlPercent;

  /// Number of filled purchases
  final int totalPurchaseCount;

  /// ISO 8601 string of the earliest purchase; null when no purchases
  final String? firstPurchaseDate;

  /// ISO 8601 string of the most recent purchase; null when no purchases
  final String? lastPurchaseDate;
}

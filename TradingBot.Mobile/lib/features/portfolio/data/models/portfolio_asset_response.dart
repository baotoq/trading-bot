class PortfolioAssetResponse {
  PortfolioAssetResponse({
    required this.id,
    required this.name,
    required this.ticker,
    required this.assetType,
    required this.nativeCurrency,
    required this.quantity,
    required this.averageCost,
    required this.currentPrice,
    required this.currentValueUsd,
    required this.currentValueVnd,
    required this.unrealizedPnlUsd,
    required this.unrealizedPnlPercent,
    required this.priceUpdatedAt,
    required this.isPriceStale,
  });

  factory PortfolioAssetResponse.fromJson(Map<String, dynamic> json) =>
      PortfolioAssetResponse(
        id: json['id'] as String,
        name: json['name'] as String,
        ticker: json['ticker'] as String,
        assetType: json['assetType'] as String,
        nativeCurrency: json['nativeCurrency'] as String,
        quantity: (json['quantity'] as num).toDouble(),
        averageCost: (json['averageCost'] as num).toDouble(),
        currentPrice: (json['currentPrice'] as num).toDouble(),
        currentValueUsd: (json['currentValueUsd'] as num).toDouble(),
        currentValueVnd: (json['currentValueVnd'] as num).toDouble(),
        unrealizedPnlUsd: (json['unrealizedPnlUsd'] as num).toDouble(),
        unrealizedPnlPercent: json['unrealizedPnlPercent'] != null
            ? (json['unrealizedPnlPercent'] as num).toDouble()
            : null,
        priceUpdatedAt: json['priceUpdatedAt'] != null
            ? DateTime.parse(json['priceUpdatedAt'] as String)
            : null,
        isPriceStale: json['isPriceStale'] as bool,
      );

  final String id;
  final String name;
  final String ticker;
  final String assetType;
  final String nativeCurrency;
  final double quantity;
  final double averageCost;
  final double currentPrice;
  final double currentValueUsd;
  final double currentValueVnd;
  final double unrealizedPnlUsd;
  final double? unrealizedPnlPercent;
  final DateTime? priceUpdatedAt;
  final bool isPriceStale;
}

/// Response from /api/dashboard/chart endpoint.
class ChartResponse {
  ChartResponse({
    required this.prices,
    required this.purchases,
    this.averageCostBasis,
  });

  factory ChartResponse.fromJson(Map<String, dynamic> json) {
    return ChartResponse(
      prices: (json['prices'] as List<dynamic>)
          .map((e) => PricePointDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      purchases: (json['purchases'] as List<dynamic>)
          .map((e) => PurchaseMarkerDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      averageCostBasis: (json['averageCostBasis'] as num?)?.toDouble(),
    );
  }

  /// Daily BTC close prices for the selected timeframe.
  final List<PricePointDto> prices;

  /// Purchases made within the selected timeframe, with tier info for coloring.
  final List<PurchaseMarkerDto> purchases;

  /// Overall average cost basis across all purchases; null when no purchases exist.
  final double? averageCostBasis;
}

/// A single daily price point returned by the chart endpoint.
class PricePointDto {
  PricePointDto({required this.date, required this.price});

  factory PricePointDto.fromJson(Map<String, dynamic> json) {
    return PricePointDto(
      date: json['date'] as String,
      price: (json['price'] as num).toDouble(),
    );
  }

  /// ISO date string in "yyyy-MM-dd" format.
  final String date;

  /// BTC/USD daily close price (Vogen Price serializes as a raw number).
  final double price;
}

/// A purchase marker overlaid on the price chart.
class PurchaseMarkerDto {
  PurchaseMarkerDto({
    required this.date,
    required this.price,
    required this.btcAmount,
    required this.tier,
  });

  factory PurchaseMarkerDto.fromJson(Map<String, dynamic> json) {
    return PurchaseMarkerDto(
      date: json['date'] as String,
      price: (json['price'] as num).toDouble(),
      btcAmount: (json['btcAmount'] as num).toDouble(),
      tier: json['tier'] as String,
    );
  }

  /// ISO date string in "yyyy-MM-dd" format matching the purchase execution date.
  final String date;

  /// Actual purchase execution price (may differ from daily close price).
  final double price;

  /// BTC quantity purchased.
  final double btcAmount;

  /// Multiplier tier label, e.g. "Base", "2x", "3x", "4x".
  final String tier;
}

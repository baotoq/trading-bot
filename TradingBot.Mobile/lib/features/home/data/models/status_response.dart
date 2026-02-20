class StatusResponse {
  StatusResponse({
    required this.healthStatus,
    required this.healthMessage,
    required this.nextBuyTime,
    required this.lastPurchaseTime,
    required this.lastPurchasePrice,
    required this.lastPurchaseBtc,
    required this.lastPurchaseTier,
    required this.lastPurchaseMultiplier,
    required this.lastPurchaseDropPercentage,
  });

  factory StatusResponse.fromJson(Map<String, dynamic> json) {
    return StatusResponse(
      healthStatus: json['healthStatus'] as String,
      healthMessage: json['healthMessage'] as String?,
      nextBuyTime: json['nextBuyTime'] as String?,
      lastPurchaseTime: json['lastPurchaseTime'] as String?,
      lastPurchasePrice: json['lastPurchasePrice'] != null
          ? (json['lastPurchasePrice'] as num).toDouble()
          : null,
      lastPurchaseBtc: json['lastPurchaseBtc'] != null
          ? (json['lastPurchaseBtc'] as num).toDouble()
          : null,
      lastPurchaseTier: json['lastPurchaseTier'] as String?,
      lastPurchaseMultiplier: json['lastPurchaseMultiplier'] != null
          ? (json['lastPurchaseMultiplier'] as num).toDouble()
          : null,
      lastPurchaseDropPercentage: json['lastPurchaseDropPercentage'] != null
          ? (json['lastPurchaseDropPercentage'] as num).toDouble()
          : null,
    );
  }

  /// Bot health state: "Healthy", "Warning", or "Error"
  final String healthStatus;

  /// Human-readable health message (e.g. "Operating normally", "No purchase in 40h")
  final String? healthMessage;

  /// ISO 8601 string for the next scheduled buy time
  final String? nextBuyTime;

  /// ISO 8601 string of the last purchase execution time; null when no purchases
  final String? lastPurchaseTime;

  /// Price (USD) at the time of the last purchase; null when no purchases
  final double? lastPurchasePrice;

  /// BTC amount of the last purchase (Vogen Quantity serializes as a raw number)
  final double? lastPurchaseBtc;

  /// Multiplier tier label of the last purchase (e.g. "Base", "2x", "3x")
  final String? lastPurchaseTier;

  /// Multiplier applied to the last purchase (e.g. 1.0, 2.0, 3.0); null when no purchases
  final double? lastPurchaseMultiplier;

  /// Drop percentage that triggered the last purchase multiplier; null when no purchases
  final double? lastPurchaseDropPercentage;
}

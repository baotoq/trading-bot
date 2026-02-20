class PurchaseHistoryResponse {
  PurchaseHistoryResponse({
    required this.items,
    required this.nextCursor,
    required this.hasMore,
  });

  factory PurchaseHistoryResponse.fromJson(Map<String, dynamic> json) {
    return PurchaseHistoryResponse(
      items: (json['items'] as List)
          .map((e) => PurchaseDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      nextCursor: json['nextCursor'] as String?,
      hasMore: json['hasMore'] as bool,
    );
  }

  final List<PurchaseDto> items;
  final String? nextCursor;
  final bool hasMore;
}

class PurchaseDto {
  PurchaseDto({
    required this.id,
    required this.executedAt,
    required this.price,
    required this.cost,
    required this.quantity,
    required this.multiplierTier,
    required this.multiplier,
    required this.dropPercentage,
  });

  factory PurchaseDto.fromJson(Map<String, dynamic> json) {
    return PurchaseDto(
      id: json['id'] as String,
      executedAt: DateTime.parse(json['executedAt'] as String),
      price: (json['price'] as num).toDouble(),
      cost: (json['cost'] as num).toDouble(),
      quantity: (json['quantity'] as num).toDouble(),
      multiplierTier: json['multiplierTier'] as String,
      multiplier: (json['multiplier'] as num).toDouble(),
      dropPercentage: (json['dropPercentage'] as num).toDouble(),
    );
  }

  final String id;
  final DateTime executedAt;
  final double price;
  final double cost;
  final double quantity;
  final String multiplierTier;
  final double multiplier;
  final double dropPercentage;
}

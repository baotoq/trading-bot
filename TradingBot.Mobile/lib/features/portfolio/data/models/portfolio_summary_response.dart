class AllocationDto {
  AllocationDto({
    required this.assetType,
    required this.valueUsd,
    required this.percentage,
  });

  factory AllocationDto.fromJson(Map<String, dynamic> json) => AllocationDto(
        assetType: json['assetType'] as String,
        valueUsd: (json['valueUsd'] as num).toDouble(),
        percentage: (json['percentage'] as num).toDouble(),
      );

  final String assetType;
  final double valueUsd;
  final double percentage;
}

class PortfolioSummaryResponse {
  PortfolioSummaryResponse({
    required this.totalValueUsd,
    required this.totalValueVnd,
    required this.totalCostUsd,
    required this.totalCostVnd,
    required this.unrealizedPnlUsd,
    required this.unrealizedPnlVnd,
    required this.unrealizedPnlPercent,
    required this.allocations,
    required this.exchangeRateUpdatedAt,
  });

  factory PortfolioSummaryResponse.fromJson(Map<String, dynamic> json) =>
      PortfolioSummaryResponse(
        totalValueUsd: (json['totalValueUsd'] as num).toDouble(),
        totalValueVnd: (json['totalValueVnd'] as num).toDouble(),
        totalCostUsd: (json['totalCostUsd'] as num).toDouble(),
        totalCostVnd: (json['totalCostVnd'] as num).toDouble(),
        unrealizedPnlUsd: (json['unrealizedPnlUsd'] as num).toDouble(),
        unrealizedPnlVnd: (json['unrealizedPnlVnd'] as num).toDouble(),
        unrealizedPnlPercent: json['unrealizedPnlPercent'] != null
            ? (json['unrealizedPnlPercent'] as num).toDouble()
            : null,
        allocations: (json['allocations'] as List)
            .map((e) => AllocationDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        exchangeRateUpdatedAt: json['exchangeRateUpdatedAt'] != null
            ? DateTime.parse(json['exchangeRateUpdatedAt'] as String)
            : null,
      );

  final double totalValueUsd;
  final double totalValueVnd;
  final double totalCostUsd;
  final double totalCostVnd;
  final double unrealizedPnlUsd;
  final double unrealizedPnlVnd;
  final double? unrealizedPnlPercent;
  final List<AllocationDto> allocations;
  final DateTime? exchangeRateUpdatedAt;
}

class MultiplierTierDto {
  MultiplierTierDto({
    required this.dropPercentage,
    required this.multiplier,
  });

  factory MultiplierTierDto.fromJson(Map<String, dynamic> json) {
    return MultiplierTierDto(
      dropPercentage: (json['dropPercentage'] as num).toDouble(),
      multiplier: (json['multiplier'] as num).toDouble(),
    );
  }

  double dropPercentage;
  double multiplier;

  Map<String, dynamic> toJson() => {
        'dropPercentage': dropPercentage,
        'multiplier': multiplier,
      };

  MultiplierTierDto copyWith({
    double? dropPercentage,
    double? multiplier,
  }) {
    return MultiplierTierDto(
      dropPercentage: dropPercentage ?? this.dropPercentage,
      multiplier: multiplier ?? this.multiplier,
    );
  }
}

class ConfigResponse {
  ConfigResponse({
    required this.baseDailyAmount,
    required this.dailyBuyHour,
    required this.dailyBuyMinute,
    required this.highLookbackDays,
    required this.dryRun,
    required this.bearMarketMaPeriod,
    required this.bearBoostFactor,
    required this.maxMultiplierCap,
    required this.tiers,
  });

  factory ConfigResponse.fromJson(Map<String, dynamic> json) {
    return ConfigResponse(
      baseDailyAmount: (json['baseDailyAmount'] as num).toDouble(),
      dailyBuyHour: json['dailyBuyHour'] as int,
      dailyBuyMinute: json['dailyBuyMinute'] as int,
      highLookbackDays: json['highLookbackDays'] as int,
      dryRun: json['dryRun'] as bool,
      bearMarketMaPeriod: json['bearMarketMaPeriod'] as int,
      bearBoostFactor: (json['bearBoostFactor'] as num).toDouble(),
      maxMultiplierCap: (json['maxMultiplierCap'] as num).toDouble(),
      tiers: (json['tiers'] as List)
          .map((e) => MultiplierTierDto.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  final double baseDailyAmount;
  final int dailyBuyHour;
  final int dailyBuyMinute;
  final int highLookbackDays;
  final bool dryRun;
  final int bearMarketMaPeriod;
  final double bearBoostFactor;
  final double maxMultiplierCap;
  final List<MultiplierTierDto> tiers;

  Map<String, dynamic> toJson() => {
        'baseDailyAmount': baseDailyAmount,
        'dailyBuyHour': dailyBuyHour,
        'dailyBuyMinute': dailyBuyMinute,
        'highLookbackDays': highLookbackDays,
        'dryRun': dryRun,
        'bearMarketMaPeriod': bearMarketMaPeriod,
        'bearBoostFactor': bearBoostFactor,
        'maxMultiplierCap': maxMultiplierCap,
        'tiers': tiers.map((t) => t.toJson()).toList(),
      };

  ConfigResponse copyWith({
    double? baseDailyAmount,
    int? dailyBuyHour,
    int? dailyBuyMinute,
    int? highLookbackDays,
    bool? dryRun,
    int? bearMarketMaPeriod,
    double? bearBoostFactor,
    double? maxMultiplierCap,
    List<MultiplierTierDto>? tiers,
  }) {
    return ConfigResponse(
      baseDailyAmount: baseDailyAmount ?? this.baseDailyAmount,
      dailyBuyHour: dailyBuyHour ?? this.dailyBuyHour,
      dailyBuyMinute: dailyBuyMinute ?? this.dailyBuyMinute,
      highLookbackDays: highLookbackDays ?? this.highLookbackDays,
      dryRun: dryRun ?? this.dryRun,
      bearMarketMaPeriod: bearMarketMaPeriod ?? this.bearMarketMaPeriod,
      bearBoostFactor: bearBoostFactor ?? this.bearBoostFactor,
      maxMultiplierCap: maxMultiplierCap ?? this.maxMultiplierCap,
      tiers: tiers ?? this.tiers,
    );
  }
}

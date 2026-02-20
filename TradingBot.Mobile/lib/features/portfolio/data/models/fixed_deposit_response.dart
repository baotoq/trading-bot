class FixedDepositResponse {
  FixedDepositResponse({
    required this.id,
    required this.bankName,
    required this.principalVnd,
    required this.annualInterestRate,
    required this.startDate,
    required this.maturityDate,
    required this.compoundingFrequency,
    required this.status,
    required this.accruedValueVnd,
    required this.projectedMaturityValueVnd,
    required this.daysToMaturity,
    required this.createdAt,
  });

  factory FixedDepositResponse.fromJson(Map<String, dynamic> json) =>
      FixedDepositResponse(
        id: json['id'] as String,
        bankName: json['bankName'] as String,
        principalVnd: (json['principalVnd'] as num).toDouble(),
        annualInterestRate: (json['annualInterestRate'] as num).toDouble(),
        startDate: json['startDate'] as String,
        maturityDate: json['maturityDate'] as String,
        compoundingFrequency: json['compoundingFrequency'] as String,
        status: json['status'] as String,
        accruedValueVnd: (json['accruedValueVnd'] as num).toDouble(),
        projectedMaturityValueVnd:
            (json['projectedMaturityValueVnd'] as num).toDouble(),
        daysToMaturity: json['daysToMaturity'] as int,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  final String id;
  final String bankName;
  final double principalVnd;
  final double annualInterestRate;
  final String startDate;
  final String maturityDate;
  final String compoundingFrequency;
  final String status;
  final double accruedValueVnd;
  final double projectedMaturityValueVnd;
  final int daysToMaturity;
  final DateTime createdAt;
}

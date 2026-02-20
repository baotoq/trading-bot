class TransactionResponse {
  TransactionResponse({
    required this.id,
    required this.date,
    required this.quantity,
    required this.pricePerUnit,
    required this.currency,
    required this.type,
    required this.fee,
    required this.source,
  });

  factory TransactionResponse.fromJson(Map<String, dynamic> json) =>
      TransactionResponse(
        id: json['id'] as String,
        date: json['date'] as String,
        quantity: (json['quantity'] as num).toDouble(),
        pricePerUnit: (json['pricePerUnit'] as num).toDouble(),
        currency: json['currency'] as String,
        type: json['type'] as String,
        fee: json['fee'] != null ? (json['fee'] as num).toDouble() : null,
        source: json['source'] as String,
      );

  final String id;
  final String date;
  final double quantity;
  final double pricePerUnit;
  final String currency;
  final String type;
  final double? fee;
  final String source;
}

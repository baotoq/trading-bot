import 'package:dio/dio.dart';
import 'package:intl/intl.dart';

import 'models/purchase_history_response.dart';

class HistoryRepository {
  HistoryRepository(this._dio);

  final Dio _dio;

  Future<PurchaseHistoryResponse> fetchPurchases({
    String? cursor,
    DateTime? startDate,
    DateTime? endDate,
    String? tier,
    int pageSize = 20,
  }) async {
    final params = <String, dynamic>{'pageSize': pageSize};

    if (cursor != null) params['cursor'] = cursor;
    if (startDate != null) {
      params['startDate'] = DateFormat('yyyy-MM-dd').format(startDate);
    }
    if (endDate != null) {
      params['endDate'] = DateFormat('yyyy-MM-dd').format(endDate);
    }
    if (tier != null && tier.isNotEmpty) params['tier'] = tier;

    final response = await _dio.get(
      '/api/dashboard/purchases',
      queryParameters: params,
    );

    return PurchaseHistoryResponse.fromJson(
      response.data as Map<String, dynamic>,
    );
  }
}

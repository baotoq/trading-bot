import 'package:dio/dio.dart';

import 'models/chart_response.dart';

class ChartRepository {
  ChartRepository(this._dio);

  final Dio _dio;

  /// Fetches price chart data for the given [timeframe].
  ///
  /// [timeframe] must be one of: '7D', '1M', '3M', '6M', '1Y', 'All'.
  Future<ChartResponse> fetchChart(String timeframe) async {
    final response = await _dio.get(
      '/api/dashboard/chart',
      queryParameters: {'timeframe': timeframe},
    );
    return ChartResponse.fromJson(response.data as Map<String, dynamic>);
  }
}

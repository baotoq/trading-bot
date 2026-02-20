import 'package:dio/dio.dart';

import 'models/portfolio_response.dart';
import 'models/status_response.dart';

class HomeRepository {
  HomeRepository(this._dio);

  final Dio _dio;

  Future<PortfolioResponse> fetchPortfolio() async {
    final response = await _dio.get('/api/dashboard/portfolio');
    return PortfolioResponse.fromJson(response.data as Map<String, dynamic>);
  }

  Future<StatusResponse> fetchStatus() async {
    final response = await _dio.get('/api/dashboard/status');
    return StatusResponse.fromJson(response.data as Map<String, dynamic>);
  }
}

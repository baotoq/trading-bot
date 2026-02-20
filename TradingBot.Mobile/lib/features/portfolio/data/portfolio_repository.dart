import 'package:dio/dio.dart';

import 'models/fixed_deposit_response.dart';
import 'models/portfolio_asset_response.dart';
import 'models/portfolio_summary_response.dart';
import 'models/transaction_response.dart';

class PortfolioRepository {
  PortfolioRepository(this._dio);

  final Dio _dio;

  Future<PortfolioSummaryResponse> fetchSummary() async {
    final response = await _dio.get('/api/portfolio/summary');
    return PortfolioSummaryResponse.fromJson(
        response.data as Map<String, dynamic>);
  }

  Future<List<PortfolioAssetResponse>> fetchAssets() async {
    final response = await _dio.get('/api/portfolio/assets');
    return (response.data as List)
        .map((e) =>
            PortfolioAssetResponse.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<FixedDepositResponse>> fetchFixedDeposits() async {
    final response = await _dio.get('/api/portfolio/fixed-deposits/');
    return (response.data as List)
        .map((e) =>
            FixedDepositResponse.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<TransactionResponse>> fetchTransactions(
    String assetId, {
    String? type,
    String? startDate,
    String? endDate,
  }) async {
    final queryParams = <String, dynamic>{};
    if (type != null) queryParams['type'] = type;
    if (startDate != null) queryParams['startDate'] = startDate;
    if (endDate != null) queryParams['endDate'] = endDate;
    final response = await _dio.get(
      '/api/portfolio/assets/$assetId/transactions',
      queryParameters: queryParams,
    );
    return (response.data as List)
        .map((e) =>
            TransactionResponse.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<TransactionResponse> createTransaction(
    String assetId,
    Map<String, dynamic> body,
  ) async {
    final response = await _dio.post(
      '/api/portfolio/assets/$assetId/transactions',
      data: body,
    );
    return TransactionResponse.fromJson(
        response.data as Map<String, dynamic>);
  }

  Future<FixedDepositResponse> createFixedDeposit(
    Map<String, dynamic> body,
  ) async {
    final response = await _dio.post(
      '/api/portfolio/fixed-deposits/',
      data: body,
    );
    return FixedDepositResponse.fromJson(
        response.data as Map<String, dynamic>);
  }

  Future<Map<String, dynamic>> createAsset(Map<String, dynamic> body) async {
    final response = await _dio.post('/api/portfolio/assets', data: body);
    return response.data as Map<String, dynamic>;
  }
}

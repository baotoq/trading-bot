import 'package:dio/dio.dart';

import 'models/config_response.dart';

class ConfigRepository {
  ConfigRepository(this._dio);

  final Dio _dio;

  Future<ConfigResponse> fetchConfig() async {
    final response = await _dio.get('/api/config');
    return ConfigResponse.fromJson(response.data as Map<String, dynamic>);
  }

  /// Sends updated config to the server. Does NOT catch DioException so the
  /// caller can inspect response.data for RFC 7807 Problem Details with
  /// validation errors on 400 responses.
  Future<void> updateConfig(ConfigResponse config) async {
    await _dio.put('/api/config', data: config.toJson());
  }
}

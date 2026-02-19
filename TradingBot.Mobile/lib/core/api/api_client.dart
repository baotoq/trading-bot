import 'package:dio/dio.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import 'api_exception.dart';
import 'config.dart';

part 'api_client.g.dart';

/// Dio interceptor that injects the x-api-key header on every request
/// and wraps error responses into typed [ApiException] subclasses.
class ApiKeyInterceptor extends Interceptor {
  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    options.headers['x-api-key'] = kApiKey;
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    final statusCode = err.response?.statusCode;

    if (statusCode == 401 || statusCode == 403) {
      handler.next(
        DioException(
          requestOptions: err.requestOptions,
          response: err.response,
          type: err.type,
          error: const AuthenticationException(),
        ),
      );
    } else if (statusCode != null && statusCode >= 500) {
      handler.next(
        DioException(
          requestOptions: err.requestOptions,
          response: err.response,
          type: err.type,
          error: ServerException(statusCode),
        ),
      );
    } else if (err.type == DioExceptionType.connectionTimeout ||
        err.type == DioExceptionType.connectionError ||
        err.type == DioExceptionType.unknown) {
      handler.next(
        DioException(
          requestOptions: err.requestOptions,
          response: err.response,
          type: err.type,
          error: NetworkException(err.message ?? 'Network error'),
        ),
      );
    } else {
      handler.next(err);
    }
  }
}

/// Creates a configured [Dio] instance with [ApiKeyInterceptor].
Dio createDio() {
  final dio = Dio(
    BaseOptions(
      baseUrl: kApiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 30),
    ),
  );
  dio.interceptors.add(ApiKeyInterceptor());
  return dio;
}

@riverpod
Dio dio(Ref ref) {
  return createDio();
}

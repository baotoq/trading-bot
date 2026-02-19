/// Typed exception hierarchy for API errors.
sealed class ApiException implements Exception {
  const ApiException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Thrown when the server returns 401 or 403.
class AuthenticationException extends ApiException {
  const AuthenticationException([super.message = 'Authentication failed']);
}

/// Thrown when the request cannot reach the server (timeout, no connectivity).
class NetworkException extends ApiException {
  const NetworkException([super.message = 'Network error']);
}

/// Thrown when the server returns 5xx status codes.
class ServerException extends ApiException {
  const ServerException(this.statusCode, [String? message])
      : super(message ?? 'Server error ($statusCode)');

  final int statusCode;
}

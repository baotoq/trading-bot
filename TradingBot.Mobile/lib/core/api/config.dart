/// Build-time configuration injected via --dart-define.
/// Usage: flutter run --dart-define=API_BASE_URL=http://192.168.1.100:5000 --dart-define=API_KEY=your-key
const String kApiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://apiservice-tradingbot.dev.localhost:5459',
);

const String kApiKey = String.fromEnvironment(
  'API_KEY',
  defaultValue: '',
);

import 'package:riverpod_annotation/riverpod_annotation.dart';
import 'package:shared_preferences/shared_preferences.dart';

part 'currency_provider.g.dart';

@Riverpod(keepAlive: true)
SharedPreferences sharedPreferences(Ref ref) {
  throw UnimplementedError('Must be overridden in ProviderScope');
}

@riverpod
class CurrencyPreference extends _$CurrencyPreference {
  static const _key = 'currency_vnd';

  @override
  bool build() {
    return ref.watch(sharedPreferencesProvider).getBool(_key) ?? true;
  }

  void toggle() {
    final next = !state;
    ref.read(sharedPreferencesProvider).setBool(_key, next);
    state = next;
  }
}

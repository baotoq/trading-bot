import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_client.dart';
import 'config_repository.dart';
import 'models/config_response.dart';

part 'config_providers.g.dart';

@riverpod
ConfigRepository configRepository(Ref ref) {
  return ConfigRepository(ref.watch(dioProvider));
}

@riverpod
Future<ConfigResponse> configData(Ref ref) async {
  final repo = ref.watch(configRepositoryProvider);
  return repo.fetchConfig();
}

import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_client.dart';
import 'chart_repository.dart';
import 'models/chart_response.dart';

part 'chart_providers.g.dart';

@riverpod
ChartRepository chartRepository(Ref ref) {
  return ChartRepository(ref.watch(dioProvider));
}

@riverpod
Future<ChartResponse> chartData(Ref ref, {String timeframe = '1M'}) {
  return ref.watch(chartRepositoryProvider).fetchChart(timeframe);
}

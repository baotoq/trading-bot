import 'dart:async';

import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_client.dart';
import 'home_repository.dart';
import 'models/portfolio_response.dart';
import 'models/status_response.dart';

part 'home_providers.g.dart';

/// Combined model holding both portfolio and status data for the home screen.
class HomeData {
  HomeData({required this.portfolio, required this.status});

  final PortfolioResponse portfolio;
  final StatusResponse status;
}

@riverpod
HomeRepository homeRepository(Ref ref) {
  return HomeRepository(ref.watch(dioProvider));
}

@riverpod
Future<HomeData> homeData(Ref ref) async {
  final repo = ref.watch(homeRepositoryProvider);

  // Fetch portfolio and status in parallel for efficiency
  final results = await Future.wait([
    repo.fetchPortfolio(),
    repo.fetchStatus(),
  ]);

  // Auto-refresh every 30 seconds (silent — no animation per CONTEXT.md)
  final timer = Timer(const Duration(seconds: 30), () {
    ref.invalidateSelf();
  });
  ref.onDispose(timer.cancel);

  return HomeData(
    portfolio: results[0] as PortfolioResponse,
    status: results[1] as StatusResponse,
  );
}

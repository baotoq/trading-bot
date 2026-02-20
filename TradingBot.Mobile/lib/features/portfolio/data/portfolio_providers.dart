import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/api/api_client.dart';
import 'models/fixed_deposit_response.dart';
import 'models/portfolio_asset_response.dart';
import 'models/portfolio_summary_response.dart';
import 'portfolio_repository.dart';

part 'portfolio_providers.g.dart';

class PortfolioPageData {
  PortfolioPageData({
    required this.summary,
    required this.assets,
    required this.fixedDeposits,
  });

  final PortfolioSummaryResponse summary;
  final List<PortfolioAssetResponse> assets;
  final List<FixedDepositResponse> fixedDeposits;
}

@riverpod
PortfolioRepository portfolioRepository(Ref ref) {
  return PortfolioRepository(ref.watch(dioProvider));
}

@riverpod
Future<PortfolioPageData> portfolioPageData(Ref ref) async {
  final repo = ref.watch(portfolioRepositoryProvider);
  final results = await Future.wait([
    repo.fetchSummary(),
    repo.fetchAssets(),
    repo.fetchFixedDeposits(),
  ]);
  return PortfolioPageData(
    summary: results[0] as PortfolioSummaryResponse,
    assets: results[1] as List<PortfolioAssetResponse>,
    fixedDeposits: results[2] as List<FixedDepositResponse>,
  );
}

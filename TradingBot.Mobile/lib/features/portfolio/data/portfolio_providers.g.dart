// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'portfolio_providers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(portfolioRepository)
final portfolioRepositoryProvider = PortfolioRepositoryProvider._();

final class PortfolioRepositoryProvider
    extends
        $FunctionalProvider<
          PortfolioRepository,
          PortfolioRepository,
          PortfolioRepository
        >
    with $Provider<PortfolioRepository> {
  PortfolioRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'portfolioRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$portfolioRepositoryHash();

  @$internal
  @override
  $ProviderElement<PortfolioRepository> $createElement(
    $ProviderPointer pointer,
  ) => $ProviderElement(pointer);

  @override
  PortfolioRepository create(Ref ref) {
    return portfolioRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(PortfolioRepository value) {
    return $ProviderOverride(
      origin: this,
      providerOverride: $SyncValueProvider<PortfolioRepository>(value),
    );
  }
}

String _$portfolioRepositoryHash() =>
    r'7647fd8ace694d9754b74715f24dc5dd2ae13c88';

@ProviderFor(portfolioPageData)
final portfolioPageDataProvider = PortfolioPageDataProvider._();

final class PortfolioPageDataProvider
    extends
        $FunctionalProvider<
          AsyncValue<PortfolioPageData>,
          PortfolioPageData,
          FutureOr<PortfolioPageData>
        >
    with
        $FutureModifier<PortfolioPageData>,
        $FutureProvider<PortfolioPageData> {
  PortfolioPageDataProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'portfolioPageDataProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$portfolioPageDataHash();

  @$internal
  @override
  $FutureProviderElement<PortfolioPageData> $createElement(
    $ProviderPointer pointer,
  ) => $FutureProviderElement(pointer);

  @override
  FutureOr<PortfolioPageData> create(Ref ref) {
    return portfolioPageData(ref);
  }
}

String _$portfolioPageDataHash() => r'1b916f6d5cd0818b73753dd09db77ca4214c8588';

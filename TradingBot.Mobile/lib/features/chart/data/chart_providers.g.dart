// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'chart_providers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(chartRepository)
final chartRepositoryProvider = ChartRepositoryProvider._();

final class ChartRepositoryProvider
    extends
        $FunctionalProvider<ChartRepository, ChartRepository, ChartRepository>
    with $Provider<ChartRepository> {
  ChartRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'chartRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$chartRepositoryHash();

  @$internal
  @override
  $ProviderElement<ChartRepository> $createElement($ProviderPointer pointer) =>
      $ProviderElement(pointer);

  @override
  ChartRepository create(Ref ref) {
    return chartRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(ChartRepository value) {
    return $ProviderOverride(
      origin: this,
      providerOverride: $SyncValueProvider<ChartRepository>(value),
    );
  }
}

String _$chartRepositoryHash() => r'87e50a930bc8aa2d5fbe94037f8707dfe2c55da3';

@ProviderFor(chartData)
final chartDataProvider = ChartDataFamily._();

final class ChartDataProvider
    extends
        $FunctionalProvider<
          AsyncValue<ChartResponse>,
          ChartResponse,
          FutureOr<ChartResponse>
        >
    with $FutureModifier<ChartResponse>, $FutureProvider<ChartResponse> {
  ChartDataProvider._({
    required ChartDataFamily super.from,
    required String super.argument,
  }) : super(
         retry: null,
         name: r'chartDataProvider',
         isAutoDispose: true,
         dependencies: null,
         $allTransitiveDependencies: null,
       );

  @override
  String debugGetCreateSourceHash() => _$chartDataHash();

  @override
  String toString() {
    return r'chartDataProvider'
        ''
        '($argument)';
  }

  @$internal
  @override
  $FutureProviderElement<ChartResponse> $createElement(
    $ProviderPointer pointer,
  ) => $FutureProviderElement(pointer);

  @override
  FutureOr<ChartResponse> create(Ref ref) {
    final argument = this.argument as String;
    return chartData(ref, timeframe: argument);
  }

  @override
  bool operator ==(Object other) {
    return other is ChartDataProvider && other.argument == argument;
  }

  @override
  int get hashCode {
    return argument.hashCode;
  }
}

String _$chartDataHash() => r'2fef48b70dc768ecbb8ae3c6483de4afd38675d6';

final class ChartDataFamily extends $Family
    with $FunctionalFamilyOverride<FutureOr<ChartResponse>, String> {
  ChartDataFamily._()
    : super(
        retry: null,
        name: r'chartDataProvider',
        dependencies: null,
        $allTransitiveDependencies: null,
        isAutoDispose: true,
      );

  ChartDataProvider call({String timeframe = '1M'}) =>
      ChartDataProvider._(argument: timeframe, from: this);

  @override
  String toString() => r'chartDataProvider';
}

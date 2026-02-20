// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'history_providers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(historyRepository)
final historyRepositoryProvider = HistoryRepositoryProvider._();

final class HistoryRepositoryProvider
    extends
        $FunctionalProvider<
          HistoryRepository,
          HistoryRepository,
          HistoryRepository
        >
    with $Provider<HistoryRepository> {
  HistoryRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'historyRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$historyRepositoryHash();

  @$internal
  @override
  $ProviderElement<HistoryRepository> $createElement(
    $ProviderPointer pointer,
  ) => $ProviderElement(pointer);

  @override
  HistoryRepository create(Ref ref) {
    return historyRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(HistoryRepository value) {
    return $ProviderOverride(
      origin: this,
      providerOverride: $SyncValueProvider<HistoryRepository>(value),
    );
  }
}

String _$historyRepositoryHash() => r'3407572700ee4d375a04e219992a0cabc3111161';

@ProviderFor(PurchaseHistory)
final purchaseHistoryProvider = PurchaseHistoryProvider._();

final class PurchaseHistoryProvider
    extends $AsyncNotifierProvider<PurchaseHistory, List<PurchaseDto>> {
  PurchaseHistoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'purchaseHistoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$purchaseHistoryHash();

  @$internal
  @override
  PurchaseHistory create() => PurchaseHistory();
}

String _$purchaseHistoryHash() => r'762df0cd06d257dc57006a3b41f433ad818e3d8c';

abstract class _$PurchaseHistory extends $AsyncNotifier<List<PurchaseDto>> {
  FutureOr<List<PurchaseDto>> build();
  @$mustCallSuper
  @override
  void runBuild() {
    final ref =
        this.ref as $Ref<AsyncValue<List<PurchaseDto>>, List<PurchaseDto>>;
    final element =
        ref.element
            as $ClassProviderElement<
              AnyNotifier<AsyncValue<List<PurchaseDto>>, List<PurchaseDto>>,
              AsyncValue<List<PurchaseDto>>,
              Object?,
              Object?
            >;
    element.handleCreate(ref, build);
  }
}

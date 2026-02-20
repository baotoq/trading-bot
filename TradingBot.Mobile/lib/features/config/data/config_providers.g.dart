// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'config_providers.dart';

// **************************************************************************
// RiverpodGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, type=warning

@ProviderFor(configRepository)
final configRepositoryProvider = ConfigRepositoryProvider._();

final class ConfigRepositoryProvider
    extends
        $FunctionalProvider<
          ConfigRepository,
          ConfigRepository,
          ConfigRepository
        >
    with $Provider<ConfigRepository> {
  ConfigRepositoryProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'configRepositoryProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$configRepositoryHash();

  @$internal
  @override
  $ProviderElement<ConfigRepository> $createElement($ProviderPointer pointer) =>
      $ProviderElement(pointer);

  @override
  ConfigRepository create(Ref ref) {
    return configRepository(ref);
  }

  /// {@macro riverpod.override_with_value}
  Override overrideWithValue(ConfigRepository value) {
    return $ProviderOverride(
      origin: this,
      providerOverride: $SyncValueProvider<ConfigRepository>(value),
    );
  }
}

String _$configRepositoryHash() => r'2a8419fd4bf31dd30af6ef01b2a79915ed9b6e19';

@ProviderFor(configData)
final configDataProvider = ConfigDataProvider._();

final class ConfigDataProvider
    extends
        $FunctionalProvider<
          AsyncValue<ConfigResponse>,
          ConfigResponse,
          FutureOr<ConfigResponse>
        >
    with $FutureModifier<ConfigResponse>, $FutureProvider<ConfigResponse> {
  ConfigDataProvider._()
    : super(
        from: null,
        argument: null,
        retry: null,
        name: r'configDataProvider',
        isAutoDispose: true,
        dependencies: null,
        $allTransitiveDependencies: null,
      );

  @override
  String debugGetCreateSourceHash() => _$configDataHash();

  @$internal
  @override
  $FutureProviderElement<ConfigResponse> $createElement(
    $ProviderPointer pointer,
  ) => $FutureProviderElement(pointer);

  @override
  FutureOr<ConfigResponse> create(Ref ref) {
    return configData(ref);
  }
}

String _$configDataHash() => r'ae8281e818bf0e1379d5caa147a142618b44a93b';

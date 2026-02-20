import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/retry_widget.dart';
import '../data/config_providers.dart';
import 'widgets/config_edit_form.dart';
import 'widgets/config_view_section.dart';

class ConfigScreen extends HookConsumerWidget {
  const ConfigScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final configData = ref.watch(configDataProvider);
    final cachedValue = configData.value;
    final isEditing = useState(false);

    // Show snackbar on errors -- stale cached data remains visible
    ref.listen(configDataProvider, (previous, next) {
      if (next.hasError && !next.isLoading) {
        if (next.error is AuthenticationException) {
          showAuthErrorSnackbar(context);
        } else {
          showErrorSnackbar(context, 'Could not load configuration');
        }
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Configuration'),
        actions: [
          if (configData.hasValue && !isEditing.value)
            IconButton(
              onPressed: () => isEditing.value = true,
              icon: const Icon(CupertinoIcons.pencil),
              tooltip: 'Edit',
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(configDataProvider.future),
        child: switch (configData) {
          AsyncData(:final value) => isEditing.value
              ? ConfigEditForm(
                  initialConfig: value,
                  repository: ref.read(configRepositoryProvider),
                  onSaved: () {
                    isEditing.value = false;
                    ref.invalidate(configDataProvider);
                  },
                  onCancel: () => isEditing.value = false,
                )
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [ConfigViewSection(config: value)],
                ),
          AsyncError() when cachedValue != null => isEditing.value
              ? ConfigEditForm(
                  initialConfig: cachedValue,
                  repository: ref.read(configRepositoryProvider),
                  onSaved: () {
                    isEditing.value = false;
                    ref.invalidate(configDataProvider);
                  },
                  onCancel: () => isEditing.value = false,
                )
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [ConfigViewSection(config: cachedValue)],
                ),
          AsyncError() => RetryWidget(
              onRetry: () => ref.invalidate(configDataProvider),
            ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ),
    );
  }
}

import 'package:dio/dio.dart';
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';

import '../../../../app/theme.dart';
import '../../../../core/widgets/error_snackbar.dart';
import '../../data/config_repository.dart';
import '../../data/models/config_response.dart';
import 'tier_list_editor.dart';

/// Edit form with numeric fields, time picker, tier list CRUD, and inline
/// server validation error display (RFC 7807 Problem Details parsing).
class ConfigEditForm extends HookWidget {
  const ConfigEditForm({
    required this.initialConfig,
    required this.repository,
    required this.onSaved,
    required this.onCancel,
    super.key,
  });

  final ConfigResponse initialConfig;
  final ConfigRepository repository;
  final VoidCallback onSaved;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    // Form field controllers
    final amountCtrl = useTextEditingController(
      text: initialConfig.baseDailyAmount.toString(),
    );
    final lookbackCtrl = useTextEditingController(
      text: initialConfig.highLookbackDays.toString(),
    );
    final maPeriodCtrl = useTextEditingController(
      text: initialConfig.bearMarketMaPeriod.toString(),
    );
    final bearBoostCtrl = useTextEditingController(
      text: initialConfig.bearBoostFactor.toString(),
    );
    final maxCapCtrl = useTextEditingController(
      text: initialConfig.maxMultiplierCap.toString(),
    );

    // Local state
    final buyHour = useState(initialConfig.dailyBuyHour);
    final buyMinute = useState(initialConfig.dailyBuyMinute);
    final dryRun = useState(initialConfig.dryRun);
    final tiers = useState(
      initialConfig.tiers
          .map((t) => MultiplierTierDto(
                dropPercentage: t.dropPercentage,
                multiplier: t.multiplier,
              ))
          .toList(),
    );
    final isSaving = useState(false);

    // Validation error state — maps error codes to messages
    final fieldErrors = useState<Map<String, String>>({});
    final tierErrors = useState<Map<String, String>>({});

    // Error code to field mapping
    String? scheduleError() {
      final errors = fieldErrors.value;
      return errors['InvalidScheduleHour'] ?? errors['InvalidScheduleMinute'];
    }

    String? lookbackError() => fieldErrors.value['InvalidHighLookbackDays'];
    String? maPeriodError() => fieldErrors.value['InvalidMaPeriod'];

    Future<void> handleSave() async {
      // Clear previous errors
      fieldErrors.value = {};
      tierErrors.value = {};

      // Build config from form values
      final config = ConfigResponse(
        baseDailyAmount: double.tryParse(amountCtrl.text) ?? 0,
        dailyBuyHour: buyHour.value,
        dailyBuyMinute: buyMinute.value,
        highLookbackDays: int.tryParse(lookbackCtrl.text) ?? 0,
        dryRun: dryRun.value,
        bearMarketMaPeriod: int.tryParse(maPeriodCtrl.text) ?? 0,
        bearBoostFactor: double.tryParse(bearBoostCtrl.text) ?? 0,
        maxMultiplierCap: double.tryParse(maxCapCtrl.text) ?? 0,
        tiers: tiers.value,
      );

      isSaving.value = true;

      try {
        await repository.updateConfig(config);

        if (context.mounted) {
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              const SnackBar(
                content: Text('Configuration saved'),
                behavior: SnackBarBehavior.floating,
                backgroundColor: Colors.green,
                duration: Duration(seconds: 3),
              ),
            );
        }

        onSaved();
      } on DioException catch (e) {
        if (e.response?.statusCode == 400) {
          _parseValidationErrors(e.response?.data, fieldErrors, tierErrors);
        } else {
          if (context.mounted) {
            showErrorSnackbar(context, 'Failed to save configuration');
          }
        }
      } finally {
        isSaving.value = false;
      }
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // DCA Settings card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('DCA Settings', style: textTheme.titleMedium),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: amountCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Base Daily Amount',
                      prefixText: '\$ ',
                      border: OutlineInputBorder(),
                    ),
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                  ),
                  const SizedBox(height: 16),
                  // Daily buy time with time picker
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('Daily Buy Time', style: textTheme.bodyMedium),
                            const SizedBox(height: 4),
                            Text(
                              '${buyHour.value.toString().padLeft(2, '0')}:${buyMinute.value.toString().padLeft(2, '0')} UTC',
                              style: textTheme.titleLarge,
                            ),
                            if (scheduleError() != null)
                              Padding(
                                padding: const EdgeInsets.only(top: 4),
                                child: Text(
                                  scheduleError()!,
                                  style: TextStyle(
                                    color: Theme.of(context).colorScheme.error,
                                    fontSize: 12,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                      IconButton(
                        onPressed: () async {
                          final picked = await showTimePicker(
                            context: context,
                            initialTime: TimeOfDay(
                              hour: buyHour.value,
                              minute: buyMinute.value,
                            ),
                          );
                          if (picked != null) {
                            buyHour.value = picked.hour;
                            buyMinute.value = picked.minute;
                          }
                        },
                        icon: const Icon(CupertinoIcons.clock),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  // Dry run switch
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Dry Run'),
                    subtitle: Text(
                      dryRun.value
                          ? 'Simulated orders only'
                          : 'Live trading enabled',
                    ),
                    value: dryRun.value,
                    onChanged: (value) => dryRun.value = value,
                    activeTrackColor: Colors.amber.withAlpha(128),
                    activeThumbColor: Colors.amber,
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // Market Analysis card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Market Analysis', style: textTheme.titleMedium),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: lookbackCtrl,
                    decoration: InputDecoration(
                      labelText: 'High Lookback Days',
                      suffixText: 'days',
                      border: const OutlineInputBorder(),
                      errorText: lookbackError(),
                    ),
                    keyboardType: TextInputType.number,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: maPeriodCtrl,
                    decoration: InputDecoration(
                      labelText: 'Bear Market MA Period',
                      suffixText: 'days',
                      border: const OutlineInputBorder(),
                      errorText: maPeriodError(),
                    ),
                    keyboardType: TextInputType.number,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: bearBoostCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Bear Boost Factor',
                      suffixText: 'x',
                      border: OutlineInputBorder(),
                    ),
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: maxCapCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Max Multiplier Cap',
                      suffixText: 'x',
                      border: OutlineInputBorder(),
                    ),
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // Multiplier Tiers card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Multiplier Tiers', style: textTheme.titleMedium),
                  const SizedBox(height: 8),
                  TierListEditor(
                    tiers: tiers.value,
                    onChanged: (updated) => tiers.value = updated,
                    tierErrors: tierErrors.value,
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 24),

          // Action buttons
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: isSaving.value ? null : onCancel,
                  child: const Text('Cancel'),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: FilledButton(
                  onPressed: isSaving.value ? null : handleSave,
                  style: FilledButton.styleFrom(
                    backgroundColor: AppTheme.bitcoinOrange,
                  ),
                  child: isSaving.value
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Save'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  /// Parses RFC 7807 Problem Details response and populates field-level and
  /// tier-level error maps.
  void _parseValidationErrors(
    dynamic responseData,
    ValueNotifier<Map<String, String>> fieldErrors,
    ValueNotifier<Map<String, String>> tierErrors,
  ) {
    if (responseData is! Map<String, dynamic>) return;

    final extensions = responseData['extensions'] as Map<String, dynamic>?;
    if (extensions == null) return;

    final errors = extensions['errors'] as List?;
    if (errors == null) return;

    final newFieldErrors = <String, String>{};
    final newTierErrors = <String, String>{};

    const tierErrorCodes = {
      'TiersNotAscending',
      'TierMultiplierOutOfRange',
      'TierDropPercentageDuplicate',
    };

    for (final error in errors) {
      if (error is Map<String, dynamic>) {
        final code = error['code'] as String? ?? '';
        final message = error['message'] as String? ?? 'Validation error';

        if (tierErrorCodes.contains(code)) {
          newTierErrors[code] = message;
        } else {
          newFieldErrors[code] = message;
        }
      }
    }

    fieldErrors.value = newFieldErrors;
    tierErrors.value = newTierErrors;
  }
}

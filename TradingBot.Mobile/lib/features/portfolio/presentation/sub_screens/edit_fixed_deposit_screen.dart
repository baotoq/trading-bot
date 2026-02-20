import 'package:dio/dio.dart';
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:go_router/go_router.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/fixed_deposit_response.dart';
import '../../data/portfolio_providers.dart';

class EditFixedDepositScreen extends HookConsumerWidget {
  const EditFixedDepositScreen({required this.id, super.key});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pageData = ref.watch(portfolioPageDataProvider);
    final isSaving = useState(false);

    final fd = pageData.value?.fixedDeposits.cast<FixedDepositResponse?>()
            .firstWhere((f) => f!.id == id, orElse: () => null) ??
        pageData.value?.fixedDeposits.cast<FixedDepositResponse?>().firstWhere(
              (f) => f!.id == id,
              orElse: () => null,
            );

    // Controllers pre-filled from existing deposit
    final bankNameController = useTextEditingController(
      text: fd?.bankName ?? '',
    );
    final principalController = useTextEditingController(
      text: fd != null ? fd.principalVnd.toStringAsFixed(0) : '',
    );
    final rateController = useTextEditingController(
      text: fd?.annualInterestRate.toString() ?? '',
    );
    final startDateController = useTextEditingController(
      text: fd?.startDate ?? DateFormat('yyyy-MM-dd').format(DateTime.now()),
    );
    final maturityDateController = useTextEditingController(
      text: fd?.maturityDate ?? '',
    );

    // Map compoundingFrequency to dropdown value
    // API returns 'None' for simple interest; dropdown uses 'Simple'
    String mapFrequencyToDropdown(String? freq) {
      if (freq == null || freq == 'None') return 'Simple';
      return freq;
    }

    final compoundingFreq = useState(mapFrequencyToDropdown(fd?.compoundingFrequency));

    Future<void> pickDate(TextEditingController controller,
        {bool allowFuture = false}) async {
      final picked = await showDatePicker(
        context: context,
        initialDate: DateTime.tryParse(controller.text) ?? DateTime.now(),
        firstDate: DateTime(2020),
        lastDate: allowFuture ? DateTime(2040) : DateTime.now(),
      );
      if (picked != null) {
        controller.text = DateFormat('yyyy-MM-dd').format(picked);
      }
    }

    Future<void> submit() async {
      if (bankNameController.text.isEmpty ||
          principalController.text.isEmpty ||
          rateController.text.isEmpty ||
          maturityDateController.text.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Please fill in all required fields')),
        );
        return;
      }

      isSaving.value = true;
      try {
        final body = {
          'bankName': bankNameController.text,
          'principal': double.parse(principalController.text),
          'annualInterestRate': double.parse(rateController.text),
          'startDate': startDateController.text,
          'maturityDate': maturityDateController.text,
          'compoundingFrequency': compoundingFreq.value,
        };
        await ref.read(portfolioRepositoryProvider).updateFixedDeposit(id, body);
        if (context.mounted) {
          ref.invalidate(portfolioPageDataProvider);
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Fixed deposit updated')),
          );
          context.pop();
        }
      } on DioException catch (e) {
        if (context.mounted) {
          final msg = e.response?.data?.toString() ?? 'Failed to update';
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg)),
          );
        }
      } finally {
        isSaving.value = false;
      }
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Edit Fixed Deposit')),
      body: switch (pageData) {
        AsyncError() => const Center(child: Text('Could not load data')),
        AsyncLoading() => const Center(child: CircularProgressIndicator()),
        _ => fd == null
            ? const Center(
                child: Text('Fixed deposit not found',
                    style: TextStyle(color: Colors.white54)),
              )
            : SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextField(
                      controller: bankNameController,
                      decoration: const InputDecoration(labelText: 'Bank name'),
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: principalController,
                      decoration:
                          const InputDecoration(labelText: 'Principal (VND)'),
                      keyboardType: const TextInputType.numberWithOptions(
                          decimal: false),
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: rateController,
                      decoration: const InputDecoration(
                        labelText: 'Annual interest rate',
                        hintText: 'e.g., 0.065 for 6.5%',
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                          decimal: true),
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: startDateController,
                      decoration: const InputDecoration(
                        labelText: 'Start date',
                        suffixIcon: Icon(CupertinoIcons.calendar),
                      ),
                      readOnly: true,
                      onTap: () =>
                          pickDate(startDateController, allowFuture: true),
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: maturityDateController,
                      decoration: const InputDecoration(
                        labelText: 'Maturity date',
                        suffixIcon: Icon(CupertinoIcons.calendar),
                      ),
                      readOnly: true,
                      onTap: () =>
                          pickDate(maturityDateController, allowFuture: true),
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<String>(
                      initialValue: compoundingFreq.value,
                      decoration: const InputDecoration(
                          labelText: 'Compounding frequency'),
                      items: const [
                        DropdownMenuItem(
                            value: 'Simple',
                            child: Text('Simple (No Compounding)')),
                        DropdownMenuItem(
                            value: 'Monthly', child: Text('Monthly')),
                        DropdownMenuItem(
                            value: 'Quarterly', child: Text('Quarterly')),
                        DropdownMenuItem(
                            value: 'SemiAnnual', child: Text('Semi-Annual')),
                        DropdownMenuItem(
                            value: 'Annual', child: Text('Annual')),
                      ],
                      onChanged: (v) => compoundingFreq.value = v!,
                    ),
                    const SizedBox(height: 32),
                    FilledButton.icon(
                      onPressed: isSaving.value ? null : submit,
                      icon: isSaving.value
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child:
                                  CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(CupertinoIcons.checkmark),
                      label: const Text('Save'),
                      style: FilledButton.styleFrom(
                        backgroundColor: AppTheme.bitcoinOrange,
                        foregroundColor: Colors.black,
                      ),
                    ),
                  ],
                ),
              ),
      },
    );
  }
}

import 'package:dio/dio.dart';
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:go_router/go_router.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_asset_response.dart';
import '../../data/portfolio_providers.dart';

enum FormMode { transaction, fixedDeposit }

class AddTransactionScreen extends HookConsumerWidget {
  const AddTransactionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mode = useState(FormMode.transaction);
    final isSaving = useState(false);

    // Transaction fields
    final selectedAsset = useState<PortfolioAssetResponse?>(null);
    final assetSearchController = useTextEditingController();
    final txType = useState('Buy');
    final dateController = useTextEditingController(
        text: DateFormat('yyyy-MM-dd').format(DateTime.now()));
    final quantityController = useTextEditingController();
    final priceController = useTextEditingController();
    final currency = useState('USD');
    final feeController = useTextEditingController();
    final showAssetResults = useState(false);
    final assetQuery = useState('');

    // Fixed deposit fields
    final bankNameController = useTextEditingController();
    final principalController = useTextEditingController();
    final rateController = useTextEditingController();
    final fdStartDateController = useTextEditingController(
        text: DateFormat('yyyy-MM-dd').format(DateTime.now()));
    final fdMaturityDateController = useTextEditingController();
    final compoundingFreq = useState('None');

    final portfolioData = ref.watch(portfolioPageDataProvider);
    final assets = portfolioData.value?.assets ?? [];

    Future<void> pickDate(TextEditingController controller,
        {bool allowFuture = false}) async {
      final picked = await showDatePicker(
        context: context,
        initialDate: DateTime.now(),
        firstDate: DateTime(2020),
        lastDate:
            allowFuture ? DateTime(2040) : DateTime.now(),
      );
      if (picked != null) {
        controller.text = DateFormat('yyyy-MM-dd').format(picked);
      }
    }

    Future<void> submitTransaction() async {
      if (selectedAsset.value == null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Please select an asset')),
        );
        return;
      }
      if (quantityController.text.isEmpty || priceController.text.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Please fill in all required fields')),
        );
        return;
      }

      isSaving.value = true;
      try {
        final body = {
          'date': dateController.text,
          'quantity': double.parse(quantityController.text),
          'pricePerUnit': double.parse(priceController.text),
          'currency': currency.value,
          'type': txType.value,
          if (feeController.text.isNotEmpty)
            'fee': double.parse(feeController.text),
        };
        await ref
            .read(portfolioRepositoryProvider)
            .createTransaction(selectedAsset.value!.id, body);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Transaction added')),
          );
          context.pop();
          ref.invalidate(portfolioPageDataProvider);
        }
      } on DioException catch (e) {
        if (context.mounted) {
          final msg = e.response?.data?.toString() ?? 'Failed to save';
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg)),
          );
        }
      } finally {
        isSaving.value = false;
      }
    }

    Future<void> submitFixedDeposit() async {
      if (bankNameController.text.isEmpty ||
          principalController.text.isEmpty ||
          rateController.text.isEmpty ||
          fdMaturityDateController.text.isEmpty) {
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
          'startDate': fdStartDateController.text,
          'maturityDate': fdMaturityDateController.text,
          'compoundingFrequency': compoundingFreq.value,
        };
        await ref.read(portfolioRepositoryProvider).createFixedDeposit(body);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Fixed deposit added')),
          );
          context.pop();
          ref.invalidate(portfolioPageDataProvider);
        }
      } on DioException catch (e) {
        if (context.mounted) {
          final msg = e.response?.data?.toString() ?? 'Failed to save';
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg)),
          );
        }
      } finally {
        isSaving.value = false;
      }
    }

    final filteredAssets = assets
        .where((a) =>
            a.name.toLowerCase().contains(assetQuery.value.toLowerCase()) ||
            a.ticker.toLowerCase().contains(assetQuery.value.toLowerCase()))
        .toList();

    return Scaffold(
      appBar: AppBar(title: const Text('Add Entry')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SegmentedButton<FormMode>(
              segments: const [
                ButtonSegment(
                  value: FormMode.transaction,
                  label: Text('Buy / Sell'),
                ),
                ButtonSegment(
                  value: FormMode.fixedDeposit,
                  label: Text('Fixed Deposit'),
                ),
              ],
              selected: {mode.value},
              onSelectionChanged: (selection) =>
                  mode.value = selection.first,
            ),
            const SizedBox(height: 24),
            if (mode.value == FormMode.transaction) ...[
              // Asset picker
              TextField(
                controller: assetSearchController,
                decoration: InputDecoration(
                  labelText: 'Asset',
                  hintText: 'Search by name or ticker',
                  suffixIcon: selectedAsset.value != null
                      ? IconButton(
                          icon: const Icon(CupertinoIcons.clear),
                          onPressed: () {
                            selectedAsset.value = null;
                            assetSearchController.clear();
                            assetQuery.value = '';
                          },
                        )
                      : null,
                ),
                readOnly: selectedAsset.value != null,
                onChanged: (v) {
                  assetQuery.value = v;
                  showAssetResults.value = v.isNotEmpty;
                },
                onTap: () {
                  if (selectedAsset.value == null) {
                    showAssetResults.value = true;
                  }
                },
              ),
              if (showAssetResults.value && selectedAsset.value == null)
                Container(
                  constraints: const BoxConstraints(maxHeight: 150),
                  margin: const EdgeInsets.only(bottom: 8),
                  decoration: BoxDecoration(
                    color: Colors.white10,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: ListView.builder(
                    shrinkWrap: true,
                    itemCount: filteredAssets.length,
                    itemBuilder: (context, index) {
                      final a = filteredAssets[index];
                      return ListTile(
                        dense: true,
                        title: Text(a.name),
                        subtitle: Text(a.ticker),
                        onTap: () {
                          selectedAsset.value = a;
                          assetSearchController.text =
                              '${a.name} (${a.ticker})';
                          showAssetResults.value = false;
                        },
                      );
                    },
                  ),
                ),
              const SizedBox(height: 16),
              // Transaction type
              SegmentedButton<String>(
                segments: const [
                  ButtonSegment(value: 'Buy', label: Text('Buy')),
                  ButtonSegment(value: 'Sell', label: Text('Sell')),
                ],
                selected: {txType.value},
                onSelectionChanged: (s) => txType.value = s.first,
              ),
              const SizedBox(height: 16),
              // Date
              TextField(
                controller: dateController,
                decoration: const InputDecoration(
                  labelText: 'Date',
                  suffixIcon: Icon(CupertinoIcons.calendar),
                ),
                readOnly: true,
                onTap: () => pickDate(dateController),
              ),
              const SizedBox(height: 16),
              // Quantity
              TextField(
                controller: quantityController,
                decoration: const InputDecoration(labelText: 'Quantity'),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
              ),
              const SizedBox(height: 16),
              // Price per unit
              TextField(
                controller: priceController,
                decoration:
                    const InputDecoration(labelText: 'Price per unit'),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
              ),
              const SizedBox(height: 16),
              // Currency
              DropdownButtonFormField<String>(
                initialValue: currency.value,
                decoration: const InputDecoration(labelText: 'Currency'),
                items: const [
                  DropdownMenuItem(value: 'USD', child: Text('USD')),
                  DropdownMenuItem(value: 'VND', child: Text('VND')),
                ],
                onChanged: (v) => currency.value = v!,
              ),
              const SizedBox(height: 16),
              // Fee
              TextField(
                controller: feeController,
                decoration:
                    const InputDecoration(labelText: 'Fee (optional)'),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
              ),
            ] else ...[
              // Fixed deposit fields
              TextField(
                controller: bankNameController,
                decoration: const InputDecoration(labelText: 'Bank name'),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: principalController,
                decoration:
                    const InputDecoration(labelText: 'Principal (VND)'),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: false),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: rateController,
                decoration: const InputDecoration(
                  labelText: 'Annual interest rate',
                  hintText: 'e.g., 0.065 for 6.5%',
                ),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: fdStartDateController,
                decoration: const InputDecoration(
                  labelText: 'Start date',
                  suffixIcon: Icon(CupertinoIcons.calendar),
                ),
                readOnly: true,
                onTap: () =>
                    pickDate(fdStartDateController, allowFuture: true),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: fdMaturityDateController,
                decoration: const InputDecoration(
                  labelText: 'Maturity date',
                  suffixIcon: Icon(CupertinoIcons.calendar),
                ),
                readOnly: true,
                onTap: () =>
                    pickDate(fdMaturityDateController, allowFuture: true),
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                initialValue: compoundingFreq.value,
                decoration: const InputDecoration(
                    labelText: 'Compounding frequency'),
                items: const [
                  DropdownMenuItem(value: 'None', child: Text('None')),
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
            ],
            const SizedBox(height: 32),
            FilledButton.icon(
              onPressed: isSaving.value
                  ? null
                  : () {
                      if (mode.value == FormMode.transaction) {
                        submitTransaction();
                      } else {
                        submitFixedDeposit();
                      }
                    },
              icon: isSaving.value
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
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
    );
  }
}

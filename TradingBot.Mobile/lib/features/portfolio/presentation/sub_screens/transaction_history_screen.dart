import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../app/theme.dart';
import '../../data/models/portfolio_asset_response.dart';
import '../../data/models/transaction_response.dart';
import '../../data/portfolio_providers.dart';

class _TransactionWithAsset {
  _TransactionWithAsset({required this.transaction, required this.asset});
  final TransactionResponse transaction;
  final PortfolioAssetResponse asset;
}

class TransactionHistoryScreen extends ConsumerStatefulWidget {
  const TransactionHistoryScreen({super.key});

  @override
  ConsumerState<TransactionHistoryScreen> createState() =>
      _TransactionHistoryScreenState();
}

class _TransactionHistoryScreenState
    extends ConsumerState<TransactionHistoryScreen> {
  List<_TransactionWithAsset> _transactions = [];
  bool _isLoading = true;

  // Filters
  String? _filterAssetId;
  String? _filterType;
  String? _filterStartDate;
  String? _filterEndDate;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadTransactions());
  }

  Future<void> _loadTransactions() async {
    setState(() => _isLoading = true);
    try {
      final pageData = ref.read(portfolioPageDataProvider).value;
      if (pageData == null) return;

      final assets = _filterAssetId != null
          ? pageData.assets.where((a) => a.id == _filterAssetId).toList()
          : pageData.assets;

      final repo = ref.read(portfolioRepositoryProvider);
      final futures = assets.map((a) => repo
          .fetchTransactions(
            a.id,
            type: _filterType,
            startDate: _filterStartDate,
            endDate: _filterEndDate,
          )
          .then((txs) =>
              txs.map((tx) => _TransactionWithAsset(transaction: tx, asset: a))
                  .toList()));

      final results = await Future.wait(futures);
      final all = results.expand((list) => list).toList();
      all.sort((a, b) => b.transaction.date.compareTo(a.transaction.date));

      setState(() {
        _transactions = all;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to load transactions: $e')),
        );
      }
    }
  }

  void _showFilterSheet() {
    final pageData = ref.read(portfolioPageDataProvider).value;
    final assets = pageData?.assets ?? [];

    showModalBottomSheet(
      context: context,
      builder: (ctx) {
        String? tempAssetId = _filterAssetId;
        String? tempType = _filterType;
        String? tempStart = _filterStartDate;
        String? tempEnd = _filterEndDate;

        return StatefulBuilder(
          builder: (ctx, setSheetState) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Filters',
                      style: Theme.of(ctx).textTheme.titleMedium),
                  const SizedBox(height: 16),
                  // Asset filter
                  DropdownButtonFormField<String?>(
                    initialValue: tempAssetId,
                    decoration: const InputDecoration(labelText: 'Asset'),
                    items: [
                      const DropdownMenuItem(value: null, child: Text('All')),
                      ...assets.map((a) => DropdownMenuItem(
                            value: a.id,
                            child: Text('${a.name} (${a.ticker})'),
                          )),
                    ],
                    onChanged: (v) =>
                        setSheetState(() => tempAssetId = v),
                  ),
                  const SizedBox(height: 12),
                  // Type filter
                  Wrap(
                    spacing: 8,
                    children: [
                      ChoiceChip(
                        label: const Text('All'),
                        selected: tempType == null,
                        onSelected: (_) =>
                            setSheetState(() => tempType = null),
                      ),
                      ChoiceChip(
                        label: const Text('Buy'),
                        selected: tempType == 'Buy',
                        onSelected: (_) =>
                            setSheetState(() => tempType = 'Buy'),
                      ),
                      ChoiceChip(
                        label: const Text('Sell'),
                        selected: tempType == 'Sell',
                        onSelected: (_) =>
                            setSheetState(() => tempType = 'Sell'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  // Date range
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          readOnly: true,
                          decoration: InputDecoration(
                            labelText: 'Start date',
                            hintText: tempStart ?? 'Any',
                            suffixIcon:
                                const Icon(CupertinoIcons.calendar, size: 18),
                          ),
                          onTap: () async {
                            final d = await showDatePicker(
                              context: ctx,
                              initialDate: DateTime.now(),
                              firstDate: DateTime(2020),
                              lastDate: DateTime.now(),
                            );
                            if (d != null) {
                              setSheetState(() => tempStart =
                                  DateFormat('yyyy-MM-dd').format(d));
                            }
                          },
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: TextField(
                          readOnly: true,
                          decoration: InputDecoration(
                            labelText: 'End date',
                            hintText: tempEnd ?? 'Any',
                            suffixIcon:
                                const Icon(CupertinoIcons.calendar, size: 18),
                          ),
                          onTap: () async {
                            final d = await showDatePicker(
                              context: ctx,
                              initialDate: DateTime.now(),
                              firstDate: DateTime(2020),
                              lastDate: DateTime.now(),
                            );
                            if (d != null) {
                              setSheetState(() => tempEnd =
                                  DateFormat('yyyy-MM-dd').format(d));
                            }
                          },
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () {
                            setState(() {
                              _filterAssetId = null;
                              _filterType = null;
                              _filterStartDate = null;
                              _filterEndDate = null;
                            });
                            Navigator.pop(ctx);
                            _loadTransactions();
                          },
                          child: const Text('Clear'),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: FilledButton(
                          onPressed: () {
                            setState(() {
                              _filterAssetId = tempAssetId;
                              _filterType = tempType;
                              _filterStartDate = tempStart;
                              _filterEndDate = tempEnd;
                            });
                            Navigator.pop(ctx);
                            _loadTransactions();
                          },
                          child: const Text('Apply'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Transaction History'),
        actions: [
          IconButton(
            icon: const Icon(CupertinoIcons.slider_horizontal_3),
            onPressed: _showFilterSheet,
            tooltip: 'Filter',
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _transactions.isEmpty
              ? const Center(
                  child: Text(
                    'No transactions found',
                    style: TextStyle(color: Colors.white54),
                  ),
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: _transactions.length,
                  separatorBuilder: (_, __) => const Divider(height: 1),
                  itemBuilder: (context, index) {
                    final item = _transactions[index];
                    return _TransactionListTile(
                      transaction: item.transaction,
                      assetName: item.asset.name,
                      assetTicker: item.asset.ticker,
                    );
                  },
                ),
    );
  }
}

class _TransactionListTile extends StatelessWidget {
  const _TransactionListTile({
    required this.transaction,
    required this.assetName,
    required this.assetTicker,
  });

  final TransactionResponse transaction;
  final String assetName;
  final String assetTicker;

  static final _usdFormatter = NumberFormat.currency(
    symbol: '\$',
    decimalDigits: 2,
    locale: 'en_US',
  );

  @override
  Widget build(BuildContext context) {
    final isBuy = transaction.type == 'Buy';
    final totalValue = transaction.quantity * transaction.pricePerUnit;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          // Type indicator
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: isBuy
                  ? AppTheme.profitGreen.withAlpha(30)
                  : AppTheme.lossRed.withAlpha(30),
              borderRadius: BorderRadius.circular(8),
            ),
            alignment: Alignment.center,
            child: Icon(
              isBuy ? CupertinoIcons.arrow_down : CupertinoIcons.arrow_up,
              size: 16,
              color: isBuy ? AppTheme.profitGreen : AppTheme.lossRed,
            ),
          ),
          const SizedBox(width: 12),
          // Details
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(
                      '$assetTicker ${transaction.type}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w500,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(width: 6),
                    if (transaction.source == 'Bot')
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: AppTheme.bitcoinOrange.withAlpha(40),
                          borderRadius: BorderRadius.circular(4),
                          border: Border.all(
                              color: AppTheme.bitcoinOrange.withAlpha(128)),
                        ),
                        child: const Text(
                          'Bot',
                          style: TextStyle(
                            color: AppTheme.bitcoinOrange,
                            fontSize: 10,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 2),
                Text(
                  '${transaction.quantity.toStringAsFixed(4)} @ ${_usdFormatter.format(transaction.pricePerUnit)}',
                  style: const TextStyle(color: Colors.white54, fontSize: 12),
                ),
              ],
            ),
          ),
          // Trailing: total + date
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                _usdFormatter.format(totalValue),
                style: const TextStyle(
                    fontWeight: FontWeight.w600, fontSize: 14),
              ),
              const SizedBox(height: 2),
              Text(
                transaction.date,
                style: const TextStyle(color: Colors.white54, fontSize: 11),
              ),
              if (transaction.fee != null && transaction.fee! > 0)
                Text(
                  'fee: ${_usdFormatter.format(transaction.fee)}',
                  style: const TextStyle(color: Colors.white38, fontSize: 10),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

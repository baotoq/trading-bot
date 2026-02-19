import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

/// Displayed when the app cold-starts with no cached data and the API fails.
///
/// Shows "Could not load data" with a Retry button centered on screen.
class RetryWidget extends StatelessWidget {
  const RetryWidget({required this.onRetry, super.key});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(CupertinoIcons.wifi_slash, size: 48, color: Colors.grey),
          const SizedBox(height: 16),
          Text(
            'Could not load data',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(CupertinoIcons.refresh),
            label: const Text('Retry'),
          ),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';

/// Shows a floating error snackbar with a red background.
void showErrorSnackbar(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
        backgroundColor: Colors.red.shade800,
        duration: const Duration(seconds: 4),
      ),
    );
}

/// Shows a floating "Authentication failed" error snackbar.
///
/// Per locked decision: "Auth failures (401/403) show a snackbar warning
/// 'Authentication failed'".
void showAuthErrorSnackbar(BuildContext context) {
  showErrorSnackbar(context, 'Authentication failed');
}

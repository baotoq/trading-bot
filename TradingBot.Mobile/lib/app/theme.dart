import 'package:flutter/material.dart';

class AppTheme {
  static const Color bitcoinOrange = Color(0xFFF7931A);
  static const Color profitGreen = Color(0xFF00C087);
  static const Color lossRed = Color(0xFFFF4D4D);
  static const Color surfaceDark = Color(0xFF121212);
  static const Color navBarDark = Color(0xFF1A1A1A);

  static ThemeData get dark => ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: bitcoinOrange,
      brightness: Brightness.dark,
    ),
    brightness: Brightness.dark,
    scaffoldBackgroundColor: surfaceDark,
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: navBarDark,
      indicatorColor: bitcoinOrange.withAlpha(51),
    ),
    snackBarTheme: const SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
    ),
  );
}

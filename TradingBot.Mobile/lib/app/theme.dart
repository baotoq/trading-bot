import 'dart:ui';

import 'package:flutter/material.dart';

class GlassTheme extends ThemeExtension<GlassTheme> {
  const GlassTheme({
    required this.blurSigma,
    required this.tintOpacity,
    required this.tintColor,
    required this.borderColor,
    required this.borderWidth,
    required this.glowColor,
    required this.cardRadius,
    required this.opaqueSurface,
    required this.opaqueBorder,
  });

  /// Blur sigma for BackdropFilter — kept moderate (12.0) per Impeller guidance.
  final double blurSigma;

  /// Tint opacity over the blur surface — subtle dark tint on navy background.
  final double tintOpacity;

  /// Tint color applied at [tintOpacity] over the blurred surface.
  final Color tintColor;

  /// Border color for glass card edges — translucent white shimmer.
  final Color borderColor;

  /// Border width for glass card edges.
  final double borderWidth;

  /// Subtle glow color token — BTC orange at very low opacity for ambient warmth.
  final Color glowColor;

  /// Border radius for glass card corners.
  final double cardRadius;

  /// Opaque surface color used when Reduce Transparency is enabled.
  /// Navy lifted ~10 lightness so it's distinguishable from the base.
  final Color opaqueSurface;

  /// Opaque border color used when Reduce Transparency is enabled.
  final Color opaqueBorder;

  @override
  GlassTheme copyWith({
    double? blurSigma,
    double? tintOpacity,
    Color? tintColor,
    Color? borderColor,
    double? borderWidth,
    Color? glowColor,
    double? cardRadius,
    Color? opaqueSurface,
    Color? opaqueBorder,
  }) {
    return GlassTheme(
      blurSigma: blurSigma ?? this.blurSigma,
      tintOpacity: tintOpacity ?? this.tintOpacity,
      tintColor: tintColor ?? this.tintColor,
      borderColor: borderColor ?? this.borderColor,
      borderWidth: borderWidth ?? this.borderWidth,
      glowColor: glowColor ?? this.glowColor,
      cardRadius: cardRadius ?? this.cardRadius,
      opaqueSurface: opaqueSurface ?? this.opaqueSurface,
      opaqueBorder: opaqueBorder ?? this.opaqueBorder,
    );
  }

  @override
  GlassTheme lerp(ThemeExtension<GlassTheme>? other, double t) {
    if (other is! GlassTheme) return this;
    return GlassTheme(
      blurSigma: lerpDouble(blurSigma, other.blurSigma, t)!,
      tintOpacity: lerpDouble(tintOpacity, other.tintOpacity, t)!,
      tintColor: Color.lerp(tintColor, other.tintColor, t)!,
      borderColor: Color.lerp(borderColor, other.borderColor, t)!,
      borderWidth: lerpDouble(borderWidth, other.borderWidth, t)!,
      glowColor: Color.lerp(glowColor, other.glowColor, t)!,
      cardRadius: lerpDouble(cardRadius, other.cardRadius, t)!,
      opaqueSurface: Color.lerp(opaqueSurface, other.opaqueSurface, t)!,
      opaqueBorder: Color.lerp(opaqueBorder, other.opaqueBorder, t)!,
    );
  }
}

class AppTheme {
  static const Color bitcoinOrange = Color(0xFFF7931A);
  static const Color profitGreen = Color(0xFF00C087);
  static const Color lossRed = Color(0xFFFF4D4D);
  static const Color surfaceDark = Color(0xFF121212);
  static const Color navBarDark = Color(0xFF1A1A1A);

  /// Dark navy base — the app scaffold background per design decision.
  /// Not pure black; slight blue warmth for a premium, understated aesthetic.
  static const Color navyBackground = Color(0xFF0D1117);

  /// Base TextStyle that enables tabular figures (OpenType tnum feature).
  /// Digits become uniform width, aligning vertically in monetary value lists.
  /// Usage: merge into any monetary TextStyle via copyWith:
  ///   Theme.of(context).textTheme.headlineSmall?.merge(AppTheme.moneyStyle)
  static const TextStyle moneyStyle = TextStyle(
    fontFeatures: [FontFeature.tabularFigures()],
  );

  static ThemeData get dark => ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: bitcoinOrange,
      brightness: Brightness.dark,
    ),
    brightness: Brightness.dark,
    // Transparent so AmbientBackground provides the actual navy base color and orbs.
    // If set to a solid color, Scaffold paints over the ambient gradient.
    scaffoldBackgroundColor: Colors.transparent,
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: navBarDark,
      indicatorColor: bitcoinOrange.withAlpha(51),
    ),
    snackBarTheme: const SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
    ),
    extensions: [
      GlassTheme(
        blurSigma: 12.0,
        tintOpacity: 0.08,
        tintColor: Colors.white,
        borderColor: Colors.white.withAlpha(31), // ~0.12 opacity
        borderWidth: 1.0,
        glowColor: const Color(0xFFF7931A).withAlpha(15), // ~0.06 opacity — subtle BTC orange glow
        cardRadius: 16.0,
        opaqueSurface: const Color(0xFF1C2333), // navy lifted ~10 lightness
        opaqueBorder: const Color(0xFF2D3748),
      ),
    ],
  );
}

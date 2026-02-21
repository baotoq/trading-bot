import 'dart:ui';

import 'package:flutter/material.dart';

import '../../../app/theme.dart';

/// A frosted glass surface widget — the primary card/panel across all app screens.
///
/// Reads all design tokens from [GlassTheme] (registered in [ThemeData.extensions]).
/// No blur sigma, opacity, or border values are hardcoded.
///
/// ## Accessibility
///
/// **Reduce Transparency:** When [MediaQuery.of(context).highContrast] is true,
/// BackdropFilter is skipped entirely and an opaque dark card is rendered instead.
/// This prevents performance and legibility issues for users who prefer reduced blur.
///
/// **Reduce Motion:** Use [GlassCard.shouldReduceMotion(context)] before starting
/// any animation in a child widget to respect the platform's motion preference.
///
/// ## Usage
///
/// ```dart
/// GlassCard(
///   child: Text('Hello'),
/// )
/// ```
///
/// With optional overrides:
///
/// ```dart
/// GlassCard(
///   padding: const EdgeInsets.all(24),
///   borderRadius: 20,
///   margin: const EdgeInsets.symmetric(horizontal: 16),
///   child: MyContent(),
/// )
/// ```
class GlassCard extends StatelessWidget {
  const GlassCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.borderRadius,
    this.margin,
  });

  /// The widget rendered inside the card surface.
  final Widget child;

  /// Inner padding applied around [child]. Defaults to 16 on all sides.
  final EdgeInsets padding;

  /// Optional corner radius override. Defaults to [GlassTheme.cardRadius].
  final double? borderRadius;

  /// Optional outer margin around the card. Defaults to null (no margin).
  final EdgeInsets? margin;

  /// Whether the platform requests reduced motion.
  ///
  /// Use this to skip or disable animations throughout the app before
  /// starting any [AnimationController] or running a transition.
  ///
  /// Flutter's [MediaQueryData.disableAnimations] is the cross-platform flag
  /// that maps to:
  /// - iOS: Settings → Accessibility → Motion → Reduce Motion
  /// - Android: Developer Options → Disable all animations
  ///
  /// This single flag covers all supported platforms. There is no separate
  /// iOS-only [AccessibilityFeatures.reduceMotion] in Flutter's MediaQuery API;
  /// [disableAnimations] is already sourced from the platform accessibility
  /// features under the hood.
  ///
  /// Usage in downstream phases:
  /// ```dart
  /// if (!GlassCard.shouldReduceMotion(context)) {
  ///   _controller.forward();
  /// }
  /// ```
  static bool shouldReduceMotion(BuildContext context) {
    return MediaQuery.of(context).disableAnimations;
  }

  @override
  Widget build(BuildContext context) {
    final glass = Theme.of(context).extension<GlassTheme>()!;

    // Flutter lacks a direct 'reduceTransparency' API. highContrast is the closest
    // proxy (maps to iOS "Increase Contrast", not "Reduce Transparency"). See
    // 33-RESEARCH.md Open Question #1. For now this correctly prevents
    // BackdropFilter from running in high-contrast accessibility mode.
    final reduceTransparency = MediaQuery.of(context).highContrast;

    final radius = borderRadius ?? glass.cardRadius;

    if (reduceTransparency) {
      // Opaque fallback path — no BackdropFilter applied.
      // Renders a plain dark container using the opaque surface tokens.
      return Container(
        margin: margin,
        decoration: BoxDecoration(
          color: glass.opaqueSurface,
          borderRadius: BorderRadius.circular(radius),
          border: Border.all(color: glass.opaqueBorder, width: glass.borderWidth),
        ),
        padding: padding,
        child: child,
      );
    }

    // Glass surface path — BackdropFilter blurs content behind the card.
    //
    // CRITICAL: ClipRRect MUST wrap BackdropFilter. Without it, the blur
    // bleeds beyond the card's rounded corners, creating an unsightly halo
    // effect. This is pitfall #1 from the 33-RESEARCH.md investigation.
    return Container(
      margin: margin,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(radius),
        child: BackdropFilter(
          filter: ImageFilter.blur(
            sigmaX: glass.blurSigma,
            sigmaY: glass.blurSigma,
          ),
          child: Container(
            decoration: BoxDecoration(
              color: glass.tintColor.withAlpha((glass.tintOpacity * 255).round()),
              borderRadius: BorderRadius.circular(radius),
              border: Border.all(color: glass.borderColor, width: glass.borderWidth),
            ),
            padding: padding,
            child: child,
          ),
        ),
      ),
    );
  }
}

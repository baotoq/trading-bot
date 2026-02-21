import 'package:flutter/material.dart';
import 'package:skeletonizer/skeletonizer.dart';

import 'glass_card.dart';

/// App-wide shimmer skeleton wrapper. Configures dark-themed shimmer colors
/// matching the glassmorphism design system. All screens should use this
/// instead of Skeletonizer directly.
///
/// Uses [GlassTheme] opaqueSurface (0xFF1C2333) and opaqueBorder (0xFF2D3748)
/// as base and highlight colors — these are the non-blur card surface tokens,
/// providing visual continuity between skeleton and loaded card surfaces.
///
/// Respects the platform's Reduce Motion accessibility preference:
/// - Normal mode: ShimmerEffect with lateral sweep animation
/// - Reduce Motion: PulseEffect with gentle fade (no lateral motion)
class AppShimmer extends StatelessWidget {
  const AppShimmer({
    super.key,
    required this.enabled,
    required this.child,
  });

  /// Whether the shimmer effect is active. Pass `true` while loading,
  /// `false` once data is available (Skeletonizer renders child normally).
  final bool enabled;

  /// The widget tree to skeletonize. Use real widget layouts with
  /// placeholder/dummy data — Skeletonizer paints over them with bones.
  final Widget child;

  /// Dark-themed base color matching GlassTheme.opaqueSurface.
  static const Color _baseColor = Color(0xFF1C2333);

  /// Dark-themed highlight color matching GlassTheme.opaqueBorder.
  static const Color _highlightColor = Color(0xFF2D3748);

  @override
  Widget build(BuildContext context) {
    final reduceMotion = GlassCard.shouldReduceMotion(context);

    final PaintingEffect effect;
    if (reduceMotion) {
      // PulseEffect: gentle opacity fade with no lateral sweep — safe for
      // users with vestibular disorders or motion sensitivity.
      effect = const PulseEffect(
        from: _baseColor,
        to: _highlightColor,
        duration: Duration(seconds: 1),
      );
    } else {
      // ShimmerEffect: horizontal sweep animation — the standard loading state.
      effect = const ShimmerEffect(
        baseColor: _baseColor,
        highlightColor: _highlightColor,
        duration: Duration(seconds: 1),
      );
    }

    return Skeletonizer(
      enabled: enabled,
      effect: effect,
      child: child,
    );
  }
}

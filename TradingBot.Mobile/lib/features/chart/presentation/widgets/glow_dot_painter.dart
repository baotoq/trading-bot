import 'dart:ui' show lerpDouble;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

/// A custom [FlDotPainter] that renders a two-layer glow halo + solid inner dot
/// for purchase marker spots on the price line chart.
///
/// ## Layers
///
/// 1. **Outer glow halo** — A radial gradient circle at `radius * 2.5` using
///    [RadialGradient] with colors from [effectiveGlowColor] (alpha 153) to
///    transparent (alpha 0). Creates the "halo" effect around the dot.
///
/// 2. **Inner solid circle** — A filled circle at [radius] with [color] fill
///    and a [strokeColor] stroke at 1.5 width.
///
/// ## Usage
///
/// ```dart
/// getDotPainter: (spot, _, __, ___) => GlowDotPainter(
///   radius: 5,
///   color: tierColor,
///   glowColor: AppTheme.bitcoinOrange,
///   strokeColor: Colors.white,
/// )
/// ```
class GlowDotPainter extends FlDotPainter {
  const GlowDotPainter({
    required this.color,
    this.radius = 5.0,
    this.glowColor,
    this.strokeColor = Colors.white,
  });

  /// The fill color of the inner solid dot.
  final Color color;

  /// The color used for the outer radial glow halo.
  /// Defaults to [color] if null.
  final Color? glowColor;

  /// The radius of the inner solid dot.
  final double radius;

  /// The stroke color applied around the inner solid dot.
  final Color strokeColor;

  /// Effective glow color — falls back to [color] when [glowColor] is null.
  Color get effectiveGlowColor => glowColor ?? color;

  @override
  void draw(Canvas canvas, FlSpot spot, Offset center) {
    // Layer 1: Outer radial glow halo
    final glowRadius = radius * 2.5;
    final glowRect = Rect.fromCircle(center: center, radius: glowRadius);
    final glowGradient = RadialGradient(
      colors: [
        effectiveGlowColor.withAlpha(153), // ~0.60 opacity at center
        effectiveGlowColor.withAlpha(0),   // transparent at edge
      ],
      stops: const [0.0, 1.0],
    );
    final glowPaint = Paint()
      ..shader = glowGradient.createShader(glowRect)
      ..style = PaintingStyle.fill;
    canvas.drawCircle(center, glowRadius, glowPaint);

    // Layer 2: Inner solid circle with stroke
    final fillPaint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    canvas.drawCircle(center, radius, fillPaint);

    final strokePaint = Paint()
      ..color = strokeColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;
    canvas.drawCircle(center, radius, strokePaint);
  }

  @override
  Color get mainColor => color;

  @override
  Size getSize(FlSpot spot) {
    // Full glow halo extent: radius * 2.5 on each side
    final diameter = radius * 2.5 * 2;
    return Size(diameter, diameter);
  }

  @override
  bool hitTest(FlSpot spot, Offset touched, Offset center, double extraThreshold) {
    return (touched - center).distance <= radius + extraThreshold;
  }

  @override
  FlDotPainter lerp(FlDotPainter a, FlDotPainter b, double t) {
    if (a is! GlowDotPainter || b is! GlowDotPainter) {
      return b;
    }
    return GlowDotPainter(
      color: Color.lerp(a.color, b.color, t)!,
      radius: lerpDouble(a.radius, b.radius, t)!,
      glowColor: Color.lerp(a.effectiveGlowColor, b.effectiveGlowColor, t),
      strokeColor: Color.lerp(a.strokeColor, b.strokeColor, t)!,
    );
  }

  /// Used for equality check, see [EquatableMixin].
  @override
  List<Object?> get props => [color, radius, glowColor, strokeColor];
}

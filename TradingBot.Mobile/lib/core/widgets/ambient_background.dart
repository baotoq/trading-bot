import 'package:flutter/material.dart';
import 'package:trading_bot_app/app/theme.dart';

/// Full-screen ambient background with dark navy base and static radial gradient orbs.
///
/// Place this widget in the navigation shell (wrapping the [StatefulNavigationShell] body)
/// so all tab screens share the same background without per-screen recreation.
///
/// The orbs are subtle color pools at 10-15% opacity that add warmth and depth
/// to the dark navy base without competing with glass card surfaces. They are
/// completely static — no animation — resulting in zero per-frame rebuild cost.
///
/// Orb palette (complements navy #0D1117 + BTC orange #F7931A brand):
/// - Orb 1 (top-left): Warm amber — resonates with BTC orange brand
/// - Orb 2 (bottom-right): Cool indigo — provides cool depth contrast
/// - Orb 3 (center-left): Muted teal — mid-screen warmth
class AmbientBackground extends StatelessWidget {
  const AmbientBackground({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        // Base layer: dark navy fills the entire screen.
        // ColoredBox is more efficient than Container for flat color fills.
        const SizedBox.expand(
          child: ColoredBox(color: AppTheme.navyBackground),
        ),

        // Orb 1 — warm amber (top-left, resonates with BTC orange brand)
        // Offset partially off-screen for a natural bleed-in effect.
        Positioned(
          top: -80,
          left: -60,
          child: _buildOrb(
            size: 320,
            color: const Color(0xFFF7931A).withAlpha(28), // ~0.11 opacity
          ),
        ),

        // Orb 2 — cool indigo (bottom-right, provides depth contrast)
        Positioned(
          bottom: -100,
          right: -80,
          child: _buildOrb(
            size: 360,
            color: const Color(0xFF4F46E5).withAlpha(26), // ~0.10 opacity
          ),
        ),

        // Orb 3 — muted teal (center-left, mid-screen warmth)
        Positioned(
          top: 240,
          left: -120,
          child: _buildOrb(
            size: 280,
            color: const Color(0xFF0D9488).withAlpha(20), // ~0.08 opacity
          ),
        ),

        // Content renders on top of orbs.
        child,
      ],
    );
  }

  /// Builds a circular radial-gradient orb container.
  ///
  /// The [color] is the center color; the gradient fades to transparent at the
  /// edge, creating a soft glow-pool effect. Keep [color] alpha at 10-15% max
  /// so orbs remain barely visible and don't compete with glass card surfaces.
  Widget _buildOrb({required double size, required Color color}) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [color, Colors.transparent],
          stops: const [0.0, 1.0],
        ),
      ),
    );
  }
}

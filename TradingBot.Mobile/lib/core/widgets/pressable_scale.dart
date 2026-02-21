import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'glass_card.dart';

/// Wraps a child widget with a press-scale micro-interaction.
///
/// On tap down, shrinks to 0.97 scale with haptic pulse. On release, springs
/// back to 1.0. Respects Reduce Motion accessibility setting: when Reduce
/// Motion is enabled, the scale animation is skipped entirely and only the
/// [onTap] callback is forwarded.
///
/// ## Usage
///
/// ```dart
/// PressableScale(
///   onTap: () => print('tapped'),
///   child: GlassCard(child: Text('Press me')),
/// )
/// ```
///
/// ## Accessibility
///
/// Checks [GlassCard.shouldReduceMotion] in [didChangeDependencies]. When true,
/// no [AnimationController] is driven — the child is returned as-is wrapped in
/// a [GestureDetector] so [onTap] still works.
class PressableScale extends StatefulWidget {
  const PressableScale({
    super.key,
    required this.child,
    this.onTap,
  });

  /// The widget to wrap with the press-scale interaction.
  final Widget child;

  /// Optional callback invoked when the user completes a tap.
  final VoidCallback? onTap;

  @override
  State<PressableScale> createState() => _PressableScaleState();
}

class _PressableScaleState extends State<PressableScale>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _scaleAnimation;

  bool _reduceMotion = false;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      duration: const Duration(milliseconds: 100),
      vsync: this,
    );
    _scaleAnimation = Tween<double>(
      begin: 1.0,
      end: 0.97,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: Curves.easeInOut,
      ),
    );
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Check accessibility preference here — context is available in
    // didChangeDependencies (unlike initState) so MediaQuery is accessible.
    _reduceMotion = GlassCard.shouldReduceMotion(context);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onTapDown(TapDownDetails details) {
    if (_reduceMotion) return;
    HapticFeedback.lightImpact();
    _controller.forward();
  }

  void _onTapUp(TapUpDetails details) {
    if (_reduceMotion) return;
    _controller.reverse();
  }

  void _onTapCancel() {
    if (_reduceMotion) return;
    _controller.reverse();
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTapDown: _onTapDown,
      onTapUp: _onTapUp,
      onTapCancel: _onTapCancel,
      onTap: widget.onTap,
      child: AnimatedBuilder(
        animation: _scaleAnimation,
        builder: (context, child) {
          return Transform.scale(
            scale: _scaleAnimation.value,
            child: child,
          );
        },
        child: widget.child,
      ),
    );
  }
}

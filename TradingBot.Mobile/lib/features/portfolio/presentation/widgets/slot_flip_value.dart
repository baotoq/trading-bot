import 'package:flutter/material.dart';

import '../../../../core/widgets/glass_card.dart';

/// A value label that animates with a vertical slot-flip effect when
/// the displayed [value] string changes.
///
/// Uses [AnimatedSwitcher] with a [SlideTransition] transitionBuilder
/// to create a slot-machine reveal. The incoming child slides in from
/// below (offset Y: 1 → 0) while the outgoing child slides out upward
/// (offset Y: 0 → -1).
///
/// Wraps the switcher in [ClipRect] to prevent the outgoing/incoming
/// children from painting outside the parent's bounds during transition.
///
/// Respects reduce-motion: when [GlassCard.shouldReduceMotion] is true,
/// the switcher duration is set to zero so values update instantly with
/// no visible animation.
///
/// Usage:
/// ```dart
/// SlotFlipValue(
///   value: _formatValue(totalUsd, totalVnd),
///   style: Theme.of(context).textTheme.headlineLarge,
/// )
/// ```
class SlotFlipValue extends StatelessWidget {
  const SlotFlipValue({
    required this.value,
    this.style,
    super.key,
  });

  /// The formatted value string to display. Changing this triggers the flip animation.
  final String value;

  /// Optional text style applied to the inner [Text] widget.
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final reduceMotion = GlassCard.shouldReduceMotion(context);

    return ClipRect(
      child: AnimatedSwitcher(
        duration: reduceMotion
            ? Duration.zero
            : const Duration(milliseconds: 250),
        switchInCurve: Curves.easeOut,
        switchOutCurve: Curves.easeIn,
        transitionBuilder: (child, animation) {
          // Distinguish incoming (forward) vs outgoing (reverse) child.
          // Outgoing: animation runs 1.0 → 0.0 (reverse), exits upward.
          // Incoming: animation runs 0.0 → 1.0 (forward), enters from below.
          final isIncoming = animation.status != AnimationStatus.reverse;
          final slideTween = isIncoming
              ? Tween<Offset>(begin: const Offset(0, 1), end: Offset.zero)
              : Tween<Offset>(begin: const Offset(0, -1), end: Offset.zero);
          return SlideTransition(
            position: slideTween.animate(animation),
            child: FadeTransition(opacity: animation, child: child),
          );
        },
        // CRITICAL: ValueKey triggers re-animation when value string changes.
        // Without this, AnimatedSwitcher reuses the existing widget and skips
        // the transition entirely.
        child: Text(value, key: ValueKey(value), style: style),
      ),
    );
  }
}

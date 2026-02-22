import 'package:flutter/material.dart';

import '../../../../app/theme.dart';

/// Overview / Transactions tab switcher for the Portfolio screen.
///
/// Uses [ValueNotifier<int>] for tab state — driven by [useState] from the
/// parent [PortfolioScreen] so tab selection survives widget rebuilds.
///
/// Rendered inside [StickyTabBarDelegate] which wraps it in a
/// [SliverPersistentHeader(pinned: true)] so it stays pinned at the top
/// of the [CustomScrollView] during scroll.
class PortfolioTabBar extends StatelessWidget {
  const PortfolioTabBar({
    required this.selectedTab,
    super.key,
  });

  final ValueNotifier<int> selectedTab;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<int>(
      valueListenable: selectedTab,
      builder: (context, current, _) {
        return Container(
          height: 44,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          // Tinted glass-adjacent background — matches the navy AmbientBackground
          // without requiring BackdropFilter in this sticky region.
          color: const Color(0xFF0D1117).withAlpha(230),
          child: Row(
            children: [
              _TabButton(
                label: 'Overview',
                index: 0,
                current: current,
                onTap: () => selectedTab.value = 0,
              ),
              _TabButton(
                label: 'Transactions',
                index: 1,
                current: current,
                onTap: () => selectedTab.value = 1,
              ),
            ],
          ),
        );
      },
    );
  }
}

/// Individual tab button used inside [PortfolioTabBar].
class _TabButton extends StatelessWidget {
  const _TabButton({
    required this.label,
    required this.index,
    required this.current,
    required this.onTap,
  });

  final String label;
  final int index;
  final int current;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isActive = index == current;
    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Container(
          alignment: Alignment.center,
          decoration: BoxDecoration(
            border: Border(
              bottom: BorderSide(
                color: isActive ? AppTheme.bitcoinOrange : Colors.transparent,
                width: 2,
              ),
            ),
          ),
          child: Text(
            label,
            style: TextStyle(
              color: isActive ? AppTheme.bitcoinOrange : Colors.white54,
              fontWeight: isActive ? FontWeight.w600 : FontWeight.normal,
              fontSize: 14,
            ),
          ),
        ),
      ),
    );
  }
}

/// [SliverPersistentHeaderDelegate] that keeps [PortfolioTabBar] pinned at the
/// top of a [CustomScrollView] during scroll.
///
/// Fixed height of 44 pixels — matches [PortfolioTabBar] intrinsic height.
class StickyTabBarDelegate extends SliverPersistentHeaderDelegate {
  const StickyTabBarDelegate({required this.tabBar});

  final PortfolioTabBar tabBar;

  @override
  double get minExtent => 44;

  @override
  double get maxExtent => 44;

  @override
  Widget build(
    BuildContext context,
    double shrinkOffset,
    bool overlapsContent,
  ) {
    // Material with transparent color lets AmbientBackground orbs show through.
    // The tab bar container itself applies a semi-transparent navy tint for
    // legibility without a solid paint-over.
    return Material(
      color: Colors.transparent,
      child: tabBar,
    );
  }

  @override
  bool shouldRebuild(StickyTabBarDelegate oldDelegate) {
    return tabBar != oldDelegate.tabBar;
  }
}

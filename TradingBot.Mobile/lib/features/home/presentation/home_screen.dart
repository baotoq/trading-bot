import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:go_router/go_router.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/widgets/error_snackbar.dart';
import '../../../core/widgets/glass_card.dart';
import '../../../core/widgets/retry_widget.dart';
import '../../../core/widgets/shimmer_loading.dart';
import '../../history/data/history_providers.dart';
import '../../portfolio/data/portfolio_providers.dart';
import '../data/home_providers.dart';
import 'widgets/home_hero_balance_card.dart';
import 'widgets/home_mini_donut_card.dart';
import 'widgets/home_quick_actions_card.dart';
import 'widgets/home_recent_activity_card.dart';

/// Card stagger constants — 4 cards, 50ms stagger, 300ms per card.
const _kStaggerMs = 50.0;
const _kAnimMs = 300.0;
const _kTotalMs = _kAnimMs + _kStaggerMs * 3; // 450ms

/// Produces a per-card [CurvedAnimation] driven by [Interval] timing.
///
/// Each card starts at a staggered offset from the controller's timeline,
/// so the 4 cards fade+slide in sequentially with a 50ms gap.
Animation<double> _cardEntrance(AnimationController ctrl, int index) {
  final start = (index * _kStaggerMs) / _kTotalMs;
  final end = ((index * _kStaggerMs + _kAnimMs) / _kTotalMs).clamp(0.0, 1.0);
  return CurvedAnimation(
    parent: ctrl,
    curve: Interval(start, end, curve: Curves.easeOut),
  );
}

/// Wraps [card] in an [AnimatedBuilder] that fades and slides upward.
///
/// The animation is driven by [ctrl] with a per-card [Interval] offset
/// so sibling cards cascade in sequentially.
Widget _animatedCard(AnimationController ctrl, int index, Widget card) {
  return AnimatedBuilder(
    animation: ctrl,
    builder: (context, child) {
      final anim = _cardEntrance(ctrl, index);
      return FadeTransition(
        opacity: anim,
        child: SlideTransition(
          position: Tween<Offset>(
            begin: const Offset(0, 0.06),
            end: Offset.zero,
          ).animate(anim),
          child: child,
        ),
      );
    },
    child: card,
  );
}

class HomeScreen extends HookConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // --- Provider data ---
    final homeData = ref.watch(homeDataProvider);
    final portfolioData = ref.watch(portfolioPageDataProvider);
    final purchaseHistory = ref.watch(purchaseHistoryProvider);

    // --- Error snackbar listener for homeDataProvider ---
    ref.listen(homeDataProvider, (previous, next) {
      if (next.hasError && !next.isLoading) {
        if (next.error is AuthenticationException) {
          showAuthErrorSnackbar(context);
        } else {
          showErrorSnackbar(context, 'Could not load portfolio data');
        }
      }
    });

    // --- Stagger entrance animation (ANIM-03) ---
    final hasAnimated = useRef(false);
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 450),
    );
    final reduceMotion = GlassCard.shouldReduceMotion(context);

    useEffect(() {
      if (!hasAnimated.value) {
        hasAnimated.value = true;
        if (reduceMotion) {
          // Snap to final state — no lateral motion for accessibility
          controller.value = 1.0;
        } else {
          controller.forward();
        }
      }
      return null;
    }, [reduceMotion]);

    // --- Count-up animation guard (ANIM-02) ---
    // shouldAnimateCountUp starts true; flips to false after first data load.
    // This prevents the count-up from replaying on every 30-second auto-refresh.
    final shouldAnimateCountUp = useRef(true);
    final animateCountUp = shouldAnimateCountUp.value && !reduceMotion;

    // Mark count-up as done once homeData has a value for the first time.
    if (shouldAnimateCountUp.value && homeData.hasValue) {
      shouldAnimateCountUp.value = false;
    }

    // --- Quick action handler ---
    void handleActionTap(String action) {
      switch (action) {
        case 'bot-detail':
          context.push('/home/bot-detail');
        case 'chart':
          StatefulNavigationShell.of(context).goBranch(1);
        case 'history':
          StatefulNavigationShell.of(context).goBranch(2);
      }
    }

    // --- Derive data for cards ---
    final homeValue = homeData.value;
    final portfolioValue = homeValue?.portfolio.currentPrice != null &&
            homeValue?.portfolio.totalBtc != null
        ? (homeValue!.portfolio.currentPrice! * homeValue.portfolio.totalBtc)
        : (homeValue?.portfolio.totalCost ?? 0.0);

    final pnlAbsolute = homeValue?.portfolio.unrealizedPnl ?? 0.0;
    final pnlPercent = homeValue?.portfolio.unrealizedPnlPercent;
    final healthStatus = homeValue?.status.healthStatus;
    final healthMessage = homeValue?.status.healthMessage;

    final allocations =
        portfolioData.value?.summary.allocations ?? const [];

    final recentPurchases = purchaseHistory.value?.take(3).toList() ?? const [];

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(homeDataProvider);
          ref.invalidate(portfolioPageDataProvider);
          ref.invalidate(purchaseHistoryProvider);
        },
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              backgroundColor: Colors.transparent,
              title: const Text('Home'),
              floating: true,
              snap: true,
            ),
            // Error full-screen — only when no cached data
            if (homeData.hasError && homeValue == null)
              SliverFillRemaining(
                child: RetryWidget(
                  onRetry: () => ref.invalidate(homeDataProvider),
                ),
              )
            // Loading skeleton — only on first load (no cached data yet)
            else if (homeData.isLoading && homeValue == null)
              SliverToBoxAdapter(child: _buildLoadingSkeleton())
            // Loaded state (or stale + error with cached data)
            else
              SliverList.list(
                children: [
                  _animatedCard(
                    controller,
                    0,
                    HomeHeroBalanceCard(
                      portfolioValue: portfolioValue,
                      pnlAbsolute: pnlAbsolute,
                      pnlPercentage: pnlPercent,
                      shouldAnimate: animateCountUp,
                      healthStatus: healthStatus,
                      healthMessage: healthMessage,
                    ),
                  ),
                  _animatedCard(
                    controller,
                    1,
                    HomeMiniDonutCard(
                      allocations: allocations,
                      isVnd: false,
                    ),
                  ),
                  _animatedCard(
                    controller,
                    2,
                    HomeRecentActivityCard(
                      purchases: recentPurchases,
                    ),
                  ),
                  _animatedCard(
                    controller,
                    3,
                    HomeQuickActionsCard(
                      onActionTap: handleActionTap,
                    ),
                  ),
                ],
              ),
          ],
        ),
      ),
    );
  }

  /// Skeleton loading state — 4 GlassCard shapes matching the real layout.
  Widget _buildLoadingSkeleton() {
    return AppShimmer(
      enabled: true,
      child: Column(
        children: [
          // Hero balance card skeleton
          GlassCard(
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Bone.text(words: 2, fontSize: 13),
                const SizedBox(height: 8),
                Bone.text(words: 1, fontSize: 32),
                const SizedBox(height: 8),
                Bone.text(words: 3, fontSize: 14),
              ],
            ),
          ),
          // Mini donut card skeleton
          GlassCard(
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Bone.text(words: 1, fontSize: 14),
                const SizedBox(height: 12),
                const Bone(width: double.infinity, height: 140),
                const SizedBox(height: 10),
                Bone.text(words: 3, fontSize: 12),
              ],
            ),
          ),
          // Recent activity card skeleton
          GlassCard(
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Bone.text(words: 2, fontSize: 14),
                const SizedBox(height: 8),
                for (int i = 0; i < 3; i++) ...[
                  if (i > 0) const SizedBox(height: 8),
                  Bone(
                    width: double.infinity,
                    height: 72,
                    borderRadius: BorderRadius.circular(10),
                  ),
                ],
              ],
            ),
          ),
          // Quick actions card skeleton
          GlassCard(
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Bone.text(words: 2, fontSize: 14),
                const SizedBox(height: 12),
                Row(
                  children: [
                    for (int i = 0; i < 3; i++) ...[
                      if (i > 0) const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          children: [
                            const Bone(width: 28, height: 28),
                            const SizedBox(height: 6),
                            Bone.text(words: 1, fontSize: 12),
                          ],
                        ),
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

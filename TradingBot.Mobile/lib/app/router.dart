import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../features/chart/presentation/chart_screen.dart';
import '../features/config/presentation/config_screen.dart';
import '../features/history/presentation/history_screen.dart';
import '../features/home/presentation/dca_bot_detail_screen.dart';
import '../features/home/presentation/home_screen.dart';
import '../features/portfolio/presentation/portfolio_screen.dart';
import '../features/portfolio/presentation/sub_screens/add_transaction_screen.dart';
import '../features/portfolio/presentation/sub_screens/edit_fixed_deposit_screen.dart';
import '../features/portfolio/presentation/sub_screens/fixed_deposit_detail_screen.dart';
import '../features/portfolio/presentation/sub_screens/transaction_history_screen.dart';
import '../shared/navigation_shell.dart';

final GlobalKey<NavigatorState> rootNavigatorKey =
    GlobalKey<NavigatorState>(debugLabel: 'root');
final GlobalKey<NavigatorState> _homeNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'home');
final GlobalKey<NavigatorState> _chartNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'chart');
final GlobalKey<NavigatorState> _historyNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'history');
final GlobalKey<NavigatorState> _configNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'config');
final GlobalKey<NavigatorState> _portfolioNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'portfolio');

/// Reusable fade+scale page transition for all routes (ANIM-05).
CustomTransitionPage<void> fadeScalePage({
  required LocalKey key,
  required Widget child,
}) {
  return CustomTransitionPage<void>(
    key: key,
    child: child,
    transitionDuration: const Duration(milliseconds: 200),
    reverseTransitionDuration: const Duration(milliseconds: 200),
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      return FadeTransition(
        opacity: CurvedAnimation(parent: animation, curve: Curves.easeOut),
        child: ScaleTransition(
          scale: Tween<double>(begin: 0.95, end: 1.0).animate(
            CurvedAnimation(parent: animation, curve: Curves.easeOut),
          ),
          child: child,
        ),
      );
    },
  );
}

final GoRouter appRouter = GoRouter(
  navigatorKey: rootNavigatorKey,
  initialLocation: '/home',
  routes: [
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) =>
          ScaffoldWithNavigation(navigationShell: navigationShell),
      branches: [
        StatefulShellBranch(
          navigatorKey: _homeNavKey,
          routes: [
            GoRoute(
              path: '/home',
              pageBuilder: (context, state) => fadeScalePage(
                key: state.pageKey,
                child: const HomeScreen(),
              ),
              routes: [
                GoRoute(
                  path: 'bot-detail',
                  parentNavigatorKey: rootNavigatorKey,
                  pageBuilder: (context, state) => fadeScalePage(
                    key: state.pageKey,
                    child: const DcaBotDetailScreen(),
                  ),
                ),
              ],
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _chartNavKey,
          routes: [
            GoRoute(
              path: '/chart',
              pageBuilder: (context, state) => fadeScalePage(
                key: state.pageKey,
                child: const ChartScreen(),
              ),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _historyNavKey,
          routes: [
            GoRoute(
              path: '/history',
              pageBuilder: (context, state) => fadeScalePage(
                key: state.pageKey,
                child: const HistoryScreen(),
              ),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _configNavKey,
          routes: [
            GoRoute(
              path: '/config',
              pageBuilder: (context, state) => fadeScalePage(
                key: state.pageKey,
                child: const ConfigScreen(),
              ),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _portfolioNavKey,
          routes: [
            GoRoute(
              path: '/portfolio',
              pageBuilder: (context, state) => fadeScalePage(
                key: state.pageKey,
                child: const PortfolioScreen(),
              ),
              routes: [
                GoRoute(
                  path: 'add-transaction',
                  parentNavigatorKey: rootNavigatorKey,
                  pageBuilder: (context, state) => fadeScalePage(
                    key: state.pageKey,
                    child: const AddTransactionScreen(),
                  ),
                ),
                GoRoute(
                  path: 'transaction-history',
                  parentNavigatorKey: rootNavigatorKey,
                  pageBuilder: (context, state) => fadeScalePage(
                    key: state.pageKey,
                    child: const TransactionHistoryScreen(),
                  ),
                ),
                GoRoute(
                  path: 'fixed-deposit/:id',
                  parentNavigatorKey: rootNavigatorKey,
                  pageBuilder: (context, state) => fadeScalePage(
                    key: state.pageKey,
                    child: FixedDepositDetailScreen(
                      id: state.pathParameters['id']!,
                    ),
                  ),
                  routes: [
                    GoRoute(
                      path: 'edit',
                      parentNavigatorKey: rootNavigatorKey,
                      pageBuilder: (context, state) => fadeScalePage(
                        key: state.pageKey,
                        child: EditFixedDepositScreen(
                          id: state.pathParameters['id']!,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ],
    ),
  ],
);

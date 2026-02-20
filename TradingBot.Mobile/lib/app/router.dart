import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../features/chart/presentation/chart_screen.dart';
import '../features/config/presentation/config_screen.dart';
import '../features/history/presentation/history_screen.dart';
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
              builder: (context, state) => const HomeScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _chartNavKey,
          routes: [
            GoRoute(
              path: '/chart',
              builder: (context, state) => const ChartScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _historyNavKey,
          routes: [
            GoRoute(
              path: '/history',
              builder: (context, state) => const HistoryScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _configNavKey,
          routes: [
            GoRoute(
              path: '/config',
              builder: (context, state) => const ConfigScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _portfolioNavKey,
          routes: [
            GoRoute(
              path: '/portfolio',
              builder: (context, state) => const PortfolioScreen(),
              routes: [
                GoRoute(
                  path: 'add-transaction',
                  parentNavigatorKey: rootNavigatorKey,
                  builder: (_, __) => const AddTransactionScreen(),
                ),
                GoRoute(
                  path: 'transaction-history',
                  parentNavigatorKey: rootNavigatorKey,
                  builder: (_, __) => const TransactionHistoryScreen(),
                ),
                GoRoute(
                  path: 'fixed-deposit/:id',
                  parentNavigatorKey: rootNavigatorKey,
                  builder: (context, state) => FixedDepositDetailScreen(
                    id: state.pathParameters['id']!,
                  ),
                  routes: [
                    GoRoute(
                      path: 'edit',
                      parentNavigatorKey: rootNavigatorKey,
                      builder: (context, state) => EditFixedDepositScreen(
                        id: state.pathParameters['id']!,
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

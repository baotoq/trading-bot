import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../features/chart/presentation/chart_screen.dart';
import '../features/config/presentation/config_screen.dart';
import '../features/history/presentation/history_screen.dart';
import '../features/home/presentation/home_screen.dart';
import '../shared/navigation_shell.dart';

final GlobalKey<NavigatorState> _rootNavigatorKey =
    GlobalKey<NavigatorState>(debugLabel: 'root');
final GlobalKey<NavigatorState> _homeNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'home');
final GlobalKey<NavigatorState> _chartNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'chart');
final GlobalKey<NavigatorState> _historyNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'history');
final GlobalKey<NavigatorState> _configNavKey =
    GlobalKey<NavigatorState>(debugLabel: 'config');

final GoRouter appRouter = GoRouter(
  navigatorKey: _rootNavigatorKey,
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
      ],
    ),
  ],
);

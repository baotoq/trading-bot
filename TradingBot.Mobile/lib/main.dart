import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:hooks_riverpod/hooks_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app/router.dart';
import 'app/theme.dart';
import 'core/api/api_client.dart';
import 'core/services/fcm_service.dart';
import 'features/portfolio/data/currency_provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();
  final prefs = await SharedPreferences.getInstance();

  runApp(
    ProviderScope(
      overrides: [sharedPreferencesProvider.overrideWithValue(prefs)],
      child: const TradingBotApp(),
    ),
  );
}

class TradingBotApp extends ConsumerStatefulWidget {
  const TradingBotApp({super.key});

  @override
  ConsumerState<TradingBotApp> createState() => _TradingBotAppState();
}

class _TradingBotAppState extends ConsumerState<TradingBotApp> {
  @override
  void initState() {
    super.initState();
    // Initialize FCM after the first frame so Dio provider is available
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final dio = ref.read(dioProvider);
      FcmService(dio).initialize();
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Trading Bot',
      theme: AppTheme.dark,
      routerConfig: appRouter,
      debugShowCheckedModeBanner: false,
    );
  }
}

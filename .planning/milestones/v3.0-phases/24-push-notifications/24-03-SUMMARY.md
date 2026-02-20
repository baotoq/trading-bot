# Plan 24-03 Summary: Flutter FCM Integration

## Status: COMPLETE (code only -- requires manual Firebase setup for testing)

## What was built

1. **Firebase dependencies** - Added `firebase_core` and `firebase_messaging` packages to pubspec.yaml.

2. **FcmService** (`lib/core/services/fcm_service.dart`) - Central service handling all FCM operations:
   - Registers top-level background message handler (`_firebaseMessagingBackgroundHandler`)
   - Requests notification permission (iOS native dialog)
   - Configures foreground notification presentation (banners, badges, sounds)
   - Gets and registers FCM token with backend via `POST /api/devices/register`
   - Listens for token refresh and re-registers automatically
   - Handles foreground messages (displayed as system banners via iOS presentation options)
   - Handles notification taps from background state via `onMessageOpenedApp`
   - Handles notification taps from terminated state via `getInitialMessage`
   - Deep-links to the correct screen using `appRouter.go(route)` from the notification data payload

3. **main.dart updated** - Changed `TradingBotApp` from `StatelessWidget` to `ConsumerStatefulWidget` to access the Dio provider. Firebase is initialized in `main()` before `runApp()`. FcmService is initialized in `initState` via `addPostFrameCallback` to ensure the widget tree is built first.

4. **Router updated** (`lib/app/router.dart`) - Renamed `_rootNavigatorKey` to `rootNavigatorKey` (public) so FcmService can access the router for deep-link navigation.

5. **AppDelegate.swift updated** - Added `import FirebaseCore`, `import FirebaseMessaging`, `FirebaseApp.configure()`, `UNUserNotificationCenter.current().delegate = self`, and `application.registerForRemoteNotifications()`.

6. **Podfile updated** - Uncommented and set `platform :ios, '13.0'` for Firebase compatibility.

## Manual setup required before testing

1. Create Firebase project and register iOS app at https://console.firebase.google.com/
2. Download `GoogleService-Info.plist` and place in `TradingBot.Mobile/ios/Runner/`
3. Upload APNs Authentication Key (.p8) to Firebase Console
4. Enable Push Notifications capability in Xcode (Runner target -> Signing & Capabilities)
5. Enable Background Modes -> Remote notifications in Xcode
6. Set Firebase service account key in .NET user secrets:
   `dotnet user-secrets set "Firebase:ServiceAccountKeyJson" '<json>'`

## Files created
- `TradingBot.Mobile/lib/core/services/fcm_service.dart`

## Files modified
- `TradingBot.Mobile/pubspec.yaml`
- `TradingBot.Mobile/lib/main.dart`
- `TradingBot.Mobile/lib/app/router.dart`
- `TradingBot.Mobile/ios/Runner/AppDelegate.swift`
- `TradingBot.Mobile/ios/Podfile`

## Verification
- `dart analyze lib/` passes with no issues
- `flutter pub get` resolves all dependencies

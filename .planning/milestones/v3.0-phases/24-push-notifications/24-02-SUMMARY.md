# Plan 24-02 Summary: Backend Notification Hooks

## Status: COMPLETE

## What was built

1. **PurchaseCompletedHandler FCM integration** - Added `FcmNotificationService` to constructor. After Telegram notification, sends FCM push with title "BTC Purchased" (or "[SIM] BTC Purchased" for dry runs), body showing quantity/price/multiplier, and data payload `{type: "purchase_completed", route: "/history"}`.

2. **PurchaseFailedHandler FCM integration** - Added `FcmNotificationService` to constructor. After Telegram notification, sends FCM push with title "Purchase Failed", body showing error message, and data payload `{type: "purchase_failed", route: "/home"}`.

3. **MissedPurchaseVerificationService enhancements**:
   - Added FCM notification alongside existing Telegram alert for daily missed purchases with data payload `{type: "missed_purchase", route: "/home"}`.
   - Added 36-hour gap detection (`CheckGapAlertAsync`) that runs independently of the daily verification window. When no real purchase has occurred in over 36 hours, sends both Telegram and FCM alerts with title "No Purchase in 36+ Hours". Uses `_lastGapAlertDate` to prevent duplicate alerts on the same day.

## Key design decisions

- FCM notifications have separate try/catch blocks from Telegram, so one failure does not block the other.
- All push notification data payloads include `type` and `route` keys for Flutter deep-link navigation.
- The 36-hour gap check runs at the beginning of `ProcessAsync`, before the daily verification window check, so it catches gaps regardless of the daily schedule.

## Files modified
- `TradingBot.ApiService/Application/Handlers/PurchaseCompletedHandler.cs`
- `TradingBot.ApiService/Application/Handlers/PurchaseFailedHandler.cs`
- `TradingBot.ApiService/Application/BackgroundJobs/MissedPurchaseVerificationService.cs`

## Verification
- `dotnet build TradingBot.slnx` succeeds
- `dotnet test` passes all 62 tests

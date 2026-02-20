# Plan 24-01 Summary: Backend FCM Infrastructure

## Status: COMPLETE

## What was built

1. **DeviceToken entity** (`Models/DeviceToken.cs`) - Simple entity inheriting from `BaseEntity<DeviceTokenId>` with `Token` (FCM registration token string) and `Platform` ("ios"/"android") properties.

2. **DeviceTokenId Vogen type** (`Models/Ids/DeviceTokenId.cs`) - Strongly-typed ID using UUIDv7, following the same pattern as PurchaseId.

3. **EF Core migration** - `AddDeviceToken` migration creates `DeviceTokens` table with unique index on `Token` column. DbContext updated with `DeviceTokens` DbSet, Vogen converter registration, and entity configuration.

4. **FirebaseAdmin SDK integration** (`Infrastructure/Firebase/FirebaseServiceCollectionExtensions.cs`) - DI extension method `AddFirebase()` that initializes FirebaseApp with service account credentials from configuration. Gracefully handles missing credentials for dev mode.

5. **FcmNotificationService** (`Infrastructure/Firebase/FcmNotificationService.cs`) - Scoped service that sends multicast push notifications to all registered device tokens. Automatically cleans up stale/unregistered tokens when FCM reports them as `Unregistered`. Gracefully no-ops when Firebase is not configured.

6. **Device REST endpoints** (`Endpoints/DeviceEndpoints.cs`):
   - `POST /api/devices/register` - Upserts a device FCM token (creates new or updates existing)
   - `DELETE /api/devices/{token}` - Removes a device token (idempotent, returns 204)
   - Both protected by `ApiKeyEndpointFilter` (x-api-key header)

## Files created
- `TradingBot.ApiService/Models/Ids/DeviceTokenId.cs`
- `TradingBot.ApiService/Models/DeviceToken.cs`
- `TradingBot.ApiService/Infrastructure/Firebase/FirebaseServiceCollectionExtensions.cs`
- `TradingBot.ApiService/Infrastructure/Firebase/FcmNotificationService.cs`
- `TradingBot.ApiService/Endpoints/DeviceEndpoints.cs`

## Files modified
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - Added DeviceTokens DbSet, Vogen converter, entity configuration
- `TradingBot.ApiService/TradingBot.ApiService.csproj` - Added FirebaseAdmin NuGet package
- `TradingBot.ApiService/Program.cs` - Added Firebase DI registration and device endpoint mapping

## Verification
- `dotnet build TradingBot.slnx` succeeds
- `dotnet test` passes all 62 tests
- EF migration `AddDeviceToken` generated successfully

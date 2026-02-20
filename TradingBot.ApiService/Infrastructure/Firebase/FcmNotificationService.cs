using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Infrastructure.Firebase;

public class FcmNotificationService(
    TradingBotDbContext dbContext,
    ILogger<FcmNotificationService> logger)
{
    public async Task SendToAllDevicesAsync(
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await dbContext.DeviceTokens
            .Select(d => d.Token)
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            logger.LogInformation("No device tokens registered, skipping push notification");
            return;
        }

        if (FirebaseMessaging.DefaultInstance is null)
        {
            logger.LogWarning("Firebase not configured, skipping push notification");
            return;
        }

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data,
            Apns = new ApnsConfig
            {
                Aps = new Aps { Sound = "default" }
            }
        };

        logger.LogInformation("Sending FCM push to {TokenCount} devices: {Title}", tokens.Count, title);

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, ct);

        // Clean up stale/unregistered tokens
        var tokensToRemove = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var sendResponse = response.Responses[i];
            if (!sendResponse.IsSuccess &&
                sendResponse.Exception is FirebaseMessagingException fmEx &&
                fmEx.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                tokensToRemove.Add(tokens[i]);
            }
        }

        if (tokensToRemove.Count > 0)
        {
            var staleDevices = await dbContext.DeviceTokens
                .Where(d => tokensToRemove.Contains(d.Token))
                .ToListAsync(ct);

            dbContext.DeviceTokens.RemoveRange(staleDevices);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("Removed {Count} stale device tokens", tokensToRemove.Count);
        }

        logger.LogInformation(
            "FCM push sent: {SuccessCount} succeeded, {FailureCount} failed",
            response.SuccessCount,
            response.FailureCount);
    }
}

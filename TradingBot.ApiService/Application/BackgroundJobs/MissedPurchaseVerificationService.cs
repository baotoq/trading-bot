using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Telegram;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.BackgroundJobs;

/// <summary>
/// Background service for missed purchase detection.
/// Checks ~40 minutes after execution window and sends Telegram alert with diagnostics if no purchase recorded.
/// </summary>
public class MissedPurchaseVerificationService(
    ILogger<MissedPurchaseVerificationService> logger,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<DcaOptions> dcaOptions) : TimeBackgroundService(logger)
{
    private DateOnly _lastAlertSent = DateOnly.MinValue;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var options = dcaOptions.CurrentValue;

        // Calculate the expected purchase time for today
        var targetTime = new TimeOnly(options.DailyBuyHour, options.DailyBuyMinute);
        var todayTarget = new DateTime(now.Year, now.Month, now.Day, targetTime.Hour, targetTime.Minute, 0, DateTimeKind.Utc);

        // Verification time: target + 10 min execution window + 30 min grace = target + 40 minutes
        var verificationTime = todayTarget.AddMinutes(40);

        // If current UTC time is before verification time, return early
        if (now < verificationTime)
        {
            return;
        }

        // If current UTC time is more than 2 hours past verification time, return early (avoid stale alerts)
        if (now > verificationTime.AddHours(2))
        {
            return;
        }

        // Prevent duplicate alerts for the same day
        var today = DateOnly.FromDateTime(now);
        if (_lastAlertSent == today)
        {
            return;
        }

        try
        {
            logger.LogInformation("Checking for missed purchase on {Date}", today);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
            var telegramService = scope.ServiceProvider.GetRequiredService<TelegramNotificationService>();

            // Check if a purchase exists today (any status Filled/PartiallyFilled, IsDryRun=false)
            var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var todayEnd = todayStart.AddDays(1);

            var todayPurchase = await dbContext.Purchases
                .Where(p => p.ExecutedAt >= todayStart && p.ExecutedAt < todayEnd)
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .Where(p => !p.IsDryRun)
                .FirstOrDefaultAsync(cancellationToken);

            if (todayPurchase != null)
            {
                logger.LogInformation("Purchase found for {Date}, no alert needed", today);
                return;
            }

            // Missed purchase detected - check for failed/skipped records for diagnostics
            logger.LogWarning("No successful purchase found for {Date}, checking diagnostics", today);

            var failedToday = await dbContext.Purchases
                .Where(p => p.ExecutedAt >= todayStart && p.ExecutedAt < todayEnd)
                .Where(p => p.Status == PurchaseStatus.Failed)
                .OrderByDescending(p => p.ExecutedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Build diagnostic reasoning
            var diagnostic = failedToday != null
                ? $"Order was attempted but failed: {failedToday.FailureReason}"
                : "No purchase attempt detected. Scheduler may not have triggered or was skipped.";

            // Send Telegram alert
            var message = $"""
                *⚠️ Missed Purchase Alert*

                No successful BTC purchase recorded today.
                Expected by: `{verificationTime:HH:mm}` UTC

                *Diagnosis:* {diagnostic}

                Please check logs for more details.

                _{now:yyyy-MM-dd HH:mm:ss} UTC_
                """;

            await telegramService.SendMessageAsync(message, cancellationToken);

            _lastAlertSent = today;

            logger.LogWarning("Missed purchase alert sent for {Date}", today);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for missed purchase");
        }
    }
}

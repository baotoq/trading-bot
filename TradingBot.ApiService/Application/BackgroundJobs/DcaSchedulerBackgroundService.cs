using MediatR;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Hyperliquid.Models;

namespace TradingBot.ApiService.Application.BackgroundJobs;

/// <summary>
/// Daily DCA scheduler that triggers purchases at configured time.
/// Extends TimeBackgroundService to run on 5-minute intervals.
/// Only executes within 10-minute window after target time.
/// Retries transient failures with exponential backoff + jitter.
/// </summary>
public class DcaSchedulerBackgroundService(
    ILogger<DcaSchedulerBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<DcaOptions> dcaOptions
) : TimeBackgroundService(logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);
    private const int MaxRetries = 3;
    private static readonly TimeSpan ExecutionWindow = TimeSpan.FromMinutes(10);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var options = dcaOptions.CurrentValue;
        var targetTime = new TimeOnly(options.DailyBuyHour, options.DailyBuyMinute);
        var todayTarget = new DateTimeOffset(
            DateOnly.FromDateTime(now.UtcDateTime.Date).ToDateTime(targetTime),
            TimeSpan.Zero
        );

        // Check if within execution window (target time to target time + 10 minutes)
        if (now < todayTarget || now >= todayTarget.Add(ExecutionWindow))
        {
            // Not within execution window — skip silently (no log spam every 5 min)
            return;
        }

        logger.LogInformation("DCA execution window reached: {TargetTime} UTC", targetTime);

        // Create scope and resolve services
        await using var scope = scopeFactory.CreateAsyncScope();
        var dcaService = scope.ServiceProvider.GetRequiredService<IDcaExecutionService>();
        var purchaseDate = DateOnly.FromDateTime(now.UtcDateTime.Date);

        // Retry loop with exponential backoff + jitter
        var retryCount = 0;
        while (retryCount <= MaxRetries)
        {
            try
            {
                await dcaService.ExecuteDailyPurchaseAsync(purchaseDate, cancellationToken);
                logger.LogInformation("DCA execution completed for {Date}", purchaseDate);
                return; // Success — exit retry loop
            }
            catch (HyperliquidApiException ex) when (ex.StatusCode.HasValue && ex.StatusCode.Value >= 400 && ex.StatusCode.Value < 500)
            {
                // Permanent error (4xx) — fail immediately, no retry
                // Purchase-level failures are handled inside DcaExecutionService.
                // Scheduler-level infrastructure errors are logged only (no Purchase aggregate exists here).
                logger.LogError(ex, "Permanent error during DCA execution for {Date}, not retrying", purchaseDate);
                return;
            }
            catch (Exception ex) when (retryCount < MaxRetries)
            {
                retryCount++;
                // Exponential backoff: 2^retry * 1s + jitter (0-500ms)
                var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                var delay = baseDelay + jitter;

                logger.LogWarning(ex, "DCA execution failed for {Date}, retry {Retry}/{MaxRetries} in {Delay}",
                    purchaseDate, retryCount, MaxRetries, delay);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                // Final retry exhausted — log only, no Purchase aggregate exists at scheduler level
                logger.LogError(ex, "DCA execution failed for {Date} after {MaxRetries} retries", purchaseDate, MaxRetries);
                return;
            }
        }
    }
}

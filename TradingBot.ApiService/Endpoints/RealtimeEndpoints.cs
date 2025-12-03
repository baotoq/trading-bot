using Microsoft.AspNetCore.Mvc;
using TradingBot.ApiService.Application.Requests;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Endpoints;

public static class RealtimeEndpoints
{
    public static RouteGroupBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/realtime")
            .WithTags("Real-time Monitoring");

        group.MapPost("/start", StartMonitoring)
            .WithName("StartMonitoring")
            .WithSummary("Start real-time candle monitoring and signal generation for a symbol");

        group.MapPost("/stop", StopMonitoring)
            .WithName("StopMonitoring")
            .WithSummary("Stop real-time monitoring for a symbol");

        group.MapGet("/status", GetMonitoringStatus)
            .WithName("GetMonitoringStatus")
            .WithSummary("Get all active monitoring sessions");

        group.MapPost("/test-telegram", TestTelegramNotification)
            .WithName("TestTelegramNotification")
            .WithSummary("Send a test message to Telegram");

        return group;
    }

    private static async Task<IResult> StartMonitoring(
        [FromBody] MonitoringRequest request,
        IRealtimeCandleService candleService,
        ISignalGeneratorService signalGenerator,
        ILogger<RealtimeEndpoints> logger)
    {
        try
        {
            // Start real-time candle monitoring
            await candleService.StartMonitoringAsync(request.Symbol, request.Interval);

            // Enable signal notifications
            await signalGenerator.EnableSignalNotificationsAsync(request.Symbol, request.Strategy);

            logger.LogInformation(
                "Started monitoring {Symbol} on {Interval} with {Strategy}",
                request.Symbol, request.Interval, request.Strategy);

            return Results.Ok(new
            {
                Success = true,
                Message = $"Started monitoring {request.Symbol} on {request.Interval} interval with {request.Strategy} strategy",
                Symbol = request.Symbol,
                Interval = request.Interval,
                Strategy = request.Strategy
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start monitoring for {Symbol}", request.Symbol);
            return Results.BadRequest(new
            {
                Success = false,
                Message = $"Failed to start monitoring: {ex.Message}"
            });
        }
    }

    private static async Task<IResult> StopMonitoring(
        [FromBody] StopMonitoringRequest request,
        IRealtimeCandleService candleService,
        ISignalGeneratorService signalGenerator,
        ILogger<RealtimeEndpoints> logger)
    {
        try
        {
            // Stop candle monitoring
            await candleService.StopMonitoringAsync(request.Symbol, request.Interval);

            // Disable signal notifications
            await signalGenerator.DisableSignalNotificationsAsync(request.Symbol);

            logger.LogInformation("Stopped monitoring {Symbol} on {Interval}", request.Symbol, request.Interval);

            return Results.Ok(new
            {
                Success = true,
                Message = $"Stopped monitoring {request.Symbol} on {request.Interval} interval",
                Symbol = request.Symbol,
                Interval = request.Interval
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop monitoring for {Symbol}", request.Symbol);
            return Results.BadRequest(new
            {
                Success = false,
                Message = $"Failed to stop monitoring: {ex.Message}"
            });
        }
    }

    private static IResult GetMonitoringStatus(
        IRealtimeCandleService candleService,
        ISignalGeneratorService signalGenerator)
    {
        var activeMonitors = candleService.GetActiveMonitors();
        var enabledNotifications = signalGenerator.GetEnabledNotifications();

        var status = activeMonitors.Select(m => new
        {
            Symbol = m.Symbol,
            Interval = m.Interval,
            IsMonitoring = true,
            IsNotificationEnabled = enabledNotifications.ContainsKey(m.Symbol),
            Strategy = enabledNotifications.TryGetValue(m.Symbol, out var strategy) ? strategy : null
        }).ToList();

        return Results.Ok(new
        {
            TotalActiveMonitors = activeMonitors.Count,
            Monitors = status
        });
    }

    private static async Task<IResult> TestTelegramNotification(
        ITelegramNotificationService telegramService,
        ILogger<RealtimeEndpoints> logger)
    {
        try
        {
            await telegramService.SendMessageAsync(
                "🤖 <b>Test Message</b>\n\nYour Telegram notification service is working correctly!\n\n<i>This is a test message from your Trading Bot.</i>");

            logger.LogInformation("Test Telegram notification sent");

            return Results.Ok(new
            {
                Success = true,
                Message = "Test notification sent successfully. Check your Telegram."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send test Telegram notification");
            return Results.BadRequest(new
            {
                Success = false,
                Message = $"Failed to send test notification: {ex.Message}"
            });
        }
    }
}

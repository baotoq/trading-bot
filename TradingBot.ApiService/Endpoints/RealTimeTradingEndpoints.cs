using Microsoft.AspNetCore.Mvc;
using TradingBot.ApiService.Services.RealTimeTrading;

namespace TradingBot.ApiService.Endpoints;

public static class RealTimeTradingEndpoints
{
    public static void MapRealTimeTradingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/realtime")
            .WithTags("Real-Time Trading");

        // Start monitoring a symbol
        group.MapPost("/monitor/start", async (
                [FromBody] StartMonitoringRequest request,
                [FromServices] IRealTimeTradingService tradingService) =>
            {
                var success = await tradingService.StartMonitoringAsync(
                    request.Symbol,
                    request.Interval,
                    request.Strategy,
                    request.AutoTrade);

                return success
                    ? Results.Ok(new
                    {
                        success = true,
                        message = $"Started monitoring {request.Symbol} with {request.Strategy} strategy",
                        symbol = request.Symbol,
                        interval = request.Interval,
                        strategy = request.Strategy,
                        autoTrade = request.AutoTrade
                    })
                    : Results.BadRequest(new
                    {
                        success = false, message = $"Failed to start monitoring {request.Symbol}"
                    });
            })
            .WithName("StartMonitoring")
            .WithSummary("Start real-time monitoring of a symbol")
            .WithDescription(
                "Connects to Binance WebSocket and monitors price changes, generating trading signals based on the selected strategy.");

        // Stop monitoring a symbol
        group.MapPost("/monitor/stop/{symbol}", async (
                string symbol,
                [FromServices] IRealTimeTradingService tradingService) =>
            {
                var success = await tradingService.StopMonitoringAsync(symbol);

                return success
                    ? Results.Ok(new { success = true, message = $"Stopped monitoring {symbol}" })
                    : Results.BadRequest(new { success = false, message = $"Failed to stop monitoring {symbol}" });
            })
            .WithName("StopMonitoring")
            .WithSummary("Stop monitoring a symbol");

        // Get all active monitoring sessions
        group.MapGet("/monitor/active", ([FromServices] IRealTimeTradingService tradingService) =>
            {
                var sessions = tradingService.GetActiveMonitoringSessions();
                return Results.Ok(new
                {
                    success = true,
                    count = sessions.Count,
                    sessions = sessions.Select(s => new
                    {
                        s.Symbol,
                        s.Interval,
                        s.StrategyName,
                        s.AutoTrade,
                        s.StartTime,
                        s.SignalsGenerated,
                        s.TradesExecuted,
                        LatestSignal = s.LatestSignal != null
                            ? new
                            {
                                s.LatestSignal.Type,
                                s.LatestSignal.Price,
                                s.LatestSignal.Confidence,
                                s.LatestSignal.Reason,
                                s.LatestSignal.Timestamp
                            }
                            : null
                    })
                });
            })
            .WithName("GetActiveMonitoring")
            .WithSummary("Get all active monitoring sessions");

        // Get latest signals
        group.MapGet("/signals", ([FromServices] IRealTimeTradingService tradingService) =>
            {
                var signals = tradingService.GetLatestSignals();
                return Results.Ok(new
                {
                    success = true,
                    count = signals.Count,
                    signals = signals.Select(kvp => new
                    {
                        Symbol = kvp.Key,
                        kvp.Value.Type,
                        kvp.Value.Price,
                        kvp.Value.Confidence,
                        kvp.Value.Strategy,
                        kvp.Value.Reason,
                        kvp.Value.Timestamp,
                        kvp.Value.Indicators
                    })
                });
            })
            .WithName("GetLatestSignals")
            .WithSummary("Get latest trading signals for all monitored symbols");
    }
}

public record StartMonitoringRequest
{
    public string Symbol { get; init; } = "BTCUSDT";
    public string Interval { get; init; } = "15m";
    public string Strategy { get; init; } = "RSI";
    public bool AutoTrade { get; init; } = false;
}


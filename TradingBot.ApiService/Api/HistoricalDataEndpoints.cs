using MediatR;
using TradingBot.ApiService.Application.HistoricalData;

namespace TradingBot.ApiService.Api;

public static class HistoricalDataEndpoints
{
    public static IEndpointRouteBuilder MapHistoricalDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/historical-data")
            .WithTags("Historical Data");

        group.MapGet("/cached", GetCachedCandles)
            .WithName("GetCachedCandles")
            .WithSummary("Get historical candles from database cache");

        group.MapGet("/cache-stats", GetCacheStats)
            .WithName("GetCacheStats")
            .WithSummary("Get statistics about cached historical data");

        return app;
    }

    private static async Task<IResult> GetCachedCandles(
        string symbol,
        string interval,
        DateTime? startTime,
        DateTime? endTime,
        int? limit,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetCachedCandles.Query(
            symbol,
            interval,
            startTime,
            endTime,
            limit));

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCacheStats(IMediator mediator)
    {
        var result = await mediator.Send(new GetCacheStats.Query());
        return Results.Ok(result);
    }
}


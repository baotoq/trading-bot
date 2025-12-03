using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Endpoints;

public static class MarketEndpoints
{
    public static RouteGroupBuilder MapMarketEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/market")
            .WithTags("Market Analysis");

        group.MapGet("/condition/{symbol}", GetMarketCondition)
            .WithName("GetMarketCondition")
            .WithSummary("Get current market condition and trading permission");

        return group;
    }

    private static async Task<IResult> GetMarketCondition(
        Symbol symbol,
        IMarketAnalysisService marketAnalysis)
    {
        var condition = await marketAnalysis.AnalyzeMarketConditionAsync(symbol);
        return Results.Ok(condition);
    }
}

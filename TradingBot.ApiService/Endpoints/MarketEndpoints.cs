using MediatR;
using TradingBot.ApiService.Application.Queries;
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
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new AnalyzeMarketConditionQuery(symbol);
        var condition = await mediator.Send(query, cancellationToken);
        return Results.Ok(condition);
    }
}

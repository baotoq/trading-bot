using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingBot.ApiService.Application.Requests;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Endpoints;

public static class TradingEndpoints
{
    public static RouteGroupBuilder MapTradingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/trade")
            .WithTags("Trading");

        group.MapPost("/execute", ExecuteTrade)
            .WithName("ExecuteTrade")
            .WithSummary("Execute a trade with full signal detection and risk management");

        group.MapGet("/analyze/{symbol}", AnalyzeSymbol)
            .WithName("AnalyzeSymbol")
            .WithSummary("Analyze a symbol and get trading signal without executing");

        return group;
    }

    private static async Task<IResult> ExecuteTrade(
        [FromBody] ExecuteTradeRequest request,
        IMediator mediator)
    {
        var command = new Application.Commands.ExecuteTradeCommand(
            request.Symbol,
            request.AccountEquity,
            request.RiskPercent);

        var result = await mediator.Send(command);

        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> AnalyzeSymbol(
        Symbol symbol,
        EmaMomentumScalperStrategy strategy)
    {
        var signal = await strategy.AnalyzeAsync(symbol);
        return Results.Ok(signal);
    }
}

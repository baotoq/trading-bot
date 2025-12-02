using Microsoft.AspNetCore.Mvc;
using TradingBot.ApiService.Application.Requests;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Endpoints;

public static class BacktestEndpoints
{
    public static RouteGroupBuilder MapBacktestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/backtest")
            .WithTags("Backtesting");

        group.MapPost("/run", RunBacktest)
            .WithName("RunBacktest")
            .WithSummary("Run backtest for a single strategy on historical data");

        group.MapPost("/compare", CompareStrategies)
            .WithName("CompareStrategies")
            .WithSummary("Compare multiple strategies side-by-side");

        return group;
    }

    private static async Task<IResult> RunBacktest(
        [FromBody] BacktestRequest request,
        IBacktestService backtestService)
    {
        var result = await backtestService.RunBacktestAsync(
            request.Symbol,
            request.Strategy,
            request.StartDate,
            request.EndDate,
            request.InitialCapital,
            request.RiskPercent);

        return Results.Ok(result);
    }

    private static async Task<IResult> CompareStrategies(
        [FromBody] CompareRequest request,
        IBacktestService backtestService)
    {
        var result = await backtestService.CompareStrategiesAsync(
            request.Symbol,
            request.Strategies,
            request.StartDate,
            request.EndDate,
            request.InitialCapital,
            request.RiskPercent);

        return Results.Ok(result);
    }
}

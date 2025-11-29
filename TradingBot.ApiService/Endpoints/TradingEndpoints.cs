using MediatR;
using TradingBot.ApiService.Features.Trading;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Endpoints;

public static class TradingEndpoints
{
    public static IEndpointRouteBuilder MapTradingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/trading")
            .WithTags("Trading");

        group.MapPost("/signal", GenerateSignal)
            .WithName("GenerateSignal")
            .WithSummary("Generate trading signal for a symbol using specified strategy");

        group.MapPost("/backtest", RunBacktest)
            .WithName("RunBacktest")
            .WithSummary("Run backtest for a strategy");

        group.MapPost("/order", PlaceOrder)
            .WithName("PlaceSpotOrder")
            .WithSummary("Place a spot trading order");

        return app;
    }

    private static async Task<IResult> GenerateSignal(
        GenerateSignalRequest request,
        IMediator mediator)
    {
        var signal = await mediator.Send(new GenerateSignal.Query(
            request.Symbol,
            request.StrategyName,
            request.Interval,
            request.CandleCount));

        return signal is not null ? Results.Ok(signal) : Results.NotFound("Unable to generate signal");
    }

    private static async Task<IResult> RunBacktest(
        BacktestRequest request,
        IMediator mediator)
    {
        var result = await mediator.Send(new RunBacktest.Query(
            request.StrategyName,
            request.Symbol,
            request.Interval,
            request.StartDate,
            request.EndDate,
            request.InitialCapital,
            request.PositionSize));

        return result is not null ? Results.Ok(result) : Results.BadRequest("Unable to run backtest");
    }

    private static async Task<IResult> PlaceOrder(
        PlaceOrderRequest request,
        IMediator mediator)
    {
        var result = await mediator.Send(new PlaceSpotOrder.Command(
            request.Symbol,
            request.Side,
            request.Type,
            request.Quantity,
            request.Price,
            request.StopPrice));

        return result is not null ? Results.Ok(result) : Results.BadRequest("Unable to place order");
    }
}

// Request DTOs
public record GenerateSignalRequest(
    string Symbol,
    string StrategyName,
    string Interval = "1h",
    int CandleCount = 100);

public record BacktestRequest(
    string StrategyName,
    string Symbol,
    string Interval,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialCapital = 10000m,
    decimal PositionSize = 0.1m);

public record PlaceOrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price = null,
    decimal? StopPrice = null);



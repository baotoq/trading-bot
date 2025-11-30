using MediatR;
using TradingBot.ApiService.Application.Binance;

namespace TradingBot.ApiService.Api;

public static class BinanceEndpoints
{
    public static IEndpointRouteBuilder MapBinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/binance")
            .WithTags("Binance");

        group.MapGet("/ping", PingBinance)
            .WithName("PingBinance")
            .WithSummary("Test Binance API connectivity");

        group.MapGet("/ticker/{symbol}", GetBinanceTicker)
            .WithName("GetBinanceTicker")
            .WithSummary("Get 24-hour price statistics for a symbol");

        group.MapGet("/tickers", GetAllBinanceTickers)
            .WithName("GetAllBinanceTickers")
            .WithSummary("Get 24-hour price statistics for all symbols");

        group.MapGet("/orderbook/{symbol}", GetBinanceOrderBook)
            .WithName("GetBinanceOrderBook")
            .WithSummary("Get order book for a symbol");

        group.MapGet("/account", GetBinanceAccount)
            .WithName("GetBinanceAccount")
            .WithSummary("Get account information and balances");

        return app;
    }

    private static async Task<IResult> PingBinance(IMediator mediator)
    {
        var response = await mediator.Send(new PingBinance.Query());
        return Results.Ok(response);
    }

    private static async Task<IResult> GetBinanceTicker(string symbol, IMediator mediator)
    {
        var ticker = await mediator.Send(new GetTicker.Query(symbol));
        return ticker is not null ? Results.Ok(ticker) : Results.NotFound();
    }

    private static async Task<IResult> GetAllBinanceTickers(IMediator mediator)
    {
        var tickers = await mediator.Send(new GetAllTickers.Query());
        return Results.Ok(tickers);
    }

    private static async Task<IResult> GetBinanceOrderBook(string symbol, int? limit, IMediator mediator)
    {
        var orderBook = await mediator.Send(new GetOrderBook.Query(symbol, limit ?? 20));
        return orderBook is not null ? Results.Ok(orderBook) : Results.NotFound();
    }

    private static async Task<IResult> GetBinanceAccount(IMediator mediator)
    {
        var account = await mediator.Send(new GetAccountInfo.Query());
        return account is not null ? Results.Ok(account) : Results.Unauthorized();
    }
}


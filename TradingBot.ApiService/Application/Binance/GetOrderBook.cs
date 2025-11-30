using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Binance;

public static class GetOrderBook
{
    public record Query(string Symbol, int Limit = 20) : IRequest<BinanceOrderBookData?>;

    public class Handler : IRequestHandler<Query, BinanceOrderBookData?>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<BinanceOrderBookData?> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _binanceService.GetOrderBookAsync(
                request.Symbol.ToUpper(),
                request.Limit,
                cancellationToken);
        }
    }
}




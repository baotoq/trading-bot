using MediatR;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Services;

namespace TradingBot.ApiService.Features.Binance;

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




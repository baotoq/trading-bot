using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Binance;

public static class GetTicker
{
    public record Query(string Symbol) : IRequest<BinanceTickerData?>;

    public class Handler : IRequestHandler<Query, BinanceTickerData?>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<BinanceTickerData?> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _binanceService.GetTickerAsync(request.Symbol.ToUpper(), cancellationToken);
        }
    }
}




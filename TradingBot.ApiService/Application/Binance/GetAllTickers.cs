using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Binance;

public static class GetAllTickers
{
    public record Query : IRequest<IEnumerable<BinanceTickerData>>;

    public class Handler : IRequestHandler<Query, IEnumerable<BinanceTickerData>>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<IEnumerable<BinanceTickerData>> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _binanceService.GetAllTickersAsync(cancellationToken);
        }
    }
}




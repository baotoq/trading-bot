using MediatR;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Services;

namespace TradingBot.ApiService.Features.Binance;

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




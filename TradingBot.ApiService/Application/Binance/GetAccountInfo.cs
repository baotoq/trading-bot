using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Binance;

public static class GetAccountInfo
{
    public record Query : IRequest<BinanceAccountInfo?>;

    public class Handler : IRequestHandler<Query, BinanceAccountInfo?>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<BinanceAccountInfo?> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _binanceService.GetAccountInfoAsync(cancellationToken);
        }
    }
}




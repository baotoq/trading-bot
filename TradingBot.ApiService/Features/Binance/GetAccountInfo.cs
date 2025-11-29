using MediatR;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Services;

namespace TradingBot.ApiService.Features.Binance;

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




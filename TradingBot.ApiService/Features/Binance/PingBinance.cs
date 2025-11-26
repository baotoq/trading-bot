using MediatR;
using TradingBot.ApiService.Services;

namespace TradingBot.ApiService.Features.Binance;

public static class PingBinance
{
    public record Query : IRequest<Response>;

    public record Response(bool Connected, DateTime Timestamp);

    public class Handler : IRequestHandler<Query, Response>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var isConnected = await _binanceService.TestConnectionAsync(cancellationToken);
            return new Response(isConnected, DateTime.UtcNow);
        }
    }
}



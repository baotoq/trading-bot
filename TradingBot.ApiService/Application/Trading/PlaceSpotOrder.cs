using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;

namespace TradingBot.ApiService.Application.Trading;

public static class PlaceSpotOrder
{
    public record Command(
        string Symbol,
        OrderSide Side,
        OrderType Type,
        decimal Quantity,
        decimal? Price = null,
        decimal? StopPrice = null
    ) : IRequest<OrderResult?>;

    public class Handler : IRequestHandler<Command, OrderResult?>
    {
        private readonly IBinanceService _binanceService;
        private readonly ILogger<Handler> _logger;

        public Handler(IBinanceService binanceService, ILogger<Handler> logger)
        {
            _binanceService = binanceService;
            _logger = logger;
        }

        public async Task<OrderResult?> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Placing spot order: {Symbol} {Side} {Type} {Quantity} @ {Price}",
                request.Symbol, request.Side, request.Type, request.Quantity, request.Price);

            try
            {
                var result = await _binanceService.PlaceSpotOrderAsync(
                    request.Symbol,
                    request.Side,
                    request.Type,
                    request.Quantity,
                    request.Price,
                    request.StopPrice,
                    cancellationToken);

                if (result != null)
                {
                    _logger.LogInformation(
                        "Order placed successfully: OrderId={OrderId}, Status={Status}",
                        result.OrderId, result.Status);
                }
                else
                {
                    _logger.LogWarning("Failed to place order");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing spot order");
                return null;
            }
        }
    }
}



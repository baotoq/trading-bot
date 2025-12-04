using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Candles.DomainEvents;

public record CandleCapturedDomainEvent(
    Candle Candle
) : IDomainEvent;

public class SignalDispatcher(
        ILogger<SignalDispatcher> logger,
        ISignalGeneratorService signalGenerator
    ) : INotificationHandler<CandleCapturedDomainEvent>
{
    public async Task Handle(CandleCapturedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation("Candle captured {@Candle}", @event.Candle);

        await signalGenerator.GenerateSignalAsync(@event.Candle.Symbol, cancellationToken);
    }
}

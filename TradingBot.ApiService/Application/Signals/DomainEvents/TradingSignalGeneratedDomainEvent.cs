using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Signals.DomainEvents;

public record TradingSignalGeneratedDomainEvent(
    TradingSignal TradingSignal
) : IDomainEvent;

public class SendSignalHandler(
    ITelegramNotificationService telegramService,
    ILogger<SendSignalHandler> logger
) : INotificationHandler<TradingSignalGeneratedDomainEvent>
{
    public async Task Handle(TradingSignalGeneratedDomainEvent @event, CancellationToken cancellationToken)
    {
        var signal = @event.TradingSignal;

        logger.LogInformation(
            "Sending signal notification for {Symbol}: {SignalType} (Confidence: {Confidence}%)",
            signal.Symbol, signal.Type, signal.Confidence * 100);

        try
        {
            await telegramService.SendSignalNotificationAsync(signal, cancellationToken);

            logger.LogInformation(
                "Signal notification sent successfully for {Symbol}",
                @signal.Symbol);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - we don't want notification failures to break signal generation
            logger.LogError(ex, "Failed to send signal notification for {Symbol}", @signal.Symbol);
        }
    }
}

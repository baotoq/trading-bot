using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Positions.DomainEvents;

public record PositionOpenedDomainEvent(
    Guid PositionId,
    Symbol Symbol,
    TradeSide Side,
    decimal EntryPrice,
    decimal Quantity,
    decimal StopLoss,
    decimal TakeProfit1,
    decimal TakeProfit2,
    decimal? TakeProfit3,
    int Leverage,
    decimal RiskAmount,
    string Strategy,
    string SignalReason,
    long? EntryOrderId,
    DateTime EntryTime
) : IDomainEvent;

public class SendPositionOpenedNotificationHandler(
    ITelegramNotificationService telegramService,
    ILogger<SendPositionOpenedNotificationHandler> logger
) : INotificationHandler<PositionOpenedDomainEvent>
{
    public async Task Handle(PositionOpenedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Position opened: {PositionId} | {Symbol} {Side} @ ${Price} | Qty: {Quantity} | Leverage: {Leverage}x",
            @event.PositionId, @event.Symbol, @event.Side, @event.EntryPrice, @event.Quantity, @event.Leverage);

        try
        {
            var emoji = @event.Side == TradeSide.Long ? "🟢" : "🔴";
            var riskPercent = (@event.RiskAmount / (@event.EntryPrice * @event.Quantity / @event.Leverage)) * 100;
            var slDistance = Math.Abs(@event.EntryPrice - @event.StopLoss);
            var slPercent = (slDistance / @event.EntryPrice) * 100;
            var tp1Distance = Math.Abs(@event.TakeProfit1 - @event.EntryPrice);
            var rr1 = tp1Distance / slDistance;

            var message = $"{emoji} <b>Position Opened</b> {emoji}\n\n" +
                         $"<b>Symbol:</b> {@event.Symbol}\n" +
                         $"<b>Side:</b> {@event.Side} ({@event.Leverage}x)\n" +
                         $"<b>Strategy:</b> {@event.Strategy}\n" +
                         $"<b>Entry:</b> ${@event.EntryPrice:F2}\n" +
                         $"<b>Quantity:</b> {@event.Quantity:F4}\n\n" +
                         $"<b>🛡️ Risk Management:</b>\n" +
                         $"  • Stop Loss: ${@event.StopLoss:F2} ({slPercent:F2}%)\n" +
                         $"  • TP1: ${@event.TakeProfit1:F2} ({rr1:F1}R)\n" +
                         $"  • TP2: ${@event.TakeProfit2:F2}\n";

            if (@event.TakeProfit3.HasValue)
            {
                message += $"  • TP3: ${@event.TakeProfit3:F2}\n";
            }

            message += $"  • Risk: ${@event.RiskAmount:F2} ({riskPercent:F2}%)\n\n" +
                      $"<b>📝 Reason:</b>\n{@event.SignalReason}\n\n" +
                      $"<i>⏰ {@event.EntryTime:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send position opened notification");
        }
    }
}

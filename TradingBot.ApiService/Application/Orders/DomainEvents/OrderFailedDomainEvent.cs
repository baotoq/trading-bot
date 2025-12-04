using Binance.Net.Enums;
using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Orders.DomainEvents;

public record OrderFailedDomainEvent(
    Symbol Symbol,
    OrderSide Side,
    FuturesOrderType Type,
    string ErrorMessage,
    DateTime FailedAt
) : IDomainEvent;

public class SendOrderFailedAlertHandler(
    ITelegramNotificationService telegramService,
    ILogger<SendOrderFailedAlertHandler> logger
) : INotificationHandler<OrderFailedDomainEvent>
{
    public async Task Handle(OrderFailedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogError(
            "Order failed: {Symbol} {Side} {Type} | Error: {Error}",
            @event.Symbol, @event.Side, @event.Type, @event.ErrorMessage);

        try
        {
            var message = $"❌ <b>Order Failed</b>\n\n" +
                         $"<b>Symbol:</b> {@event.Symbol}\n" +
                         $"<b>Side:</b> {@event.Side}\n" +
                         $"<b>Type:</b> {@event.Type}\n" +
                         $"<b>Error:</b> {@event.ErrorMessage}\n" +
                         $"<b>Time:</b> {@event.FailedAt:yyyy-MM-dd HH:mm:ss} UTC";

            await telegramService.SendErrorNotificationAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send order failure notification");
        }
    }
}

using Binance.Net.Enums;
using MediatR;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Orders.DomainEvents;

public record OrderFilledDomainEvent(
    Symbol Symbol,
    long OrderId,
    OrderSide Side,
    FuturesOrderType Type,
    decimal Quantity,
    decimal FilledPrice,
    decimal Commission,
    DateTime FilledAt
) : IDomainEvent;

public class LogOrderFilledHandler(
    ILogger<LogOrderFilledHandler> logger
) : INotificationHandler<OrderFilledDomainEvent>
{
    public Task Handle(OrderFilledDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Order filled: {Symbol} {Side} {Type} | Qty: {Quantity} @ ${Price} | OrderId: {OrderId} | Commission: ${Commission}",
            @event.Symbol, @event.Side, @event.Type, @event.Quantity, @event.FilledPrice, @event.OrderId, @event.Commission);

        return Task.CompletedTask;
    }
}

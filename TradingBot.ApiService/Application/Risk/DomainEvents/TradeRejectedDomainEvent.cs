using MediatR;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Risk.DomainEvents;

public record TradeRejectedDomainEvent(
    Symbol Symbol,
    SignalType SignalType,
    string RejectionReason,
    List<string> Violations,
    decimal Confidence,
    string Strategy,
    DateTime RejectedAt
) : IDomainEvent;

public class LogTradeRejectedHandler(
    ILogger<LogTradeRejectedHandler> logger
) : INotificationHandler<TradeRejectedDomainEvent>
{
    public Task Handle(TradeRejectedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Trade rejected: {Symbol} {SignalType} | Strategy: {Strategy} | Confidence: {Confidence}% | Reason: {Reason} | Violations: {Violations}",
            @event.Symbol, @event.SignalType, @event.Strategy, @event.Confidence * 100,
            @event.RejectionReason, string.Join(", ", @event.Violations));

        return Task.CompletedTask;
    }
}

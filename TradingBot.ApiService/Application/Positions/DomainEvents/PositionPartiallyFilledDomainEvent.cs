using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Positions.DomainEvents;

public record PositionPartiallyFilledDomainEvent(
    Guid PositionId,
    Symbol Symbol,
    decimal QuantityFilled,
    decimal RemainingQuantity,
    decimal FillPrice,
    string FillReason, // "TP1", "TP2", "TP3"
    decimal PartialPnL
) : IDomainEvent;

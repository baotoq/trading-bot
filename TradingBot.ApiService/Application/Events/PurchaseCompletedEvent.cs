using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Application.Events;

public record PurchaseCompletedEvent(PurchaseId PurchaseId) : IDomainEvent;

using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Positions.DomainEvents;

public record PositionClosedDomainEvent(
    Guid PositionId,
    Symbol Symbol,
    TradeSide Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal RealizedPnL,
    decimal RealizedPnLPercent,
    string ExitReason,
    TimeSpan HoldingTime,
    bool IsWin,
    string Strategy
) : IDomainEvent;

public class SendPositionClosedNotificationHandler(
    ITelegramNotificationService telegramService,
    ILogger<SendPositionClosedNotificationHandler> logger
) : INotificationHandler<PositionClosedDomainEvent>
{
    public async Task Handle(PositionClosedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Position closed: {PositionId} | {Symbol} {Side} | Entry: ${Entry} Exit: ${Exit} | P&L: ${PnL} ({PnLPercent:F2}%) | {Result}",
            @event.PositionId, @event.Symbol, @event.Side, @event.EntryPrice, @event.ExitPrice,
            @event.RealizedPnL, @event.RealizedPnLPercent, @event.IsWin ? "WIN" : "LOSS");

        try
        {
            var emoji = @event.IsWin ? "✅" : "❌";
            var pnlEmoji = @event.RealizedPnL > 0 ? "📈" : "📉";
            var sideEmoji = @event.Side == TradeSide.Long ? "🟢" : "🔴";

            var message = $"{emoji} <b>Position Closed</b> {emoji}\n\n" +
                         $"<b>Symbol:</b> {@event.Symbol} {sideEmoji}\n" +
                         $"<b>Strategy:</b> {@event.Strategy}\n" +
                         $"<b>Entry:</b> ${@event.EntryPrice:F2}\n" +
                         $"<b>Exit:</b> ${@event.ExitPrice:F2}\n" +
                         $"<b>Quantity:</b> {@event.Quantity:F4}\n\n" +
                         $"<b>{pnlEmoji} Performance:</b>\n" +
                         $"  • P&L: ${@event.RealizedPnL:F2} ({@event.RealizedPnLPercent:F2}%)\n" +
                         $"  • Result: {@event.IsWin.ToString().ToUpper()}\n" +
                         $"  • Holding Time: {@event.HoldingTime.TotalHours:F1}h\n" +
                         $"  • Exit Reason: {@event.ExitReason}\n\n" +
                         $"<i>⏰ {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await telegramService.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send position closed notification");
        }
    }
}

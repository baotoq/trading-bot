using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Risk.DomainEvents;

public record RiskViolationDetectedDomainEvent(
    Symbol Symbol,
    string ViolationType, // "MaxConsecutiveLosses", "DailyDrawdownExceeded", "MaxPositionsReached", "RiskPercentTooHigh"
    string Description,
    decimal CurrentValue,
    decimal ThresholdValue,
    DateTimeOffset DetectedAt
) : IDomainEvent;

public class SendRiskViolationAlertHandler(
    ITelegramNotificationService telegramService,
    ILogger<SendRiskViolationAlertHandler> logger
) : INotificationHandler<RiskViolationDetectedDomainEvent>
{
    public async Task Handle(RiskViolationDetectedDomainEvent @event, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Risk violation detected: {ViolationType} | {Symbol} | Current: {Current}, Threshold: {Threshold}",
            @event.ViolationType, @event.Symbol, @event.CurrentValue, @event.ThresholdValue);

        try
        {
            var message = $"⚠️ <b>Risk Violation Detected</b> ⚠️\n\n" +
                         $"<b>Symbol:</b> {@event.Symbol}\n" +
                         $"<b>Type:</b> {@event.ViolationType}\n" +
                         $"<b>Description:</b> {@event.Description}\n\n" +
                         $"<b>📊 Values:</b>\n" +
                         $"  • Current: {@event.CurrentValue:F2}\n" +
                         $"  • Threshold: {@event.ThresholdValue:F2}\n\n" +
                         $"<i>⏰ {@event.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await telegramService.SendErrorNotificationAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send risk violation alert");
        }
    }
}

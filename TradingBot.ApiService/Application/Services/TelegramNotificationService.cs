using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly TelegramBotClient? _botClient;
    private readonly long? _chatId;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly bool _isEnabled;

    public TelegramNotificationService(IConfiguration configuration, ILogger<TelegramNotificationService> logger)
    {
        _logger = logger;

        var botToken = configuration["Telegram:BotToken"];
        var chatIdStr = configuration["Telegram:ChatId"];

        if (!string.IsNullOrEmpty(botToken) && !string.IsNullOrEmpty(chatIdStr) && long.TryParse(chatIdStr, out var chatId))
        {
            _botClient = new TelegramBotClient(botToken);
            _chatId = chatId;
            _isEnabled = true;
            _logger.LogInformation("Telegram notification service initialized successfully");
        }
        else
        {
            _isEnabled = false;
            _logger.LogWarning("Telegram notification service is disabled. Configure Telegram:BotToken and Telegram:ChatId to enable.");
        }
    }

    public async Task SendSignalNotificationAsync(TradingSignal signal, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _botClient == null || _chatId == null)
        {
            _logger.LogDebug("Telegram is not configured, skipping notification");
            return;
        }

        try
        {
            var emoji = signal.Type switch
            {
                SignalType.StrongBuy => "🚀",
                SignalType.Buy => "📈",
                SignalType.StrongSell => "⚠️",
                SignalType.Sell => "📉",
                _ => "⏸️"
            };

            var signalText = signal.Type switch
            {
                SignalType.StrongBuy => "STRONG BUY",
                SignalType.Buy => "BUY",
                SignalType.StrongSell => "STRONG SELL",
                SignalType.Sell => "SELL",
                _ => "HOLD"
            };

            var message = $@"{emoji} <b>Trading Signal Detected</b> {emoji}

<b>Symbol:</b> {signal.Symbol}
<b>Signal:</b> {signalText}
<b>Strategy:</b> {signal.Strategy}
<b>Price:</b> ${signal.Price:F2}
<b>Confidence:</b> {signal.Confidence * 100:F1}%

<b>📊 Indicators:</b>";

            if (signal.Indicators.TryGetValue("RSI", out var rsi))
            {
                message += $"\n  • RSI: {rsi:F2}";
            }
            if (signal.Indicators.TryGetValue("EMA9", out var ema9))
            {
                message += $"\n  • EMA 9: ${ema9:F2}";
            }
            if (signal.Indicators.TryGetValue("EMA21", out var ema21))
            {
                message += $"\n  • EMA 21: ${ema21:F2}";
            }
            if (signal.Indicators.TryGetValue("MACD", out var macd))
            {
                message += $"\n  • MACD: {macd:F4}";
            }

            if (signal.StopLoss.HasValue && signal.EntryPrice.HasValue)
            {
                var riskAmount = Math.Abs(signal.EntryPrice.Value - signal.StopLoss.Value);
                var riskPercent = (riskAmount / signal.EntryPrice.Value) * 100;

                message += $"\n\n<b>🛡️ Risk Management:</b>";
                message += $"\n  • Entry: ${signal.EntryPrice:F2}";
                message += $"\n  • Stop Loss: ${signal.StopLoss:F2}";
                message += $"\n  • Risk: ${riskAmount:F2} ({riskPercent:F2}%)";

                if (signal.TakeProfit1.HasValue)
                {
                    var tp1Profit = Math.Abs(signal.TakeProfit1.Value - signal.EntryPrice.Value);
                    var tp1RR = tp1Profit / riskAmount;
                    message += $"\n  • TP1: ${signal.TakeProfit1:F2} ({tp1RR:F1}R)";
                }
                if (signal.TakeProfit2.HasValue)
                {
                    var tp2Profit = Math.Abs(signal.TakeProfit2.Value - signal.EntryPrice.Value);
                    var tp2RR = tp2Profit / riskAmount;
                    message += $"\n  • TP2: ${signal.TakeProfit2:F2} ({tp2RR:F1}R)";
                }
                if (signal.TakeProfit3.HasValue)
                {
                    var tp3Profit = Math.Abs(signal.TakeProfit3.Value - signal.EntryPrice.Value);
                    var tp3RR = tp3Profit / riskAmount;
                    message += $"\n  • TP3: ${signal.TakeProfit3:F2} ({tp3RR:F1}R)";
                }
            }

            message += $"\n\n<b>📝 Reason:</b>\n{signal.Reason}";
            message += $"\n\n<i>⏰ {signal.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await _botClient.SendMessage(
                chatId: _chatId.Value,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Telegram signal notification sent for {Symbol}: {Signal}", signal.Symbol, signalText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram signal notification");
        }
    }

    public async Task SendTradeExecutionNotificationAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _botClient == null || _chatId == null)
        {
            return;
        }

        try
        {
            var formattedMessage = $"✅ <b>Trade Executed</b>\n\n{message}\n\n<i>⏰ {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await _botClient.SendMessage(
                chatId: _chatId.Value,
                text: formattedMessage,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Telegram trade execution notification sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram trade execution notification");
        }
    }

    public async Task SendErrorNotificationAsync(string error, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _botClient == null || _chatId == null)
        {
            return;
        }

        try
        {
            var message = $"❌ <b>Error Alert</b>\n\n{error}\n\n<i>⏰ {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</i>";

            await _botClient.SendMessage(
                chatId: _chatId.Value,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Telegram error notification sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram error notification");
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _botClient == null || _chatId == null)
        {
            return;
        }

        try
        {
            await _botClient.SendMessage(
                chatId: _chatId.Value,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Telegram message sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message");
        }
    }
}

using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Infrastructure.Telegram;

public class TelegramNotificationService(
    ITelegramBotClient telegramBotClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotificationService> logger)
{
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        try
        {
            await telegramBotClient.SendMessage(
                chatId: options.Value.ChatId,
                text: message,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);

            logger.LogInformation("Telegram notification sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram notification");
        }
    }
}

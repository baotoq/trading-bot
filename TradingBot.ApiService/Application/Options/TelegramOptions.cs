namespace TradingBot.ApiService.Application.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsEnabled => !string.IsNullOrEmpty(BotToken) && !string.IsNullOrEmpty(ChatId);
}

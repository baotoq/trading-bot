using Microsoft.Extensions.Options;
using Telegram.Bot;
using TradingBot.ApiService.Application.Handlers;
using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Infrastructure.Telegram;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Telegram configuration
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection("Telegram"));

        // Register TelegramBotClient as singleton
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(options.BotToken);
        });

        // Register TelegramNotificationService
        services.AddSingleton<TelegramNotificationService>();

        // Register MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PurchaseCompletedHandler).Assembly));

        return services;
    }
}

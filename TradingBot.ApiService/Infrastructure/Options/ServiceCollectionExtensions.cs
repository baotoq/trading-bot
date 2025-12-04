namespace TradingBot.ApiService.Application.Options;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddApplicationOptions(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
        builder.Services.Configure<BinanceOptions>(builder.Configuration.GetSection(BinanceOptions.SectionName));

        return builder;
    }
}

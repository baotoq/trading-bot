using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Services;

namespace TradingBot.ApiService;

public static class ServiceCollectionExtensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Configure Binance API
        var apiKey = builder.Configuration["Binance:ApiKey"] ?? string.Empty;
        var apiSecret = builder.Configuration["Binance:ApiSecret"] ?? string.Empty;
        var testMode = builder.Configuration.GetValue<bool>("Binance:TestMode");

        builder.Services.AddSingleton<IBinanceRestClient>(_ =>
        {
            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                return new BinanceRestClient(opts =>
                {
                    opts.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    opts.Environment = testMode ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
                });
            }

            return new BinanceRestClient();
        });

        builder.Services.AddScoped<IBinanceService, BinanceService>();

        // Register trading services
        builder.Services
            .AddScoped<IHistoricalDataService,
                HistoricalDataService>();
        builder.Services
            .AddScoped<Services.Backtesting.IBacktestingService,
                Services.Backtesting.BacktestingService>();

        // Register trading strategies
        builder.Services.AddScoped<Services.Strategy.MovingAverageCrossoverStrategy>();
        builder.Services.AddScoped<Services.Strategy.RSIStrategy>();
        builder.Services.AddScoped<Services.Strategy.MACDStrategy>();
        builder.Services.AddScoped<Services.Strategy.CombinedStrategy>();
    }
}
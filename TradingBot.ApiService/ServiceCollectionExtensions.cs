using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Services;
using TradingBot.ApiService.Services.Backtesting;
using TradingBot.ApiService.Services.RealTimeTrading;
using TradingBot.ApiService.Services.Strategy;

namespace TradingBot.ApiService;

public static class ServiceCollectionExtensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var apiKey = builder.Configuration["Binance:ApiKey"] ?? string.Empty;
        var apiSecret = builder.Configuration["Binance:ApiSecret"] ?? string.Empty;
        var testMode = builder.Configuration.GetValue<bool>("Binance:TestMode");

        builder.Services.AddSingleton<IRealTimeTradingService, RealTimeTradingService>();
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

        builder.Services.AddSingleton<IBinanceSocketClient>(_ =>
        {
            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                return new BinanceSocketClient(opts =>
                {
                    opts.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    opts.Environment = testMode ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
                });
            }

            return new BinanceSocketClient();
        });

        builder.Services.AddScoped<IBinanceService, BinanceService>();
        builder.Services.AddScoped<IHistoricalDataService, HistoricalDataService>();
        builder.Services.AddScoped<IBacktestingService, BacktestingService>();
        builder.Services.AddScoped<MovingAverageCrossoverStrategy>();
        builder.Services.AddScoped<RSIStrategy>();
        builder.Services.AddScoped<MACDStrategy>();
    }
}
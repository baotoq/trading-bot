using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.Backtesting;
using TradingBot.ApiService.Application.Services.RealTimeTrading;
using TradingBot.ApiService.Application.Services.Strategy;

namespace TradingBot.ApiService.Application;

public static class ServiceCollectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddApplicationServices()
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

            // Register the base API service
            builder.Services.AddScoped<HistoricalDataService>();

            // Register the cached version as the primary implementation
            builder.Services.AddScoped<IHistoricalDataService, CachedHistoricalDataService>();

            builder.Services.AddScoped<IBacktestingService, BacktestingService>();
            builder.Services.AddScoped<MovingAverageCrossoverStrategy>();
            builder.Services.AddScoped<RSIStrategy>();
            builder.Services.AddScoped<MACDStrategy>();
        }
    }
}
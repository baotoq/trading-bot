using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;

namespace TradingBot.ApiService.Application;

public static class ServiceCollectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddApplicationServices()
        {
            // Add MediatR for commands and queries
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

            // Register trading services
            builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
            builder.Services.AddScoped<IMarketAnalysisService, MarketAnalysisService>();
            builder.Services.AddScoped<IPositionCalculatorService, PositionCalculatorService>();
            builder.Services.AddScoped<IRiskManagementService, RiskManagementService>();
            builder.Services.AddScoped<IBinanceService, BinanceService>();
            builder.Services.AddScoped<IBacktestService, BacktestService>();

            // Register strategies
            builder.Services.AddScoped<EmaMomentumScalperStrategy>();
            builder.Services.AddScoped<IStrategy, EmaMomentumScalperStrategy>();

            // Configure Binance API clients
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
        }
    }
}
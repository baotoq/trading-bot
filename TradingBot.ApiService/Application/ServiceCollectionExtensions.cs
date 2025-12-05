using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Http.Json;
using TradingBot.ApiService.Application.Options;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.BuildingBlocks.DistributedLocks;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        // Add MediatR for commands and queries
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        builder.Services.Configure<JsonOptions>(opts =>
        {
            opts.SerializerOptions.Converters.Add(new SymbolJsonConverter());
            opts.SerializerOptions.Converters.Add(new CandleIntervalJsonConverter());
            opts.SerializerOptions.Converters.Add(new StrategyNameJsonConverter());
        });

        // Register trading services
        builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
        builder.Services.AddScoped<IPositionCalculatorService, PositionCalculatorService>();
        builder.Services.AddScoped<IRiskManagementService, RiskManagementService>();
        builder.Services.AddScoped<IBinanceService, BinanceService>();
        builder.Services.AddScoped<IBacktestService, BacktestService>();

        // Register real-time services
        builder.Services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
        builder.Services.AddSingleton<IRealtimeCandleService, RealtimeCandleService>();
        builder.Services.AddSingleton<ISignalGeneratorService, SignalGeneratorService>();
        builder.Services.AddSingleton<ICandleCacheService, CandleCacheService>();
        builder.Services.AddSingleton<IInMemoryCandleCache, InMemoryCandleCache>();

        // Register strategy factory
        builder.Services.AddScoped<IStrategyFactory, StrategyFactory>();

        builder.Services.AddScoped<EmaMomentumScalperStrategy>();
        builder.Services.AddScoped<BollingerSqueezeStrategy>();
        builder.Services.AddScoped<RsiDivergenceStrategy>();
        builder.Services.AddScoped<BtcSpotDcaStrategy>();
        builder.Services.AddScoped<BtcSpotTrendStrategy>();
        builder.Services.AddScoped<FundingRateArbitrageStrategy>();
        builder.Services.AddScoped<IStrategy, EmaMomentumScalperStrategy>();
        builder.Services.AddScoped<IStrategy, BollingerSqueezeStrategy>();
        builder.Services.AddScoped<IStrategy, RsiDivergenceStrategy>();
        builder.Services.AddScoped<IStrategy, BtcSpotDcaStrategy>();
        builder.Services.AddScoped<IStrategy, BtcSpotTrendStrategy>();
        builder.Services.AddScoped<IStrategy, FundingRateArbitrageStrategy>();

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

        return builder;
    }
}

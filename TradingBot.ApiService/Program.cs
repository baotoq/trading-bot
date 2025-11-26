using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Endpoints;
using TradingBot.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add MediatR
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
builder.Services.AddScoped<TradingBot.ApiService.Services.IHistoricalDataService, TradingBot.ApiService.Services.HistoricalDataService>();
builder.Services.AddScoped<TradingBot.ApiService.Services.Backtesting.IBacktestingService, TradingBot.ApiService.Services.Backtesting.BacktestingService>();

// Register trading strategies
builder.Services.AddScoped<TradingBot.ApiService.Services.Strategy.MovingAverageCrossoverStrategy>();
builder.Services.AddScoped<TradingBot.ApiService.Services.Strategy.RSIStrategy>();
builder.Services.AddScoped<TradingBot.ApiService.Services.Strategy.MACDStrategy>();
builder.Services.AddScoped<TradingBot.ApiService.Services.Strategy.CombinedStrategy>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "Trading Bot API service is running. Visit /binance or /trading endpoints.");

// Map Binance API endpoints
app.MapBinanceEndpoints();

// Map Trading endpoints
app.MapTradingEndpoints();

app.MapDefaultEndpoints();

app.Run();
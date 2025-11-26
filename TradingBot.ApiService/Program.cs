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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries =
    ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

// Map Binance API endpoints
app.MapBinanceEndpoints();

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
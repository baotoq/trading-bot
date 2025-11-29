using TradingBot.ApiService;
using TradingBot.ApiService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "Trading Bot API service is running. Visit /binance, /trading, or /realtime endpoints.");
app.MapDefaultEndpoints();

app.MapBinanceEndpoints();
app.MapTradingEndpoints();
app.MapRealTimeTradingEndpoints();

app.Run();
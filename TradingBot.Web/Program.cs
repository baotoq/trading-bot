using Refit;
using TradingBot.Web.Components;
using TradingBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRedisOutputCache("cache");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Ant Design
builder.Services.AddAntDesign();

// Register Refit client for Binance API
builder.Services
    .AddRefitClient<IBinanceApiClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https+http://apiservice");
    });

// Register wrapper for better error handling in UI
builder.Services.AddScoped<BinanceApiClientWrapper>();

// Register Trading API client
builder.Services
    .AddRefitClient<ITradingApiClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
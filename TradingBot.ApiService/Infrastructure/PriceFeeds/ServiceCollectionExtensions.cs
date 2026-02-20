using Microsoft.Extensions.Http.Resilience;
using TradingBot.ApiService.Infrastructure.CoinGecko;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;
using TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPriceFeeds(this IServiceCollection services, IConfiguration configuration)
    {
        // CoinGecko live price provider (separate from existing CoinGeckoClient for historical data)
        var coinGeckoApiKey = configuration.GetSection("CoinGecko").Get<CoinGeckoOptions>()?.ApiKey;

        services.AddHttpClient<CoinGeckoPriceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);

            // Add CoinGecko demo API key header if configured (30 req/min vs 5-15 req/min without)
            if (!string.IsNullOrEmpty(coinGeckoApiKey))
            {
                client.DefaultRequestHeaders.Add("x-cg-demo-api-key", coinGeckoApiKey);
            }
        })
        .AddStandardResilienceHandler(ConfigureResilience);

        services.AddScoped<ICryptoPriceProvider, CoinGeckoPriceProvider>();

        // VNDirect dchart API for VN ETF prices
        services.AddHttpClient<VNDirectPriceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(ConfigureResilience);

        services.AddScoped<IEtfPriceProvider, VNDirectPriceProvider>();

        // open.er-api.com for USD/VND exchange rate
        services.AddHttpClient<OpenErApiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://open.er-api.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(ConfigureResilience);

        services.AddScoped<IExchangeRateProvider, OpenErApiProvider>();

        return services;
    }

    /// <summary>
    /// Shared resilience configuration for all price feed HTTP clients.
    /// All calls are read-only and safe to retry.
    /// </summary>
    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    }
}

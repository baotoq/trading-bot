using Microsoft.Extensions.Http.Resilience;

namespace TradingBot.ApiService.Infrastructure.CoinGecko;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoinGecko(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind CoinGeckoOptions from configuration
        services.Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));

        // Register CoinGeckoClient with HttpClient factory and resilience
        services.AddHttpClient<CoinGeckoClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler(options =>
        {
            // CoinGecko API calls are read-only, safe to retry
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

            // Circuit breaker settings
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.2; // Open after 20% failures
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);

            // Timeout settings
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}

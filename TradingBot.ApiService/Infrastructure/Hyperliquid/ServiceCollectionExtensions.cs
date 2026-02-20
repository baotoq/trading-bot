using Microsoft.Extensions.Http.Resilience;
using TradingBot.ApiService.Configuration;

namespace TradingBot.ApiService.Infrastructure.Hyperliquid;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHyperliquid(this IServiceCollection services, IConfiguration configuration)
    {
        // Register HyperliquidSigner as singleton (private key doesn't change during app lifetime)
        services.AddSingleton<HyperliquidSigner>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<HyperliquidSigner>>();
            var privateKey = configuration["Hyperliquid:PrivateKey"];

            if (string.IsNullOrWhiteSpace(privateKey))
            {
                throw new InvalidOperationException(
                    "Hyperliquid private key not configured. " +
                    "Set via user secrets: dotnet user-secrets set 'Hyperliquid:PrivateKey' '0xYOUR_KEY' --project TradingBot.ApiService");
            }

            return new HyperliquidSigner(privateKey, logger);
        });

        // Register HyperliquidClient via HttpClientFactory with resilience
        var hyperliquidOptions = configuration.GetSection("Hyperliquid").Get<HyperliquidOptions>() ?? new HyperliquidOptions();

        services.AddHttpClient<HyperliquidClient>(client =>
        {
            client.BaseAddress = new Uri(hyperliquidOptions.ApiUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler(options =>
        {
            // CRITICAL: Disable retries for POST requests (order placement)
            // Retrying order requests can cause duplicate orders
            options.Retry.ShouldHandle = args =>
            {
                // Only retry GET requests, never POST
                if (args.Outcome.Result?.RequestMessage?.Method == HttpMethod.Post)
                {
                    return ValueTask.FromResult(false);
                }

                // Retry on transient HTTP errors for GET requests
                return ValueTask.FromResult(
                    args.Outcome.Result?.IsSuccessStatusCode == false ||
                    args.Outcome.Exception != null);
            };

            // Circuit breaker settings
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.1; // Open after 10% failures
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);

            // Timeout settings
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}

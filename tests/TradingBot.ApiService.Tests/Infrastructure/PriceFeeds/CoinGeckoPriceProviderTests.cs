using System.Net;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Infrastructure.CoinGecko;
using TradingBot.ApiService.Infrastructure.PriceFeeds;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Crypto;

namespace TradingBot.ApiService.Tests.Infrastructure.PriceFeeds;

public class CoinGeckoPriceProviderTests
{
    private const string CoinId = "bitcoin";
    private const string CacheKey = $"price:crypto:{CoinId}";
    private const decimal FreshPrice = 95000m;
    private const decimal StalePrice = 88000m;

    private static readonly ILogger<CoinGeckoPriceProvider> Logger =
        Substitute.For<ILogger<CoinGeckoPriceProvider>>();

    private static IOptionsMonitor<CoinGeckoOptions> CreateOptions(string? apiKey = "test-api-key")
    {
        var monitor = Substitute.For<IOptionsMonitor<CoinGeckoOptions>>();
        monitor.CurrentValue.Returns(new CoinGeckoOptions { ApiKey = apiKey });
        return monitor;
    }

    private static byte[] SerializeFreshEntry(decimal price) =>
        MessagePackSerializer.Serialize(
            PriceFeedEntry.Create(price, "USD"),
            MessagePackSerializerOptions.Standard);

    private static byte[] SerializeStaleEntry(decimal price)
    {
        var staleTime = DateTimeOffset.UtcNow.AddHours(-2); // 2 hours old, well past 5-min freshness window
        var entry = new PriceFeedEntry(price, staleTime.ToUnixTimeSeconds(), "USD");
        return MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
    }

    private static HttpResponseMessage CreateCoinGeckoResponse(decimal price) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"{CoinId}\":{{\"usd\":{price},\"last_updated_at\":1234567890}}}}",
                Encoding.UTF8,
                "application/json")
        };

    [Fact]
    public async Task GetPriceAsync_FreshCacheHit_ReturnsFreshResultWithoutHttpCall()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        var cachedBytes = SerializeFreshEntry(FreshPrice);
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(cachedBytes);

        var httpHandler = new MockHttpMessageHandler(_ =>
            throw new InvalidOperationException("Should not make HTTP call for fresh cache"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://api.coingecko.com/api/v3/") };
        var provider = new CoinGeckoPriceProvider(httpClient, cache, CreateOptions(), Logger);

        // Act
        var result = await provider.GetPriceAsync(CoinId, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse();
        result.Price.Should().Be(FreshPrice);
        result.Currency.Should().Be("USD");
        httpHandler.SentRequests.Should().BeEmpty("no HTTP call should be made for fresh cache hit");
    }

    [Fact]
    public async Task GetPriceAsync_StaleCacheAndApiSuccess_ReturnsFreshResultFromApi()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        var staleBytes = SerializeStaleEntry(StalePrice);
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(staleBytes);

        HttpRequestMessage? capturedRequest = null;
        var httpHandler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return CreateCoinGeckoResponse(FreshPrice);
        });

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://api.coingecko.com/api/v3/") };
        var provider = new CoinGeckoPriceProvider(httpClient, cache, CreateOptions("my-api-key"), Logger);

        // Act
        var result = await provider.GetPriceAsync(CoinId, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("stale cache should trigger API refresh returning fresh result");
        result.Price.Should().Be(FreshPrice);
        httpHandler.SentRequests.Should().HaveCount(1, "exactly one HTTP call should be made");

        // Verify API key header was added
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().ContainKey("x-cg-demo-api-key");
        capturedRequest.Headers.GetValues("x-cg-demo-api-key").Should().Contain("my-api-key");
    }

    [Fact]
    public async Task GetPriceAsync_StaleCacheAndApiFailure_ReturnsStaleResult()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        var staleBytes = SerializeStaleEntry(StalePrice);
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(staleBytes);

        var httpHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException("CoinGecko is down"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://api.coingecko.com/api/v3/") };
        var provider = new CoinGeckoPriceProvider(httpClient, cache, CreateOptions(), Logger);

        // Act
        var result = await provider.GetPriceAsync(CoinId, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeTrue("API failure with stale cache should return stale result");
        result.Price.Should().Be(StalePrice, "stale cached price should be used as fallback");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetPriceAsync_EmptyCacheAndApiSuccess_ReturnsFreshResult()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var httpHandler = new MockHttpMessageHandler(_ => CreateCoinGeckoResponse(FreshPrice));
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://api.coingecko.com/api/v3/") };
        var provider = new CoinGeckoPriceProvider(httpClient, cache, CreateOptions(), Logger);

        // Act
        var result = await provider.GetPriceAsync(CoinId, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("empty cache with successful API call should return fresh result");
        result.Price.Should().Be(FreshPrice);
        httpHandler.SentRequests.Should().HaveCount(1);

        // Verify cache was written
        await cache.Received(1).SetAsync(
            CacheKey,
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPriceAsync_EmptyCacheAndApiFailure_ThrowsException()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var httpHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException("No network connection"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://api.coingecko.com/api/v3/") };
        var provider = new CoinGeckoPriceProvider(httpClient, cache, CreateOptions(), Logger);

        // Act
        var act = async () => await provider.GetPriceAsync(CoinId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>(
            "empty cache with API failure should propagate the exception");
    }
}

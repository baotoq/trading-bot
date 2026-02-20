using System.Net;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Infrastructure.PriceFeeds;
using TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

namespace TradingBot.ApiService.Tests.Infrastructure.PriceFeeds;

public class OpenErApiProviderTests
{
    private const string CacheKey = "price:exchangerate:usd-vnd";
    private const decimal FreshRate = 25_380m;
    private const decimal StaleRate = 25_000m;

    private static readonly ILogger<OpenErApiProvider> Logger =
        Substitute.For<ILogger<OpenErApiProvider>>();

    private static byte[] SerializeFreshEntry(decimal rate)
    {
        var entry = PriceFeedEntry.Create(rate, "VND");
        return MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
    }

    private static byte[] SerializeStaleEntry(decimal rate)
    {
        var staleTime = DateTimeOffset.UtcNow.AddHours(-24); // 24 hours old, past 12h freshness window
        var entry = new PriceFeedEntry(rate, staleTime.ToUnixTimeSeconds(), "VND");
        return MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
    }

    private static HttpResponseMessage CreateOpenErApiResponse(decimal vndRate = FreshRate) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"result\":\"success\",\"base_code\":\"USD\",\"time_last_update_unix\":1234567890,\"rates\":{{\"VND\":{vndRate},\"EUR\":0.92}}}}",
                Encoding.UTF8,
                "application/json")
        };

    [Fact]
    public async Task GetUsdToVndRateAsync_FreshCacheHit_ReturnsFreshResultWithoutHttpCall()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(SerializeFreshEntry(FreshRate));

        var httpHandler = new MockHttpMessageHandler(_ =>
            throw new InvalidOperationException("Should not make HTTP call for fresh cache"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://open.er-api.com/") };
        var provider = new OpenErApiProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetUsdToVndRateAsync(CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("fresh cache should return immediately without HTTP call");
        result.Price.Should().Be(FreshRate);
        result.Currency.Should().Be("VND");
        httpHandler.SentRequests.Should().BeEmpty("no HTTP call for fresh cache hit");
    }

    [Fact]
    public async Task GetUsdToVndRateAsync_StaleCacheAndApiSuccess_ReturnsFreshResult()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(SerializeStaleEntry(StaleRate));

        var httpHandler = new MockHttpMessageHandler(_ => CreateOpenErApiResponse(FreshRate));
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://open.er-api.com/") };
        var provider = new OpenErApiProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetUsdToVndRateAsync(CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("successful API refresh should return fresh result");
        result.Price.Should().Be(FreshRate, "should return the API-fetched rate");
        httpHandler.SentRequests.Should().HaveCount(1, "one HTTP call should be made to refresh stale cache");

        // Verify new rate was cached
        await cache.Received(1).SetAsync(
            CacheKey,
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToVndRateAsync_StaleCacheAndApiFailure_ReturnsStaleResult()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(SerializeStaleEntry(StaleRate));

        var httpHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException("Exchange rate API is down"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://open.er-api.com/") };
        var provider = new OpenErApiProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetUsdToVndRateAsync(CancellationToken.None);

        // Assert
        result.IsStale.Should().BeTrue("API failure with stale cache should return stale result as fallback");
        result.Price.Should().Be(StaleRate, "should return the stale cached rate");
        result.Currency.Should().Be("VND");
    }

    [Fact]
    public async Task GetUsdToVndRateAsync_EmptyCacheAndApiSuccess_ReturnsFreshResult()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var httpHandler = new MockHttpMessageHandler(_ => CreateOpenErApiResponse(FreshRate));
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://open.er-api.com/") };
        var provider = new OpenErApiProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetUsdToVndRateAsync(CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("empty cache with successful API call returns fresh result");
        result.Price.Should().Be(FreshRate);
        result.Currency.Should().Be("VND");
        httpHandler.SentRequests.Should().HaveCount(1);

        // Verify result was cached for next call
        await cache.Received(1).SetAsync(
            CacheKey,
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsdToVndRateAsync_EmptyCacheAndApiFailure_ThrowsException()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var httpHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException("No network connection"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://open.er-api.com/") };
        var provider = new OpenErApiProvider(httpClient, cache, Logger);

        // Act
        var act = async () => await provider.GetUsdToVndRateAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>(
            "empty cache with API failure should propagate the exception — no fallback available");
    }
}

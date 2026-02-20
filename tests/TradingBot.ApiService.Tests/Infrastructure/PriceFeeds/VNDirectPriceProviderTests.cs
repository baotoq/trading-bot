using System.Net;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Infrastructure.PriceFeeds;
using TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;

namespace TradingBot.ApiService.Tests.Infrastructure.PriceFeeds;

public class VNDirectPriceProviderTests
{
    private const string Ticker = "E1VFVN30";
    private const string CacheKey = $"price:etf:{Ticker}";
    private const decimal FreshPriceVnd = 20_290m;  // 20.29 * 1000
    private const decimal StalePriceVnd = 19_500m;

    private static readonly ILogger<VNDirectPriceProvider> Logger =
        Substitute.For<ILogger<VNDirectPriceProvider>>();

    private static byte[] SerializeFreshEntry(decimal priceVnd)
    {
        var entry = PriceFeedEntry.Create(priceVnd, "VND");
        return MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
    }

    private static byte[] SerializeStaleEntry(decimal priceVnd)
    {
        var staleTime = DateTimeOffset.UtcNow.AddHours(-72); // 72 hours old, past 48h freshness window
        var entry = new PriceFeedEntry(priceVnd, staleTime.ToUnixTimeSeconds(), "VND");
        return MessagePackSerializer.Serialize(entry, MessagePackSerializerOptions.Standard);
    }

    /// <summary>
    /// Valid VNDirect dchart response with close price of 20.29 (= 20,290 VND).
    /// </summary>
    private static HttpResponseMessage CreateVNDirectResponse(decimal closePriceThousands = 20.29m) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"t\":[1234567890],\"c\":[{closePriceThousands.ToString(System.Globalization.CultureInfo.InvariantCulture)}],\"o\":[20.1],\"h\":[20.5],\"l\":[20.0],\"v\":[1000],\"s\":\"ok\"}}",
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage CreateVNDirectErrorResponse() =>
        new(HttpStatusCode.InternalServerError);

    [Fact]
    public async Task GetPriceAsync_FreshCacheHit_ReturnsFreshResultWithoutHttpCall()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(SerializeFreshEntry(FreshPriceVnd));

        var httpHandler = new MockHttpMessageHandler(_ =>
            throw new InvalidOperationException("Should not make HTTP call for fresh cache"));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/") };
        var provider = new VNDirectPriceProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetPriceAsync(Ticker, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse();
        result.Price.Should().Be(FreshPriceVnd);
        result.Currency.Should().Be("VND");
        httpHandler.SentRequests.Should().BeEmpty("no HTTP call for fresh cache hit");
    }

    [Fact]
    public async Task GetPriceAsync_StaleCacheHit_ReturnsStaleImmediatelyAndTriggersBackgroundRefresh()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns(SerializeStaleEntry(StalePriceVnd));

        var refreshCompleted = new TaskCompletionSource<bool>();
        var httpHandler = new MockHttpMessageHandler(_ =>
        {
            refreshCompleted.TrySetResult(true);
            return CreateVNDirectResponse(closePriceThousands: 20.29m);
        });

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/") };
        var provider = new VNDirectPriceProvider(httpClient, cache, Logger);

        // Act — returns stale immediately (fire-and-forget refresh happens in background)
        var result = await provider.GetPriceAsync(Ticker, CancellationToken.None);

        // Assert: stale returned immediately
        result.IsStale.Should().BeTrue("stale cache should be returned immediately without waiting for refresh");
        result.Price.Should().Be(StalePriceVnd, "should return stale cached price");

        // Wait for background refresh to complete (with timeout)
        var backgroundCompleted = await Task.WhenAny(refreshCompleted.Task, Task.Delay(3000));
        backgroundCompleted.Should().Be(refreshCompleted.Task, "background refresh should have been triggered");
        httpHandler.SentRequests.Should().HaveCount(1, "background refresh should have made one HTTP call");
    }

    [Fact]
    public async Task GetPriceAsync_EmptyCacheAndApiSuccess_ReturnsFreshResultWithCorrectVndConversion()
    {
        // Arrange
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(CacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var httpHandler = new MockHttpMessageHandler(_ => CreateVNDirectResponse(closePriceThousands: 20.29m));
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/") };
        var provider = new VNDirectPriceProvider(httpClient, cache, Logger);

        // Act
        var result = await provider.GetPriceAsync(Ticker, CancellationToken.None);

        // Assert
        result.IsStale.Should().BeFalse("empty cache with successful API returns fresh result");
        result.Price.Should().Be(20_290m, "close price 20.29 thousands-of-VND = 20,290 VND");
        result.Currency.Should().Be("VND");
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

        var httpHandler = new MockHttpMessageHandler(_ => CreateVNDirectErrorResponse());
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/") };
        var provider = new VNDirectPriceProvider(httpClient, cache, Logger);

        // Act
        var act = async () => await provider.GetPriceAsync(Ticker, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>("empty cache with API failure should propagate an exception");
    }
}

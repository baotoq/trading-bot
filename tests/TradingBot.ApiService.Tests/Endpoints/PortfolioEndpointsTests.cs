using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TradingBot.ApiService.Endpoints;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.PriceFeeds;

namespace TradingBot.ApiService.Tests.Endpoints;

[Collection("Endpoints")]
public class PortfolioEndpointsTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PortfolioEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("x-api-key", "test-api-key");

        // Set up default mock responses for price providers
        _factory.ExchangeRateMock
            .GetUsdToVndRateAsync(Arg.Any<CancellationToken>())
            .Returns(PriceFeedResult.Fresh(25000m, DateTimeOffset.UtcNow, "VND"));

        _factory.CryptoMock
            .SearchCoinIdAsync("BTC", Arg.Any<CancellationToken>())
            .Returns("bitcoin");

        _factory.CryptoMock
            .GetPriceAsync("bitcoin", Arg.Any<CancellationToken>())
            .Returns(PriceFeedResult.Fresh(95000m, DateTimeOffset.UtcNow, "USD"));

        _factory.EtfMock
            .GetPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PriceFeedResult.Fresh(20000m, DateTimeOffset.UtcNow, "VND"));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test data to avoid cross-test interference
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        db.AssetTransactions.RemoveRange(db.AssetTransactions);
        db.PortfolioAssets.RemoveRange(db.PortfolioAssets);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummary_EmptyPortfolio_ReturnsOkWithZeroTotals()
    {
        // Act
        var response = await _client.GetAsync("/api/portfolio/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<PortfolioSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalValueUsd.Should().Be(0m);
        summary.TotalValueVnd.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsset_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateAssetRequest("Bitcoin", "BTC-" + Guid.NewGuid().ToString("N")[..8], "Crypto", "USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/portfolio/assets", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await response.Content.ReadFromJsonAsync<CreateAssetResponse>();
        asset.Should().NotBeNull();
        asset!.Ticker.Should().Be(request.Ticker.ToUpperInvariant());
        asset.AssetType.Should().Be("Crypto");
    }

    [Fact]
    public async Task CreateTransaction_ValidRequest_ReturnsCreated()
    {
        // Arrange — first create an asset
        var ticker = "ETH-" + Guid.NewGuid().ToString("N")[..8];
        var createAssetRequest = new CreateAssetRequest("Ethereum", ticker, "Crypto", "USD");
        var createAssetResponse = await _client.PostAsJsonAsync("/api/portfolio/assets", createAssetRequest);
        createAssetResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await createAssetResponse.Content.ReadFromJsonAsync<CreateAssetResponse>();
        asset.Should().NotBeNull();

        // Act — create a transaction for the asset
        var transactionRequest = new CreateTransactionRequest(
            Date: new DateOnly(2025, 1, 1),
            Quantity: 0.5m,
            PricePerUnit: 3000m,
            Currency: "USD",
            Type: "Buy",
            Fee: null);

        var response = await _client.PostAsJsonAsync(
            $"/api/portfolio/assets/{asset!.Id}/transactions",
            transactionRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        transaction.Should().NotBeNull();
        transaction!.Quantity.Should().Be(0.5m);
        transaction.PricePerUnit.Should().Be(3000m);
        transaction.Type.Should().Be("Buy");
    }

    [Fact]
    public async Task GetSummary_WithAssetAndTransaction_ReturnsNonZeroTotals()
    {
        // Arrange — create a BTC asset with a transaction
        var createAssetRequest = new CreateAssetRequest("Bitcoin", "BTC", "Crypto", "USD");
        var createAssetResponse = await _client.PostAsJsonAsync("/api/portfolio/assets", createAssetRequest);
        createAssetResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await createAssetResponse.Content.ReadFromJsonAsync<CreateAssetResponse>();
        asset.Should().NotBeNull();

        var transactionRequest = new CreateTransactionRequest(
            Date: new DateOnly(2025, 1, 1),
            Quantity: 0.1m,
            PricePerUnit: 95000m,
            Currency: "USD",
            Type: "Buy",
            Fee: null);

        var createTxResponse = await _client.PostAsJsonAsync(
            $"/api/portfolio/assets/{asset!.Id}/transactions",
            transactionRequest);
        createTxResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — get summary
        var response = await _client.GetAsync("/api/portfolio/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<PortfolioSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalValueUsd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task CreateTransaction_FutureDate_ReturnsBadRequest()
    {
        // Arrange — create an asset
        var ticker = "FUTURE-" + Guid.NewGuid().ToString("N")[..6];
        var createAssetRequest = new CreateAssetRequest("FutureTest", ticker, "Crypto", "USD");
        var createAssetResponse = await _client.PostAsJsonAsync("/api/portfolio/assets", createAssetRequest);
        var asset = await createAssetResponse.Content.ReadFromJsonAsync<CreateAssetResponse>();

        // Act — submit a transaction with a future date
        var transactionRequest = new CreateTransactionRequest(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            Quantity: 1m,
            PricePerUnit: 100m,
            Currency: "USD",
            Type: "Buy",
            Fee: null);

        var response = await _client.PostAsJsonAsync(
            $"/api/portfolio/assets/{asset!.Id}/transactions",
            transactionRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_DuplicateTicker_ReturnsConflict()
    {
        // Arrange
        var ticker = "DUP-" + Guid.NewGuid().ToString("N")[..6];
        var request = new CreateAssetRequest("Test Asset", ticker, "Crypto", "USD");

        // Create first
        var first = await _client.PostAsJsonAsync("/api/portfolio/assets", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — create duplicate
        var second = await _client.PostAsJsonAsync("/api/portfolio/assets", request);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

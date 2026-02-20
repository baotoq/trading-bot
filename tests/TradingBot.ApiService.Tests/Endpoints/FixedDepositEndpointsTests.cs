using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TradingBot.ApiService.Endpoints;
using TradingBot.ApiService.Infrastructure.Data;

namespace TradingBot.ApiService.Tests.Endpoints;

[Collection("Endpoints")]
public class FixedDepositEndpointsTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FixedDepositEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("x-api-key", "test-api-key");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test data to avoid cross-test interference
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        db.FixedDeposits.RemoveRange(db.FixedDeposits);
        await db.SaveChangesAsync();
    }

    private static CreateFixedDepositRequest BuildValidRequest(string? bankName = null) =>
        new(
            BankName: bankName ?? "Test Bank",
            Principal: 100_000_000m,
            AnnualInterestRate: 0.06m,
            StartDate: new DateOnly(2025, 1, 1),
            MaturityDate: new DateOnly(2026, 1, 1),
            CompoundingFrequency: "Monthly");

    [Fact]
    public async Task GetAll_Empty_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/portfolio/fixed-deposits/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<FixedDepositResponse>>();
        list.Should().NotBeNull();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = BuildValidRequest("Bank A");

        // Act
        var response = await _client.PostAsJsonAsync("/api/portfolio/fixed-deposits/", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var deposit = await response.Content.ReadFromJsonAsync<FixedDepositResponse>();
        deposit.Should().NotBeNull();
        deposit!.BankName.Should().Be("Bank A");
        deposit.PrincipalVnd.Should().Be(100_000_000m);
        deposit.AnnualInterestRate.Should().Be(0.06m);
        deposit.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetById_ExistingDeposit_ReturnsOk()
    {
        // Arrange — create a deposit
        var request = BuildValidRequest("Bank GetById");
        var createResponse = await _client.PostAsJsonAsync("/api/portfolio/fixed-deposits/", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<FixedDepositResponse>();
        created.Should().NotBeNull();

        // Act
        var response = await _client.GetAsync($"/api/portfolio/fixed-deposits/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deposit = await response.Content.ReadFromJsonAsync<FixedDepositResponse>();
        deposit.Should().NotBeNull();
        deposit!.Id.Should().Be(created.Id);
        deposit.BankName.Should().Be("Bank GetById");
    }

    [Fact]
    public async Task Update_ExistingDeposit_ReturnsOk()
    {
        // Arrange — create a deposit
        var createRequest = BuildValidRequest("Bank Before Update");
        var createResponse = await _client.PostAsJsonAsync("/api/portfolio/fixed-deposits/", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<FixedDepositResponse>();
        created.Should().NotBeNull();

        // Act — update bank name
        var updateRequest = new UpdateFixedDepositRequest(
            BankName: "Bank After Update",
            Principal: 100_000_000m,
            AnnualInterestRate: 0.07m,
            StartDate: new DateOnly(2025, 1, 1),
            MaturityDate: new DateOnly(2026, 1, 1),
            CompoundingFrequency: "Monthly");

        var response = await _client.PutAsJsonAsync($"/api/portfolio/fixed-deposits/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<FixedDepositResponse>();
        updated.Should().NotBeNull();
        updated!.BankName.Should().Be("Bank After Update");
        updated.AnnualInterestRate.Should().Be(0.07m);
    }

    [Fact]
    public async Task Delete_ExistingDeposit_ReturnsNoContent()
    {
        // Arrange — create a deposit
        var request = BuildValidRequest("Bank To Delete");
        var createResponse = await _client.PostAsJsonAsync("/api/portfolio/fixed-deposits/", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<FixedDepositResponse>();
        created.Should().NotBeNull();

        // Act — delete it
        var deleteResponse = await _client.DeleteAsync($"/api/portfolio/fixed-deposits/{created!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone — should return 404
        var getResponse = await _client.GetAsync($"/api/portfolio/fixed-deposits/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_InvalidCompoundingFrequency_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateFixedDepositRequest(
            BankName: "Test Bank",
            Principal: 100_000_000m,
            AnnualInterestRate: 0.06m,
            StartDate: new DateOnly(2025, 1, 1),
            MaturityDate: new DateOnly(2026, 1, 1),
            CompoundingFrequency: "InvalidFrequency");

        // Act
        var response = await _client.PostAsJsonAsync("/api/portfolio/fixed-deposits/", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/portfolio/fixed-deposits/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

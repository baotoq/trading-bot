using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Tests.Application.Specifications.Portfolio;

/// <summary>
/// Integration tests verifying all portfolio entities round-trip correctly through PostgreSQL.
/// Proves: Vogen converters registered, precision correct, FK relationships working,
/// enums stored as strings, cascade delete working, status lifecycle working.
/// </summary>
public class PortfolioEntityTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PortfolioEntityTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PortfolioAsset_PersistsAndRoundTrips()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var asset = PortfolioAsset.Create("Bitcoin", "BTC", AssetType.Crypto, Currency.USD);
        asset.ClearDomainEvents();
        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync();

        var loaded = await db.PortfolioAssets.FindAsync(asset.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Bitcoin");
        loaded.Ticker.Should().Be("BTC");
        loaded.AssetType.Should().Be(AssetType.Crypto);
        loaded.NativeCurrency.Should().Be(Currency.USD);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PortfolioAsset_WithTransaction_PersistsAndLoads()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var asset = PortfolioAsset.Create("Bitcoin", "BTC", AssetType.Crypto, Currency.USD);
        asset.ClearDomainEvents();
        asset.AddTransaction(
            new DateOnly(2024, 6, 15),
            0.001m,
            50_000m,
            Currency.USD,
            TransactionType.Buy,
            0.50m,
            TransactionSource.Manual);

        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync();

        // Reload with navigation
        var loaded = await db.PortfolioAssets
            .Include(a => a.Transactions)
            .FirstAsync(a => a.Id == asset.Id);

        loaded.Transactions.Should().HaveCount(1);
        var tx = loaded.Transactions[0];
        tx.Date.Should().Be(new DateOnly(2024, 6, 15));
        tx.Quantity.Should().Be(0.001m);
        tx.PricePerUnit.Should().Be(50_000m);
        tx.Currency.Should().Be(Currency.USD);
        tx.Type.Should().Be(TransactionType.Buy);
        tx.Fee.Should().Be(0.50m);
        tx.Source.Should().Be(TransactionSource.Manual);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PortfolioAsset_ETF_WithIntegerQuantity_Persists()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var asset = PortfolioAsset.Create("VN30 ETF", "E1VFVN30", AssetType.ETF, Currency.VND);
        asset.ClearDomainEvents();
        asset.AddTransaction(
            new DateOnly(2024, 3, 1),
            100m,
            15_000m,
            Currency.VND,
            TransactionType.Buy,
            null,
            TransactionSource.Manual);

        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync();

        var loaded = await db.PortfolioAssets
            .Include(a => a.Transactions)
            .FirstAsync(a => a.Id == asset.Id);

        loaded.Transactions.Should().HaveCount(1);
        loaded.Transactions[0].Quantity.Should().Be(100m);
        loaded.Transactions[0].Fee.Should().BeNull();

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task FixedDeposit_PersistsAndRoundTrips()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var deposit = FixedDeposit.Create(
            "BIDV",
            VndAmount.From(10_000_000m),
            0.065m,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 7, 1),
            CompoundingFrequency.Simple);
        deposit.ClearDomainEvents();

        db.FixedDeposits.Add(deposit);
        await db.SaveChangesAsync();

        var loaded = await db.FixedDeposits.FindAsync(deposit.Id);
        loaded.Should().NotBeNull();
        loaded!.BankName.Should().Be("BIDV");
        loaded.Principal.Value.Should().Be(10_000_000m);
        loaded.AnnualInterestRate.Should().Be(0.065m);
        loaded.StartDate.Should().Be(new DateOnly(2024, 1, 1));
        loaded.MaturityDate.Should().Be(new DateOnly(2024, 7, 1));
        loaded.CompoundingFrequency.Should().Be(CompoundingFrequency.Simple);
        loaded.Status.Should().Be(FixedDepositStatus.Active);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task FixedDeposit_StatusLifecycle_ActiveToMatured()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var deposit = FixedDeposit.Create(
            "Vietcombank",
            VndAmount.From(50_000_000m),
            0.055m,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31),
            CompoundingFrequency.Quarterly);
        deposit.ClearDomainEvents();

        db.FixedDeposits.Add(deposit);
        await db.SaveChangesAsync();

        // Mature the deposit
        deposit.Mature();
        db.FixedDeposits.Update(deposit);
        await db.SaveChangesAsync();

        // Reload and verify
        var loaded = await db.FixedDeposits.FindAsync(deposit.Id);
        loaded!.Status.Should().Be(FixedDepositStatus.Matured);
        loaded.UpdatedAt.Should().NotBeNull();

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task AssetTransaction_CascadeDelete_WithPortfolioAsset()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var asset = PortfolioAsset.Create("Bitcoin", "BTC", AssetType.Crypto, Currency.USD);
        asset.ClearDomainEvents();
        asset.AddTransaction(new DateOnly(2024, 1, 1), 0.001m, 50_000m, Currency.USD, TransactionType.Buy, null, TransactionSource.Manual);
        asset.AddTransaction(new DateOnly(2024, 2, 1), 0.002m, 48_000m, Currency.USD, TransactionType.Buy, 0.25m, TransactionSource.Bot);

        db.PortfolioAssets.Add(asset);
        await db.SaveChangesAsync();

        // Verify transactions exist
        var txCount = await db.AssetTransactions.CountAsync(t => t.PortfolioAssetId == asset.Id);
        txCount.Should().Be(2);

        // Remove the asset
        db.PortfolioAssets.Remove(asset);
        await db.SaveChangesAsync();

        // Cascade delete should remove transactions
        var remainingTxCount = await db.AssetTransactions.CountAsync(t => t.PortfolioAssetId == asset.Id);
        remainingTxCount.Should().Be(0);

        await transaction.RollbackAsync();
    }
}

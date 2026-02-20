using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Specifications;
using TradingBot.ApiService.Application.Specifications.Purchases;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Tests.Application.Specifications.Purchases;

/// <summary>
/// Integration tests verifying all Purchase specifications against real PostgreSQL.
/// Proves QP-02: specs translate to server-side SQL, no client-side evaluation.
/// Uses TestContainers to spin up a real PostgreSQL instance with EF Core migrations applied,
/// including Vogen value converters via TradingBotDbContext.ConfigureConventions.
/// </summary>
public class PurchaseSpecsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PurchaseSpecsTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Creates a minimal Purchase using the factory method.
    /// ClearDomainEvents prevents interceptor issues during SaveChangesAsync in tests.
    /// </summary>
    private static Purchase CreatePurchase(string? multiplierTier = null)
    {
        var purchase = Purchase.Create(
            price: Price.From(50_000m),
            cost: UsdAmount.From(10m),
            multiplier: Multiplier.From(1m),
            multiplierTier: multiplierTier,
            dropPercentage: Percentage.From(0m),
            high30Day: 0m,
            ma200Day: 0m,
            isDryRun: false);
        purchase.ClearDomainEvents();
        return purchase;
    }

    /// <summary>
    /// Forces a specific ExecutedAt via EF Core change tracker (private setter bypass for tests).
    /// </summary>
    private static void SetExecutedAt(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Purchase> entry, DateTimeOffset date)
    {
        entry.Property(nameof(Purchase.ExecutedAt)).CurrentValue = date;
    }

    [Fact]
    public async Task PurchaseFilledStatusSpec_ExcludesDryRunAndFailedPurchases()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed purchases
        // A: Filled, not dry-run -> should be included
        var purchaseA = CreatePurchase("5% Drop");
        purchaseA.RecordFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), "order-a", 0.0002m, 0.0002m, 10m, 1);
        purchaseA.ClearDomainEvents();

        // B: Filled, not dry-run, no tier -> should be included
        var purchaseB = CreatePurchase(null);
        purchaseB.RecordFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), "order-b", 0.0002m, 0.0004m, 20m, 2);
        purchaseB.ClearDomainEvents();

        // C: Failed, not dry-run -> should be excluded (not Filled/PartiallyFilled)
        var purchaseC = CreatePurchase();
        purchaseC.RecordFailure("Order rejected by exchange");
        purchaseC.ClearDomainEvents();

        // D: Filled but dry-run -> should be excluded
        var purchaseD = Purchase.Create(
            price: Price.From(50_000m),
            cost: UsdAmount.From(10m),
            multiplier: Multiplier.From(1m),
            multiplierTier: null,
            dropPercentage: Percentage.From(0m),
            high30Day: 0m,
            ma200Day: 0m,
            isDryRun: true);
        purchaseD.ClearDomainEvents();
        purchaseD.RecordDryRunFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), 0m, 0m, 0);
        purchaseD.ClearDomainEvents();

        // E: PartiallyFilled, not dry-run -> should be included
        var purchaseE = CreatePurchase("10% Drop");
        purchaseE.RecordFill(Quantity.From(0.00015m), Price.From(50_000m), UsdAmount.From(7.5m), "order-e", 0.0002m, 0.00055m, 27.5m, 3); // partial: < 95% of requested
        purchaseE.ClearDomainEvents();

        db.Purchases.AddRange(purchaseA, purchaseB, purchaseC, purchaseD, purchaseE);
        await db.SaveChangesAsync();

        // Act
        var spec = new PurchaseFilledStatusSpec();
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: A, B, E included; C (failed) and D (dry-run) excluded
        results.Should().HaveCount(3);
        results.Select(p => p.Id).Should().Contain([purchaseA.Id, purchaseB.Id, purchaseE.Id]);
        results.Select(p => p.Id).Should().NotContain(purchaseC.Id);
        results.Select(p => p.Id).Should().NotContain(purchaseD.Id);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PurchaseDateRangeSpec_FiltersToDateRange()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed purchases at different dates
        var purchaseJan = CreatePurchase();
        var purchaseFeb = CreatePurchase();
        var purchaseMar = CreatePurchase();

        db.Purchases.AddRange(purchaseJan, purchaseFeb, purchaseMar);
        await db.SaveChangesAsync();

        // Override ExecutedAt via change tracker to simulate purchases at different dates
        db.Entry(purchaseJan).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        db.Entry(purchaseFeb).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 2, 10, 12, 0, 0, TimeSpan.Zero);
        db.Entry(purchaseMar).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 3, 5, 12, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        // Act: filter to January only
        var spec = new PurchaseDateRangeSpec(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: only January purchase
        results.Should().HaveCount(1);
        results.Single().Id.Should().Be(purchaseJan.Id);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PurchaseTierFilterSpec_FiltersByTier()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed purchases with different tiers
        var purchase5Drop = CreatePurchase("5% Drop");
        var purchase10Drop = CreatePurchase("10% Drop");
        var purchaseBase = CreatePurchase(null);

        db.Purchases.AddRange(purchase5Drop, purchase10Drop, purchaseBase);
        await db.SaveChangesAsync();

        // Act: filter to "5% Drop" tier only
        var spec = new PurchaseTierFilterSpec("5% Drop");
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: only the "5% Drop" purchase
        results.Should().HaveCount(1);
        results.Single().Id.Should().Be(purchase5Drop.Id);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PurchaseTierFilterSpec_HandlesBaseTier()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed: null tier and "Base" tier -- both should match PurchaseTierFilterSpec("Base")
        var purchaseNullTier = CreatePurchase(null);
        var purchaseBaseTier = CreatePurchase("Base");
        var purchaseOtherTier = CreatePurchase("5% Drop");

        db.Purchases.AddRange(purchaseNullTier, purchaseBaseTier, purchaseOtherTier);
        await db.SaveChangesAsync();

        // Act: filter to "Base" tier (matches null OR "Base")
        var spec = new PurchaseTierFilterSpec("Base");
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: both null-tier and "Base"-tier purchases included; "5% Drop" excluded
        results.Should().HaveCount(2);
        results.Select(p => p.Id).Should().Contain([purchaseNullTier.Id, purchaseBaseTier.Id]);
        results.Select(p => p.Id).Should().NotContain(purchaseOtherTier.Id);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PurchaseCursorSpec_FiltersBeforeCursorAndSortsDescending()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed purchases at different dates
        var purchaseEarly = CreatePurchase();
        var purchaseMid = CreatePurchase();
        var purchaseLate = CreatePurchase();

        db.Purchases.AddRange(purchaseEarly, purchaseMid, purchaseLate);
        await db.SaveChangesAsync();

        // Set specific dates via change tracker
        var earlyDate = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var midDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero);
        var lateDate = new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var cursorDate = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero);

        db.Entry(purchaseEarly).Property(nameof(Purchase.ExecutedAt)).CurrentValue = earlyDate;
        db.Entry(purchaseMid).Property(nameof(Purchase.ExecutedAt)).CurrentValue = midDate;
        db.Entry(purchaseLate).Property(nameof(Purchase.ExecutedAt)).CurrentValue = lateDate;
        await db.SaveChangesAsync();

        // Act: cursor at Feb 15 -- should only return purchases before that date
        var spec = new PurchaseCursorSpec(cursorDate);
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: only early (Jan) and mid (Feb 10) purchases, ordered descending
        results.Should().HaveCount(2);
        results.Select(p => p.Id).Should().Contain([purchaseEarly.Id, purchaseMid.Id]);
        results.Select(p => p.Id).Should().NotContain(purchaseLate.Id);

        // Verify descending order: mid (Feb 10) comes before early (Jan 10)
        results[0].Id.Should().Be(purchaseMid.Id);
        results[1].Id.Should().Be(purchaseEarly.Id);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task PurchasesOrderedByDateSpec_SortsDescendingWithNoTracking()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed multiple purchases
        var purchaseFirst = CreatePurchase();
        var purchaseSecond = CreatePurchase();
        var purchaseThird = CreatePurchase();

        db.Purchases.AddRange(purchaseFirst, purchaseSecond, purchaseThird);
        await db.SaveChangesAsync();

        // Set distinct dates
        db.Entry(purchaseFirst).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        db.Entry(purchaseSecond).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        db.Entry(purchaseThird).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        // Act
        var spec = new PurchasesOrderedByDateSpec();
        var results = await db.Purchases.WithSpecification(spec).ToListAsync();

        // Assert: results are ordered descending (most recent first)
        // The 3 seeded purchases should be in Dec, Jun, Jan order
        var seededIds = new[] { purchaseFirst.Id, purchaseSecond.Id, purchaseThird.Id };
        var seededResults = results.Where(p => seededIds.Contains(p.Id)).ToList();

        seededResults.Should().HaveCount(3);
        seededResults[0].Id.Should().Be(purchaseThird.Id);   // Dec 2024 first
        seededResults[1].Id.Should().Be(purchaseSecond.Id);  // Jun 2024 second
        seededResults[2].Id.Should().Be(purchaseFirst.Id);   // Jan 2024 last

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task ComposedSpecs_FilledStatusAndDateRange_WorkTogether()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed: mix of status, dates, dry-run flags
        // Filled Jan, not dry-run -> INCLUDED (filled + in date range)
        var filledJan = CreatePurchase();
        filledJan.RecordFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), "order-1", 0.0002m, 0.0002m, 10m, 1);
        filledJan.ClearDomainEvents();

        // Failed Jan, not dry-run -> EXCLUDED (not filled)
        var failedJan = CreatePurchase();
        failedJan.RecordFailure("Exchange error");
        failedJan.ClearDomainEvents();

        // Filled Feb, not dry-run -> EXCLUDED (outside date range)
        var filledFeb = CreatePurchase();
        filledFeb.RecordFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), "order-3", 0.0002m, 0.0004m, 20m, 2);
        filledFeb.ClearDomainEvents();

        // Filled Jan, dry-run -> EXCLUDED (dry run)
        var dryRunJan = Purchase.Create(
            Price.From(50_000m), UsdAmount.From(10m), Multiplier.From(1m),
            null, Percentage.From(0m), 0m, 0m, isDryRun: true);
        dryRunJan.ClearDomainEvents();
        dryRunJan.RecordDryRunFill(Quantity.From(0.0002m), Price.From(50_000m), UsdAmount.From(10m), 0m, 0m, 0);
        dryRunJan.ClearDomainEvents();

        db.Purchases.AddRange(filledJan, failedJan, filledFeb, dryRunJan);
        await db.SaveChangesAsync();

        // Set dates via change tracker
        db.Entry(filledJan).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        db.Entry(failedJan).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);
        db.Entry(filledFeb).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero);
        db.Entry(dryRunJan).Property(nameof(Purchase.ExecutedAt)).CurrentValue =
            new DateTimeOffset(2024, 1, 25, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        // Act: chain PurchaseFilledStatusSpec + PurchaseDateRangeSpec (January only)
        var results = await db.Purchases
            .WithSpecification(new PurchaseFilledStatusSpec())
            .WithSpecification(new PurchaseDateRangeSpec(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31)))
            .ToListAsync();

        // Assert: only filledJan survives both filters
        results.Should().HaveCount(1);
        results.Single().Id.Should().Be(filledJan.Id);

        await transaction.RollbackAsync();
    }
}

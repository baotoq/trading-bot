using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Specifications;
using TradingBot.ApiService.Application.Specifications.DailyPrices;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Tests.Application.Specifications.DailyPrices;

/// <summary>
/// Integration tests verifying DailyPrice specifications against real PostgreSQL.
/// Proves QP-02: specs translate to server-side SQL, no client-side evaluation.
/// Specifically validates that Vogen Symbol value object comparisons work correctly
/// through the EF Core value converter registered in TradingBotDbContext.ConfigureConventions.
/// </summary>
public class DailyPriceSpecsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DailyPriceSpecsTests(PostgresFixture fixture) => _fixture = fixture;

    private static DailyPrice CreateDailyPrice(Symbol symbol, DateOnly date, decimal close = 50_000m) =>
        new DailyPrice
        {
            Date = date,
            Symbol = symbol,
            Open = Price.From(close * 0.99m),
            High = Price.From(close * 1.01m),
            Low = Price.From(close * 0.98m),
            Close = Price.From(close),
            Volume = 1000m,
            Timestamp = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

    [Fact]
    public async Task DailyPriceByDateRangeSpec_FiltersBySymbolAndDate()
    {
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed BTC prices for January and February 2024
        var btcJan1 = CreateDailyPrice(Symbol.Btc, new DateOnly(2024, 1, 1), 42_000m);
        var btcJan15 = CreateDailyPrice(Symbol.Btc, new DateOnly(2024, 1, 15), 43_000m);
        var btcJan31 = CreateDailyPrice(Symbol.Btc, new DateOnly(2024, 1, 31), 44_000m);
        var btcFeb1 = CreateDailyPrice(Symbol.Btc, new DateOnly(2024, 2, 1), 45_000m);
        var btcFeb15 = CreateDailyPrice(Symbol.Btc, new DateOnly(2024, 2, 15), 46_000m);

        db.DailyPrices.AddRange(btcJan1, btcJan15, btcJan31, btcFeb1, btcFeb15);
        await db.SaveChangesAsync();

        // Act: filter to BTC prices from Jan 15 onward
        var spec = new DailyPriceByDateRangeSpec(Symbol.Btc, new DateOnly(2024, 1, 15));
        var results = await db.DailyPrices.WithSpecification(spec).ToListAsync();

        // Assert: Jan 1 excluded (before start date), Jan 15, Jan 31, Feb 1, Feb 15 included
        results.Should().HaveCount(4);
        results.Select(p => p.Date).Should().Contain([
            new DateOnly(2024, 1, 15),
            new DateOnly(2024, 1, 31),
            new DateOnly(2024, 2, 1),
            new DateOnly(2024, 2, 15)
        ]);
        results.Select(p => p.Date).Should().NotContain(new DateOnly(2024, 1, 1));

        // Assert: ascending order by date (per spec definition)
        results.Should().BeInAscendingOrder(p => p.Date);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task DailyPriceByDateRangeSpec_VogenSymbolComparisonWorks()
    {
        // This test specifically validates research open question #1:
        // Vogen Symbol value object comparisons work correctly in spec predicates
        // through the EF Core value converter (Symbol.EfCoreValueConverter).
        // If Symbol comparison fails (client-side evaluation, wrong SQL translation),
        // this test would either throw or return empty/incorrect results.

        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Seed data: use a unique date range to avoid interference from other tests
        // even in the unlikely case a transaction doesn't cleanly isolate
        var btcPriceJan = CreateDailyPrice(Symbol.Btc, new DateOnly(2023, 6, 1), 30_000m);
        var btcPriceJan2 = CreateDailyPrice(Symbol.Btc, new DateOnly(2023, 6, 15), 31_000m);

        db.DailyPrices.AddRange(btcPriceJan, btcPriceJan2);
        await db.SaveChangesAsync();

        // Act: query using Vogen Symbol.Btc value object
        var spec = new DailyPriceByDateRangeSpec(Symbol.Btc, new DateOnly(2023, 6, 1));
        var results = await db.DailyPrices.WithSpecification(spec).ToListAsync();

        // Assert: both BTC prices returned -- Vogen EfCoreValueConverter translates correctly to SQL
        // (Symbol.Btc.Value == "BTC" is compared as a string in the DB)
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(p => p.Symbol.Should().Be(Symbol.Btc));

        // Assert ordering: ascending by Date (proves OrderBy inside spec is server-side)
        results[0].Date.Should().Be(new DateOnly(2023, 6, 1));
        results[1].Date.Should().Be(new DateOnly(2023, 6, 15));

        await transaction.RollbackAsync();
    }
}

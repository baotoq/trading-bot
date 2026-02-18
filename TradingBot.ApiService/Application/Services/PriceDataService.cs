using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Implementation of price data service for historical candle management and smart multiplier calculations.
/// Fetches data from Hyperliquid, stores in PostgreSQL, and provides cached calculations for 30-day high and 200-day SMA.
/// </summary>
public class PriceDataService(
    TradingBotDbContext dbContext,
    HyperliquidClient hyperliquidClient,
    IOptionsMonitor<DcaOptions> dcaOptions,
    ILogger<PriceDataService> logger) : IPriceDataService
{
    public async Task BootstrapHistoricalDataAsync(string symbol, CancellationToken ct = default)
    {
        var options = dcaOptions.CurrentValue;
        var maPeriod = options.BearMarketMaPeriod; // Default 200 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-maPeriod);

        // Check if historical data already exists (allow 5-day tolerance for gaps)
        var existingCount = await dbContext.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate)
            .CountAsync(ct);

        if (existingCount >= maPeriod - 5)
        {
            logger.LogInformation("Historical data already bootstrapped for {Symbol}: {Count} days available",
                symbol, existingCount);
            return;
        }

        logger.LogInformation("Bootstrapping historical data for {Symbol}: fetching {Days} days from {Start} to {End}",
            symbol, maPeriod, startDate, today);

        // Fetch candles from Hyperliquid
        var startDateOffset = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endDateOffset = new DateTimeOffset(today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var candles = await hyperliquidClient.GetCandlesAsync(symbol, startDateOffset, endDateOffset, ct);

        // Upsert candles into database
        foreach (var candle in candles)
        {
            var existing = await dbContext.DailyPrices
                .FirstOrDefaultAsync(p => p.Date == candle.Date && p.Symbol == candle.Symbol, ct);

            if (existing != null)
            {
                // Update existing record
                existing.Open = candle.Open;
                existing.High = candle.High;
                existing.Low = candle.Low;
                existing.Close = candle.Close;
                existing.Volume = candle.Volume;
                existing.Timestamp = candle.Timestamp;
            }
            else
            {
                // Insert new record
                var dailyPrice = new DailyPrice
                {
                    Date = candle.Date,
                    Symbol = candle.Symbol,
                    Open = candle.Open,
                    High = candle.High,
                    Low = candle.Low,
                    Close = candle.Close,
                    Volume = candle.Volume,
                    Timestamp = candle.Timestamp
                };
                dbContext.DailyPrices.Add(dailyPrice);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Bootstrapped {Count} days of price data for {Symbol}", candles.Count, symbol);
    }

    public async Task FetchAndStoreDailyCandleAsync(string symbol, CancellationToken ct = default)
    {
        // Find latest date in database
        var latestDate = await dbContext.DailyPrices
            .Where(p => p.Symbol == symbol)
            .MaxAsync(p => (DateOnly?)p.Date, ct);

        // If no data exists, bootstrap instead
        if (latestDate == null)
        {
            logger.LogInformation("No existing data for {Symbol}, delegating to bootstrap", symbol);
            await BootstrapHistoricalDataAsync(symbol, ct);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // If data is current, no need to fetch
        if (latestDate.Value >= today)
        {
            logger.LogDebug("Price data for {Symbol} is already current (latest: {Latest})", symbol, latestDate.Value);
            return;
        }

        // Fetch any missing candles from latest date to today
        var startDateOffset = new DateTimeOffset(latestDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endDateOffset = new DateTimeOffset(today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        logger.LogInformation("Fetching daily candles for {Symbol} from {Start} to {End}",
            symbol, latestDate.Value, today);

        var candles = await hyperliquidClient.GetCandlesAsync(symbol, startDateOffset, endDateOffset, ct);

        // Upsert candles
        foreach (var candle in candles)
        {
            var existing = await dbContext.DailyPrices
                .FirstOrDefaultAsync(p => p.Date == candle.Date && p.Symbol == candle.Symbol, ct);

            if (existing != null)
            {
                // Update existing record
                existing.Open = candle.Open;
                existing.High = candle.High;
                existing.Low = candle.Low;
                existing.Close = candle.Close;
                existing.Volume = candle.Volume;
                existing.Timestamp = candle.Timestamp;
            }
            else
            {
                // Insert new record
                var dailyPrice = new DailyPrice
                {
                    Date = candle.Date,
                    Symbol = candle.Symbol,
                    Open = candle.Open,
                    High = candle.High,
                    Low = candle.Low,
                    Close = candle.Close,
                    Volume = candle.Volume,
                    Timestamp = candle.Timestamp
                };
                dbContext.DailyPrices.Add(dailyPrice);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        var newLatestDate = candles.Any() ? candles.Max(c => c.Date) : latestDate.Value;
        logger.LogInformation("Refreshed {Count} daily candles for {Symbol}, latest: {LatestDate}",
            candles.Count, symbol, newLatestDate);
    }

    public async Task<decimal> Get30DayHighAsync(string symbol, CancellationToken ct = default)
    {
        var options = dcaOptions.CurrentValue;
        var lookbackDays = options.HighLookbackDays; // Default 30
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-lookbackDays);

        // Query max close price in lookback period
        var maxClose = await dbContext.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= today)
            .MaxAsync(p => (decimal?)p.Close, ct);

        if (maxClose == null)
        {
            logger.LogWarning("No price data available for {Symbol} 30-day high calculation (lookback: {Days} days)",
                symbol, lookbackDays);
            return 0; // Return 0 to signal "data unavailable"
        }

        var count = await dbContext.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= today)
            .CountAsync(ct);

        logger.LogDebug("30-day high for {Symbol}: {High} (from {Count} days)", symbol, maxClose.Value, count);

        return maxClose.Value;
    }

    public async Task<decimal> Get200DaySmaAsync(string symbol, CancellationToken ct = default)
    {
        var options = dcaOptions.CurrentValue;
        var maPeriod = options.BearMarketMaPeriod; // Default 200
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-maPeriod);

        // Load close prices into memory for SMA calculation
        var closePrices = await dbContext.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= today)
            .OrderBy(p => p.Date)
            .Select(p => p.Close)
            .ToListAsync(ct);

        // Check if we have enough data (allow 10% tolerance for gaps)
        var minRequiredDays = (int)(maPeriod * 0.9m);
        if (closePrices.Count < minRequiredDays)
        {
            logger.LogWarning("Insufficient data for {Symbol} {MaPeriod}-day SMA: only {Count} days available (need {Required})",
                symbol, maPeriod, closePrices.Count, minRequiredDays);
            return 0; // Return 0 to signal "data unavailable"
        }

        // Calculate simple moving average (extract underlying decimal for LINQ Average)
        var sma = closePrices.Average(p => p.Value);

        logger.LogDebug("200-day SMA for {Symbol}: {SMA} (from {Count} days)", symbol, sma, closePrices.Count);

        return sma;
    }
}

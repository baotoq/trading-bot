using System.Text.Json;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Infrastructure.CoinGecko;

/// <summary>
/// CoinGecko API client for fetching historical BTC daily price data.
/// Handles rate limiting (25 calls/min) and chunked 90-day requests for daily granularity.
/// </summary>
public class CoinGeckoClient(HttpClient httpClient, ILogger<CoinGeckoClient> logger)
{
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);

    /// <summary>
    /// Fetches daily BTC price data for the specified date range.
    /// Automatically chunks requests into 90-day windows to get daily granularity.
    /// Rate-limited to 25 calls/min (2.5s delay between calls).
    /// </summary>
    public async Task<List<DailyPrice>> FetchDailyDataAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var chunks = ChunkDateRange(startDate, endDate);
        var allPrices = new List<DailyPrice>();

        logger.LogInformation(
            "Fetching BTC daily data from {StartDate} to {EndDate} in {ChunkCount} chunks",
            startDate, endDate, chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            var (chunkStart, chunkEnd) = chunks[i];

            await _rateLimiter.WaitAsync(ct);
            try
            {
                // Convert DateOnly to Unix timestamps
                var fromTimestamp = new DateTimeOffset(chunkStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeSeconds();
                var toTimestamp = new DateTimeOffset(chunkEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)).ToUnixTimeSeconds();

                // Call CoinGecko API
                var url = $"coins/bitcoin/market_chart/range?vs_currency=usd&from={fromTimestamp}&to={toTimestamp}";
                var response = await httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                var marketChart = JsonSerializer.Deserialize<MarketChartResponse>(json);

                if (marketChart?.Prices == null || marketChart.Prices.Count == 0)
                {
                    logger.LogWarning(
                        "No data returned for chunk {ChunkNumber}/{TotalChunks}: {StartDate} to {EndDate}",
                        i + 1, chunks.Count, chunkStart, chunkEnd);
                    continue;
                }

                // Group price points by date (UTC) and create DailyPrice records
                var dailyPrices = marketChart.Prices
                    .GroupBy(p => DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).UtcDateTime.Date)
                    .Select(g =>
                    {
                        // Use last price point of the day as close
                        var lastPoint = g.OrderBy(p => p[0]).Last();
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)lastPoint[0]);
                        var closePrice = (decimal)lastPoint[1];

                        // Get volume for the day
                        var date = g.Key;
                        var volumePoints = marketChart.TotalVolumes?
                            .Where(v =>
                            {
                                var vDate = DateTimeOffset.FromUnixTimeMilliseconds((long)v[0]).UtcDateTime.Date;
                                return vDate == date;
                            })
                            .ToList() ?? [];

                        var volume = volumePoints.Count > 0
                            ? (decimal)volumePoints.Last()[1]
                            : 0m;

                        return new DailyPrice
                        {
                            Date = DateOnly.FromDateTime(date),
                            Symbol = "BTC",
                            Open = closePrice,  // Free tier limitation: O=H=L=C
                            High = closePrice,
                            Low = closePrice,
                            Close = closePrice,
                            Volume = volume,
                            Timestamp = timestamp
                        };
                    })
                    .ToList();

                allPrices.AddRange(dailyPrices);

                logger.LogInformation(
                    "Fetched chunk {ChunkNumber}/{TotalChunks}: {StartDate} to {EndDate}, {Count} daily prices",
                    i + 1, chunks.Count, chunkStart, chunkEnd, dailyPrices.Count);

                // Rate limiting: 25 calls/min = 2.4s between calls, round up to 2.5s
                if (i < chunks.Count - 1) // Don't delay after last chunk
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2500), ct);
                }
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        // Deduplicate by date (keep last)
        var deduplicated = allPrices
            .GroupBy(p => p.Date)
            .Select(g => g.Last())
            .OrderBy(p => p.Date)
            .ToList();

        logger.LogInformation(
            "Completed fetching {TotalDays} unique daily prices from {StartDate} to {EndDate}",
            deduplicated.Count, startDate, endDate);

        return deduplicated;
    }

    /// <summary>
    /// Breaks a date range into 90-day chunks for API calls.
    /// CoinGecko free tier returns hourly data for ranges > 90 days.
    /// </summary>
    private static List<(DateOnly Start, DateOnly End)> ChunkDateRange(
        DateOnly start,
        DateOnly end,
        int chunkSizeDays = 90)
    {
        var chunks = new List<(DateOnly, DateOnly)>();
        var current = start;

        while (current <= end)
        {
            var chunkEnd = current.AddDays(chunkSizeDays - 1);
            if (chunkEnd > end)
            {
                chunkEnd = end;
            }

            chunks.Add((current, chunkEnd));
            current = chunkEnd.AddDays(1);
        }

        return chunks;
    }
}

/// <summary>
/// CoinGecko market chart API response.
/// </summary>
internal class MarketChartResponse
{
    public List<List<decimal>> Prices { get; set; } = [];
    public List<List<decimal>>? TotalVolumes { get; set; }
    public List<List<decimal>>? MarketCaps { get; set; }
}


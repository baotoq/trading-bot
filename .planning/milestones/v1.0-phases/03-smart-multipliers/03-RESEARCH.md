# Phase 3: Smart Multipliers - Research

**Researched:** 2026-02-12
**Domain:** Time-series price data management, moving average calculations, dynamic DCA multiplier logic
**Confidence:** HIGH

## Summary

This phase adds intelligence to the existing DCA bot by adjusting buy amounts based on market conditions using two multipliers: (1) dip severity calculated from 30-day high, and (2) bear market indicator using 200-day SMA. The research confirms that Hyperliquid's `candleSnapshot` endpoint provides the necessary historical candle data, PostgreSQL is well-suited for storing time-series price data, and C# decimal type handles cryptocurrency precision requirements. The standard approach is to persist daily prices in a database table, calculate moving averages using window queries or in-memory LINQ, and apply multiplicative stacking of tier and bear multipliers.

**Primary recommendation:** Create a `DailyPrice` entity to store OHLC data, use a background service to fetch and refresh candles daily, calculate 30-day high and 200-day SMA using simple LINQ queries over in-memory data (not complex SQL), and apply multipliers in the existing DCA execution service.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Hyperliquid API | N/A | Historical candle data via `candleSnapshot` endpoint | Only official source for Hyperliquid spot market OHLCV data |
| EF Core 9+ | 9.x | Price data persistence and querying | Already in use, mature ORM with LINQ support |
| System.Linq | Built-in | Moving average calculations over in-memory collections | Standard .NET, performant for 30-200 day windows |
| C# decimal | Built-in | Price and multiplier calculations | Base-10 precision required for financial calculations |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Threading.PeriodicTimer | .NET 6+ | Scheduled background refresh of price data | Time-based tasks like daily candle fetches |
| IMemoryCache | Built-in | Cache 30-day high and 200-day MA values | Avoid recalculating on every purchase attempt |
| PostgreSQL BRIN Index | N/A | Efficient range queries on timestamp column | For very large time-series tables (millions of rows) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| In-memory LINQ SMA | Raw SQL with window functions | SQL is faster but harder to test/maintain; LINQ sufficient for 200 rows |
| Daily background refresh | Real-time candle websocket | More current data but higher complexity; daily refresh matches "daily close" requirement |
| PeriodicTimer | Hangfire/Quartz.NET | Heavyweight schedulers unnecessary for single daily task |

**Installation:**
```bash
# No new packages required — all capabilities already in .NET 10 / EF Core 9
```

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.ApiService/
├── Models/
│   └── DailyPrice.cs              # Entity for OHLC data
├── Infrastructure/
│   ├── Data/
│   │   └── TradingBotDbContext.cs # Add DbSet<DailyPrice>
│   └── Hyperliquid/
│       └── HyperliquidClient.cs   # Add GetCandlesAsync method
├── Application/
│   ├── Services/
│   │   ├── IPriceDataService.cs   # Abstraction for price data operations
│   │   ├── PriceDataService.cs    # Fetches, stores, calculates MA/high
│   │   └── DcaExecutionService.cs # Updated to call PriceDataService for multiplier
│   └── BackgroundJobs/
│       └── PriceDataRefreshService.cs # Daily background refresh
└── Configuration/
    └── DcaOptions.cs              # Already contains multiplier tiers, bear boost
```

### Pattern 1: Time-Series Entity with UTC Day Boundary

**What:** Store daily OHLC data with date-only partition key and UTC timestamp for ordering.

**When to use:** For price data that naturally partitions by day (matches Hyperliquid's daily candle interval).

**Example:**
```csharp
// Source: Inferred from PostgreSQL time-series best practices and EF Core patterns
public class DailyPrice : BaseEntity
{
    public DateOnly Date { get; set; }           // Partition key (UTC day boundary)
    public string Symbol { get; set; } = "BTC";  // Asset symbol
    public decimal Open { get; set; }            // Opening price
    public decimal High { get; set; }            // Daily high
    public decimal Low { get; set; }             // Daily low
    public decimal Close { get; set; }           // Closing price (most important for MA)
    public decimal Volume { get; set; }          // Trading volume
    public DateTimeOffset Timestamp { get; set; } // Exact candle close time
}

// EF Core configuration
entity.HasKey(e => new { e.Date, e.Symbol }); // Composite key
entity.HasIndex(e => e.Date);                 // Range queries
entity.Property(e => e.Open).HasPrecision(18, 8);   // BTC has 8 decimals
entity.Property(e => e.High).HasPrecision(18, 8);
entity.Property(e => e.Low).HasPrecision(18, 8);
entity.Property(e => e.Close).HasPrecision(18, 8);
entity.Property(e => e.Volume).HasPrecision(18, 8);
```

### Pattern 2: Moving Average Calculation via LINQ

**What:** Calculate simple moving average by loading recent N days into memory and using LINQ Average().

**When to use:** For small windows (30-200 days) where in-memory calculation is simpler and testable.

**Example:**
```csharp
// Source: LINQ best practices for rolling calculations
public async Task<decimal> Calculate200DaySmaAsync(string symbol, CancellationToken ct = default)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = today.AddDays(-200);

    // Load 200 days of close prices into memory (200 rows = ~16KB)
    var closePrices = await _dbContext.DailyPrices
        .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date < today)
        .OrderBy(p => p.Date)
        .Select(p => p.Close)
        .ToListAsync(ct);

    if (closePrices.Count < 200)
    {
        _logger.LogWarning("Insufficient data for 200-day SMA: only {Count} days available", closePrices.Count);
        return 0; // Or throw exception depending on requirements
    }

    // Simple moving average = mean of close prices
    return closePrices.Average();
}
```

### Pattern 3: Cache-Aside with Absolute Expiration

**What:** Cache calculated values (30-day high, 200-day MA) with absolute expiration tied to UTC day boundary.

**When to use:** Values that are expensive to calculate but only need daily refresh.

**Example:**
```csharp
// Source: ASP.NET Core caching best practices
public async Task<decimal> Get30DayHighAsync(string symbol, CancellationToken ct = default)
{
    var cacheKey = $"30d_high_{symbol}_{DateOnly.FromDateTime(DateTime.UtcNow)}";

    if (_cache.TryGetValue(cacheKey, out decimal cachedHigh))
    {
        return cachedHigh;
    }

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = today.AddDays(-30);

    var high = await _dbContext.DailyPrices
        .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date < today)
        .MaxAsync(p => p.Close, ct); // Based on daily close, not intraday high

    // Cache until midnight UTC (next day boundary)
    var tomorrow = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var expiration = tomorrow - DateTime.UtcNow;

    _cache.Set(cacheKey, high, new MemoryCacheEntryOptions
    {
        AbsoluteExpiration = DateTimeOffset.UtcNow.Add(expiration)
    });

    return high;
}
```

### Pattern 4: Background Service with PeriodicTimer

**What:** Use `PeriodicTimer` to trigger daily candle fetch at a specific time (e.g., 00:05 UTC).

**When to use:** Simple scheduled tasks without complex cron requirements.

**Example:**
```csharp
// Source: .NET 6+ background service patterns
public class PriceDataRefreshService : BackgroundService
{
    private readonly ILogger<PriceDataRefreshService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until 5 minutes past midnight UTC to give exchanges time to finalize daily candle
        var targetTime = new TimeOnly(0, 5);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var todayTarget = new DateTimeOffset(
                DateOnly.FromDateTime(now.UtcDateTime.Date).ToDateTime(targetTime),
                TimeSpan.Zero
            );

            var nextRun = todayTarget > now ? todayTarget : todayTarget.AddDays(1);
            var delay = nextRun - now;

            _logger.LogInformation("Next price data refresh at {NextRun} UTC", nextRun);

            await Task.Delay(delay, stoppingToken);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var priceService = scope.ServiceProvider.GetRequiredService<IPriceDataService>();

            await priceService.FetchAndStoreDailyCandleAsync("BTC", stoppingToken);
        }
    }
}
```

### Anti-Patterns to Avoid

- **Loading all historical data for every purchase:** Calculate once per day and cache; don't query database on every DCA execution
- **Using double/float for prices:** Always use `decimal` for cryptocurrency prices to avoid binary rounding errors
- **Fetching 5000 candles on every bootstrap:** Only fetch what you need (30-200 days max); Hyperliquid limits to 5000 candles per request but you don't need that many
- **Ignoring data staleness:** If candle fetch fails, use last known value with a logged warning — don't fail DCA execution
- **Complex SQL window functions for small datasets:** LINQ Average() over 200 in-memory decimals is faster and simpler than SQL `AVG() OVER ()`

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cron scheduling | Custom timer loop with DateTime math | `PeriodicTimer` or existing `TimeBackgroundService` base class | Built-in, handles cancellation, testable |
| Moving average calculation | Custom accumulator loops | LINQ `Average()` | Standard, tested, readable; sufficient performance for 30-200 rows |
| Date-only partitioning | String "YYYY-MM-DD" keys | C# `DateOnly` struct (.NET 6+) | Type-safe, efficient, avoids string parsing bugs |
| Price precision handling | Custom rounding logic | EF Core `HasPrecision(18, 8)` | Enforces precision at DB level; prevents truncation errors |
| Gap detection in time series | Manual day-by-day loop | LINQ `Enumerable.Range` + set difference | Declarative, handles edge cases (weekends, holidays) |

**Key insight:** Time-series calculations are well-supported by LINQ and EF Core for datasets under 10,000 rows. Only move to raw SQL or specialized libraries (TimescaleDB, etc.) when performance profiling shows it's necessary. Premature optimization adds complexity without measurable benefit.

## Common Pitfalls

### Pitfall 1: Assuming Candles Exist for Every Day

**What goes wrong:** Cryptocurrency markets run 24/7, but API issues, maintenance windows, or data gaps can cause missing candles. Code that assumes continuous data will crash or return incorrect MA values.

**Why it happens:** Hyperliquid's candleSnapshot endpoint only returns "the most recent 5000 candles" and may have gaps. Also, the API weight limit (20 per request) can cause rate limit issues if you query too frequently.

**How to avoid:**
- Check `closePrices.Count` before calculating averages
- Log warnings when data is insufficient (e.g., only 150 days for 200-day MA)
- Decide on fallback: skip bear boost, use partial MA, or fail purchase attempt
- Don't interpolate missing price data — use forward fill (last known value) or skip the day

**Warning signs:**
- `InvalidOperationException: Sequence contains no elements` from LINQ Average()
- MA values that spike or drop suddenly (indicates gap in data)
- Purchase records showing multiplier = 1.0 when you expected bear boost

### Pitfall 2: UTC vs Exchange Time Zone Confusion

**What goes wrong:** Hyperliquid candles use UTC timestamps, but if your code mixes local time or exchange time zones, you'll fetch the wrong day's candle or calculate MA with off-by-one errors.

**Why it happens:** DateTimeOffset defaults to local time if not explicitly set to UTC, and `DateTime.Now` returns local time.

**How to avoid:**
- Always use `DateTimeOffset.UtcNow` and `DateTime.UtcNow`
- Store all timestamps in database as `DateTimeOffset` or `timestamp with time zone` in PostgreSQL
- Use `DateOnly.FromDateTime(now.UtcDateTime.Date)` to get UTC day boundary
- Hyperliquid API expects epoch milliseconds: `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`

**Warning signs:**
- Price data is one day behind expected
- Tests pass locally but fail in CI/CD (different time zones)
- Candle timestamps don't match expected daily boundary (00:00 UTC)

### Pitfall 3: Decimal Precision Loss in Multiplier Math

**What goes wrong:** Multiplying decimals can exceed 28 significant digits, causing overflow or silent rounding. Also, intermediate calculations using float/double lose precision.

**Why it happens:** C# decimal has 28-29 digit limit. Expressions like `baseDailyAmount * dipMultiplier * bearBoost` can accumulate rounding errors if not careful.

**How to avoid:**
- Keep all price and multiplier types as `decimal` throughout the pipeline
- Round explicitly before persistence: `Math.Round(finalAmount, 2)` for USD amounts
- Configure EF Core precision: `HasPrecision(18, 8)` for BTC prices (8 decimals per satoshi standard)
- Use `decimal` literals: `1.5m` not `1.5` (which is double)

**Warning signs:**
- Purchase records showing 1.4999999999 instead of 1.5 multiplier
- Cost calculations off by fractions of a cent
- `OverflowException` during multiplication

### Pitfall 4: Fetching Candles on Every Purchase

**What goes wrong:** Calling Hyperliquid API in the hot path (DCA execution) adds latency, consumes rate limit, and can fail transiently, causing purchase to fail.

**Why it happens:** It feels simpler to fetch latest data when needed rather than maintain background refresh.

**How to avoid:**
- Fetch candles once per day in a background service
- Cache calculated values (30-day high, 200-day MA) with absolute expiration
- Make DCA execution service read from database/cache, not call external API
- Use stale data policy: if cache miss and DB has yesterday's data, use it with a warning

**Warning signs:**
- DCA execution takes >5 seconds (should be <500ms for buy order)
- Intermittent failures with "rate limit exceeded" or HTTP 429 errors
- Logs show multiple candleSnapshot requests within the same minute

### Pitfall 5: Not Handling Insufficient Historical Data on Bootstrap

**What goes wrong:** On first run, database is empty. Trying to calculate 200-day MA with 0 days of data crashes the application.

**Why it happens:** Assuming data exists before checking availability.

**How to avoid:**
- Add a bootstrap method that fetches 200 days of candles on first run
- Check `DailyPrices.Any()` before attempting calculations
- Use feature flag or configuration option to enable multipliers only after bootstrap completes
- Log clearly when multipliers are disabled due to insufficient data
- Gracefully degrade: if only 50 days available, skip 200-day MA but still use 30-day high

**Warning signs:**
- Application crashes on first run with "Sequence contains no elements"
- All purchase records show multiplier = 1.0 (base case)
- Logs show "Insufficient data for 200-day SMA" on every purchase attempt

## Code Examples

Verified patterns from official sources and existing codebase:

### Fetching Candles from Hyperliquid

```csharp
// Source: Hyperliquid API documentation (candleSnapshot endpoint)
public async Task<List<CandleData>> GetCandlesAsync(
    string symbol,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    CancellationToken ct = default)
{
    var request = new
    {
        type = "candleSnapshot",
        req = new
        {
            coin = symbol, // "BTC" for spot
            interval = "1d", // Daily candles
            startTime = startTime.ToUnixTimeMilliseconds(),
            endTime = endTime.ToUnixTimeMilliseconds()
        }
    };

    var response = await PostInfoAsync<List<CandleResponse>>(request, ct);

    return response.Select(c => new CandleData
    {
        Date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(c.T).UtcDateTime),
        Open = decimal.Parse(c.O, CultureInfo.InvariantCulture),
        High = decimal.Parse(c.H, CultureInfo.InvariantCulture),
        Low = decimal.Parse(c.L, CultureInfo.InvariantCulture),
        Close = decimal.Parse(c.C, CultureInfo.InvariantCulture),
        Volume = decimal.Parse(c.V, CultureInfo.InvariantCulture),
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(c.T)
    }).ToList();
}

public class CandleResponse
{
    [JsonPropertyName("t")] public long T { get; set; } // Open time
    [JsonPropertyName("T")] public long ClosedT { get; set; } // Close time
    [JsonPropertyName("o")] public string O { get; set; } = string.Empty;
    [JsonPropertyName("h")] public string H { get; set; } = string.Empty;
    [JsonPropertyName("l")] public string L { get; set; } = string.Empty;
    [JsonPropertyName("c")] public string C { get; set; } = string.Empty;
    [JsonPropertyName("v")] public string V { get; set; } = string.Empty;
    [JsonPropertyName("n")] public int N { get; set; } // Number of trades
    [JsonPropertyName("i")] public string I { get; set; } = string.Empty; // Interval
    [JsonPropertyName("s")] public string S { get; set; } = string.Empty; // Symbol
}
```

### Multiplier Calculation Logic

```csharp
// Source: CONTEXT.md decisions + multiplicative stacking pattern
public class MultiplierCalculator
{
    private readonly DcaOptions _options;
    private readonly ILogger<MultiplierCalculator> _logger;

    public async Task<MultiplierResult> CalculateMultiplierAsync(
        decimal currentPrice,
        string symbol = "BTC",
        CancellationToken ct = default)
    {
        var high30Day = await _priceDataService.Get30DayHighAsync(symbol, ct);
        var ma200Day = await _priceDataService.Get200DaySmaAsync(symbol, ct);

        // Calculate dip percentage from 30-day high
        var dropPercent = high30Day > 0
            ? (high30Day - currentPrice) / high30Day * 100
            : 0m;

        // Find matching tier (tiers are sorted ascending by DropPercentage)
        var dipMultiplier = 1.0m; // Default: no dip
        var tier = "None";

        foreach (var t in _options.MultiplierTiers.OrderByDescending(x => x.DropPercentage))
        {
            if (dropPercent >= t.DropPercentage)
            {
                dipMultiplier = t.Multiplier;
                tier = $">= {t.DropPercentage}%";
                break;
            }
        }

        // Apply bear boost if price below 200-day MA
        var bearMultiplier = 1.0m;
        if (ma200Day > 0 && currentPrice < ma200Day)
        {
            bearMultiplier = _options.BearBoostFactor;
            _logger.LogInformation("Bear market detected: price {Price} < 200-day MA {MA}", currentPrice, ma200Day);
        }

        // Multiplicative stacking
        var totalMultiplier = dipMultiplier * bearMultiplier;

        _logger.LogInformation(
            "Multiplier calculated: dip={DipMult} (tier: {Tier}, drop: {Drop}%) × bear={BearMult} = {Total}",
            dipMultiplier, tier, Math.Round(dropPercent, 2), bearMultiplier, totalMultiplier);

        return new MultiplierResult
        {
            TotalMultiplier = totalMultiplier,
            DipMultiplier = dipMultiplier,
            BearMultiplier = bearMultiplier,
            Tier = tier,
            DropPercentage = dropPercent,
            High30Day = high30Day,
            Ma200Day = ma200Day
        };
    }
}

public class MultiplierResult
{
    public decimal TotalMultiplier { get; set; }
    public decimal DipMultiplier { get; set; }
    public decimal BearMultiplier { get; set; }
    public string Tier { get; set; } = string.Empty;
    public decimal DropPercentage { get; set; }
    public decimal High30Day { get; set; }
    public decimal Ma200Day { get; set; }
}
```

### Updating Purchase Entity

```csharp
// Source: Existing Purchase.cs + CONTEXT.md requirements
public class Purchase : BaseEntity
{
    public DateTimeOffset ExecutedAt { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }

    // Phase 3 additions
    public decimal Multiplier { get; set; } = 1.0m;
    public string? MultiplierTier { get; set; }      // e.g., ">= 10%"
    public decimal DropPercentage { get; set; }      // % drop from 30-day high
    public decimal High30Day { get; set; }           // 30-day high value
    public decimal Ma200Day { get; set; }            // 200-day MA value

    // Existing fields
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }
}
```

### Handling Missing Data Gaps

```csharp
// Source: Time-series gap handling best practices
public async Task<List<DateOnly>> FindMissingDaysAsync(
    string symbol,
    DateOnly startDate,
    DateOnly endDate,
    CancellationToken ct = default)
{
    var existingDates = await _dbContext.DailyPrices
        .Where(p => p.Symbol == symbol && p.Date >= startDate && p.Date <= endDate)
        .Select(p => p.Date)
        .ToListAsync(ct);

    var expectedDates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1)
        .Select(offset => startDate.AddDays(offset))
        .ToHashSet();

    var missingDates = expectedDates.Except(existingDates).OrderBy(d => d).ToList();

    if (missingDates.Any())
    {
        _logger.LogWarning("Found {Count} missing days for {Symbol} between {Start} and {End}",
            missingDates.Count, symbol, startDate, endDate);
    }

    return missingDates;
}

// Bootstrap method to fetch historical data
public async Task BootstrapHistoricalDataAsync(string symbol, int days = 200, CancellationToken ct = default)
{
    var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = endDate.AddDays(-days);

    // Check if we already have data
    var existingCount = await _dbContext.DailyPrices
        .Where(p => p.Symbol == symbol && p.Date >= startDate)
        .CountAsync(ct);

    if (existingCount >= days - 5) // Allow 5-day tolerance for missing data
    {
        _logger.LogInformation("Historical data already bootstrapped: {Count} days available", existingCount);
        return;
    }

    _logger.LogInformation("Bootstrapping {Days} days of historical data for {Symbol}", days, symbol);

    var candles = await _hyperliquidClient.GetCandlesAsync(
        symbol,
        startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        ct
    );

    var dailyPrices = candles.Select(c => new DailyPrice
    {
        Date = c.Date,
        Symbol = symbol,
        Open = c.Open,
        High = c.High,
        Low = c.Low,
        Close = c.Close,
        Volume = c.Volume,
        Timestamp = c.Timestamp
    }).ToList();

    // Upsert: update existing, insert new
    foreach (var price in dailyPrices)
    {
        var existing = await _dbContext.DailyPrices
            .FirstOrDefaultAsync(p => p.Date == price.Date && p.Symbol == symbol, ct);

        if (existing != null)
        {
            existing.Open = price.Open;
            existing.High = price.High;
            existing.Low = price.Low;
            existing.Close = price.Close;
            existing.Volume = price.Volume;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _dbContext.DailyPrices.Add(price);
        }
    }

    await _dbContext.SaveChangesAsync(ct);

    _logger.LogInformation("Bootstrapped {Count} days of price data for {Symbol}", dailyPrices.Count, symbol);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Bitcoin 200-week MA | Bitcoin 200-day SMA | Industry standard | 200-day is the most-watched MA across all financial markets for medium-term trends |
| EMA for crypto | SMA for BTC macro analysis | Established practice | SMA is standard for Bitcoin's 200-day; EMA for shorter timeframes (20-day, 50-day) |
| Manual timer loops | PeriodicTimer | .NET 6 (Nov 2021) | Cleaner async/await pattern for periodic tasks |
| DateOnly string parsing | DateOnly struct | .NET 6 (Nov 2021) | Type-safe date-only operations without time component |
| Hangfire/Quartz for all scheduling | BackgroundService + PeriodicTimer | .NET 6+ hosting improvements | Simpler for single scheduled tasks; avoid heavyweight frameworks |
| B-tree indexes for time series | BRIN indexes | PostgreSQL 9.5+ (2016), mature since 10+ | Massive space savings (100KB vs 10GB) for naturally ordered timestamp columns |

**Deprecated/outdated:**
- **Double/float for money:** Always use decimal in .NET financial calculations; floating-point leads to rounding errors
- **String "YYYY-MM-DD" date keys:** Use DateOnly struct for type safety and efficiency
- **Task.Delay loops in background services:** Use PeriodicTimer for cleaner cancellation and async patterns

## Open Questions

Things that couldn't be fully resolved:

1. **Hyperliquid candle data completeness**
   - What we know: candleSnapshot endpoint returns "most recent 5000 candles" with daily interval, which covers ~13.7 years of data
   - What's unclear: Does Hyperliquid guarantee no gaps in daily candles? What happens during exchange maintenance or outages?
   - Recommendation: Build gap detection logic, log warnings, and use forward-fill (last known value) for missing days rather than interpolating

2. **Optimal cache expiration strategy**
   - What we know: 30-day high and 200-day MA only change once per day (at UTC midnight when new candle completes)
   - What's unclear: Should cache expire at exactly midnight UTC, or use sliding expiration with 24-hour TTL?
   - Recommendation: Use absolute expiration at next UTC midnight to align with candle boundaries; simpler reasoning and predictable cache invalidation

3. **BRIN index threshold for DailyPrice table**
   - What we know: BRIN indexes are ideal for time-series data with natural ordering, but only beneficial for very large tables (millions of rows)
   - What's unclear: At what row count should we add BRIN index? 10K rows? 100K rows?
   - Recommendation: Start with standard B-tree index on `Date` column; monitor query performance; switch to BRIN only if table exceeds 100K rows and range queries slow down

4. **Handling first-run bootstrap vs daily refresh**
   - What we know: First run needs 200 days of candles; daily refresh needs only yesterday's candle
   - What's unclear: Should bootstrap be automatic on first run, or require manual trigger?
   - Recommendation: Automatic bootstrap on application startup if `DailyPrices.Count() < 200`; log clearly and allow manual override via admin endpoint

## Sources

### Primary (HIGH confidence)

- [Hyperliquid API Info Endpoint Documentation](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint) - Official candleSnapshot endpoint specification
- [Hyperliquid API candleSnapshot (Chainstack)](https://docs.chainstack.com/reference/hyperliquid-info-candle-snapshot) - Detailed request/response format with examples
- [Hyperliquid API Rate Limits](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/rate-limits-and-user-limits) - Rate limiting logic and endpoint weights
- [Microsoft EF Core Performance Best Practices](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) - Official guidance on efficient querying
- [C# Decimal Precision for Financial Calculations](https://medium.com/@stanislavbabenko/handling-precision-in-financial-calculations-in-net-a-deep-dive-into-decimal-and-common-pitfalls-1211cc5edd3b) - Why decimal over float/double
- [Bitcoin 8 Decimal Places and Satoshi](https://medium.com/airtm/what-is-a-satoshi-bitcoin-and-its-8-decimal-places-cffeb5795758) - BTC precision standard

### Secondary (MEDIUM confidence)

- [PostgreSQL Time-Series Best Practices (Neon)](https://neon.com/guides/timeseries-data) - Indexing strategies for time-series data
- [PostgreSQL BRIN Indexes (Crunchy Data)](https://www.crunchydata.com/blog/postgresql-brin-indexes-big-data-performance-with-minimal-storage) - When to use BRIN vs B-tree
- [ASP.NET Core Caching Best Practices](https://www.milanjovanovic.tech/blog/caching-in-aspnetcore-improving-application-performance) - Cache-aside pattern and expiration strategies
- [.NET Background Service Best Practices 2026](https://medium.com/net-code-chronicles/background-jobs-schedulers-dotnet-abfbf49aa79f) - BackgroundService vs Hangfire vs Quartz
- [Moving Averages in Crypto 2026](https://www.hyrotrader.com/blog/moving-averages-in-crypto/) - 200-day SMA as standard for BTC

### Tertiary (LOW confidence)

- [Time Series Gap Handling Methods](https://medium.com/@datasciencewizards/preprocessing-and-data-exploration-for-time-series-handling-missing-values-e5c507f6c71c) - Forward fill vs interpolation approaches
- [LINQ Performance Optimization](https://www.bytehide.com/blog/linq-performance-optimization-csharp) - General LINQ best practices

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Hyperliquid API official docs, EF Core and LINQ are established .NET patterns
- Architecture: HIGH - Patterns verified against existing codebase structure and .NET best practices
- Pitfalls: MEDIUM - Based on common time-series and financial calculation mistakes, not Hyperliquid-specific war stories

**Research date:** 2026-02-12
**Valid until:** 2026-03-14 (30 days; Hyperliquid API stable, .NET patterns evergreen)

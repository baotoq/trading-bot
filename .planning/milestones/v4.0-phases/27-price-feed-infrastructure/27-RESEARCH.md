# Phase 27: Price Feed Infrastructure - Research

**Researched:** 2026-02-20
**Domain:** HTTP client infrastructure, Redis caching, VNDirect/CoinGecko/ExchangeRate APIs
**Confidence:** HIGH (all three APIs live-verified; patterns match existing codebase)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Fetch scheduling**
- Lazy fetch + cache: prices are fetched on-demand when a consumer requests them, not via background polling
- Cold start is acceptable: no pre-warming on startup; first request per asset type triggers the fetch
- Separate provider services: ICryptoPriceProvider, IEtfPriceProvider, IExchangeRateProvider — each resolved independently via DI

**Cache design**
- Single Redis key per price with value + fetchedAt timestamp stored together (MessagePack serialized)
- Consumer checks freshness via the timestamp; TTL freshness windows: crypto 5 min, ETF 48 hours, exchange rate 12 hours
- Long TTL safety net (30 days) on the Redis key itself so ancient data eventually cleans up
- "Always return stale" policy: if cache exists but is past freshness window and provider is down, return the stale value with its timestamp so the UI can show staleness
- If cache is completely empty (first-ever fetch) and provider is down, throw an exception — let it fail loudly

**Multi-asset resolution**
- Crypto provider supports any CoinGecko-listed coin, not just BTC — accepts CoinGecko IDs
- Symbol-to-CoinGecko-ID mapping maintained in the provider (e.g., BTC → bitcoin, ETH → ethereum)
- ETF provider uses friendly-name-to-VNDirect-ticker mapping (e.g., "VN30 ETF" → "E1VFVN30")
- CoinGecko provider batch-fetches all portfolio crypto assets in a single API call (multi-coin query)

**Error handling**
- Log only on provider failure — no Telegram notifications for price feed issues
- Shared default HTTP timeout across all providers
- Polly retry policy: 2-3 retries with exponential backoff on transient HTTP errors
- No health check endpoint for price feeds — staleness timestamps on the data are sufficient

### Claude's Discretion

- Stale-while-revalidate vs wait-for-fetch when cached price is expired (pick based on UX and complexity tradeoffs)
- Exact Polly retry count and backoff intervals
- MessagePack schema for cached price entries
- Provider interface method signatures and return types (e.g., PriceFeedResult record with Price, FetchedAt, Currency)

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PRICE-01 | Crypto asset prices auto-fetch from CoinGecko with Redis caching (5-min TTL) | CoinGecko `/simple/price` batch endpoint verified live; Redis `IDistributedCache` + MessagePack pattern documented |
| PRICE-02 | VN30 ETF prices auto-fetch from VNDirect finfo API with graceful degradation to cached values (48h TTL) | VNDirect `dchart-api` endpoint live-verified with confirmed JSON schema; finfo-api.vndirect.com.vn timed out (blocked externally) |
| PRICE-03 | USD/VND exchange rate auto-fetches daily from open.er-api.com with Redis caching (12h TTL) | open.er-api.com `/v6/latest/USD` live-verified; VND field confirmed in rates object |
| PRICE-04 | Price staleness is tracked and surfaced (last updated timestamp available for all price types) | `PriceFeedEntry` MessagePack record design documented with `FetchedAt` field; `PriceFeedResult` return type design documented |
</phase_requirements>

## Summary

Phase 27 implements three independent price providers wired to Redis cache with lazy-fetch semantics. The research confirms all three external APIs are accessible and have verified JSON schemas. The critical flag from STATE.md — "VNDirect finfo API schema unconfirmed" — is now partially resolved: `finfo-api.vndirect.com.vn` times out from this environment (private IP 10.210.100.8, likely geo-blocked), but the alternative `dchart-api.vndirect.com.vn` responds successfully with OHLCV data. The dchart endpoint returns a compact array format (`t`, `c`, `o`, `h`, `l`, `v`, `s`) rather than individual named records, which is simpler to parse.

The codebase already has `MessagePack` (v3.1.4) installed and `Microsoft.Extensions.Http.Resilience` (v10.3.0) installed with `AddStandardResilienceHandler` used consistently. The new providers should follow the same `AddHttpClient<T> + AddStandardResilienceHandler` pattern used by `HyperliquidClient` and `CoinGeckoClient`. Redis is already wired via `builder.AddRedisDistributedCache("redis")` in `Program.cs`.

**Primary recommendation:** Use `IDistributedCache` (already registered) with MessagePack serialization for a `PriceFeedEntry` record containing `Price` (decimal) + `FetchedAt` (DateTimeOffset). Apply the "stale-while-revalidate" variant (return stale immediately + fire-and-forget refresh) for ETF — this is the better UX tradeoff since 48h TTL means staleness happens only during VN market hours when the API may be overloaded.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Caching.Abstractions` (IDistributedCache) | 10.0.0 | Redis cache operations (GetAsync/SetAsync/RefreshAsync) | Already registered via Aspire Redis; no new package needed |
| `MessagePack` | 3.1.4 | Binary serialize `PriceFeedEntry` for Redis storage | Already in .csproj; used by HyperliquidSigner; faster/smaller than JSON |
| `Microsoft.Extensions.Http.Resilience` | 10.3.0 | `AddStandardResilienceHandler` on all three HttpClients | Already in .csproj; used by CoinGecko + Hyperliquid clients |
| `System.Text.Json` | inbox (.NET 10) | Deserialize API responses from CoinGecko, VNDirect, open.er-api.com | Already used throughout codebase; no extra package |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Polly` (via Http.Resilience) | 10.3.0 | Retry with exponential backoff + circuit breaker | Already pulled in by Http.Resilience; no direct Polly reference needed |
| `IOptionsMonitor<T>` | inbox | Configuration (symbol→coin ID mappings, timeouts) | Standard pattern already used in codebase |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| IDistributedCache | StackExchange.Redis directly | IDistributedCache is already registered and tested; direct Redis is more powerful but unnecessary complexity |
| MessagePack binary | JSON string in Redis | JSON is human-readable but ~2-3x larger; MessagePack already in the project |
| dchart-api (VNDirect) | finfo-api (VNDirect) | finfo-api timed out in testing; dchart-api is responsive and returns daily OHLCV — adequate for close price |

**No new NuGet packages needed.** All required libraries are already in `TradingBot.ApiService.csproj`.

## Architecture Patterns

### Recommended Project Structure

```
Infrastructure/
├── PriceFeeds/
│   ├── ServiceCollectionExtensions.cs      # AddPriceFeeds() wires all three providers
│   ├── PriceFeedEntry.cs                   # [MessagePackObject] record (Price, FetchedAt, Currency)
│   ├── PriceFeedResult.cs                  # Return type: Price, FetchedAt, IsStale
│   ├── Crypto/
│   │   ├── ICryptoPriceProvider.cs
│   │   ├── CoinGeckoPriceProvider.cs       # HttpClient + IDistributedCache
│   │   └── CoinGeckoPriceResponse.cs       # JSON DTO for /simple/price response
│   ├── Etf/
│   │   ├── IEtfPriceProvider.cs
│   │   ├── VNDirectPriceProvider.cs        # HttpClient + IDistributedCache
│   │   └── VNDirectDchartResponse.cs       # JSON DTO for dchart array format
│   └── ExchangeRate/
│       ├── IExchangeRateProvider.cs
│       ├── OpenErApiProvider.cs            # HttpClient + IDistributedCache
│       └── OpenErApiResponse.cs            # JSON DTO for /v6/latest/USD response
```

### Pattern 1: Lazy Fetch with Stale-While-Revalidate

**What:** Check Redis first. If entry exists and is fresh → return it. If entry is stale → return stale immediately AND fire-and-forget refresh. If cache is empty → await fresh fetch (blocking).

**When to use:** ETF provider (48h window) where staleness is acceptable and avoids blocking the caller.

**For crypto (5 min TTL):** Use simple wait-for-fetch — 5 min is short enough that blocking is fine; stale crypto price could cause bad DCA decisions.

```csharp
// Source: Adapted from project patterns + official IDistributedCache docs
public async Task<PriceFeedResult> GetPriceAsync(string ticker, CancellationToken ct)
{
    var cacheKey = $"price:etf:{ticker}";
    var cached = await _cache.GetAsync(cacheKey, ct);

    if (cached is not null)
    {
        var entry = MessagePackSerializer.Deserialize<PriceFeedEntry>(cached);
        var age = DateTimeOffset.UtcNow - entry.FetchedAt;
        var isFresh = age <= _freshnessWindow; // 48h for ETF

        if (isFresh)
            return PriceFeedResult.Fresh(entry.Price, entry.FetchedAt);

        // Stale: return immediately, refresh in background (fire-and-forget)
        _ = RefreshInBackgroundAsync(ticker, cacheKey, ct: default);
        return PriceFeedResult.Stale(entry.Price, entry.FetchedAt);
    }

    // Cache empty: must await — throw if provider fails
    var freshEntry = await FetchAndCacheAsync(ticker, cacheKey, ct);
    return PriceFeedResult.Fresh(freshEntry.Price, freshEntry.FetchedAt);
}
```

### Pattern 2: MessagePack Cache Entry

**What:** A [MessagePackObject] record containing the price and when it was fetched. Serialized to byte[] for IDistributedCache.SetAsync.

```csharp
// Source: MessagePack-CSharp README pattern
[MessagePackObject]
public record PriceFeedEntry
{
    [Key(0)] public decimal Price { get; init; }
    [Key(1)] public DateTimeOffset FetchedAt { get; init; }
    [Key(2)] public string Currency { get; init; } = null!; // "USD" or "VND"
}
```

Redis key TTL (the physical expiry safety net): 30 days
Freshness window (logical staleness check): crypto 5 min, ETF 48h, exchange rate 12h

```csharp
var bytes = MessagePackSerializer.Serialize(entry);
await _cache.SetAsync(cacheKey, bytes, new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // safety net cleanup
}, ct);
```

### Pattern 3: Provider Registration (match existing pattern)

**What:** Dedicated `ServiceCollectionExtensions` per provider, aggregated in `AddPriceFeeds()`.

```csharp
// Follow Infrastructure/CoinGecko/ServiceCollectionExtensions.cs pattern
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPriceFeeds(this IServiceCollection services, IConfiguration configuration)
    {
        // CoinGecko provider (already exists for historical — new Scoped provider for live prices)
        services.AddHttpClient<CoinGeckoPriceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
        });

        services.AddScoped<ICryptoPriceProvider, CoinGeckoPriceProvider>();

        // VNDirect dchart provider
        services.AddHttpClient<VNDirectPriceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://dchart-api.vndirect.com.vn/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(/* same options */);

        services.AddScoped<IEtfPriceProvider, VNDirectPriceProvider>();

        // open.er-api.com provider
        services.AddHttpClient<OpenErApiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://open.er-api.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddStandardResilienceHandler(/* same options */);

        services.AddScoped<IExchangeRateProvider, OpenErApiProvider>();

        return services;
    }
}
```

### Anti-Patterns to Avoid

- **Registering providers as Singleton with IDistributedCache injected via constructor:** IDistributedCache is scoped in Aspire Redis setup. Match the existing `AddScoped` pattern used by `DcaExecutionService`.
- **Catching all exceptions and returning null:** The decided policy is "throw if cache empty and provider down." Only swallow exceptions when a stale cache entry exists.
- **Using `Task.Run` for fire-and-forget refresh:** Use `_ = Task.Run(...)` only if inside a non-async context. Inside async methods, use `_ = RefreshInBackgroundAsync(...)` directly to avoid ThreadPool overhead for I/O-bound work.
- **Separate Redis keys for price and timestamp:** Store as a single `PriceFeedEntry` blob — avoids TOCTOU race where price is read but timestamp write hasn't landed.
- **CoinGecko symbol parameter confusion:** Use `ids` parameter (CoinGecko IDs like "bitcoin"), NOT `symbols` (like "btc"). The `symbols` lookup requires `include_tokens` and has a 50-symbol max.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP retry with exponential backoff | Custom retry loop | `AddStandardResilienceHandler` | Handles jitter, circuit breaker, timeout — all already configured in project |
| Redis byte serialization | Custom binary format | MessagePack (already in project) | Zero additional code; `[MessagePackObject]` attribute + `MessagePackSerializer.Serialize/Deserialize` |
| Cache TTL management | Custom expiry tracking | `DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow` | IDistributedCache handles physical expiry; freshness window is a separate logical check |
| IDistributedCache registration | Manual StackExchange.Redis setup | `builder.AddRedisDistributedCache("redis")` | Already in Program.cs; Aspire handles connection string injection |

**Key insight:** The entire caching and resilience infrastructure already exists. This phase is purely about writing three thin client classes + the PriceFeedEntry model.

## Common Pitfalls

### Pitfall 1: VNDirect finfo-api Timeout

**What goes wrong:** `finfo-api.vndirect.com.vn` resolves to `10.210.100.8` (private/internal IP) and times out completely. Any code targeting this endpoint will hang for the full request timeout.

**Why it happens:** The finfo endpoint appears to be geo-blocked or restricted to VNDirect's internal infrastructure. The roadmap flagged this risk.

**How to avoid:** Use `dchart-api.vndirect.com.vn` instead. This is publicly accessible (verified 2026-02-20), returns daily OHLCV data in a compact format, and the VN stock community broadly uses it via the dchart endpoint.

**dchart endpoint:** `GET https://dchart-api.vndirect.com.vn/dchart/history?resolution=D&symbol={ticker}&from={unixTs}&to={unixTs}`

**dchart response format (confirmed):**
```json
{
  "t": [1706745600, 1706832000],   // Unix timestamps array
  "c": [20.29, 20.23],            // Close prices (VND, as thousands — i.e. 20.29 = 20,290 VND)
  "o": [20.11, 20.31],            // Open prices
  "h": [20.29, 20.34],            // High prices
  "l": [20.10, 20.20],            // Low prices
  "v": [1344500, 1957500],        // Volume (shares)
  "s": "ok"                        // Status
}
```

**To get latest price:** Request last 2 days, take last element of `c` array.

**Warning signs:** Request timeout > 5s, curl resolves to private RFC 1918 address range.

---

### Pitfall 2: CoinGecko Free Tier Rate Limits

**What goes wrong:** Free tier (no API key) rate limit is 5-15 req/min. Demo tier (registered, free key) is 30 req/min with 10,000/month cap. Each request counts as 1 call regardless of how many coins are batched.

**Why it happens:** Price providers are called on-demand. If multiple concurrent requests arrive simultaneously for different assets not yet cached, multiple CoinGecko calls can fire at once.

**How to avoid:** Batch all portfolio crypto assets in a single `/simple/price?ids=bitcoin,ethereum,...` call. Cache per-coin after deserializing. A single batch call for N coins still counts as 1 API call.

**Warning signs:** HTTP 429 response from CoinGecko.

---

### Pitfall 3: open.er-api.com Rate Limit

**What goes wrong:** If called more frequently than the data refresh rate (24h), the API returns HTTP 429. Rate-limited IPs are blocked for 20 minutes.

**Why it happens:** Data refreshes only once per 24 hours. With a 12h Redis TTL, the provider calls the API at most twice per day — well within limits. But if Redis is flushed, multiple callers could each trigger a fetch.

**How to avoid:** 12h TTL (from decisions) means at most 2 calls/day. Use the `result` field check: if `result != "success"`, treat as failure. The Polly circuit breaker will handle 429s by opening the circuit.

**Warning signs:** `result: "error"` in response body, HTTP 429 status.

---

### Pitfall 4: VNDirect ETF Price Units

**What goes wrong:** E1VFVN30 prices from dchart are in thousands of VND (e.g., `23.54` = 23,540 VND per unit). Treating this as raw VND would give wrong portfolio value.

**Why it happens:** Vietnamese stock exchange prices are quoted in thousands of VND by convention.

**How to avoid:** Multiply by 1,000 when converting dchart `c` values to actual VND. Document in code with a clear comment.

**Warning signs:** Portfolio value 1000x lower than expected for ETF positions.

---

### Pitfall 5: MessagePack `DateTimeOffset` Serialization

**What goes wrong:** MessagePack has specific behavior for `DateTimeOffset` — it serializes as a native extension type. This requires `MessagePackSerializerOptions.Standard` or explicitly enabling the `NativeDateTimeResolver`.

**Why it happens:** Default MessagePack resolvers may not include `NativeDateTimeResolver` for `DateTimeOffset` support.

**How to avoid:** Use `DateTimeOffset` with int64 (Unix timestamp in seconds) instead of the `DateTimeOffset` type directly, OR use `MessagePack.Resolvers.StandardResolver` (which handles `DateTimeOffset` correctly). Alternatively, store `FetchedAt` as `long` (Unix timestamp) to avoid resolver complexity.

**Warning signs:** `MessagePackSerializationException` at runtime when deserializing.

## Code Examples

Verified patterns from official sources and live API tests:

### CoinGecko Batch Price Fetch (Verified 2026-02-20)

```csharp
// Source: live API test + https://docs.coingecko.com/v3.0.1/reference/simple-price
// GET https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=usd&include_last_updated_at=true
// Response:
// {"bitcoin":{"usd":67508,"last_updated_at":1771591961},"ethereum":{"usd":1947.89,"last_updated_at":1771591965}}

public async Task<Dictionary<string, decimal>> FetchPricesAsync(
    IEnumerable<string> coinGeckoIds, CancellationToken ct)
{
    var ids = string.Join(",", coinGeckoIds);
    var url = $"simple/price?ids={ids}&vs_currencies=usd&include_last_updated_at=true";
    var response = await _httpClient.GetFromJsonAsync<Dictionary<string, CoinPriceData>>(url, ct);
    return response?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Usd) ?? [];
}

// DTO for response deserialization
private record CoinPriceData(
    [property: JsonPropertyName("usd")] decimal Usd,
    [property: JsonPropertyName("last_updated_at")] long LastUpdatedAt
);
```

### VNDirect dchart Latest Price Fetch (Verified 2026-02-20)

```csharp
// Source: live API test (dchart-api.vndirect.com.vn confirmed accessible)
// GET https://dchart-api.vndirect.com.vn/dchart/history?resolution=D&symbol=E1VFVN30&from={from}&to={to}
// Response: {"t":[...], "c":[...], "o":[...], "h":[...], "l":[...], "v":[...], "s":"ok"}

public async Task<decimal?> FetchLatestClosePriceAsync(string vnDirectTicker, CancellationToken ct)
{
    var to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var from = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds(); // 3 days back for safety
    var url = $"dchart/history?resolution=D&symbol={vnDirectTicker}&from={from}&to={to}";

    var response = await _httpClient.GetFromJsonAsync<VNDirectDchartResponse>(url, ct);

    if (response?.Status != "ok" || response.Close is null || response.Close.Length == 0)
        return null;

    // Close prices in thousands of VND; multiply by 1000 for actual VND
    var latestCloseThousands = response.Close[^1];
    return latestCloseThousands * 1_000m;
}

// DTO for dchart response
private record VNDirectDchartResponse(
    [property: JsonPropertyName("t")] long[]? Timestamps,
    [property: JsonPropertyName("c")] decimal[]? Close,
    [property: JsonPropertyName("o")] decimal[]? Open,
    [property: JsonPropertyName("h")] decimal[]? High,
    [property: JsonPropertyName("l")] decimal[]? Low,
    [property: JsonPropertyName("v")] long[]? Volume,
    [property: JsonPropertyName("s")] string? Status
);
```

### open.er-api.com USD/VND Fetch (Verified 2026-02-20)

```csharp
// Source: live API test (open.er-api.com/v6/latest/USD confirmed)
// Response: {"result":"success","base_code":"USD","time_last_update_utc":"...","rates":{"VND":25905.86,...}}

public async Task<decimal> FetchUsdToVndRateAsync(CancellationToken ct)
{
    var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>("v6/latest/USD", ct);

    if (response?.Result != "success" || response.Rates is null)
        throw new InvalidOperationException("Exchange rate API returned non-success result");

    if (!response.Rates.TryGetValue("VND", out var rate))
        throw new InvalidOperationException("VND rate not found in exchange rate response");

    return rate;
}

// DTOs
private record ExchangeRateResponse(
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("base_code")] string? BaseCode,
    [property: JsonPropertyName("time_last_update_unix")] long TimeLastUpdateUnix,
    [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates
);
```

### PriceFeedEntry MessagePack Record

```csharp
// Source: MessagePack-CSharp README pattern (messagepack-csharp/messagepack-csharp)
[MessagePackObject]
public record PriceFeedEntry
{
    [Key(0)] public decimal Price { get; init; }
    [Key(1)] public long FetchedAtUnixSeconds { get; init; } // Store as long to avoid resolver issues

    public DateTimeOffset FetchedAt => DateTimeOffset.FromUnixTimeSeconds(FetchedAtUnixSeconds);

    public static PriceFeedEntry Create(decimal price) => new()
    {
        Price = price,
        FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
}
```

### Cache Read/Write Pattern

```csharp
// Source: Microsoft IDistributedCache docs + project pattern
private async Task<PriceFeedEntry?> ReadCacheAsync(string key, CancellationToken ct)
{
    var bytes = await _cache.GetAsync(key, ct);
    if (bytes is null) return null;
    return MessagePackSerializer.Deserialize<PriceFeedEntry>(bytes);
}

private async Task WriteCacheAsync(string key, PriceFeedEntry entry, CancellationToken ct)
{
    var bytes = MessagePackSerializer.Serialize(entry);
    await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // safety net
    }, ct);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| VNDirect finfo-api endpoint | VNDirect dchart-api endpoint | Discovered 2026-02-20 (finfo times out externally) | Use dchart-api.vndirect.com.vn; different response format (array-based) |
| Polly v7 direct | Microsoft.Extensions.Http.Resilience wrapping Polly v8 | .NET 8+ | Use `AddStandardResilienceHandler` rather than raw `AddPolicyHandler` |
| CoinGecko Public API (5-15 req/min) | CoinGecko Demo API (30 req/min, free, requires key) | 2024 | Project already has `CoinGeckoOptions.ApiKey` optional field; use it if set via x-cg-demo-api-key header |

**Deprecated/outdated:**
- VNDirect finfo-api.vndirect.com.vn: Do NOT use. Times out externally. Use dchart-api instead.
- Polly direct `AddPolicyHandler`: Use `AddStandardResilienceHandler` or `AddResilienceHandler` (already in project).

## Open Questions

1. **VNDirect dchart price units confirmation**
   - What we know: E1VFVN30 shows `c: [20.29, ...]` from dchart. Vietnamese ETFs are priced in thousands of VND. 20.29 * 1000 = 20,290 VND/unit seems reasonable.
   - What's unclear: Could be confirmed by cross-referencing with a published ETF NAV or broker quote.
   - Recommendation: Document the `* 1000` multiplication clearly in code comments. If wrong, the unit test for portfolio value will catch it.

2. **CoinGecko API key header for Demo tier**
   - What we know: Free public tier is 5-15 req/min. Demo tier (registered free) is 30 req/min. `CoinGeckoOptions.ApiKey` already exists in the project.
   - What's unclear: Demo API key uses `x-cg-demo-api-key` header; paid uses `x-cg-pro-api-key`. The existing `CoinGeckoClient` doesn't set this header.
   - Recommendation: New `CoinGeckoPriceProvider` should add the `x-cg-demo-api-key` header if `ApiKey` is configured. Wrap in an `if (!string.IsNullOrEmpty(apiKey))` guard.

3. **open.er-api.com rate limits**
   - What we know: Data refreshes once per 24h. Free tier has "1,500 monthly requests" (from community sources). Rate-limited IPs receive 429. 12h Redis TTL means at most ~2 calls/day.
   - What's unclear: Exact request quota not found in official docs (API renders as CSS in fetch).
   - Recommendation: With 12h TTL, worst case is 2 requests/day = 60 requests/month — well within any reasonable free tier. No action needed.

## Sources

### Primary (HIGH confidence)

- Live API test `dchart-api.vndirect.com.vn/dchart/history` — Verified 2026-02-20; confirmed response schema
- Live API test `open.er-api.com/v6/latest/USD` — Verified 2026-02-20; confirmed VND field in rates
- Live API test `api.coingecko.com/api/v3/simple/price` — Verified 2026-02-20; confirmed multi-coin batch
- `https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience` — AddStandardResilienceHandler defaults (confirmed 2025-10-22)
- `/messagepack-csharp/messagepack-csharp` (Context7) — MessagePackObject/Key attribute patterns

### Secondary (MEDIUM confidence)

- CoinGecko official docs `https://docs.coingecko.com/v3.0.1/reference/simple-price` — `/simple/price` endpoint parameters and response schema
- Web search: CoinGecko free tier 5-15 req/min, demo tier 30 req/min + 10k/month — supported by multiple sources including CoinGecko support articles
- Web search: VNDirect dchart response field names (`t`, `c`, `o`, `h`, `l`, `v`, `s`) — confirmed by qnaut.py source code analysis

### Tertiary (LOW confidence)

- VNDirect finfo-api field names (ticker_name, date, close, adClose, etc.) — from Python vnstock community code, unverified; moot since we use dchart-api instead
- open.er-api.com "1,500 monthly free requests" — from web search, no official docs page accessible

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in project, patterns already established
- Architecture: HIGH — follows existing codebase patterns (CoinGecko + Hyperliquid clients)
- API schemas: HIGH for all three (live-verified 2026-02-20)
- Pitfalls: HIGH for VNDirect endpoint switch (confirmed by direct testing); MEDIUM for others (based on docs/community)

**Research date:** 2026-02-20
**Valid until:** 2026-03-20 (APIs could change; dchart endpoint is undocumented/unofficial)

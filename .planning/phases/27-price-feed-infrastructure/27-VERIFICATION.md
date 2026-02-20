---
phase: 27-price-feed-infrastructure
verified: 2026-02-21T00:00:00Z
status: passed
score: 4/4 must-haves verified
re_verification: false
---

# Phase 27: Price Feed Infrastructure Verification Report

**Phase Goal:** Three price feed providers (CoinGecko crypto, VNDirect ETF, OpenErApi exchange rate) with Redis caching, staleness tracking, and DI wiring in Program.cs
**Verified:** 2026-02-21
**Status:** PASSED
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | CoinGeckoPriceProvider uses a 5-minute freshness window, 30-day physical Redis TTL, and returns PriceFeedResult.Fresh or PriceFeedResult.Stale based on cache age | VERIFIED | `CoinGeckoPriceProvider.cs` line 21: `private static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(5)`; line 22: `PhysicalTtl = TimeSpan.FromDays(30)`; line 36: `if (age <= FreshnessWindow)` returns Fresh; line 59: falls through to return Stale; line 38: `PriceFeedResult.Fresh(...)`, line 59: `PriceFeedResult.Stale(...)` |
| 2 | VNDirectPriceProvider uses a 48-hour freshness window with stale-while-revalidate: returns stale immediately and fires background refresh via `_ = RefreshInBackgroundAsync()` | VERIFIED | `VNDirectPriceProvider.cs` line 18: `FreshnessWindow = TimeSpan.FromHours(48)`; line 45: `_ = RefreshInBackgroundAsync(vnDirectTicker, cacheKey)` (fire-and-forget); line 46: `return PriceFeedResult.Stale(cached.Price, cached.FetchedAt, Currency)`; `RefreshInBackgroundAsync` uses `CancellationToken.None` on line 62 |
| 3 | OpenErApiProvider uses a 12-hour freshness window with wait-for-fetch: stale cache triggers a blocking refresh, falling back to stale on failure | VERIFIED | `OpenErApiProvider.cs` line 18: `FreshnessWindow = TimeSpan.FromHours(12)`; lines 38-51: stale path calls `await FetchAndCacheAsync(ct)` (blocking, not fire-and-forget); falls back to stale on exception; empty cache path line 55: `var freshEntry = await FetchAndCacheAsync(ct)` |
| 4 | PriceFeedResult has FetchedAt timestamp and IsStale bool on all 3 providers; PriceFeedEntry stores timestamp as long FetchedAtUnixSeconds with computed FetchedAt property | VERIFIED | `PriceFeedResult.cs` line 7: `record PriceFeedResult(decimal Price, DateTimeOffset FetchedAt, bool IsStale, string Currency)`; static factories `Fresh` (line 12, IsStale: false) and `Stale` (line 17, IsStale: true); `PriceFeedEntry.cs` lines 11-19: positional record with `long FetchedAtUnixSeconds`, `[IgnoreMember] DateTimeOffset FetchedAt` computed property |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Provides | Exists | Substantive | Wired | Status |
|----------|----------|--------|-------------|-------|--------|
| `TradingBot.ApiService/Infrastructure/PriceFeeds/PriceFeedEntry.cs` | MessagePack-serializable cache record with FetchedAtUnixSeconds | Yes | Yes — `[MessagePackObject]` positional record with `[Key(0)] decimal Price`, `[Key(1)] long FetchedAtUnixSeconds`, `[Key(2)] string Currency`; `[IgnoreMember]` computed `FetchedAt` property; static `Create` factory | Yes — used by all 3 providers for cache read/write | VERIFIED |
| `TradingBot.ApiService/Infrastructure/PriceFeeds/PriceFeedResult.cs` | Provider return type with IsStale tracking | Yes | Yes — `record PriceFeedResult(decimal Price, DateTimeOffset FetchedAt, bool IsStale, string Currency)`; `Fresh()` factory sets IsStale=false; `Stale()` factory sets IsStale=true | Yes — returned by all 3 provider interfaces, consumed by PortfolioEndpoints for price staleness indicators | VERIFIED |
| `TradingBot.ApiService/Infrastructure/PriceFeeds/Crypto/CoinGeckoPriceProvider.cs` | PRICE-01 crypto price with 5-min freshness | Yes | Yes — 5-min FreshnessWindow line 21; batch fetch via `/simple/price?ids={csv}&vs_currencies=usd` line 149; optional `x-cg-demo-api-key` header line 157; Redis read/write with MessagePack; both single and batch `GetPricesAsync` methods | Yes — registered as `ICryptoPriceProvider` in `ServiceCollectionExtensions`; consumed by `GetSummaryAsync` and `GetAssetsAsync` in `PortfolioEndpoints.cs` | VERIFIED |
| `TradingBot.ApiService/Infrastructure/PriceFeeds/Etf/VNDirectPriceProvider.cs` | PRICE-02 VN ETF price with 48h TTL stale-while-revalidate | Yes | Yes — 48h FreshnessWindow line 18; stale-while-revalidate pattern lines 44-46; `ThousandsToVndMultiplier = 1_000m` line 27; fetches `/dchart/history?resolution=D&symbol={ticker}` line 81; fire-and-forget `RefreshInBackgroundAsync` using CancellationToken.None | Yes — registered as `IEtfPriceProvider`; consumed by `GetSummaryAsync` for ETF assets | VERIFIED |
| `TradingBot.ApiService/Infrastructure/PriceFeeds/ExchangeRate/OpenErApiProvider.cs` | PRICE-03 USD/VND exchange rate with 12h TTL | Yes | Yes — 12h FreshnessWindow line 18; wait-for-fetch pattern (blocking refresh, not fire-and-forget); fetches `v6/latest/USD` line 64; extracts `VND` rate from response.Rates dictionary line 72 | Yes — registered as `IExchangeRateProvider`; consumed by `GetSummaryAsync` for currency conversion | VERIFIED |
| `TradingBot.ApiService/Infrastructure/PriceFeeds/ServiceCollectionExtensions.cs` | DI wiring for all 3 providers with shared resilience config | Yes | Yes — `AddPriceFeeds()` extension wires CoinGecko (api.coingecko.com), VNDirect (dchart-api.vndirect.com.vn), OpenErApi (open.er-api.com) HttpClients; shared resilience: 2 retries, 1s exponential backoff, 15s total/8s attempt timeout | Yes — called from `Program.cs` | VERIFIED |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `CoinGeckoPriceProvider.GetPriceAsync` | Redis IDistributedCache | MessagePack serialization of PriceFeedEntry | WIRED | `ReadCacheAsync` deserializes via `MessagePackSerializer.Deserialize<PriceFeedEntry>`; `WriteCacheAsync` serializes with 30-day PhysicalTtl |
| `VNDirectPriceProvider.GetPriceAsync` | `RefreshInBackgroundAsync` | `_ = RefreshInBackgroundAsync(...)` fire-and-forget | WIRED | Stale path returns immediately with stale PriceFeedResult, then fires background task that calls `FetchAndCacheAsync` with `CancellationToken.None` |
| `PriceFeedResult.IsStale` | `PortfolioAssetResponse.isPriceStale` | `PriceFeedResult` returned by providers consumed in GetAssetsAsync | WIRED | `GetAssetsAsync` stores `priceFeedResult.IsStale` and `priceFeedResult.FetchedAt` in `PortfolioAssetResponse`; Flutter `AssetRow` shows `StalenessLabel` when `asset.isPriceStale == true` |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PRICE-01 | 27-01-PLAN.md | Crypto asset prices auto-fetch from CoinGecko with Redis caching (5-min TTL) | SATISFIED | `CoinGeckoPriceProvider.cs`: `FreshnessWindow = TimeSpan.FromMinutes(5)` line 21; batch fetch via `/simple/price` API; Redis `SetAsync` with 30-day physical TTL; optional API key header; registered as `ICryptoPriceProvider` via `ServiceCollectionExtensions` |
| PRICE-02 | 27-02-PLAN.md | VN30 ETF prices auto-fetch from VNDirect API with graceful degradation to cached values (48h TTL) | SATISFIED | `VNDirectPriceProvider.cs`: `FreshnessWindow = TimeSpan.FromHours(48)` line 18; stale-while-revalidate returns stale immediately and refreshes in background; dchart API endpoint fetches latest close price multiplied by 1000 for actual VND |
| PRICE-03 | 27-02-PLAN.md | USD/VND exchange rate auto-fetches daily from open.er-api.com with Redis caching (12h TTL) | SATISFIED | `OpenErApiProvider.cs`: `FreshnessWindow = TimeSpan.FromHours(12)` line 18; `v6/latest/USD` endpoint line 64; VND rate extracted from rates dictionary; 30-day physical Redis TTL |
| PRICE-04 | 27-01-PLAN.md | Price staleness is tracked and surfaced (last updated timestamp available for all price types) | SATISFIED | `PriceFeedResult.cs`: `record PriceFeedResult(decimal Price, DateTimeOffset FetchedAt, bool IsStale, string Currency)` — all providers return FetchedAt and IsStale; `PriceFeedEntry.cs`: `long FetchedAtUnixSeconds` stored in Redis, computed back to `DateTimeOffset FetchedAt` via `[IgnoreMember]` property |

All 4 requirement IDs (PRICE-01, PRICE-02, PRICE-03, PRICE-04) are accounted for and satisfied. No orphaned requirements detected for this phase.

### Anti-Patterns Found

None detected. No TODO/FIXME comments, no empty implementations. Background refresh in VNDirectPriceProvider correctly uses `CancellationToken.None` to avoid cancellation by request lifecycle.

### Human Verification Required

#### 1. Live: CoinGecko price fetch

**Test:** Call `GET /api/portfolio/summary` with the app running. Check logs for "Fetched crypto prices for bitcoin from CoinGecko". Call again within 5 minutes — verify no second CoinGecko call (served from Redis).
**Expected:** First call fetches and caches; subsequent calls within 5 minutes use cache (Fresh result).
**Why human:** Requires live Redis and CoinGecko API access; staleness boundary testing needs a running app.

#### 2. Live: VNDirect stale-while-revalidate

**Test:** After VNDirect data is cached, wait for cache to age past 48h (or manually set a stale timestamp in Redis). Make a portfolio summary call. Observe that the response returns immediately (stale) while background refresh occurs.
**Expected:** Response is instant, logs show background refresh task started with staleness indicator.
**Why human:** 48h TTL testing requires time manipulation or direct Redis inspection.

### Gaps Summary

No gaps. All 4 observable truths are verified: CoinGeckoPriceProvider has 5-min FreshnessWindow with Redis caching; VNDirectPriceProvider has 48h TTL with stale-while-revalidate; OpenErApiProvider has 12h TTL with wait-for-fetch; PriceFeedResult carries FetchedAt and IsStale on all providers, PriceFeedEntry stores timestamp as long for MessagePack compatibility.

Build compiles with 0 errors and all 76 tests pass.

---

_Verified: 2026-02-21_
_Verifier: Claude (gsd-execute-phase)_

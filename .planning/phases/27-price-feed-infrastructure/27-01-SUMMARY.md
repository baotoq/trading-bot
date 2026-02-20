# Phase 27 Plan 01 — Summary

**Completed:** 2026-02-20
**Duration:** Single pass, zero errors

## What Was Built

Created the shared price feed infrastructure and CoinGecko crypto price provider:

1. **PriceFeedEntry** — MessagePack-serializable positional record with `Price`, `FetchedAtUnixSeconds`, `Currency`. Uses `long` for timestamp to avoid DateTimeOffset resolver issues. Computed `FetchedAt` property via `[IgnoreMember]`.

2. **PriceFeedResult** — Return type record with `Price`, `FetchedAt`, `IsStale`, `Currency`. Static factories `Fresh()` and `Stale()` for clarity.

3. **ICryptoPriceProvider** — Interface with `GetPriceAsync(string coinGeckoId)` and batch `GetPricesAsync(IEnumerable<string> coinGeckoIds)`.

4. **IEtfPriceProvider** — Interface with `GetPriceAsync(string vnDirectTicker)`.

5. **IExchangeRateProvider** — Interface with `GetUsdToVndRateAsync()`.

6. **CoinGeckoPriceProvider** — Full implementation with:
   - 5-minute freshness window, 30-day physical Redis TTL
   - Lazy fetch: fresh cache returns immediately, stale attempts refresh with fallback, empty cache blocks
   - Batch API call via `/simple/price?ids={csv}&vs_currencies=usd`
   - Optional `x-cg-demo-api-key` header from CoinGeckoOptions.ApiKey
   - Primary constructor with HttpClient, IDistributedCache, IOptionsMonitor, ILogger

## Files Created

| File | Purpose |
|------|---------|
| `Infrastructure/PriceFeeds/PriceFeedEntry.cs` | MessagePack cache record |
| `Infrastructure/PriceFeeds/PriceFeedResult.cs` | Provider return type |
| `Infrastructure/PriceFeeds/Crypto/ICryptoPriceProvider.cs` | Crypto provider interface |
| `Infrastructure/PriceFeeds/Crypto/CoinGeckoPriceProvider.cs` | CoinGecko implementation |
| `Infrastructure/PriceFeeds/Etf/IEtfPriceProvider.cs` | ETF provider interface |
| `Infrastructure/PriceFeeds/ExchangeRate/IExchangeRateProvider.cs` | Exchange rate interface |

## Verification

- `dotnet build TradingBot.slnx` — 0 errors
- All MessagePack attributes correctly applied (MsgPack017 warning resolved with positional record)
- No new NuGet packages required

## Decisions Made

- Used positional record syntax for PriceFeedEntry to avoid MessagePack init accessor warnings
- CoinGecko API key sent per-request via HttpRequestMessage header (not default header on HttpClient — that's handled in ServiceCollectionExtensions in Plan 02)
- Batch method returns stale results for cached IDs and throws only if uncached IDs fail to fetch

---
requirements-completed: [PRICE-02, PRICE-03, PRICE-04]
---

# Phase 27 Plan 02 — Summary

**Completed:** 2026-02-20
**Duration:** Single pass, zero errors

## What Was Built

Completed the price feed infrastructure with VNDirect ETF provider, exchange rate provider, DI wiring, and Program.cs integration:

1. **VNDirectPriceProvider** — Stale-while-revalidate pattern:
   - Fresh cache (< 48h) returns immediately
   - Stale cache returns immediately + fire-and-forget background refresh via `_ = RefreshInBackgroundAsync()`
   - Empty cache blocks on fetch, throws if API is down
   - Close prices multiplied by 1000 (dchart returns thousands-of-VND)
   - Uses `CancellationToken.None` for background refresh (not tied to original request)

2. **VNDirectDchartResponse** — JSON DTO for dchart API array format (`t`, `c`, `o`, `h`, `l`, `v`, `s`).

3. **OpenErApiProvider** — Wait-for-fetch pattern (not stale-while-revalidate):
   - Fresh cache (< 12h) returns immediately
   - Stale cache tries refresh, falls back to stale on failure
   - Empty cache blocks, throws if API is down
   - Fetches from `v6/latest/USD`, extracts VND rate

4. **OpenErApiResponse** — JSON DTO for exchange rate response.

5. **ServiceCollectionExtensions.AddPriceFeeds()** — Wires all three providers:
   - CoinGeckoPriceProvider HttpClient (api.coingecko.com, optional x-cg-demo-api-key header)
   - VNDirectPriceProvider HttpClient (dchart-api.vndirect.com.vn)
   - OpenErApiProvider HttpClient (open.er-api.com)
   - Shared resilience config: 2 retries, 1s exponential backoff, 15s total timeout, 8s attempt timeout
   - All registered as Scoped (matching existing DI patterns)

6. **Program.cs** — Added `builder.Services.AddPriceFeeds(builder.Configuration)` after CoinGecko registration.

## Files Created/Modified

| File | Action |
|------|--------|
| `Infrastructure/PriceFeeds/Etf/VNDirectPriceProvider.cs` | Created |
| `Infrastructure/PriceFeeds/Etf/VNDirectDchartResponse.cs` | Created |
| `Infrastructure/PriceFeeds/ExchangeRate/OpenErApiProvider.cs` | Created |
| `Infrastructure/PriceFeeds/ExchangeRate/OpenErApiResponse.cs` | Created |
| `Infrastructure/PriceFeeds/ServiceCollectionExtensions.cs` | Created |
| `Program.cs` | Modified (added using + AddPriceFeeds call) |

## Verification

- `dotnet build TradingBot.slnx` — 0 errors
- `dotnet test` — 76/76 passed (all existing tests unchanged)
- No new NuGet packages required

## Decisions Made

- VNDirect uses stale-while-revalidate (returns stale immediately, refreshes in background) per research recommendation — 48h TTL means staleness only during VN market hours
- OpenErApiProvider uses wait-for-fetch (not stale-while-revalidate) — exchange rate accuracy matters more for currency conversion
- Shared `ConfigureResilience` method avoids repeating resilience options 3 times
- CoinGecko API key added as DefaultRequestHeader on HttpClient at registration time (ServiceCollectionExtensions), not per-request

# Phase 27: Price Feed Infrastructure - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Live prices for all portfolio asset types — crypto from CoinGecko, VN ETFs from VNDirect finfo, and USD/VND exchange rate from open.er-api.com — with Redis caching and graceful degradation. This phase builds the price provider infrastructure; consuming endpoints and UI are Phase 28 and 29.

</domain>

<decisions>
## Implementation Decisions

### Fetch scheduling
- Lazy fetch + cache: prices are fetched on-demand when a consumer requests them, not via background polling
- Cold start is acceptable: no pre-warming on startup; first request per asset type triggers the fetch
- Separate provider services: ICryptoPriceProvider, IEtfPriceProvider, IExchangeRateProvider — each resolved independently via DI

### Cache design
- Single Redis key per price with value + fetchedAt timestamp stored together (MessagePack serialized)
- Consumer checks freshness via the timestamp; TTL freshness windows: crypto 5 min, ETF 48 hours, exchange rate 12 hours
- Long TTL safety net (30 days) on the Redis key itself so ancient data eventually cleans up
- "Always return stale" policy: if cache exists but is past freshness window and provider is down, return the stale value with its timestamp so the UI can show staleness
- If cache is completely empty (first-ever fetch) and provider is down, throw an exception — let it fail loudly

### Multi-asset resolution
- Crypto provider supports any CoinGecko-listed coin, not just BTC — accepts CoinGecko IDs
- Symbol-to-CoinGecko-ID mapping maintained in the provider (e.g., BTC → bitcoin, ETH → ethereum)
- ETF provider uses friendly-name-to-VNDirect-ticker mapping (e.g., "VN30 ETF" → "E1VFVN30")
- CoinGecko provider batch-fetches all portfolio crypto assets in a single API call (multi-coin query)

### Error handling
- Log only on provider failure — no Telegram notifications for price feed issues
- Shared default HTTP timeout across all providers
- Polly retry policy: 2-3 retries with exponential backoff on transient HTTP errors
- No health check endpoint for price feeds — staleness timestamps on the data are sufficient

### Claude's Discretion
- Stale-while-revalidate vs wait-for-fetch when cached price is expired (pick based on UX and complexity tradeoffs)
- Exact Polly retry count and backoff intervals
- MessagePack schema for cached price entries
- Provider interface method signatures and return types (e.g., PriceFeedResult record with Price, FetchedAt, Currency)

</decisions>

<specifics>
## Specific Ideas

- The existing codebase already uses MessagePack for Redis caching — follow the same pattern
- VNDirect finfo API schema is unconfirmed (noted in roadmap research flag) — researcher needs to verify endpoint and field names
- CoinGecko free tier rate limits apply — batch fetch helps stay within limits

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 27-price-feed-infrastructure*
*Context gathered: 2026-02-20*

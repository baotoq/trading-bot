# Phase 3: Smart Multipliers - Context

**Gathered:** 2026-02-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Adjust daily DCA buy amount based on dip severity (30-day high tiers) and bear market conditions (200-day MA). Multipliers stack multiplicatively. This is an additive enhancement — the existing fixed-amount buy flow continues to work; this layers intelligence on top. Price data fetching, storage, and historical bootstrap are in scope. Rich notification content about multiplier reasoning belongs in Phase 4.

</domain>

<decisions>
## Implementation Decisions

### Dip tier thresholds
- 4 tiers: no dip (1x), small dip (1.5x), medium dip (2x), large dip (3x)
- Breakpoints at >=5% drop, >=10% drop, >=20% drop from 30-day high
- Boundary rule: >= comparison (exactly 10% down = Tier 3 at 2x)
- Tier thresholds and multipliers configurable in DcaOptions (appsettings.json)

### 30-day high tracking
- Bootstrap: Fetch 30 days of candle data from Hyperliquid API on first run
- Based on daily close prices (not intraday highs)
- Persisted in database table (DailyPrice) — survives restarts, queryable
- Day boundary: UTC midnight (00:00–23:59 UTC)

### 200-day MA behavior
- Bootstrap: Fetch 200 days of daily candles from Hyperliquid (or external API like CoinGecko) on first run
- Bear boost: 1.5x multiplier when current price < 200-day MA
- Stale data policy: Use last known values even if >24h old — don't fall back to 1x
- Persisted in same DailyPrice table alongside 30-day data

### Multiplier stacking & caps
- Stacking: Multiplicative (dip tier × bear boost, e.g., 2x × 1.5x = 3x)
- Configurable max multiplier cap in DcaOptions, default 4.5x (natural max, effectively uncapped)
- Insufficient funds: Buy what you can — use available balance up to the multiplied amount

### Claude's Discretion
- MA type (SMA vs EMA) — pick what's standard for BTC macro analysis
- Exact candle API endpoint and pagination strategy
- DailyPrice entity schema design
- How to handle missing/gap days in price data
- Price data refresh scheduling (when to fetch new daily candle)

</decisions>

<specifics>
## Specific Ideas

- Multiplier metadata recorded per purchase: multiplier, tier, 30-day high, MA value, drop %
- Stale data threshold from roadmap: >24h old triggers fresh fetch attempt, but if fetch fails, use last known values anyway
- All new config should follow existing IOptionsMonitor hot-reload pattern

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-smart-multipliers*
*Context gathered: 2026-02-12*

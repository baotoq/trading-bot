# Phase 7: Historical Data Pipeline - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Ingest 2-4 years of BTC daily prices from CoinGecko into the existing DailyPrice PostgreSQL table. Detect and report data gaps. Expose data status and ingestion via API endpoints. Backtest endpoints and parameter sweep belong to Phase 8.

</domain>

<decisions>
## Implementation Decisions

### Ingestion behavior
- Default date range: 4 years back (covers a full BTC halving cycle)
- Ingestion runs asynchronously — POST /ingest returns job ID + estimated completion time, caller polls for status
- Built-in throttle for CoinGecko rate limits — automatically pace requests to stay under free tier limits, silent to caller
- BTC only — hardcode BTC/USD, no multi-coin abstraction
- Single job at a time — reject new ingest requests if one is already running

### Data quality & gaps
- Every calendar day is expected — BTC trades 24/7, any missing date is a gap
- Gap detection runs automatically after every ingestion completes
- Auto-fill attempt on gaps — automatically retry fetching missing dates, report any that still can't be filled
- Backtesting blocked if gaps exist in the requested date range — refuse to run with error, forces clean data

### API response design
- GET /data/status returns rich info: date range, total days stored, gap count, gap dates, last ingestion time, data source info, freshness indicator, coverage percentage
- POST /data/ingest returns job ID + estimated completion time
- Job status polling endpoint — Claude's discretion on whether dedicated endpoint or folded into /data/status
- Single concurrent job only — reject if already running

### Error & edge cases
- CoinGecko down: retry with exponential backoff up to max retries, then fail job with clear error
- Partial ingestion: keep successfully fetched data — next run picks up where it left off (incremental)
- Force re-ingestion supported via --force/overwrite flag to re-fetch and update existing dates
- Store OHLC (open, high, low, close) per day — richer than close-only for potential future analysis

### Claude's Discretion
- Job polling endpoint structure (dedicated vs folded into status)
- Exact throttle timing and batch sizes for CoinGecko
- Exponential backoff parameters (retry count, base delay)
- Estimated time calculation approach
- Internal data storage schema details

</decisions>

<specifics>
## Specific Ideas

- 4-year range covers a full halving cycle — important for meaningful BTC backtesting
- OHLC storage future-proofs the data even though current backtest only uses close price
- Incremental design means a failed run isn't wasted — just re-run to continue
- Block-on-gaps is strict but ensures backtest results are trustworthy

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 07-historical-data-pipeline*
*Context gathered: 2026-02-13*

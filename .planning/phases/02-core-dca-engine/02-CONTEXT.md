# Phase 2: Core DCA Engine - Context

**Gathered:** 2026-02-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Bot buys a fixed USD amount of BTC daily on Hyperliquid, persists the result, and sends a Telegram confirmation. End-to-end pipeline: schedule → lock → balance check → order → persist → notify. Smart multipliers (Phase 3) and rich notification formatting (Phase 4) are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Scheduling & timing
- Fixed time daily, configurable via appsettings.json (e.g., `"BuyTimeUtc": "08:00"`)
- UTC always — day boundary is midnight UTC, no timezone config
- If bot starts after today's scheduled time: skip, wait for next day (no catch-up buys)

### Order execution
- Market order — execute immediately at current price
- Buy what you can — if balance is less than target, buy with available balance
- Check USDC balance before placing order — skip and notify if too low (e.g., < $1)
- Trust immediate response from market order as fill confirmation (no polling)

### Failure & retry behavior
- 3 retries with backoff on transient failures (network timeout, API 5xx)
- Fail fast on permanent errors (auth failure, invalid params) — no retry
- After all retries fail: record as failed purchase, send Telegram alert, wait for next day
- Crash recovery: on startup, query Hyperliquid for today's recent fills to detect already-executed orders and prevent double buys

### Telegram notifications
- Notify on ALL events: success, failure, and skips (low balance, already purchased)
- Success message — detailed: BTC amount bought, price, USD spent, current BTC balance, remaining USDC
- Failure message — full error details: error type, message, retry count, stack context
- Skip message — reason for skip (low balance, already bought today, missed window)
- Formatted with Telegram Markdown (bold labels, monospace numbers)

### Claude's Discretion
- Exact retry backoff strategy (exponential, jitter, etc.)
- Minimum balance threshold amount
- Telegram message template layout
- Background service implementation pattern
- Order size rounding / minimum order handling

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-core-dca-engine*
*Context gathered: 2026-02-12*

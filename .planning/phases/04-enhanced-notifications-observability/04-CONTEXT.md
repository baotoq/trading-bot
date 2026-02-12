# Phase 4: Enhanced Notifications & Observability - Context

**Gathered:** 2026-02-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Rich Telegram notifications explaining buy reasoning with running totals, weekly summary reports, health monitoring endpoint, daily missed-purchase verification, and dry-run mode for testing without placing real orders. The bot already sends basic notifications (Phase 2) — this phase enhances message content, adds operational visibility, and enables safe testing.

</domain>

<decisions>
## Implementation Decisions

### Buy message design
- Key facts + natural language reasoning (not raw numbers dump)
- Show: price, quantity, cost, multiplier value, and a readable explanation of WHY (e.g., "Buying 3x: BTC is 15% below 30-day high and below 200-day MA")
- Include running totals inline on every buy message (total BTC, total USD spent, avg cost)
- Skip notifications also get enhanced formatting — include reasoning (why skipped) and context (when next buy expected)

### Summary & totals
- Full weekly report: this week's daily buys (date, amount, multiplier), running totals, avg cost vs current price, unrealized P&L percentage
- Weekly summary sent Sunday evening (UTC)
- Running totals calculated from ALL purchases in the database (lifetime accumulation)
- Current BTC price for P&L fetched from Hyperliquid mid price at summary time

### Health & verification
- Basic liveness health check: app running, DB connected, scheduler alive (standard /health endpoint)
- Daily missed purchase check runs shortly after the 10-minute execution window closes (~30 min after scheduled time)
- Missed purchase alert sent via Telegram (not just health endpoint)
- Alert includes diagnostics: possible reasons why purchase was missed (scheduler didn't run, order failed, insufficient balance, etc.)

### Dry-run mode
- Activated via appsettings.json config toggle (DcaOptions:DryRun = true)
- Dry-run purchases persisted to database with IsDryRun flag — keeps history, excluded from real totals
- Notifications use a clear "SIMULATION" banner/section (not just a prefix tag)
- Fetches real prices and balances from Hyperliquid — only skips actual order placement

### Claude's Discretion
- Exact Telegram message formatting and emoji usage
- Health check response JSON structure
- Diagnostic message detail level for missed purchases
- Weekly summary Telegram formatting (table vs list)
- How dry-run purchases are excluded from running totals queries

</decisions>

<specifics>
## Specific Ideas

- Natural language reasoning: "Buying 3x: BTC is 15% below 30-day high and below 200-day MA" — readable, not a formula
- Every buy notification should feel like a brief explanation of what the bot did and why
- Weekly summary should be a full report you can glance at to know how your DCA is performing

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-enhanced-notifications-observability*
*Context gathered: 2026-02-12*

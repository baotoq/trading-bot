# Phase 28: Portfolio Backend API - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

All backend endpoints for portfolio read (summary, asset breakdown), manual transaction writes, DCA auto-import from existing Purchase events, and fixed deposit CRUD. This is the complete API surface the Flutter app will consume. Does NOT include UI/display (Phase 29) or charts/analytics (v4.x).

</domain>

<decisions>
## Implementation Decisions

### Portfolio summary calculation
- P&L uses **weighted average cost basis**: total cost / total quantity = avg cost per unit; P&L = (current price - avg cost) * quantity
- Allocation percentages based on **current market value**, not cost basis
- Cross-currency aggregation: each asset converts to USD and VND **independently** (no intermediate conversion), then sum each currency total separately
- **Split endpoints**: GET /api/portfolio/summary for totals + allocation; GET /api/portfolio/assets for per-asset breakdown with P&L detail

### DCA auto-import
- **Event-driven**: when DCA bot creates a Purchase, a domain event triggers auto-import into AssetTransaction via existing MediatR event flow
- Idempotency via **PurchaseId as source reference**: AssetTransaction stores SourcePurchaseId; check for existence before inserting
- BTC asset must be **manually created** by user first — auto-import does NOT auto-create the PortfolioAsset
- If BTC asset doesn't exist when Purchase event fires: **silently skip with warning log**. User sets up asset later and historical migration catches up

### Historical migration
- Triggered **automatically on first summary call**: when BTC asset exists but has no imported transactions, migration runs
- **Idempotent and re-runnable**: uses PurchaseId as source reference to skip already-imported records; safe to run multiple times
- Maps **all available fields** from Purchase to AssetTransaction (date, quantity, USD amount, price, fees)
- Original Purchase records **left untouched** — no schema changes to existing table; link tracked via SourcePurchaseId on AssetTransaction side

### Fixed deposit endpoints
- GET response includes both **current accrued value** (as of today) and **projected maturity value**
- **No early withdrawal** support — deposits run to maturity; user can only delete the record entirely
- PUT allows **all fields editable** (principal, rate, dates, compounding frequency) — useful for correcting typos
- DELETE is a **hard delete** — record removed entirely from database

### Claude's Discretion
- Exact API response DTOs and field naming conventions
- Error response format and validation error structure
- Endpoint URL structure beyond what's specified above
- Whether to use specifications pattern for queries or inline EF queries
- Transaction creation validation details beyond what requirements specify

</decisions>

<specifics>
## Specific Ideas

- Summary endpoint should be fast enough for Flutter pull-to-refresh — price data comes from Redis cache (Phase 27), so computation is the only cost
- Auto-import via domain events aligns with existing OutboxMessage → MediatR handler pattern in the codebase
- Historical migration on first summary call means zero setup friction — user creates BTC asset, opens portfolio, and history is there

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 28-portfolio-backend-api*
*Context gathered: 2026-02-20*

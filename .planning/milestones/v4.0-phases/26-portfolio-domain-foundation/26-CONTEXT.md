# Phase 26: Portfolio Domain Foundation - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Database schema and domain model for multi-asset portfolio tracking. Entities for PortfolioAsset, AssetTransaction, and FixedDeposit with Vogen typed IDs, correct currency precision (VND: no decimals, ETF: whole numbers), and fixed deposit accrued value calculation. No API endpoints, no price feeds, no UI — just the domain layer and EF Core persistence.

</domain>

<decisions>
## Implementation Decisions

### Asset inventory
- Support three asset types: Crypto, ETF, Fixed Deposit
- Crypto: BTC (auto-seeded from DCA bot), plus user-added alts (ETH, SOL, and others)
- ETF: E1VFVN30 as the primary ETF, user can add others
- BTC asset is auto-created from existing DCA purchase data; all other assets added manually
- Assets are dynamically added by the user — no hardcoded catalog beyond the BTC auto-seed

### Fixed deposit modeling
- User typically has 1-3 fixed deposits at a time
- Primary use case is simple interest (no compounding) — but domain model must support both simple and compound (monthly/quarterly/semi-annual/annual) per requirements PORT-06
- No early withdrawal modeling — just start date, maturity date, and rate
- Matured deposits get a "Matured" status rather than being deleted — keeps history visible
- Fixed deposit lifecycle: Active → Matured (status field on entity)

### Transaction data capture
- Track fees per transaction — fee amount field on AssetTransaction
- No notes field — keep transactions minimal (date, quantity, price, currency, fee)
- No exchange/source field — just the trade data
- DCA bot auto-imported transactions should pull fee data from existing Purchase records if available
- Auto-imported transactions are read-only (flagged with source = Bot, not editable/deletable per DISP-08)

### Claude's Discretion
- Entity inheritance strategy (TPH vs TPT vs separate tables)
- Aggregate root boundaries
- Value object choices beyond what Vogen requires
- Test data and edge case coverage approach

</decisions>

<specifics>
## Specific Ideas

- BTC should be auto-seeded when the DCA bot import runs (Phase 28), but the entity/schema must support it now
- Fixed deposit status enum: Active, Matured — simple two-state lifecycle
- Fee field should be optional (nullable or default zero) since not all transactions have fees

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 26-portfolio-domain-foundation*
*Context gathered: 2026-02-20*

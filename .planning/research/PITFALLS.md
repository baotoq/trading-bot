# Pitfalls Research

**Domain:** Multi-asset portfolio tracker (crypto, VN30 ETF, fixed deposits) added to existing BTC DCA bot
**Researched:** 2026-02-20
**Confidence:** HIGH for integration/architecture pitfalls (code inspected), MEDIUM for VN price API pitfalls (limited official sources), HIGH for currency/P&L calculation pitfalls (verified against financial domain literature)

---

## Critical Pitfalls

### Pitfall 1: Currency Conversion Timing Corrupts P&L Calculations

**What goes wrong:**
Multi-currency P&L is only meaningful if all conversions use the same exchange rate snapshot. A portfolio containing BTC (priced in USD), VN30 ETF (priced in VND), and fixed deposits (in VND) requires converting everything to a single base currency for total portfolio value. The pitfall: using the current live rate for total value, but a different (historical) rate embedded in each transaction's cost basis. This produces P&L figures that include currency fluctuation as investment gain/loss, even when the asset price itself did not change.

Concrete example: User bought ETF at 25,000 VND/share when USD/VND was 24,000. Today the ETF is 25,500 VND/share (2% gain) but USD/VND is 25,500 (6% VND depreciation). Total P&L in USD shows a loss, even though the VND-denominated investment gained 2%. Neither figure is wrong — but showing only one without labeling the currency creates user confusion and incorrect decision-making.

**Why it happens:**
Developers store transaction cost in the native currency (VND), then apply today's rate at display time. This makes the current value correct, but the cost basis in the denominator is computed with a different (implicit) historical rate, making the P&L percentage meaningless.

**How to avoid:**
Store cost basis in both native currency AND the base currency at transaction time. Schema must include: `cost_native` (VND), `cost_usd`, `exchange_rate_at_transaction`. At display time, always compare current value in the same currency as the stored cost. Show P&L in native currency by default; USD conversion is display-only (not used in P&L math). Never recompute historical cost basis using today's exchange rate.

**Warning signs:**
- P&L percentage changes significantly on days when USD/VND moves but ETF price is flat
- Cost basis is stored in only one currency and converted at read time
- The `portfoliovalue` endpoint uses one rate for current value and another for cost

**Phase to address:** Data model phase (first portfolio phase). The schema must capture both currencies at transaction write time; retrofitting this after transactions are entered requires re-entering all history.

---

### Pitfall 2: DCA Bot Purchases Auto-Imported as Duplicates

**What goes wrong:**
The DCA bot's `Purchase` table already tracks all BTC buys. The portfolio tracker needs to show these as portfolio transactions. The naive approach — querying `Purchases` and synthesizing `PortfolioTransaction` records — leads to the same purchase appearing twice: once from auto-import and again if the user manually enters "I bought BTC on [date]" because they didn't realize it was already imported.

A second variant: the auto-import job runs on a schedule and re-processes already-imported purchases, creating duplicate `PortfolioTransaction` rows for each DCA buy.

**Why it happens:**
Auto-import and manual entry are treated as independent flows with no cross-reference check. The auto-import job lacks an idempotency key linking it to the source `PurchaseId`.

**How to avoid:**
Store `source_type` (enum: `DcaBot` | `Manual`) and `source_reference_id` (nullable, stores `PurchaseId` for DCA bot imports) on every `PortfolioTransaction`. Add a unique index on `(asset_id, source_type, source_reference_id)` where `source_type = DcaBot`. Auto-import job uses `INSERT ... ON CONFLICT DO NOTHING` (upsert). The UI should visually distinguish DCA Bot transactions (locked, not editable) from manual transactions (editable/deletable), preventing user confusion.

**Warning signs:**
- `PortfolioTransaction` has no `source_reference_id` column
- Auto-import job uses `INSERT` without conflict handling
- No unique constraint covering (asset + source + reference) for bot-imported rows
- BTC holdings count in portfolio does not match Hyperliquid spot balance

**Phase to address:** Auto-import phase. Idempotency must be designed before the first import run executes.

---

### Pitfall 3: VN Asset Price APIs Are Scraping-Dependent and Fragile

**What goes wrong:**
There is no official, free, production-grade JSON API for Vietnamese stock prices (HOSE/HNX). The available open-source options (vnstock, vnquant) work by scraping SSI, CafeF, or VPS broker websites. These scrapers break silently when the source site changes HTML structure or rate-limits the IP. vnstock's own documentation explicitly states: "Data may be incomplete, discontinuous, or inaccurate. Authors are not responsible for losses resulting from data inconsistencies."

Additionally, the VN stock market closes at 14:45 ICT (07:45 UTC) on weekdays. Price fetchers that are not timezone-aware will attempt to fetch prices outside market hours and receive either stale prices (previous close) or 404/empty responses, then incorrectly cache the stale result as "current."

**Why it happens:**
Developers test with vnstock or cafef scraping during development when the site is live, and it works fine. They don't test the failure mode when the site is down, rate-limited, or restructures its HTML. They also assume UTC timestamps align with market close.

**How to avoid:**
Treat VN asset prices as best-effort, not real-time. Design pattern:
1. Fetch prices after market close each day (cron at 15:00 ICT = 08:00 UTC weekdays)
2. Cache last-known price in Redis with a TTL of 48 hours (covers weekends and holidays)
3. Always show the data timestamp ("Price as of [date]") — never display without a staleness indicator
4. Design the UI to show stale prices grayed out with a warning if older than 2 trading days
5. Accept that VN asset prices will be end-of-day, not intraday — document this as a product decision, not a limitation to fix

For the data source: prefer SSI Fast Connect API (requires SSI account) over scraping if the user has an SSI brokerage account. Otherwise, Yahoo Finance provides `E1VFVN30.VN` end-of-day data via an unofficial JSON endpoint that is more stable than scraper-based solutions.

**Warning signs:**
- Price fetch is called synchronously on portfolio load (no cache layer)
- No staleness indicator on displayed prices
- Fetch schedule is not timezone-aware (uses UTC cron without VN market hours logic)
- Error from price API results in 500 instead of returning cached last-known price

**Phase to address:** Price feed phase. Establish the "best-effort, cached, with staleness indicator" contract before building any price-dependent feature.

---

### Pitfall 4: Fixed Deposit Accrual Computed Incorrectly Due to Compounding Frequency Mismatch

**What goes wrong:**
Vietnamese bank fixed deposits use quarterly compounding for cumulative FDs, but simple interest (no compounding) for non-cumulative payout FDs. If the system applies compound interest to a non-cumulative FD, or uses annual compounding for a quarterly-compounded FD, the accrued value shown will be wrong — appearing correct at deposit date and maturity date, but wrong at any intermediate check.

Additionally, premature withdrawal typically forfeits the contracted interest rate (Vietnamese banks apply a lower "demand deposit" rate to the withdrawn principal). A portfolio system that shows "maturity value" without modeling early withdrawal penalties gives the user an overconfident liquidity picture.

**Why it happens:**
Developers use a single FD formula for all deposits. The distinction between cumulative (compounding) and non-cumulative (periodic payout) FD is a Vietnamese banking domain concept not obvious to non-specialist developers. The compounding frequency (quarterly = every 3 months) is also easy to confuse with annual.

**How to avoid:**
Model FDs with explicit `compounding_frequency` enum: `None` (simple interest, non-cumulative), `Monthly`, `Quarterly`, `SemiAnnual`, `Annual`. Store the actual bank's interest rate, start date, maturity date, and compounding frequency per deposit. Compute daily accrued value as a pure function of these inputs. The accrued value formula for quarterly compound:
```
A = P × (1 + r/4)^(4 × t)  where t = days_elapsed / 365
```
Show both current accrued value and maturity value. Mark deposits approaching maturity (< 30 days) prominently so the user acts before rollover.

**Warning signs:**
- FD model has only `interest_rate` and `maturity_date`, no `compounding_frequency`
- FD value on intermediate dates does not match manual bank statement calculation
- No distinction between cumulative and non-cumulative FD types

**Phase to address:** Fixed deposit model phase. The data model must capture compounding parameters at deposit entry time; changing this later requires re-entering all FD data.

---

### Pitfall 5: Snapshot-Based Portfolio Chart Becomes Expensive or Inaccurate

**What goes wrong:**
A portfolio value chart over time (e.g., "what was my total portfolio worth each day for the past year?") requires knowing the quantity of each asset and its price on each historical date. Two common failure modes:

**Option A: Compute on demand** — Sum all transactions up to date D to get quantities, then fetch historical prices for all assets on date D, then convert. For a 365-day chart with 5 assets this is 365 × 5 = 1,825 price lookups plus cumulative transaction sums. Acceptable at first, but the query pattern becomes O(days × assets) and joins across `Portfolio Transactions` + multiple price tables. Slow enough to be noticeable (2-5s) even at low data volumes.

**Option B: Daily snapshots** — Store a `PortfolioSnapshot` row per day per asset with quantity and value. Accurate but requires a daily background job to run successfully every single day. If the job fails one day, that day's snapshot is missing. If the user enters a past transaction (backdated), all snapshots from that date forward are stale. Reconciliation is complex.

**Why it happens:**
Developers pick one approach at the start without anticipating that backdated transaction entry is a core feature. Option A handles backdated entries naturally (just recompute); Option B requires snapshot invalidation logic.

**How to avoid:**
Use Option A (compute on demand) with aggressive caching:
1. Pre-compute the chart on a daily background job (after price fetch completes) and cache in Redis with a 25-hour TTL
2. On cache miss, compute on demand (triggered by first load of the day)
3. When a new transaction is saved, invalidate the chart cache for all dates >= transaction date
4. On chart load, return cached result + a staleness timestamp so the UI can show "updated X minutes ago"

This gives Option A's correctness (handles backdated entries) with Option B's read performance (cached result).

**Warning signs:**
- Chart endpoint does not have a Redis cache
- No cache invalidation hook on `PortfolioTransaction` save
- Chart query does N+1 price lookups (one per asset per date instead of batch)
- Backdated transaction entry does not trigger any recomputation

**Phase to address:** Portfolio chart phase. Caching strategy must be designed before the chart endpoint is built, not added when performance problems appear.

---

### Pitfall 6: Adding New Aggregates to Existing DbContext Breaks Vogen Convention Registration

**What goes wrong:**
The existing `TradingBotDbContext` registers Vogen value converter conventions globally in `ConfigureConventions()` for all existing typed IDs (`PurchaseId`, `IngestionJobId`, etc.) and value objects (`Price`, `UsdAmount`, etc.). New portfolio entities will have new typed IDs (e.g., `AssetId`, `PortfolioTransactionId`, `FixedDepositId`) and possibly new value objects (e.g., `VndAmount`, `InterestRate`). If the new ID types are defined but not registered in `ConfigureConventions()`, EF Core silently uses the underlying primitive type (`Guid`) for the column instead of the Vogen wrapper, causing runtime errors on queries or inserts.

The error manifests as: `InvalidOperationException: The entity type 'Asset' requires a primary key to be defined` or `Cannot convert Guid to AssetId` — confusing because the property type appears correct in C# but the EF mapping is broken.

**Why it happens:**
`ConfigureConventions()` is not in the same file as the new entity definition. Developers add the Vogen `[ValueObject]` attribute and the entity class but forget the DbContext registration step. There is no compile-time warning for this.

**How to avoid:**
After creating any new Vogen ID or value object type, immediately add the corresponding convention registration to `TradingBotDbContext.ConfigureConventions()`:
```csharp
configurationBuilder.Properties<AssetId>()
    .HaveConversion<AssetId.EfCoreValueConverter, AssetId.EfCoreValueComparer>();
```
Add a test that verifies the DbContext can be created and a round-trip read/write works for each new entity type. This catches missing convention registrations at test time, not runtime.

**Warning signs:**
- A new `[ValueObject]` ID type exists in `Models/Ids/` but has no matching entry in `ConfigureConventions()`
- EF Core migration generates a `Guid` column where a named ID type was expected
- First insert of a new entity type fails at runtime with a type conversion error

**Phase to address:** Data model phase (first portfolio phase). Each new entity's Vogen registration must be verified in the same PR that introduces the entity.

---

### Pitfall 7: Mixed Asset Quantities Stored in Incompatible Precision

**What goes wrong:**
The existing `Quantity` value object (from `Quantity.cs`) uses `decimal` precision suitable for BTC (8 decimal places, e.g., 0.00023450 BTC). VN30 ETF shares are whole units. Fixed deposit principal is a VND amount with at most 2 decimal places. If all assets share the same `Quantity` type (or the same DB column precision), precision is either wasted (ETF as `0.00000000 units`) or the business rule that "ETF quantity must be whole number" is not enforced at the domain level.

More critically: cost basis for ETF is `price_per_share × quantity` in VND, but `UsdAmount` value objects have `HasPrecision(18, 2)` in the existing schema — which is correct for USD but insufficient for VND amounts (1 USD ≈ 25,500 VND, so $1,000 ≈ 25,500,000 VND, still fits in 18,2 but is semantically wrong as a "USD Amount" type).

**Why it happens:**
Developers reuse existing value objects to save time. `UsdAmount` seems general enough for "a monetary amount." It is not — the type name implies currency semantics that do not hold for VND.

**How to avoid:**
Introduce separate value objects for new currency/quantity semantics:
- `VndAmount` — `decimal`, `HasPrecision(18, 0)` (VND has no subunit in practice)
- `ShareQuantity` — `int` (ETF shares are always whole units)
- Keep `UsdAmount` for USD-denominated values, `Quantity` for BTC-scale decimals
- Currency-agnostic cost storage: store `cost_in_native_currency` as `decimal(18,2)` + `currency_code` string, not as a typed value object (too many currencies to enumerate)

**Warning signs:**
- ETF transaction quantity stored as `0.00000000` shares (should be integer)
- VND amounts stored in `UsdAmount` columns (misleading type)
- P&L calculation produces floating-point rounding errors on VND amounts > 10,000,000

**Phase to address:** Data model phase. Value object design must be resolved before any transaction entity is created.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Store only USD cost basis, convert VND at display | Simpler schema, no dual-currency storage | Currency fluctuation corrupts P&L; historical VND cost irretrievable | Never for VND assets |
| Reuse `UsdAmount` for VND values | No new value object to create | Type semantics wrong; unit confusion in calculations; future devs assume USD | Never |
| Compute portfolio chart on demand without cache | No background job needed | Chart load becomes O(days × assets); degrades to 5s+ with modest history | Only for MVP with < 30 days history |
| Use vnstock/cafef scraping directly for live prices | Easy to integrate | Breaks silently on site changes; no SLA; returns stale data without warning | Only in development/prototyping, not production |
| Auto-import without idempotency key | Simpler import job | Duplicate transactions on re-run; hard to detect after data grows | Never |
| Single FD model without compounding_frequency | Faster to build | Accrual calculations wrong for non-cumulative FDs; user sees incorrect values | Never |

---

## Integration Gotchas

Common mistakes when connecting new portfolio features to the existing system.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| DCA bot Purchase → portfolio auto-import | Query `Purchases` and insert `PortfolioTransactions` on every app start | Track `last_imported_at` cursor; only import purchases newer than cursor; use `ON CONFLICT DO NOTHING` on `source_reference_id` |
| CoinGecko for multi-asset prices | Fetch each coin individually in a loop | Batch: `/simple/price?ids=bitcoin,ethereum&vs_currencies=usd` returns all in one call. Max 50 IDs per request. |
| USD/VND exchange rate API | Fetch rate on every portfolio load | Cache in Redis (TTL 60 min). Rate changes slowly; daily is sufficient for portfolio display |
| Hyperliquid spot balance vs portfolio quantity | Show Hyperliquid balance as "current BTC" without reconciling against portfolio transactions | Reconcile: portfolio quantity (from all transactions) should equal Hyperliquid balance ± tolerance. Surface discrepancy as a UI warning |
| EF Core migration with new Portfolio tables | Run migration from root of solution | EF migrations must run from `TradingBot.ApiService/` directory — existing project rule, applies equally to new migrations |
| Riverpod providers for currency toggle (VND/USD) | Store selected currency in local widget state | Store in a global Riverpod provider (persistent via `shared_preferences`) so the choice survives navigation and app restarts |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| N+1 price fetches per asset on portfolio load | Portfolio load takes 2s+ with 5 assets | Batch all price fetches into one CoinGecko call; fetch VN prices from cache | With 3+ assets on every load |
| Portfolio chart recalculation on every request | Chart endpoint takes 3-5s; visible spinner | Cache computed chart in Redis; invalidate on transaction save | After 90+ days of history |
| Snapshot table per-day × per-asset with no index | Chart historical query scans full table | Index on `(date DESC, asset_id)` if using snapshot table | After 365+ rows |
| Fixed deposit accrual computed on every load | Simple but slow for 10+ FDs with daily tick | Compute once per day in background, cache result | With 10+ FDs checking every load |
| `PortfolioTransaction` loaded entirely for quantity calc | Loads 1,000+ rows to sum current holdings | Use `SUM()` aggregate query via specification, not `.ToList()` | After 100+ transactions per asset |

---

## Security Mistakes

Domain-specific security issues for personal portfolio data.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exchange rate API key in appsettings.json committed to git | API key exposed in repo history | Use .NET User Secrets for exchange rate API keys; document in CLAUDE.md |
| VN stock price fetcher stores raw HTML responses in DB | Personal data exposure if DB is compromised | Store only the parsed price value, not raw scrape results |
| Portfolio transaction amounts logged at DEBUG level | Financial data visible in log aggregator | Structured logging with amount as property only at INFO level; never at DEBUG/TRACE |
| No validation on backdated transaction date | User enters future date, corrupts all portfolio snapshots | Backend validates `transaction_date <= today` with ErrorOr result |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Show total portfolio value without currency label | User cannot tell if "₫25,450,000" or "$25,450" | Always prefix with currency symbol; make VND/USD toggle prominent and persistent |
| P&L shown without indicating the calculation basis | User unsure if P&L includes currency gain/loss | Show "P&L in VND (native currency)" — not converted to USD in the P&L calculation |
| Fixed deposit shown as "current value" without maturity distinction | User thinks they can withdraw full accrued value today | Show two values: "Accrued today: X" and "At maturity: Y" with maturity date |
| Stale VN asset prices shown without timestamp | User acts on 3-day-old ETF price | Show "Last updated: [date]" on every VN asset price, grayed if older than 1 trading day |
| BTC portfolio value uses Hyperliquid price, ETF uses CafeF price, FD uses manual rate — shown side by side | Apparent inconsistency in data freshness | Unify "data as of" display per asset; show the source and timestamp for each price |
| VND amounts with no thousands separator | "25450000" is unreadable vs "25,450,000" | Format VND with period or comma separator: "25.450.000 ₫" (Vietnamese standard) |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Auto-import:** First import works — verify re-running import does not create duplicates (test idempotency explicitly)
- [ ] **P&L:** Total portfolio P&L updates when price changes — verify it does NOT change when only USD/VND rate changes (P&L should be in native currency)
- [ ] **Fixed deposit:** Accrued value shown — verify value is correct at an intermediate date (not just at deposit and maturity)
- [ ] **VN price feed:** Prices display on weekday during market hours — verify behavior on weekends, market holidays, and after hours (should show last close with timestamp, not an error)
- [ ] **Currency toggle:** VND/USD toggle works on portfolio screen — verify the choice persists after app restart and across screen navigation
- [ ] **Portfolio chart:** Chart renders with current data — verify a backdated transaction entry causes chart to update for all dates >= transaction date
- [ ] **BTC quantity reconciliation:** Portfolio shows BTC holdings — verify quantity matches Hyperliquid spot balance (within rounding tolerance)
- [ ] **New EF entities:** Database migrations run — verify `dotnet ef migrations add` produces expected schema (no Guid columns where Vogen IDs expected)
- [ ] **Multiple FD types:** Cumulative FD accrual correct — verify non-cumulative FD accrual uses simple interest (not compound)

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Duplicate auto-imported transactions found | MEDIUM | Write a SQL deduplication script: delete rows with `source_type = DcaBot` where `source_reference_id` appears more than once, keeping oldest. Add unique constraint after cleanup. |
| P&L calculation wrong (currency conversion at wrong time) | HIGH | Requires adding `exchange_rate_at_transaction` column, backfilling from exchange rate API historical data, recalculating all P&L. Accept this as a data migration. |
| VN price feed breaks (scraper returns wrong data) | LOW | Switch data source (vnstock → Yahoo Finance `E1VFVN30.VN` endpoint). Cached prices remain valid for 48h TTL, buying time for fix. |
| Portfolio chart is slow (no cache) | LOW | Add Redis cache layer to chart endpoint; no data migration needed, purely additive change |
| Wrong FD compounding model (simple vs compound) | MEDIUM | Update calculation logic (code change only); no stored data changes needed if `compounding_frequency` is stored. If not stored, requires user to re-enter FD details. |
| Vogen type missing from `ConfigureConventions()` | LOW | Add registration, generate new migration, apply. Test suite catches this before production if round-trip test exists. |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Currency conversion timing corrupts P&L (#1) | Data model phase | P&L does not change when USD/VND rate changes but asset price is flat |
| DCA auto-import duplicates (#2) | Auto-import phase | Run import job twice; verify transaction count is identical after second run |
| VN price API fragility (#3) | Price feed phase | Kill the price source; verify cached price is returned with staleness timestamp |
| FD compounding frequency mismatch (#4) | Fixed deposit model phase | Verify quarterly-compounded FD accrual matches bank formula at day 45 and day 90 |
| Portfolio chart performance (#5) | Portfolio chart phase | Chart loads < 1s on warm cache; backdated transaction invalidates cache correctly |
| Vogen convention registration (#6) | Data model phase | Round-trip EF test for every new entity type passes; migration produces correct column types |
| Mixed quantity/precision types (#7) | Data model phase | ETF quantity rejects fractional input; VND amount stores without float rounding |

---

## Sources

- [CoinGecko API Rate Limits — Official Docs](https://docs.coingecko.com/docs/common-errors-rate-limit) — HIGH confidence, 30 calls/min free tier, max 50 IDs per batch request
- [vnstock Data Disclaimer — Official Docs](https://docs.vnstock.site/) — HIGH confidence, explicit "data may be incomplete/inaccurate" warning
- [vnstock SSI Fast Connect API — Official Docs](https://docs.vnstock.site/integrate/ssi_fast_connect_api/) — MEDIUM confidence, requires SSI brokerage account
- [VFMVN30 ETF on Yahoo Finance](https://finance.yahoo.com/quote/E1VFVN30.VN/) — MEDIUM confidence, unofficial API, end-of-day data available
- [Vogen EF Core Value Converters — Official Docs](https://github.com/SteveDunn/Vogen) — HIGH confidence, existing codebase pattern confirmed
- [ExchangeRate-API Free Tier](https://www.exchangerate-api.com) — MEDIUM confidence, free tier supports USD/VND, 30-day historical data
- [CoinTracking Realized vs Unrealized Gains](https://cointracking.info/gains.php) — MEDIUM confidence, multi-currency P&L methodology
- [Bryt Software Interest Accrual Methods](https://www.brytsoftware.com/interest-accrual-methods/) — MEDIUM confidence, compound vs simple interest formula differences
- [Digital Asset Reconciliation — QuickNode](https://blog.quicknode.com/digital-asset-reconciliation/) — MEDIUM confidence, deduplication strategy patterns
- Existing codebase inspection: `TradingBotDbContext.cs`, `Purchase.cs`, `Price.cs`, `UsdAmount.cs` — HIGH confidence

---
*Pitfalls research for: Multi-asset portfolio tracker (crypto, VN30 ETF, fixed deposits) added to existing BTC DCA bot*
*Researched: 2026-02-20*

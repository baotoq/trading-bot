# Stack Research: v4.0 Portfolio Tracker

**Project:** BTC Smart DCA Bot — Multi-Asset Portfolio Tracker milestone
**Researched:** 2026-02-20
**Scope:** NEW stack additions only. v3.0 Flutter + .NET 10 backend baseline is validated and unchanged.
**Confidence:** HIGH for crypto/currency APIs; MEDIUM for VN stock APIs (unofficial, fragile)

---

## Existing Stack Baseline (DO NOT RE-RESEARCH)

Validated and working from v1.0–v3.0. Do not add alternatives to these:

| Layer | Technology | Version |
|-------|------------|---------|
| Backend framework | .NET 10.0 / ASP.NET Core Minimal APIs | 10.0 |
| Persistence | EF Core + PostgreSQL (Aspire.Npgsql) | 10.0.0 |
| Caching | Redis (Aspire.StackExchange.Redis) | 13.0.2 |
| HTTP resilience | Microsoft.Extensions.Http.Resilience | 10.3.0 |
| Domain patterns | Vogen (IDs), ErrorOr (Result), Ardalis.Specification | 8.0.4 / 2.0.1 / 9.3.1 |
| External price data | CoinGecko API (direct HttpClient) | — |
| Flutter state | hooks_riverpod + riverpod_annotation | ^3.2.1 / ^4.0.2 |
| Flutter HTTP | Dio | ^5.9.1 |
| Flutter charts | fl_chart | ^1.1.1 |
| Flutter formatting | intl | any (resolved at build) |

---

## New Capabilities Required

The v4.0 milestone adds four new technical domains:

1. **Multi-asset price fetching** — crypto (extend CoinGecko), VN30 ETF (new source)
2. **Currency conversion** — USD/VND exchange rate, live and cached
3. **Fixed deposit interest calculation** — pure business logic, no external dep
4. **Portfolio domain model** — multi-asset entity hierarchy, transaction history

---

## Recommended Backend Additions (.NET)

### Price Feed Additions

#### Crypto Prices: Extend Existing CoinGecko HttpClient

No new package needed. Extend existing `CoinGeckoClient` using the `/simple/price` endpoint with comma-separated coin IDs.

**Endpoint:**
```
GET https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=usd,vnd&x_cg_demo_api_key={key}
```

**Why extend rather than replace:** Project already uses CoinGecko for historical backtest data. Same client, same API key, same HttpClient registration. Adding multi-coin current price is a single new method.

**Rate limit:** Demo (free) tier — 30 calls/min. Adequate for a personal portfolio polling every 5–10 minutes.

**VND support:** CoinGecko's `/simple/supported_vs_currencies` endpoint confirms `vnd` is a supported target currency — prices can be returned directly in VND without backend conversion. HIGH confidence (verified from CoinGecko docs).

#### VN30 ETF Prices: VNDirect finfo API (Direct HttpClient)

**Source:** VNDirect public `finfo-api.vndirect.com.vn` — used by Python `vnstock` community library.

**Endpoint pattern:**
```
GET https://finfo-api.vndirect.com.vn/v4/stock_prices/?code=E1VFVN30&sort=date&size=1&page=1
```

**No authentication required.** Returns JSON with close price in VND.

**Supported symbols:** Any HOSE/HNX ticker. VN30 ETF tickers: `E1VFVN30` (VFMVN30 ETF) and `FUESSV30` (SSIAM VN30 ETF).

**Why not Yahoo Finance / YahooQuotesApi:**

| Approach | Verdict | Reason |
|----------|---------|--------|
| YahooQuotesApi NuGet 7.0.6 | Avoid | Unofficial, adds NodaTime dependency, Yahoo can change auth at any time, no VN market closing time support. Direct API call is simpler. |
| finfo-api.vndirect.com.vn | Use | No auth, JSON, works for all HOSE symbols including ETFs, already reverse-engineered by open source community. |

**Critical caveat (MEDIUM confidence):** This is an undocumented, unofficial API. VNDirect can change or auth-gate it without notice. Design a `VnStockPriceService` with a fallback: if the endpoint fails, mark price as stale and surface a "Price unavailable" state rather than crashing. Do NOT use this as the sole truth for any trade decision (it isn't — this is read-only portfolio display).

**Implementation pattern:**
```csharp
// Register as named HttpClient (no auth headers, short timeout)
builder.Services.AddHttpClient("VNDirect", client =>
{
    client.BaseAddress = new Uri("https://finfo-api.vndirect.com.vn");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddStandardResilienceHandler(); // uses existing Microsoft.Extensions.Http.Resilience
```

### Currency Conversion: ExchangeRate-API Open Access

**Source:** `open.er-api.com` — the free, no-API-key-required tier from ExchangeRate-API.

**Endpoint:**
```
GET https://open.er-api.com/v6/latest/USD
```

**VND confirmed:** Live fetch of `open.er-api.com/v6/latest/USD` returns `VND: 25905.86` — VND is included. HIGH confidence (verified by direct API call during research).

**Rate limit:** Soft limit — designed for "once per 24 hours" polling cadence; hourly polling will not be rate-limited. Adequate for a personal app that refreshes currency rates daily.

**No package needed.** Register as a typed `HttpClient` and cache the rate in Redis with a 12-hour TTL using the existing Redis distributed cache. Do not fetch on every portfolio request.

**Why not Fixer.io / ExchangeRatesAPI.io:** Both require API keys even on free tier. Open.er-api.com provides the same data key-free, matching the project's preference for zero-cost dependencies.

**Implementation pattern:**
```csharp
// Cache exchange rate with 12h TTL in existing Redis
public record ExchangeRateResponse(Dictionary<string, decimal> Rates, long TimeLastUpdateUnix);

// Cache key: "exchange_rate:USD:VND"
// TTL: TimeSpan.FromHours(12)
// Fallback: last known cached value if fetch fails (same stale-data policy used for multipliers)
```

### No New NuGet Packages Required for Backend

The backend gains three new HTTP endpoints (VNDirect, CoinGecko multi-coin, ExchangeRate) all implemented as named/typed `HttpClient` registrations using existing infrastructure:

| Capability | Implementation | NuGet Addition |
|------------|---------------|----------------|
| Multi-coin crypto prices | Extend `CoinGeckoClient` with new method | None |
| VN30 ETF prices | New `VnStockPriceService` with typed HttpClient | None |
| USD/VND exchange rate | New `ExchangeRateService` with named HttpClient + Redis cache | None |
| Fixed deposit interest | Pure C# calculation logic in domain service | None |
| Portfolio domain model | EF Core TPH inheritance (built-in) | None |

### EF Core Domain Model Strategy: TPH Inheritance

**Pattern:** Table Per Hierarchy (TPH) — single `Assets` table with a `discriminator` column for asset type.

**Why TPH over TPT/TPC:**
- Single table means simple joins for portfolio totals and P&L queries.
- EF Core 10 TPH is the default and has best query performance.
- Asset types (crypto, ETF, fixed deposit) share most fields (name, currency, transactions).
- Type-specific fields (e.g., `MaturityDate` for fixed deposits) are nullable on other asset rows — acceptable for ~3 asset types with small row counts.

```csharp
// Domain model sketch
public abstract class Asset : AggregateRoot<AssetId>
{
    public string Symbol { get; private set; }
    public string Name { get; private set; }
    public string Currency { get; private set; }  // "USD" | "VND"
    public IReadOnlyList<Transaction> Transactions => _transactions;
}

public sealed class CryptoAsset : Asset
{
    public string CoinGeckoId { get; private set; }  // "bitcoin", "ethereum"
}

public sealed class EtfAsset : Asset
{
    public string Exchange { get; private set; }  // "HOSE", "HNX"
}

public sealed class FixedDepositAsset : Asset
{
    public decimal AnnualInterestRate { get; private set; }
    public DateTimeOffset MaturityDate { get; private set; }
    public int CompoundingFrequency { get; private set; }  // 1=annual, 4=quarterly, 12=monthly
}
```

### Fixed Deposit Interest Calculation

Pure C# domain logic — no external library. The formula is standard finance:

```csharp
// Simple interest (tenure <= 6 months, typical Vietnamese bank FD)
decimal accruedInterest = principal * annualRate * (daysElapsed / 365m);

// Compound interest (tenure > 6 months)
decimal accruedValue = principal * Math.Pow(
    (double)(1 + annualRate / compoundingFrequency),
    (double)(compoundingFrequency * yearsElapsed)
);
```

Implement as a static `FixedDepositCalculator` class — same pattern as the existing `MultiplierCalculator` (zero dependencies, pure function, directly testable).

---

## Recommended Flutter Additions

### Currency Formatting

**Package:** `intl` — **already in pubspec.yaml**. No new package needed.

The existing `intl` package's `NumberFormat` handles both VND and USD formatting:

```dart
// VND (no decimal places — smallest unit is dong)
NumberFormat.currency(locale: 'vi_VN', symbol: '₫', decimalDigits: 0);

// USD (2 decimal places)
NumberFormat.currency(locale: 'en_US', symbol: '\$', decimalDigits: 2);

// Compact for large VND amounts (e.g., 25.9M₫)
NumberFormat.compactCurrency(locale: 'vi_VN', symbol: '₫');
```

### Portfolio Chart Display

**Package:** `fl_chart ^1.1.1` — **already in pubspec.yaml**. No new package needed.

Use `LineChart` for portfolio value over time (same component used for BTC price chart). Add a second series for allocation breakdown using `PieChart` (included in fl_chart).

### No New Flutter Packages Required

All new UI needs (currency toggle, VND/USD display, allocation pie chart, new transaction form) are covered by existing packages:

| Capability | Existing Package |
|------------|-----------------|
| VND/USD number formatting | `intl` (already present) |
| Portfolio value chart | `fl_chart` (already present) |
| Allocation pie chart | `fl_chart` (PieChart — already present) |
| Transaction date picker | Flutter built-in `showDatePicker` |
| Form validation | Flutter built-in `Form` + `TextFormField` |
| State management for portfolio | `hooks_riverpod` (already present) |

---

## New API Endpoints Required (Backend)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/portfolio/assets` | GET | List all assets with current value and P&L |
| `/api/portfolio/assets` | POST | Create asset (crypto/ETF/fixed deposit) |
| `/api/portfolio/assets/{id}` | GET | Asset detail with transaction history |
| `/api/portfolio/assets/{id}/transactions` | POST | Record transaction (buy/sell/deposit) |
| `/api/portfolio/assets/{id}/transactions/{txId}` | DELETE | Remove transaction |
| `/api/portfolio/summary` | GET | Aggregate portfolio: total value VND/USD, per-asset allocation % |
| `/api/portfolio/chart` | GET | Portfolio value over time (daily snapshots) |
| `/api/portfolio/prices/refresh` | POST | Trigger manual price refresh for all assets |

All endpoints use existing `x-api-key` auth — no new auth mechanism.

---

## Alternatives Considered

| Capability | Recommended | Alternative | Why Not |
|------------|-------------|-------------|---------|
| VN stock prices | VNDirect finfo API (direct HttpClient) | Yahoo Finance via YahooQuotesApi NuGet 7.0.6 | YahooQuotesApi adds NodaTime dependency, Yahoo can break auth silently. Direct finfo-api is simpler and no dep. |
| VN stock prices | VNDirect finfo API | EODHD paid API | Costs money. VNDirect free endpoint is sufficient for daily close price. |
| Currency conversion | open.er-api.com (no key) | Fixer.io / exchangeratesapi.io | Both require API key on free tier. Open.er-api.com is key-free, verified working. |
| Currency conversion | open.er-api.com + Redis cache | CoinGecko `/simple/price?vs_currencies=vnd` | CoinGecko returns crypto prices in VND directly — usable for crypto. But for non-crypto portfolio total you still need USD/VND. Use both. |
| Asset model | TPH (single table) | TPT (table per type) | TPT requires joins on every query. For ≤3 asset types with small row counts, TPH is simpler and faster. |
| Fixed deposit calc | Static domain method | NCalc / external formula library | Overkill. The interest formulas are 2 lines of C#. No library needed. |
| Portfolio value history | Daily snapshot table | Calculate from transactions on every request | Snapshot table enables O(1) chart queries. On-the-fly calculation is O(n) over all transactions — fine initially but becomes slow with history. |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **YahooFinanceApi / YahooQuotesApi NuGet** | Unofficial, adds NodaTime dep, Yahoo has broken these wrappers before without warning | Direct VNDirect finfo API HttpClient call |
| **vnstock Python library** | It's Python — not callable from .NET without subprocess | VNDirect finfo API JSON endpoint directly |
| **CryptoExchange.Net** (already in project but unused) | Binance.Net is declared but unused. Don't extend its usage for CoinGecko — project already has a working direct HttpClient for CoinGecko | Extend existing CoinGeckoClient |
| **Currency conversion library (Money.NET, NMoneys)** | Unnecessary — only need USD/VND rate. A `decimal` rate multiplied by a decimal amount is all that's needed | Fetch rate from open.er-api.com, store as decimal, multiply |
| **Separate portfolio microservice** | Over-engineering. The existing ApiService has all the infrastructure (DB, Redis, HttpClients). Adding portfolio as new endpoints in the same service is correct at this scale | New controllers/endpoints in TradingBot.ApiService |
| **Real-time WebSocket price feeds** | VN market is closed after 3pm ICT. Daily close price is sufficient for portfolio display. CoinGecko demo has 1-5 minute cache anyway | Polling every 5 minutes via existing background service pattern |
| **EODHD paid API** | Costs money. The free VNDirect finfo API serves the same data | VNDirect finfo API |

---

## Integration Points with Existing Stack

### Price Refresh Background Service

Pattern: `TimeBackgroundService` (same base class as `DcaPurchaseService`). Runs every 5 minutes during market hours, fetches prices for all active assets, stores in Redis with TTL.

```csharp
// New: PortfolioPriceRefreshService : TimeBackgroundService
// Calls:
//   CoinGeckoClient.GetMultiCoinPricesAsync(coinIds, new[]{"usd","vnd"})
//   VnStockPriceService.GetLatestPriceAsync(symbol)  // finfo-api
//   ExchangeRateService.GetUsdVndRateAsync()         // open.er-api.com + Redis cache
// Publishes: PricesRefreshedDomainEvent → outbox → Flutter via FCM (optional)
```

### Auto-Import DCA Purchases

When a `PurchaseExecutedDomainEvent` fires, an additional handler creates a `CryptoTransaction` record in the portfolio for the BTC asset. This bridges the DCA bot to the portfolio tracker without code duplication.

```csharp
// New handler: ImportDcaPurchaseToPortfolioHandler : INotificationHandler<PurchaseExecutedDomainEvent>
// Creates a Transaction for the BTC CryptoAsset automatically
```

### Redis Cache Keys for Portfolio

```
portfolio:price:crypto:{coinGeckoId}:{currency}   TTL: 5 min
portfolio:price:vn:{symbol}                        TTL: 60 min (VN market closes at 3pm)
portfolio:exchange_rate:USD:VND                    TTL: 12 hours
portfolio:summary:{userId}                         TTL: 5 min (after price refresh)
```

---

## Version Compatibility

| Package / API | Version / Status | Compatibility Note |
|---------------|------------------|--------------------|
| CoinGecko `/simple/price` | Demo API, no version | VND in supported currencies — confirmed |
| open.er-api.com | v6 | VND confirmed in response — verified 2026-02-20 |
| VNDirect finfo-api.vndirect.com.vn | v4 | Unofficial, no SLA. Use resilience handler + stale-value fallback. |
| EF Core TPH | 10.0.0 (existing) | TPH is EF Core default — no version change needed |
| Microsoft.Extensions.Http.Resilience | 10.3.0 (existing) | Apply to all 3 new HttpClients via `.AddStandardResilienceHandler()` |

---

## Confidence Assessment

| Decision | Confidence | Rationale |
|----------|------------|-----------|
| CoinGecko `/simple/price` multi-coin + VND | HIGH | Official CoinGecko docs. VND confirmed as `vs_currency`. Already using CoinGecko in project. |
| open.er-api.com for USD/VND | HIGH | Live API call confirmed VND in response (25,905 VND/USD). No API key. Free, stable service. |
| VNDirect finfo API for VN ETF prices | MEDIUM | Unofficial API. Confirmed used by open-source Python community. Works without auth. But undocumented — can break. |
| EF Core TPH for asset hierarchy | HIGH | EF Core official docs. Default strategy. Well-understood in this codebase. |
| No new NuGet packages for backend | HIGH | All capability covered by existing HttpClient patterns + EF Core. Verified no missing capability. |
| No new Flutter packages needed | HIGH | intl and fl_chart already present, confirmed to cover VND formatting and pie chart. |
| Fixed deposit calc as pure static method | HIGH | Standard finance formulas. Matches existing MultiplierCalculator pattern. Zero risk. |

---

## Gaps / Open Questions

1. **VNDirect finfo-api exact response schema** — Need to verify the exact JSON field names for close price (likely `close` or `adClose`) with a live request during implementation. The endpoint timed out during research; structure inferred from open-source Python vnstock library.

2. **VN market hours** — Price refresh should only hit VNDirect API during HOSE trading hours (9:00–11:30, 13:00–15:00 ICT Mon–Fri). Outside these hours, use cached last close price. Implement via schedule check in `VnStockPriceService`.

3. **CoinGecko coin ID mapping** — Each crypto asset needs its CoinGecko ID (e.g., `bitcoin`, `ethereum`). Store `CoinGeckoId` on `CryptoAsset` entity. User provides this on asset creation (or we provide a search endpoint that calls CoinGecko `/search`).

4. **Portfolio value history storage** — If building the portfolio chart from Day 1, need a `PortfolioSnapshot` table (daily rows with total VND/USD value). If deferred, chart can be computed on-demand from transaction history (acceptable for small history). Decision for planning phase.

---

## Sources

- [CoinGecko /simple/price docs](https://docs.coingecko.com/reference/simple-price) — Multi-coin, VND vs_currency support confirmed
- [CoinGecko API Pricing](https://www.coingecko.com/en/api/pricing) — Demo free tier: 30 calls/min confirmed
- [open.er-api.com live response](https://open.er-api.com/v6/latest/USD) — VND: 25905.86 confirmed 2026-02-20
- [ExchangeRate-API docs](https://www.exchangerate-api.com/docs/free) — No-key open access tier documented
- [Yahoo Finance E1VFVN30.VN](https://finance.yahoo.com/quote/E1VFVN30.VN/) — ETF ticker symbols confirmed
- [Yahoo Finance FUESSV30.VN](https://finance.yahoo.com/quote/FUESSV30.VN/) — ETF ticker symbols confirmed
- [vnquant GitHub issue #6](https://github.com/phamdinhkhanh/vnquant/issues/6) — VNDirect finfo-api.vndirect.com.vn endpoint pattern
- [YahooQuotesApi NuGet 7.0.6](https://www.nuget.org/packages/YahooQuotesApi) — Version + .NET 10 compat verified; rejected due to complexity vs. direct call
- [EF Core TPH docs](https://learn.microsoft.com/en-us/ef/core/modeling/inheritance) — TPH as EF Core default confirmed
- [intl NumberFormat.currency](https://api.flutter.dev/flutter/package-intl_intl/NumberFormat/NumberFormat.currency.html) — VND locale vi_VN + decimalDigits=0 confirmed

---

*Stack research for: v4.0 Multi-Asset Portfolio Tracker*
*Researched: 2026-02-20*
*Conclusion: Zero new NuGet packages, zero new Flutter packages. Three new HttpClient registrations (CoinGecko extension, VNDirect finfo API, ExchangeRate-API). EF Core TPH for asset model. Pure C# for interest calculation. All integrate cleanly into existing patterns.*
*Confidence: HIGH for crypto/currency; MEDIUM for VN stock API (unofficial endpoint)*
